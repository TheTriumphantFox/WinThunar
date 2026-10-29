using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;
public sealed class GitStatusTests
{
    [Fact]
    public async Task ReadsRealRepositoryWithoutModifyingItsIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinThunar.Git.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            async Task Git(params string[] arguments)
            {
                var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var process = System.Diagnostics.Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                await output;
                Assert.True(process.ExitCode == 0, await error);
            }
            await Git("init", "--quiet");
            await File.WriteAllTextAsync(Path.Combine(root, "tracked.txt"), "before");
            await Git("add", "tracked.txt");
            var indexPath = Path.Combine(root, ".git", "index");
            var index = await File.ReadAllBytesAsync(indexPath);
            await File.WriteAllTextAsync(Path.Combine(root, "tracked.txt"), "after with a different length");
            await File.WriteAllTextAsync(Path.Combine(root, "untracked.txt"), "new");
            var snapshot = await new GitStatusService().ReadAsync(root);
            Assert.NotNull(snapshot);
            Assert.Equal(GitFileStatus.Modified, snapshot.ForPath(Path.Combine(root, "tracked.txt"), false));
            Assert.Equal(GitFileStatus.Untracked, snapshot.ForPath(Path.Combine(root, "untracked.txt"), false));
            Assert.Equal(index, await File.ReadAllBytesAsync(indexPath));
        }
        finally
        {
            var expected = Path.Combine(Path.GetTempPath(), "WinThunar.Git.Tests") + Path.DirectorySeparatorChar;
            Assert.StartsWith(expected, root);
            LinkFileSystem.Delete(root);
        }
    }

    [Fact]
    public async Task SlowProcessStartDoesNotBlockTheCaller()
    {
        // Windows can hold CreateProcess's PATH search for a minute; the UI thread must not wait on it.
        var callerThread = Environment.CurrentManagedThreadId;
        var startThread = 0;
        var service = new GitStatusService(_ =>
        {
            startThread = Environment.CurrentManagedThreadId;
            Thread.Sleep(1500);
            throw new System.ComponentModel.Win32Exception(2);
        });
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var read = service.ReadAsync(Path.GetTempPath());
        Assert.True(clock.ElapsedMilliseconds < 500, $"ReadAsync blocked its caller for {clock.ElapsedMilliseconds} ms.");
        Assert.Null(await read);
        Assert.NotEqual(0, startThread);
        Assert.NotEqual(callerThread, startThread);
    }

    [Fact]
    public void ParsesNulDelimitedNamesRenamesAndConflicts()
    {
        var paths = GitStatusService.Parse("clean.txt\0old name.txt\0", " M space name.txt\0R  new name.txt\0old name.txt\0?? untracked\nname\0UU conflict\0!! ignored/\0");
        Assert.Equal(GitFileStatus.Clean, paths["clean.txt"]);
        Assert.Equal(GitFileStatus.Modified, paths["space name.txt"]);
        Assert.Equal(GitFileStatus.Renamed, paths["new name.txt"]);
        Assert.False(paths.ContainsKey("old name.txt"));
        Assert.Equal(GitFileStatus.Untracked, paths["untracked\nname"]);
        Assert.Equal(GitFileStatus.Conflict, paths["conflict"]);
        Assert.Equal(GitFileStatus.Ignored, paths["ignored"]);
    }

    [Fact]
    public void FoldersAggregateOnlyTheirOwnDescendants()
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var snapshot = new GitStatusSnapshot(root, GitStatusService.Parse("folder/a\0folder/b\0", " M folder/b\0UU folder-other/c\0"));
        Assert.Equal(GitFileStatus.Modified, snapshot.ForPath(Path.Combine(root, "folder"), true));
        Assert.Equal(GitFileStatus.Clean, snapshot.ForPath(Path.Combine(root, "folder", "a"), false));
        Assert.Equal(GitFileStatus.None, snapshot.ForPath(Path.Combine(root, "absent"), true));
    }
}
