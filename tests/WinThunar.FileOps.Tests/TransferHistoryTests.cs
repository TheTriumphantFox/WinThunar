using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class TransferHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));
    private readonly FileOperationService _operations;
    public TransferHistoryTests()
    {
        Directory.CreateDirectory(_root);
        _operations = new(new TransferRecoveryService(Path.Combine(_root, "journal")));
    }

    [Theory]
    [InlineData(FileTransferMode.Copy)]
    [InlineData(FileTransferMode.Move)]
    public async Task LongPathsPreserveStreamsAndReplacementHistory(FileTransferMode mode)
    {
        var parent = _root;
        while (parent.Length < 280) parent = Path.Combine(parent, "long-path-fixture-component");
        var sourceRoot = Directory.CreateDirectory(Path.Combine(parent, "source")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(parent, "destination")).FullName;
        var source = Path.Combine(sourceRoot, "item.txt");
        var destination = Path.Combine(destinationRoot, "item.txt");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(source + ":notes", "incoming stream");
        await File.WriteAllTextAsync(destination, "old");
        await File.WriteAllTextAsync(destination + ":notes", "old stream");
        var beforeSource = FileOperationService.CapturePathState(source);
        var beforeDestination = FileOperationService.CapturePathState(destination);
        Assert.True(FileOperationService.ArePathsOnSameVolume([source], _root));
        Assert.True(FileOperationService.CanWriteToDirectory(destinationRoot, true));
        var result = await _operations.TransferAsync([source], destinationRoot, mode, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        using (var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers))
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                await history.UndoAsync();
                Assert.True(FileOperationService.PathMatchesState(source, beforeSource));
                Assert.True(FileOperationService.PathMatchesState(destination, beforeDestination));
                await history.RedoAsync();
                Assert.True(FileOperationService.PathMatchesState(destination, beforeSource));
                Assert.Equal(mode == FileTransferMode.Copy, File.Exists(source));
            }
            // Stream edits at long paths must still block replay before mutation.
            await File.WriteAllTextAsync(destination + ":notes", "edited stream");
            await Assert.ThrowsAsync<IOException>(history.UndoAsync);
            Assert.Equal("edited stream", await File.ReadAllTextAsync(destination + ":notes"));
        }
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "journal")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(parent, ".winthunar-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(FileTransferMode.Copy)]
    [InlineData(FileTransferMode.Move)]
    public async Task ReplacementRestoresOriginalContentAndRetainsExactRedoData(FileTransferMode mode)
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceDirectory, "item.txt");
        var destination = Path.Combine(destinationDirectory, "item.txt");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(destination, "old destination");
        await File.WriteAllTextAsync(destination + ":history-test", "old stream");
        var result = await _operations.TransferAsync([source], destinationDirectory, mode, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        var transfer = Assert.Single(result.Transfers);
        Assert.Equal("old destination", await File.ReadAllTextAsync(transfer.BackupPath!));
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await history.UndoAsync();
            Assert.Equal("old destination", await File.ReadAllTextAsync(destination));
            Assert.Equal("old stream", await File.ReadAllTextAsync(destination + ":history-test"));
            Assert.Equal("incoming", await File.ReadAllTextAsync(source));
            if (mode == FileTransferMode.Copy) await File.WriteAllTextAsync(source, "source later edited");
            await history.RedoAsync();
            Assert.Equal("incoming", await File.ReadAllTextAsync(destination));
            Assert.Equal(mode == FileTransferMode.Copy, File.Exists(source));
            if (mode == FileTransferMode.Copy) await File.WriteAllTextAsync(source, "incoming");
        }
    }

    [Theory]
    [InlineData(FileTransferMode.Copy)]
    [InlineData(FileTransferMode.Move)]
    public async Task NestedMergeUndoPreservesExistingItemsAndRestoresSourceTree(FileTransferMode mode)
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source", "tree")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(destinationRoot, "tree")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        Directory.CreateDirectory(Path.Combine(destination, "nested"));
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        await File.WriteAllTextAsync(Path.Combine(source, "nested", "replace.txt"), "incoming");
        await File.WriteAllTextAsync(Path.Combine(source, "new.txt"), "new file");
        await File.WriteAllTextAsync(Path.Combine(destination, "nested", "replace.txt"), "old");
        await File.WriteAllTextAsync(Path.Combine(destination, "keep.txt"), "untouched");
        var beforeSource = FileOperationService.CapturePathState(source);
        var beforeDestination = FileOperationService.CapturePathState(destination);
        var result = await _operations.TransferAsync([source], destinationRoot, mode, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await history.UndoAsync();
            Assert.True(FileOperationService.PathMatchesState(source, beforeSource));
            Assert.True(FileOperationService.PathMatchesState(destination, beforeDestination));
            await history.RedoAsync();
            Assert.Equal("incoming", await File.ReadAllTextAsync(Path.Combine(destination, "nested", "replace.txt")));
            Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(destination, "keep.txt")));
            Assert.Equal(mode == FileTransferMode.Copy, Directory.Exists(source));
        }
        history.Dispose();
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "journal")));
    }

    [Fact]
    public async Task EditedDestinationOrBackupBlocksTheEntireBatchBeforeMutation()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(source, "b.txt"), "b");
        await File.WriteAllTextAsync(Path.Combine(destination, "b.txt"), "old b");
        var result = await _operations.TransferAsync(Directory.GetFiles(source), destination, FileTransferMode.Move, Replace, retainReplacedItems: true);
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        await File.WriteAllTextAsync(result.Transfers.Single(item => item.BackupPath is not null).BackupPath!, "backup edited");
        await Assert.ThrowsAsync<IOException>(history.UndoAsync);
        Assert.Empty(Directory.GetFiles(source));
        Assert.Equal("a", await File.ReadAllTextAsync(Path.Combine(destination, "a.txt")));
        Assert.Equal("b", await File.ReadAllTextAsync(Path.Combine(destination, "b.txt")));
    }

    [Fact]
    public async Task ChangesBeforeHistoryCreationAreNotAdoptedAsSafeUndoData()
    {
        var source = Path.Combine(_root, "item.txt");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        await File.WriteAllTextAsync(source, "incoming");
        var result = await _operations.TransferAsync([source], destination, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        await File.WriteAllTextAsync(Path.Combine(destination, "item.txt"), "later edit");
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        await Assert.ThrowsAsync<IOException>(history.UndoAsync);
        Assert.Equal("later edit", await File.ReadAllTextAsync(Path.Combine(destination, "item.txt")));
    }

    [Fact]
    public async Task CancelledMergeRetainsCompletedWorkForUndo()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source", "tree")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(destinationRoot, "tree")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "incoming");
        await File.WriteAllTextAsync(Path.Combine(source, "b.txt"), "second");
        await File.WriteAllTextAsync(Path.Combine(destination, "a.txt"), "old");
        await File.WriteAllTextAsync(Path.Combine(destination, "b.txt"), "old second");
        var count = 0;
        var result = await _operations.TransferAsync([source], destinationRoot, FileTransferMode.Move,
            conflict => Task.FromResult(new ConflictResolution(conflict.SourceIsDirectory || count++ == 0 ? ConflictAction.Replace : ConflictAction.Cancel)),
            retainReplacedItems: true);
        Assert.True(result.Cancelled);
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        await history.UndoAsync();
        Assert.Equal("incoming", await File.ReadAllTextAsync(Path.Combine(source, "a.txt")));
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(source, "b.txt")));
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(destination, "a.txt")));
        Assert.Equal("old second", await File.ReadAllTextAsync(Path.Combine(destination, "b.txt")));
    }

    [Fact]
    public async Task HistoryExpiryDeletesRetainedDataAndStartupCanCleanAnAbandonedHistory()
    {
        var source = Path.Combine(_root, "item.txt");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(Path.Combine(destination, "item.txt"), "old");
        var result = await _operations.TransferAsync([source], destination, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        var backup = Assert.Single(result.Transfers).BackupPath!;
        var recovery = _operations.RecoverInterruptedTransfers();
        Assert.Empty(recovery.Errors);
        Assert.False(File.Exists(backup));
        Assert.Equal("incoming", await File.ReadAllTextAsync(Path.Combine(destination, "item.txt")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "journal")));

        result = await _operations.TransferAsync([source], destination, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        using (var session = await TransferHistorySession.CreateAsync(_operations, result.Transfers)) await session.UndoAsync();
        Assert.DoesNotContain(Directory.EnumerateDirectories(destination), path => Path.GetFileName(path).StartsWith(".winthunar-undo-"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "journal")));
    }

    private static Task<ConflictResolution> Replace(FileConflict _) => Task.FromResult(new ConflictResolution(ConflictAction.Replace));

    [Fact]
    public async Task AlternateStreamChangesBlockUndoEvenWithUnchangedMainContentAndTimestamp()
    {
        var source = Path.Combine(_root, "item.txt");
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(source + ":notes", "original stream");
        var result = await _operations.TransferAsync([source], destinationRoot, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        var destination = Path.Combine(destinationRoot, "item.txt");
        var timestamp = File.GetLastWriteTimeUtc(destination);
        await File.WriteAllTextAsync(destination + ":notes", "modified stream");
        File.SetLastWriteTimeUtc(destination, timestamp);
        await Assert.ThrowsAsync<IOException>(history.UndoAsync);
        Assert.Equal("modified stream", await File.ReadAllTextAsync(destination + ":notes"));
    }

    [Fact]
    public async Task FailedReplayRollsBackCompletedItemsAndCanBeRetried()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(source, "b.txt"), "b");
        var result = await _operations.TransferAsync([Path.Combine(source, "a.txt"), Path.Combine(source, "b.txt")], destination,
            FileTransferMode.Move, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        using (var locked = new FileStream(Path.Combine(destination, "a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(history.UndoAsync);
            Assert.Empty(Directory.GetFiles(source));
            Assert.Equal(2, Directory.GetFiles(destination).Length);
        }
        await history.UndoAsync();
        Assert.Equal(2, Directory.GetFiles(source).Length);
        Assert.Empty(Directory.GetFiles(destination));
    }

    [Fact]
    public async Task StartupRestoresDisplacedContentWhenUndoStopsBetweenRenames()
    {
        var source = Path.Combine(_root, "item.txt");
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var destination = Path.Combine(destinationRoot, "item.txt");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(destination, "old destination");
        var result = await _operations.TransferAsync([source], destinationRoot, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        var record = Assert.Single(result.Transfers);
        _operations.SetHistoryRetention(record.RecoveryId, false);
        var storage = Directory.CreateDirectory(Path.Combine(destinationRoot, ".winthunar-undo-" + Guid.NewGuid().ToString("N") + ".tmp")).FullName;
        var parked = Path.Combine(storage, "incoming");
        _operations.RetainHistoryPath(parked, destination);
        await _operations.MoveHistoryPathAsync(destination, parked);
        var recovery = _operations.RecoverInterruptedTransfers();
        Assert.Empty(recovery.Errors);
        Assert.Equal("old destination", await File.ReadAllTextAsync(destination));
        Assert.False(File.Exists(parked));
    }

    [Fact]
    public async Task HistoryReleasesExpiredEntriesAndDefersDisposalDuringReplay()
    {
        var released = new List<int>();
        var history = new FileOperationHistory();
        for (var index = 0; index < 11; index++)
        {
            var id = index;
            history.Push(new FileHistoryEntry($"{id}", () => Task.CompletedTask, () => Task.CompletedTask, () => released.Add(id)));
        }
        Assert.Equal([0], released);
        await history.UndoAsync();
        history.Push(new FileHistoryEntry("new", () => Task.CompletedTask, () => Task.CompletedTask));
        Assert.Equal([0, 10], released);
        history.Dispose();
        Assert.Equal(11, released.Distinct().Count());

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false;
        history = new FileOperationHistory();
        history.Push(new FileHistoryEntry("pending", async () => { started.SetResult(); await resume.Task; }, () => Task.CompletedTask, () => cleaned = true));
        var replay = history.UndoAsync();
        await started.Task;
        history.Dispose();
        Assert.False(cleaned);
        await Assert.ThrowsAsync<InvalidOperationException>(history.RedoAsync);
        resume.SetResult();
        await replay;
        Assert.True(cleaned);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public async Task OpeningAnotherWindowCannotExpireALiveWindowsUndoData()
    {
        var source = Path.Combine(_root, "item.txt");
        var destinationRoot = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var destination = Path.Combine(destinationRoot, "item.txt");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(destination, "old");
        var result = await _operations.TransferAsync([source], destinationRoot, FileTransferMode.Copy, Replace, retainReplacedItems: true);
        Assert.True(result.Succeeded, string.Join(";", result.Errors));
        using var history = await TransferHistorySession.CreateAsync(_operations, result.Transfers);
        var recovery = new FileOperationService(new TransferRecoveryService(Path.Combine(_root, "journal")))
            .RecoverInterruptedTransfers(skipActiveProcesses: true);
        Assert.Equal(0, recovery.RecoveredOperations);
        Assert.Empty(recovery.Errors);
        Assert.True(File.Exists(Assert.Single(result.Transfers).BackupPath));
        await history.UndoAsync();
        Assert.Equal("old", await File.ReadAllTextAsync(destination));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RecoveryPreservesHistoryMoveDataWhenAnotherRecordReoccupiesItsSource(bool historyMove, bool matchingStage)
    {
        var source = Path.Combine(_root, "restored-original.txt");
        var destination = Path.Combine(_root, "undo-destination.txt");
        var sourceRecovery = Path.Combine(_root, ".winthunar-history-source-fixture.tmp");
        var staged = Path.Combine(_root, ".winthunar-history-fixture.tmp");
        await File.WriteAllTextAsync(source, "old destination restored by another recovery record");
        await File.WriteAllTextAsync(sourceRecovery, "incoming data that must remain accessible");
        await File.WriteAllTextAsync(sourceRecovery + ":notes", "preserved stream");
        var incoming = FileOperationService.CapturePathState(sourceRecovery);
        File.Copy(sourceRecovery, staged);
        if (!matchingStage) await File.WriteAllTextAsync(staged, "truncated or edited staging data");
        var journal = new TransferRecoveryService(Path.Combine(_root, "history-recovery"));
        journal.Register(source, destination, staged, sourceRecoveryPath: sourceRecovery,
            finishStagedMoveOnSourceConflict: historyMove);

        var result = journal.RecoverPending();

        Assert.Equal("old destination restored by another recovery record", await File.ReadAllTextAsync(source));
        if (historyMove && matchingStage)
        {
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.RecoveredOperations);
            Assert.True(FileOperationService.PathMatchesState(destination, incoming));
            Assert.False(File.Exists(sourceRecovery));
            Assert.False(File.Exists(staged));
            Assert.Equal(0, journal.RecoverPending().RecoveredOperations);
        }
        else
        {
            Assert.Single(result.Errors);
            Assert.Equal(0, result.RecoveredOperations);
            Assert.False(File.Exists(destination));
            Assert.True(FileOperationService.PathMatchesState(sourceRecovery, incoming));
            Assert.True(File.Exists(staged));
            Assert.Single(Directory.GetFiles(Path.Combine(_root, "history-recovery"), "*.json"));
        }
    }
    public void Dispose()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(_root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        LinkFileSystem.Delete(_root);
    }
}
