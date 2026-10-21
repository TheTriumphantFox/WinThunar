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
public sealed class GitStatusService
{
    public async Task<GitStatusSnapshot?> ReadAsync(string directory, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var root = (await RunAsync(directory, ["rev-parse", "--show-toplevel"], timeout.Token))?.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(root)) return null;
            var tracked = await RunAsync(root, ["ls-files", "-z"], timeout.Token);
            var changed = await RunAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=normal", "--ignored=matching"], timeout.Token);
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

    private static async Task<string?> RunAsync(string directory, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8 };
        start.ArgumentList.Add("--no-optional-locks");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("core.fsmonitor=false");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // A caller's repository environment must not redirect this browser's Git commands.
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start)!;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(token);
            var error = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var text = await output;
            await error;
            return process.ExitCode == 0 ? text : null;
        }
        finally
        {
            if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        }
    }
}
