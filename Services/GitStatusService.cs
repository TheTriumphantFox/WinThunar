using System.Diagnostics;

namespace WinThunar.Services;

public enum GitFileStatus { None, Clean, Ignored, Untracked, Added, Renamed, Deleted, Modified, Conflict }

public sealed record GitStatusSnapshot(string Root, IReadOnlyDictionary<string, GitFileStatus> Paths)
{
    public GitFileStatus ForPath(string path, bool directory)
    {
        var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal)) return GitFileStatus.None;
        var status = Paths.GetValueOrDefault(relative, GitFileStatus.None);
        if (status == GitFileStatus.None)
        {
            var parent = relative;
            while (parent.LastIndexOf('/') is var slash && slash >= 0)
            {
                parent = parent[..slash];
                if (Paths.TryGetValue(parent, out var inherited) && inherited is GitFileStatus.Ignored or GitFileStatus.Untracked)
                { status = inherited; break; }
            }
        }
        if (!directory) return status;
        var prefix = relative == "." ? "" : relative.TrimEnd('/') + "/";
        foreach (var item in Paths)
            if (item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && item.Value > status)
                status = item.Value;
        return status;
    }
}

/// <summary>Read-only Git commands, without a command shell, with cancellation and a time limit.</summary>
public sealed class GitStatusService(Func<ProcessStartInfo, Process>? startProcess = null)
{
    private readonly Func<ProcessStartInfo, Process> _startProcess = startProcess ?? (start => Process.Start(start)!);

    /// <summary>
    /// Runs entirely on the thread pool. Starting Git searches PATH, and killing its process tree
    /// opens every process on the system; Windows can hold either for a minute (docs/STABILITY.md).
    /// </summary>
    public Task<GitStatusSnapshot?> ReadAsync(string directory, CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadCoreAsync(directory, cancellationToken), CancellationToken.None);

    private async Task<GitStatusSnapshot?> ReadCoreAsync(string directory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var root = (await RunAsync(directory, ["rev-parse", "--show-toplevel"], [], timeout.Token))?.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(root)) return null;
            var filters = await ReadRepositoryFiltersCoreAsync(root, timeout.Token);
            if (filters is null) return null;
            var tracked = await RunAsync(root, ["ls-files", "-z"], filters, timeout.Token);
            // A submodule has its own config, whose filters the list above does not cover.
            var changed = await RunAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=normal", "--ignored=matching", "--ignore-submodules=all"], filters, timeout.Token);
            if (tracked is null || changed is null) return null;
            return new GitStatusSnapshot(root, Parse(tracked, changed));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; } // Git is optional.
        catch (IOException) { return null; }
    }

    public static IReadOnlyDictionary<string, GitFileStatus> Parse(string tracked, string porcelain)
    {
        var result = new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in tracked.Split('\0', StringSplitOptions.RemoveEmptyEntries)) result[path] = GitFileStatus.Clean;
        var records = porcelain.Split('\0');
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length < 4) continue;
            var code = record[..2];
            var path = record[3..].TrimEnd('/');
            var status = code.Contains('U') || code is "AA" or "DD" ? GitFileStatus.Conflict
                : code == "??" ? GitFileStatus.Untracked
                : code == "!!" ? GitFileStatus.Ignored
                : code.Contains('D') ? GitFileStatus.Deleted
                : code.Contains('M') || code.Contains('T') ? GitFileStatus.Modified
                : code.Contains('R') ? GitFileStatus.Renamed
                : code.Contains('A') || code.Contains('C') ? GitFileStatus.Added : GitFileStatus.None;
            result[path] = status;
            // With -z Git emits destination first, then the original path as a separate record.
            if ((code.Contains('R') || code.Contains('C')) && index + 1 < records.Length)
            {
                var original = records[++index];
                if (code.Contains('R')) result.Remove(original);
            }
        }
        return result;
    }

    /// <summary>
    /// Names of the filters <paramref name="root"/>'s own config defines, to switch off with
    /// <see cref="GitCommand.CreateStartInfo"/>; null when Git fails or a filter cannot be switched off.
    /// Runs on the thread pool, like <see cref="ReadAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<string>?> ReadRepositoryFiltersAsync(string root, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            try { return await ReadRepositoryFiltersCoreAsync(root, cancellationToken); }
            catch (System.ComponentModel.Win32Exception) { return null; }
            catch (IOException) { return null; }
        }, CancellationToken.None);

    private async Task<IReadOnlyList<string>?> ReadRepositoryFiltersCoreAsync(string root, CancellationToken token)
    {
        var (exitCode, output) = await ExecuteAsync(root, GitCommand.ListFilterArguments, [], token);
        // git config --get-regexp exits 1 when nothing matches.
        return exitCode switch
        {
            0 => GitCommand.RepositoryFilterNames(output),
            1 when output.Length == 0 => [],
            _ => null
        };
    }

    private async Task<string?> RunAsync(string directory, string[] arguments, IReadOnlyCollection<string> filters, CancellationToken token)
    {
        var (exitCode, output) = await ExecuteAsync(directory, arguments, filters, token);
        return exitCode == 0 ? output : null;
    }

    private async Task<(int ExitCode, string Output)> ExecuteAsync(string directory, string[] arguments, IReadOnlyCollection<string> filters, CancellationToken token)
    {
        // Never a bare name: Windows would look in this process's current folder first.
        var git = ExecutableLocator.Git ?? throw new System.ComponentModel.Win32Exception(2);
        using var process = _startProcess(GitCommand.CreateStartInfo(git, directory, arguments, filters));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(token);
            var error = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var text = await output;
            await error;
            return (process.ExitCode, text);
        }
        finally
        {
            if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        }
    }
}
