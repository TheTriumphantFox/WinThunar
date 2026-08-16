using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WinThunar.Models;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class FileOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinThunar.FileOps.Tests",
        Guid.NewGuid().ToString("N"));

    public FileOperationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ValidatesWindowsNames()
    {
        Assert.Null(FileOperationService.ValidateLeafName("estimate.xlsx"));
        Assert.NotNull(FileOperationService.ValidateLeafName("CON.txt"));
        Assert.NotNull(FileOperationService.ValidateLeafName("bad/name"));
        Assert.NotNull(FileOperationService.ValidateLeafName("trailing."));
    }

    [Fact]
    public void OnlyLatestNavigationGenerationCanCommit()
    {
        var generation = new RequestGeneration();
        var first = generation.Next();
        var second = generation.Next();

        Assert.False(generation.IsCurrent(first));
        Assert.True(generation.IsCurrent(second));
    }

    [Fact]
    public void RefreshReconciliationNeverResetsAnUnchangedCollection()
    {
        var first = new RefreshItem("first", 1);
        var second = new RefreshItem("second", 1);
        var collection = new ObservableCollection<RefreshItem> { first, second };
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, args) => actions.Add(args.Action);

        var unchanged = ObservableCollectionReconciler.Reconcile(
            collection,
            [first, second],
            (left, right) => left.Id == right.Id);

        Assert.False(unchanged);
        Assert.Empty(actions);
        Assert.Same(first, collection[0]);
        Assert.Same(second, collection[1]);

        var refreshedSecond = new RefreshItem("second", 2);
        var changed = ObservableCollectionReconciler.Reconcile(
            collection,
            [first, refreshedSecond],
            (left, right) => left.Id == right.Id);

        Assert.True(changed);
        Assert.Equal([NotifyCollectionChangedAction.Replace], actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.Same(first, collection[0]);
        Assert.Same(refreshedSecond, collection[1]);
    }

    [Fact]
    public async Task RenamesFilesWhenOnlyCasingChanges()
    {
        var source = Path.Combine(_root, "before.txt");
        await File.WriteAllTextAsync(source, "unchanged");

        var destination = await new FileOperationService().RenameAsync(source, "BEFORE.txt");

        Assert.Equal("BEFORE.txt", Path.GetFileName(destination));
        Assert.Contains("BEFORE.txt", Directory.EnumerateFiles(_root).Select(Path.GetFileName));
        Assert.Equal("unchanged", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task BulkRenameAppliesCaseOnlyPlans()
    {
        var first = Path.Combine(_root, "first.txt");
        var second = Path.Combine(_root, "second.txt");
        await File.WriteAllTextAsync(first, "one");
        await File.WriteAllTextAsync(second, "two");
        var service = new BulkRenameService();
        var plan = service.BuildPlan(
            [first, second],
            new BulkRenameOptions(BulkRenameMode.Uppercase, string.Empty, string.Empty));

        await service.ApplyAsync(plan);

        var names = Directory.EnumerateFiles(_root).Select(Path.GetFileName).ToArray();
        Assert.Contains("FIRST.txt", names);
        Assert.Contains("SECOND.txt", names);
    }

    [Fact]
    public async Task FailedReplacementKeepsExistingDestination()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceDirectory, "locked.txt");
        var destination = Path.Combine(destinationDirectory, "locked.txt");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(destination, "existing");
        await using var lockStream = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await new FileOperationService().TransferAsync(
            [source], destinationDirectory, FileTransferMode.Copy, ReplaceConflict);

        Assert.NotEmpty(result.Errors);
        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(destinationDirectory),
            path => Path.GetFileName(path).StartsWith(".winthunar-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NativeCopyPreservesAttributesAndAlternateStreams()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceDirectory, "metadata.txt");
        await File.WriteAllTextAsync(source, "data");
        await File.WriteAllTextAsync(source + ":WinThunar.Test", "stream-data");
        File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.Hidden);

        var result = await new FileOperationService().TransferAsync(
            [source], destinationDirectory, FileTransferMode.Copy, ReplaceConflict);
        var destination = Path.Combine(destinationDirectory, "metadata.txt");

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("stream-data", await File.ReadAllTextAsync(destination + ":WinThunar.Test"));
        Assert.True(File.GetAttributes(destination).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public async Task RecordsCompletedChildrenWhenFolderTransferIsCancelled()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(destinationRoot, "source")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "one.txt"), "incoming-one");
        await File.WriteAllTextAsync(Path.Combine(source, "two.txt"), "incoming-two");
        await File.WriteAllTextAsync(Path.Combine(destination, "two.txt"), "old-two");

        var result = await new FileOperationService().TransferAsync(
            [source],
            destinationRoot,
            FileTransferMode.Copy,
            conflict => Task.FromResult(new ConflictResolution(
                conflict.SourceIsDirectory ? ConflictAction.Replace : ConflictAction.Cancel)));

        Assert.True(result.Cancelled);
        Assert.Single(result.Transfers);
        Assert.False(result.Transfers[0].ReplacedExistingItem);
        Assert.Equal("incoming-one", await File.ReadAllTextAsync(Path.Combine(destination, "one.txt")));
    }

    [Fact]
    public async Task StateSnapshotDetectsChangedOrReplacedDestinations()
    {
        var path = Path.Combine(_root, "guarded.txt");
        await File.WriteAllTextAsync(path, "original");
        var snapshot = FileOperationService.CapturePathState(path);
        var originalTimestamp = File.GetLastWriteTimeUtc(path);

        Assert.True(FileOperationService.PathMatchesState(path, snapshot));
        await File.WriteAllTextAsync(path, "modified");
        File.SetLastWriteTimeUtc(path, originalTimestamp);
        Assert.False(FileOperationService.PathMatchesState(path, snapshot));
    }

    [Fact]
    public async Task ArchiveCreationIsAtomicAndPreservesEmptyDirectories()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "Materials")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "Empty"));
        await File.WriteAllTextAsync(Path.Combine(source, "list.txt"), "contents");
        var destination = Path.Combine(_root, "materials.zip");

        await new ArchiveService().CreateAsync([source], destination);

        using var archive = ZipFile.OpenRead(destination);
        Assert.Contains(archive.Entries, entry => entry.FullName == "Materials/Empty/");
        Assert.Contains(archive.Entries, entry => entry.FullName == "Materials/list.txt");
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(_root),
            path => Path.GetFileName(path).StartsWith(".winthunar-archive-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedArchiveCreationLeavesNoDestinationOrPartialArchive()
    {
        var source = Path.Combine(_root, "locked.txt");
        var destination = Path.Combine(_root, "failed.zip");
        await File.WriteAllTextAsync(source, "contents");
        await using var lockStream = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAnyAsync<IOException>(() => new ArchiveService().CreateAsync([source], destination));

        Assert.False(File.Exists(destination));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(_root),
            path => Path.GetFileName(path).StartsWith(".winthunar-archive-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArchiveCreationRejectsDirectoryReparsePoints()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source-with-link")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        await File.WriteAllTextAsync(Path.Combine(outside, "outside.txt"), "must not be followed");
        Directory.CreateSymbolicLink(Path.Combine(source, "linked"), outside);
        var destination = Path.Combine(_root, "linked.zip");

        var error = await Assert.ThrowsAsync<IOException>(() =>
            new ArchiveService().CreateAsync([source], destination));

        Assert.Contains("link or junction", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task NullSessionCollectionsAreNormalized()
    {
        var path = Path.Combine(_root, "session.json");
        await File.WriteAllTextAsync(path, """{"bookmarks":null,"bookmarkItems":null,"tabs":null,"folderViewSettings":null,"customShortcuts":null}""");

        var state = new AppSessionService(path).Load();

        Assert.Empty(state.Bookmarks);
        Assert.Empty(state.BookmarkItems);
        Assert.Empty(state.Tabs);
        Assert.Empty(state.FolderViewSettings);
        Assert.Empty(state.CustomShortcuts);
    }

    [Fact]
    public async Task NullPluginListsBecomeDiagnosticsInsteadOfCrashes()
    {
        var bundled = Directory.CreateDirectory(Path.Combine(_root, "plugins")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(_root, "user-plugins")).FullName;
        await File.WriteAllTextAsync(Path.Combine(bundled, "bad.json"), """{"id":"bad.plugin","name":"Bad","actions":null}""");

        var service = new PluginService(bundled, user);
        service.Reload();

        Assert.Empty(service.Plugins);
        Assert.Single(service.Diagnostics);
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

    private sealed record RefreshItem(string Id, int Version);
}
