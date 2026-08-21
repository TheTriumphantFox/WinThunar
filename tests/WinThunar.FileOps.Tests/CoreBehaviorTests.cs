using WinThunar.Models;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class CoreBehaviorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinThunar.FileOps.Tests",
        Guid.NewGuid().ToString("N"));

    public CoreBehaviorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task CreatesMovesAndCopiesItems()
    {
        var service = new FileOperationService();
        var sourceFolder = await service.CreateDirectoryAsync(_root, "source");
        var destinationFolder = await service.CreateDirectoryAsync(_root, "destination");
        var source = await service.CreateFileAsync(sourceFolder, "item.txt");
        await File.WriteAllTextAsync(source, "contents");

        var copied = await service.TransferAsync([source], destinationFolder, FileTransferMode.Copy, ReplaceConflict);
        var copy = Path.Combine(destinationFolder, "item.txt");
        Assert.True(copied.Succeeded, string.Join(Environment.NewLine, copied.Errors));
        Assert.True(File.Exists(source));
        Assert.Equal("contents", await File.ReadAllTextAsync(copy));

        var movedFolder = Directory.CreateDirectory(Path.Combine(_root, "moved")).FullName;
        var moved = await service.TransferAsync([source], movedFolder, FileTransferMode.Move, ReplaceConflict);
        Assert.True(moved.Succeeded, string.Join(Environment.NewLine, moved.Errors));
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(movedFolder, "item.txt")));
    }

    [Fact]
    public async Task MergesFolderTreesAndRejectsSelfCopies()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
        await File.WriteAllTextAsync(Path.Combine(nested, "list.txt"), "contents");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var service = new FileOperationService();

        var copied = await service.TransferAsync([source], destination, FileTransferMode.Copy, ReplaceConflict);
        Assert.True(copied.Succeeded, string.Join(Environment.NewLine, copied.Errors));
        Assert.True(File.Exists(Path.Combine(destination, "source", "nested", "list.txt")));

        var rejected = await service.TransferAsync([source], nested, FileTransferMode.Copy, ReplaceConflict);
        Assert.Single(rejected.Errors);
    }

    [Fact]
    public async Task ResolvesSkipAndRenameConflicts()
    {
        var sourceFolder = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationFolder = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceFolder, "panel.txt");
        var destination = Path.Combine(destinationFolder, "panel.txt");
        await File.WriteAllTextAsync(source, "new");
        await File.WriteAllTextAsync(destination, "old");
        var service = new FileOperationService();

        var skipped = await service.TransferAsync(
            [source], destinationFolder, FileTransferMode.Copy,
            _ => Task.FromResult(new ConflictResolution(ConflictAction.Skip)));
        Assert.Equal(1, skipped.SkippedItems);
        Assert.Equal("old", await File.ReadAllTextAsync(destination));

        var renamed = await service.TransferAsync(
            [source], destinationFolder, FileTransferMode.Copy,
            _ => Task.FromResult(new ConflictResolution(ConflictAction.Rename, "panel alternate.txt")));
        Assert.True(renamed.Succeeded, string.Join(Environment.NewLine, renamed.Errors));
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(destinationFolder, "panel alternate.txt")));
    }

    [Fact]
    public async Task ExactTransferUsesTheRecordedDestination()
    {
        var sourceFolder = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationFolder = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceFolder, "original.txt");
        var destination = Path.Combine(destinationFolder, "restored-name.txt");
        await File.WriteAllTextAsync(source, "history");

        var result = await new FileOperationService().TransferExactAsync(
            source, destination, FileTransferMode.Move, ReplaceConflict);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.False(File.Exists(source));
        Assert.Equal("history", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public void GeneratesDuplicateNamesWithoutOverwriting()
    {
        var source = Path.Combine(_root, "cover.pdf");
        File.WriteAllText(source, "pdf");
        File.WriteAllText(Path.Combine(_root, "cover (copy 1).pdf"), "existing");

        Assert.Equal("cover (copy 2).pdf", Path.GetFileName(FileOperationService.GetDuplicatePath(source)));
    }

    [Fact]
    public async Task HistoryIsBoundedAndSupportsRedo()
    {
        var value = 0;
        var history = new FileOperationHistory();
        for (var index = 0; index < 11; index++)
        {
            value++;
            history.Push(new FileHistoryEntry(
                $"Step {index}",
                () => { value--; return Task.CompletedTask; },
                () => { value++; return Task.CompletedTask; }));
        }

        for (var index = 0; index < 10; index++)
        {
            await history.UndoAsync();
        }
        Assert.Equal(1, value);
        for (var index = 0; index < 10; index++)
        {
            await history.RedoAsync();
        }
        Assert.Equal(11, value);
    }

    [Fact]
    public async Task QueueRunsTransfersSequentially()
    {
        var sources = Directory.CreateDirectory(Path.Combine(_root, "sources")).FullName;
        var firstDestination = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        var secondDestination = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        var first = Path.Combine(sources, "first.txt");
        var second = Path.Combine(sources, "second.txt");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");
        var queue = new FileTransferQueue(new FileOperationService());

        var results = await Task.WhenAll(
            queue.EnqueueAsync([first], firstDestination, FileTransferMode.Copy, ReplaceConflict),
            queue.EnqueueAsync([second], secondDestination, FileTransferMode.Copy, ReplaceConflict));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.All(queue.Jobs, job => Assert.Equal(FileTransferJobState.Completed, job.State));
        Assert.Null(queue.ActiveJob);
    }

    [Fact]
    public async Task SessionAndPluginRoundTripsRemainValid()
    {
        var sessionPath = Path.Combine(_root, "state", "session.json");
        var sessionService = new AppSessionService(sessionPath);
        sessionService.Save(new AppSessionState
        {
            LastPath = _root,
            Bookmarks = [Path.Combine(_root, "bookmark")],
            Tabs = [_root],
            FolderViewSettings = new Dictionary<string, FolderViewState>
            {
                [_root] = new() { ZoomLevel = 4 }
            }
        });
        var session = sessionService.Load();
        Assert.Equal(_root, session.LastPath);
        Assert.Equal(4, session.FolderViewSettings[_root].ZoomLevel);

        var bundled = Directory.CreateDirectory(Path.Combine(_root, "plugins")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(_root, "user-plugins")).FullName;
        await File.WriteAllTextAsync(Path.Combine(bundled, "viewer.json"), """
        {"id":"test.viewer","name":"Viewer","actions":[{"id":"open","name":"Open","command":"viewer.exe","arguments":["{selected}"],"filePatterns":["*.txt"],"targets":"Files"}]}
        """);
        var plugins = new PluginService(bundled, user);
        plugins.Reload();
        Assert.Single(plugins.GetApplicableActions([
            new PluginSelectionItem("notes.txt", Path.Combine(_root, "notes.txt"), false)
        ]));
    }

    [Theory]
    [InlineData("folder.lnk", true)]
    [InlineData("WEBSITE.URL", true)]
    [InlineData("notes.txt", false)]
    [InlineData("almost.lnk.txt", false)]
    public void RecognizesWindowsShortcutFiles(string fileName, bool expected) =>
        Assert.Equal(expected, ShellIntegrationService.IsShortcutFile(fileName));

    [Fact]
    public void ResolvesFolderShortcutTargets()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "shortcut-target")).FullName;
        var shortcutPath = Path.Combine(_root, "folder.lnk");
        object? shellObject = null;
        object? shortcutObject = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shellObject = shellType is null ? null : Activator.CreateInstance(shellType);
            Assert.NotNull(shellObject);

            dynamic shell = shellObject;
            shortcutObject = shell.CreateShortcut(shortcutPath);
            dynamic shortcut = shortcutObject;
            shortcut.TargetPath = target;
            shortcut.Save();
        }
        finally
        {
            foreach (var value in new[] { shortcutObject, shellObject })
            {
                if (value is not null && System.Runtime.InteropServices.Marshal.IsComObject(value))
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
                }
            }
        }

        Assert.True(ShellIntegrationService.TryResolveShortcutTarget(shortcutPath, out var resolved));
        Assert.Equal(Path.GetFullPath(target), resolved, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests"));
        var target = Path.GetFullPath(_root);
        if (!target.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test cleanup path escaped its generated test root.");
        }
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }
    }

    private static Task<ConflictResolution> ReplaceConflict(FileConflict _) =>
        Task.FromResult(new ConflictResolution(ConflictAction.Replace));
}
