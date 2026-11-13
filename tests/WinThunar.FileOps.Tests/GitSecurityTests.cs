using System.Diagnostics;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

/// <summary>
/// A folder the user only browses can hold a crafted .git folder whose config names programs.
/// Each test also runs plain Git on the same fixture to prove the planted program is live.
/// </summary>
public sealed class GitSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.Git.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _marker;

    public GitSecurityTests()
    {
        Directory.CreateDirectory(_root);
        _marker = Path.Combine(_root, "marker.txt");
    }

    public void Dispose()
    {
        var expected = Path.Combine(Path.GetTempPath(), "WinThunar.Git.Tests") + Path.DirectorySeparatorChar;
        Assert.StartsWith(expected, _root);
        LinkFileSystem.Delete(_root);
    }

    private string MarkerForShell => _marker.Replace('\\', '/');

    public static TheoryData<string> FilterSetups => new() { "clean", "process", "required", "include" };

    [Theory]
    [MemberData(nameof(FilterSetups))]
    public async Task RepositoryFiltersDoNotRunWhenReadingBadges(string setup)
    {
        await CreateRepositoryAsync();
        var touch = $"touch '{MarkerForShell}'";
        switch (setup)
        {
            case "clean":
                await GitAsync("config", "filter.demo.clean", touch + "; cat");
                break;
            case "process":
                await GitAsync("config", "filter.demo.process", touch + "; exit 1");
                break;
            case "required":
                await GitAsync("config", "filter.demo.clean", touch + "; cat");
                await GitAsync("config", "filter.demo.required", "true");
                break;
            case "include":
                // A config file may not hold ';' unquoted, so the included command is only the touch.
                await File.WriteAllTextAsync(Path.Combine(_root, ".git", "extra.cfg"), $"[filter \"demo\"]\n\tclean = \"{touch}; cat\"\n");
                await GitAsync("config", "include.path", "extra.cfg");
                break;
        }
        await TriggerFilterAsync("demo");

        var snapshot = await new GitStatusService().ReadAsync(_root);

        Assert.False(File.Exists(_marker), $"The repository's {setup} filter ran while reading badges.");
        Assert.NotNull(snapshot);
        Assert.Equal(GitFileStatus.Modified, snapshot.ForPath(Path.Combine(_root, "a.txt"), false));
        await AssertPlainGitRunsMarkerAsync("status", "--porcelain");
    }

    [Fact]
    public async Task FilterNameThatCannotBeOverriddenStopsBadges()
    {
        await CreateRepositoryAsync();
        // "-c filter.a=b.clean=" would split at the first '=', so this filter cannot be switched off.
        await GitAsync("config", "filter.a=b.clean", $"touch '{MarkerForShell}'; cat");
        await TriggerFilterAsync("a=b");

        Assert.Null(await new GitStatusService().ReadAsync(_root));
        Assert.False(File.Exists(_marker));
        await AssertPlainGitRunsMarkerAsync("status", "--porcelain");
    }

    [Fact]
    public async Task PluginDiffDoesNotRunExternalDiffOrFilters()
    {
        await CreateRepositoryAsync();
        await GitAsync("config", "diff.external", $"touch '{MarkerForShell}'; true");
        await GitAsync("config", "filter.demo.clean", $"touch '{MarkerForShell}'; cat");
        await TriggerFilterAsync("demo");

        var (exitCode, output) = await RunPluginAsync("diff", ["a.txt"]);

        Assert.False(File.Exists(_marker), "The repository's diff program or filter ran.");
        Assert.Equal(0, exitCode);
        Assert.Contains("+jello", output);
        await AssertPlainGitRunsMarkerAsync("diff");
    }

    [Fact]
    public async Task PluginLogDoesNotRunSignatureProgram()
    {
        await CreateRepositoryAsync();
        // Any commit carrying a signature header makes Git call the configured verifier.
        var commit = await GitAsync("cat-file", "commit", "HEAD");
        var forged = System.Text.RegularExpressions.Regex.Replace(
            commit, "(?m)^(committer .*)$", "$1\ngpgsig -----BEGIN PGP SIGNATURE-----\n \n -----END PGP SIGNATURE-----");
        var commitFile = Path.Combine(_root, ".git", "forged.txt");
        await File.WriteAllTextAsync(commitFile, forged);
        var forgedId = (await GitAsync("hash-object", "-t", "commit", "-w", commitFile)).Trim();
        await GitAsync("update-ref", "HEAD", forgedId);
        var verifier = Path.Combine(_root, "verifier.bat");
        await File.WriteAllTextAsync(verifier, $"@echo off\r\necho ran> \"{_marker}\"\r\n");
        await GitAsync("config", "log.showSignature", "true");
        await GitAsync("config", "gpg.program", verifier.Replace('\\', '/'));

        var (exitCode, output) = await RunPluginAsync("log", []);

        Assert.False(File.Exists(_marker), "The repository's signature program ran.");
        Assert.Equal(0, exitCode);
        Assert.Contains("init", output);
        await AssertPlainGitRunsMarkerAsync("log", "--oneline");
    }

    [Fact]
    public void OnlyRepositoryScopedFiltersAreSwitchedOff()
    {
        var output = string.Join('\0',
            "system", "filter.lfs.clean\ngit-lfs clean -- %f",
            "global", "filter.mine.process\nmine",
            "local", "filter.demo.clean\ntouch x",
            "local", "filter.demo.required\ntrue",
            "worktree", "filter.dotted.name.smudge\ncat",
            "local", "core.bare\nfalse") + '\0';

        Assert.Equal(new[] { "demo", "dotted.name" }, GitCommand.RepositoryFilterNames(output));
        Assert.Empty(GitCommand.RepositoryFilterNames(string.Empty)!);
        Assert.Null(GitCommand.RepositoryFilterNames("local\0filter.a=b.clean\nx\0"));
    }

    [Fact]
    public void GitStartsByFullPathWithHardening()
    {
        var git = ExecutableLocator.Git;
        Assert.NotNull(git);
        Assert.True(Path.IsPathFullyQualified(git), git);

        var start = GitCommand.CreateStartInfo(git, _root, ["status"], ["demo"]);

        Assert.Equal(git, start.FileName);
        var arguments = start.ArgumentList.ToArray();
        Assert.Equal("--no-optional-locks", arguments[0]);
        Assert.Equal("status", arguments[^1]);
        foreach (var setting in new[] { "core.fsmonitor=false", "protocol.allow=never", "filter.demo.clean=", "filter.demo.process=", "filter.demo.required=false" })
        {
            Assert.Contains(setting, arguments);
        }
        Assert.Equal("1", start.Environment["GIT_NO_LAZY_FETCH"]);
    }

    private async Task CreateRepositoryAsync()
    {
        await GitAsync("init", "--quiet");
        await GitAsync("config", "user.email", "test@example.invalid");
        await GitAsync("config", "user.name", "Test");
        await GitAsync("config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(_root, "a.txt"), "hello\n");
        await GitAsync("add", "a.txt");
        await GitAsync("commit", "--quiet", "-m", "init");
    }

    /// <summary>
    /// Git only cleans a file whose stat data no longer matches the index; a size change alone is
    /// reported without running the filter, so the edit keeps the size and moves the timestamp.
    /// </summary>
    private async Task TriggerFilterAsync(string filter)
    {
        await File.WriteAllTextAsync(Path.Combine(_root, ".gitattributes"), $"a.txt filter={filter}\n");
        var file = Path.Combine(_root, "a.txt");
        await File.WriteAllTextAsync(file, "jello\n");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
    }

    private async Task AssertPlainGitRunsMarkerAsync(params string[] arguments)
    {
        Assert.False(File.Exists(_marker));
        File.SetLastWriteTimeUtc(Path.Combine(_root, "a.txt"), DateTime.UtcNow.AddMinutes(2));
        await GitAsync(arguments, requireSuccess: false);
        Assert.True(File.Exists(_marker), "The fixture's planted program did not run under plain Git, so this test proves nothing.");
    }

    private async Task<(int ExitCode, string Output)> RunPluginAsync(string operation, string[] paths)
    {
        var filters = await new GitStatusService().ReadRepositoryFiltersAsync(_root);
        Assert.NotNull(filters);
        var start = GitCommand.CreateStartInfo(ExecutableLocator.Git!, _root, GitCommand.PluginArguments(operation, paths), filters);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }

    private Task<string> GitAsync(params string[] arguments) => GitAsync(arguments, requireSuccess: true);

    private async Task<string> GitAsync(string[] arguments, bool requireSuccess)
    {
        var start = new ProcessStartInfo(ExecutableLocator.Git!)
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (requireSuccess)
        {
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {await error}");
        }
        return await output;
    }
}
