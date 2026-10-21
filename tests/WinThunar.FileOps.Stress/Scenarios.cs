using System.ComponentModel;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal static class Scenarios
{
    internal static Task<ConflictResolution> Replace(FileConflict _) => Task.FromResult(new ConflictResolution(ConflictAction.Replace));

    internal static async Task<string> RecoveryDoesNotBlockLiveJournalWrites(Fixture fixture)
    {
        var journal = new TransferRecoveryService(fixture.Journal);
        var operations = new FileOperationService(journal);
        var stage = Path.Combine(Path.GetDirectoryName(fixture.State.Destination)!, ".winthunar-responsiveness-fixture.tmp");
        File.WriteAllText(stage, "partial generated data");
        var id = journal.Register(fixture.State.Source, fixture.State.Destination, stage);
        var recordPath = Path.Combine(fixture.Journal, id + ".json");
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(recordPath))!;
        record["OwnerProcessId"] = 0;
        File.WriteAllText(recordPath, record.ToJsonString());
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var observation = new FileOperationFaultHooks.Scope(new(Checkpoint: point =>
        {
            if (point.Phase != "RecoveryBeforeDelete" || point.Source != stage) return;
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Recovery test pause timed out.");
        }));
        var recovery = Task.Run(() => operations.RecoverInterruptedTransfers(skipActiveProcesses: true));
        Task<string>? registration = null;
        try
        {
            Fixture.Check(entered.Wait(TimeSpan.FromSeconds(5)), "Recovery did not reach the controlled pause.");
            registration = Task.Run(() => journal.Register(fixture.State.Source, fixture.State.Destination,
                Path.Combine(Path.GetDirectoryName(fixture.State.Destination)!, ".winthunar-live-fixture.tmp")));
            var liveId = await registration.WaitAsync(TimeSpan.FromSeconds(2));
            journal.Complete(liveId);
        }
        finally
        {
            resume.Set();
            var result = await recovery;
            Fixture.Check(result.Errors.Count == 0, string.Join("; ", result.Errors));
            if (registration is not null) journal.Complete(await registration);
        }
        fixture.AssertOriginals();
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        return "A paused recovery worker did not hold the live service's journal lock; another transfer registered while recovery was still paused. Process lease and live-owner protection remained enabled.";
    }

    internal static async Task<string> RealDiskFull(Fixture fixture, DisposableVolume volume, FileTransferMode mode, bool requireProgress = false)
    {
        var allocated = volume.Fill(fixture, leaveRoomForMainStream: requireProgress);
        var progress = 0L;
        int? observedError = null;
        // Observe the real native error before the service turns it into a UI error string.
        // No forced fallback, injected error, or callback mutation is enabled here.
        using (var observation = new FileOperationFaultHooks.Scope(new(Checkpoint: point =>
        {
            if (point.Phase == "NativeCopyProgress") progress = Math.Max(progress, point.Bytes);
            if (point.Phase == "NativeCopyFailed") observedError = (int)point.Bytes;
        })))
        {
            var result = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                mode, Replace, retainReplacedItems: true);
            Fixture.Check(observedError is 112 or 39, $"Expected a real native disk-full error (112/39), observed {observedError}: {string.Join("; ", result.Errors)}");
            Fixture.Check(!result.Succeeded && !result.Cancelled && result.Errors.Count > 0 && result.Transfers.Count == 0,
                "Real disk-full was not reported as a failed transfer.");
            Fixture.Check(!requireProgress || progress >= Fixture.PayloadBytes,
                $"Disk-full occurred without the required completed main-stream progress ({progress} bytes). This does not establish failure after partial writes.");
        }
        fixture.AssertOriginals();
        // Release only this fixture's filler so recovery and a real retry have working space.
        File.Delete(Path.Combine(fixture.State.ExternalRoot!, "capacity-filler.bin"));
        var recovery = fixture.Operations().RecoverInterruptedTransfers();
        Fixture.Check(recovery.Errors.Count == 0, string.Join("; ", recovery.Errors));
        fixture.AssertOriginals();
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        var retry = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
            mode, Replace, retainReplacedItems: true);
        Fixture.Check(retry.Succeeded && retry.Transfers.Count == 1, "Retry after freeing capacity failed: " + string.Join("; ", retry.Errors));
        fixture.AssertIncoming(fixture.State.PayloadDestination);
        using (var history = await TransferHistorySession.CreateAsync(fixture.Operations(), retry.Transfers))
        {
            await history.UndoAsync();
            fixture.AssertOriginals();
        }
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        return $"Bounded disposable NTFS VHD returned native error {observedError}; {allocated} filler bytes, {progress} copy bytes observed. Originals/streams preserved; recovery, retry and Undo passed.";
    }

    internal static async Task<string> NativeFailure(Fixture fixture, int error, FileTransferMode mode)
    {
        var injected = 0;
        using (var faults = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: true, CopyError: point =>
        {
            if (point.Bytes < 64 * 1024) return null;
            Interlocked.Increment(ref injected);
            return error;
        })))
        {
            var result = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                mode, Replace, retainReplacedItems: true);
            Fixture.Check(injected > 0, "The requested native error was never injected.");
            Fixture.Check(!result.Succeeded && !result.Cancelled && result.Errors.Any(message => message.Contains($"error {error}:")),
                "Native failure was not reported as an operation error: " + string.Join("; ", result.Errors));
            Fixture.Check(result.Transfers.Count == 0, "A failed incoming item was recorded as a completed transfer.");
        }
        fixture.AssertOriginals();
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        return $"Injected Windows error {error} after native copy progress. Source, old destination, metadata and streams unchanged; staging cleaned.";
    }

    internal static async Task<string> Cancellation(Fixture fixture, bool beforeStart)
    {
        using var cancellation = new CancellationTokenSource();
        var progressed = false;
        if (beforeStart) cancellation.Cancel();
        using var faults = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: true, Checkpoint: point =>
        {
            if (point.Phase == "NativeCopyProgress" && point.Bytes > 0)
            {
                progressed = true;
                cancellation.Cancel();
            }
        }));
        var result = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
            FileTransferMode.Move, Replace, cancellationToken: cancellation.Token, retainReplacedItems: true);
        Fixture.Check(result.Cancelled && result.Errors.Count == 0, "Cancellation was not returned as cancellation.");
        Fixture.Check(beforeStart || progressed, "The copy never reached a cancellation point.");
        fixture.AssertOriginals();
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        return "Cancellation kept both originals and cleaned incomplete incoming data.";
    }

    internal static async Task<string> LockedCleanupAndUnavailableRecovery(Fixture fixture)
    {
        FileStream? locked = null;
        try
        {
            using (var faults = new FileOperationFaultHooks.Scope(new(CopyError: point => point.Bytes >= 64 * 1024 ? 112 : null,
                Checkpoint: point =>
                {
                    if (point.Phase == "FileBeforeStagingCleanup")
                    {
                        // Some copy providers remove their partial file themselves. Model a
                        // provider that leaves a remnant, then exercise a real Windows sharing lock.
                        if (!File.Exists(point.Destination)) File.WriteAllBytes(point.Destination, new byte[64 * 1024]);
                        locked = new FileStream(point.Destination, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                })))
            {
                var result = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                    FileTransferMode.Copy, Replace, retainReplacedItems: true);
                Fixture.Check(!result.Succeeded && locked is not null, "The cleanup lock was not exercised.");
            }
            fixture.AssertOriginals();
            Fixture.Check(Directory.EnumerateFiles(fixture.Journal, "*.json").Any(), "Failed staging cleanup lost its recovery record.");
        }
        finally { locked?.Dispose(); }

        using (var unavailable = new FileOperationFaultHooks.Scope(new(Checkpoint: point =>
        {
            if (point.Phase == "RecoveryBeforeDelete") throw new Win32Exception(1167, "Injected device unavailable during recovery cleanup.");
        })))
        {
            var retry = fixture.Operations().RecoverInterruptedTransfers();
            Fixture.Check(retry.Errors.Count > 0 && retry.RecoveredOperations == 0, "Unavailable recovery was incorrectly reported as successful.");
            Fixture.Check(Directory.EnumerateFiles(fixture.Journal, "*.json").Any(), "Failed recovery removed its journal record.");
        }
        var retried = fixture.Operations().RecoverInterruptedTransfers(); // Explicit retry of this process's failed recovery.
        Fixture.Check(retried.Errors.Count == 0, string.Join("; ", retried.Errors));
        AssertRecovered(fixture);
        fixture.AssertOriginals();
        return "A fixture staging remnant and real sharing lock blocked cleanup; an injected device outage blocked recovery. Both retained the journal and recovered on retry.";
    }

    internal static async Task<string> HistoryRoundTrip(Fixture fixture, bool forceFallback, bool actualVolume, bool alternateStreams = true)
    {
        if (actualVolume) Fixture.Check(!FileOperationService.ArePathsOnSameVolume([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!),
            "The supplied path is on the source volume; this cannot count as a real cross-volume test.");
        var copies = 0;
        using var faults = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: forceFallback, Checkpoint: point =>
        { if (point.Phase == "NativeCopyProgress" && point.Bytes > 0) Interlocked.Increment(ref copies); }));
        var operations = fixture.Operations();
        var result = await operations.TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
            FileTransferMode.Move, Replace, retainReplacedItems: true);
        Fixture.Check(result.Succeeded && result.Transfers.Count == 1, string.Join("; ", result.Errors));
        Fixture.Check(!LinkFileSystem.Exists(fixture.State.Source), "Move left its source behind.");
        fixture.AssertIncoming(fixture.State.PayloadDestination);
        using (var history = await TransferHistorySession.CreateAsync(operations, result.Transfers))
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                await history.UndoAsync();
                fixture.AssertOriginals();
                await history.RedoAsync();
                Fixture.Check(!LinkFileSystem.Exists(fixture.State.Source), "Redo left its move source behind.");
                fixture.AssertIncoming(fixture.State.PayloadDestination);
                if (Directory.Exists(fixture.State.Destination))
                    Fixture.Check(File.ReadAllText(Path.Combine(fixture.State.Destination, "keep.txt")) == "unrelated destination item", "Merge damaged an unrelated item.");
            }
        }
        Fixture.Check(copies > 0, "The fallback native-copy path was never exercised.");
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        return actualVolume ? alternateStreams ? "Real distinct-volume Move/Undo/Redo preserved metadata and alternate streams across two cycles."
            : "Real distinct-volume Move/Undo/Redo preserved file data, timestamps and directory structure across two cycles. This basic non-NTFS test contains no alternate streams."
            : "Forced copy/delete fallback on one volume preserved replacement/merge data through two Undo/Redo cycles; this is not a real second volume.";
    }

    internal static async Task<string> KilledTransfer(Fixture fixture, string phase, FileTransferMode mode)
    {
        var point = await CrashWorker.KillAtCheckpointAsync(fixture, new("transfer", phase, mode, ForceCopyDelete: true));
        AssertRecovered(fixture);
        if (phase is "ReplacementAfterPromotion" or "ReplacementAfterSourceStaged" or "FileAfterPromotion")
        {
            fixture.AssertIncoming(fixture.State.PayloadDestination);
            // An interrupted cross-volume file move may still have its complete original source.
            if (LinkFileSystem.Exists(fixture.State.PayloadSource)) fixture.AssertIncoming(fixture.State.PayloadSource);
        }
        else
        {
            fixture.AssertOldPayload();
            Fixture.Check(FileOperationService.PathMatchesState(fixture.State.Source, fixture.State.SourceBefore), "Crash before promotion damaged its source.");
        }
        if (Directory.Exists(fixture.State.Destination))
            Fixture.Check(File.ReadAllText(Path.Combine(fixture.State.Destination, "keep.txt")) == "unrelated destination item", "Interrupted merge damaged an unrelated item.");
        return $"Killed owned worker at {point.Phase} ({point.Bytes}/{point.TotalBytes} bytes when applicable); live-owner skip and repeat startup recovery passed.";
    }

    internal static async Task<string> KilledRecovery(Fixture fixture)
    {
        await CrashWorker.KillAtCheckpointAsync(fixture, new("transfer", "ReplacementAfterBackup", FileTransferMode.Copy));
        await CrashWorker.KillAtCheckpointAsync(fixture, new("recover", "RecoveryAfterBackupRestore", FileTransferMode.Copy));
        AssertRecovered(fixture);
        fixture.AssertOriginals();
        return "Killed transfer after backing up the destination, then killed recovery after restoring it. A second restart completed cleanup without changing originals.";
    }

    internal static async Task<string> KilledHistory(Fixture fixture, string phase, bool originalFirst)
    {
        await CrashWorker.KillAtCheckpointAsync(fixture, new("undo", phase,
            phase == "HistoryAfterIncomingParked" ? FileTransferMode.Copy : FileTransferMode.Move, ForceCopyDelete: true));
        if (phase != "HistoryAfterIncomingParked")
        {
            // Force each journal ordering by temporarily withholding one record. Never delete a
            // record to simulate order: it must still be recovered in the second pass.
            var records = Directory.GetFiles(fixture.Journal, "*.json");
            var original = records.Single(path =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.GetProperty("BackupPath").ValueKind == JsonValueKind.String;
            });
            var replay = records.Single(path => path != original);
            var withheld = originalFirst ? replay : original;
            File.Move(withheld, withheld + ".waiting");
            try
            {
                var first = fixture.Operations().RecoverInterruptedTransfers(skipActiveProcesses: true);
                Fixture.Check(first.Errors.Count == 0, string.Join("; ", first.Errors));
            }
            finally { File.Move(withheld + ".waiting", withheld); }
        }
        AssertRecovered(fixture);
        var inputAtSource = LinkFileSystem.Exists(fixture.State.PayloadSource) && FileOperationService.PathMatchesState(fixture.State.PayloadSource, fixture.State.PayloadBefore);
        var inputAtDestination = LinkFileSystem.Exists(fixture.State.PayloadDestination) && FileOperationService.PathMatchesState(fixture.State.PayloadDestination, fixture.State.PayloadBefore);
        Fixture.Check(inputAtSource || inputAtDestination, "Interrupted Undo lost the incoming data at both user-visible paths.");
        if (inputAtSource) fixture.AssertOldPayload();
        return $"Killed Undo at {phase}; {(originalFirst ? "original" : "replay")} journal first. Incoming data stayed accessible and cleanup was repeatable.";
    }

    private static void AssertRecovered(Fixture fixture)
    {
        var recovery = fixture.Operations().RecoverInterruptedTransfers(skipActiveProcesses: true);
        Fixture.Check(recovery.Errors.Count == 0, string.Join("; ", recovery.Errors));
        fixture.AssertJournalEmpty();
        fixture.AssertNoTemporaryItems();
        var again = fixture.Operations().RecoverInterruptedTransfers(skipActiveProcesses: true);
        Fixture.Check(again.Errors.Count == 0 && again.RecoveredOperations == 0, "Startup recovery is not idempotent.");
    }
}
