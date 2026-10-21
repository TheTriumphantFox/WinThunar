using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace WinThunar.Services;

public sealed record TransferRecoveryIssue(string JournalPath, string Message,
    string? SourcePath = null, string? DestinationPath = null, string? StagedPath = null,
    string? BackupPath = null, string? SourceRecoveryPath = null);

public sealed record TransferRecoveryResult(int RecoveredOperations, IReadOnlyList<string> Errors)
{
    public IReadOnlyList<TransferRecoveryIssue> Issues { get; init; } = [];
    public int ActiveOperations { get; init; }
    public bool RecoveryBusy { get; init; }
}

public sealed class TransferRecoveryService
{
    private static readonly long ProcessStartTicks = ReadCurrentProcessStart();
    private readonly string _journalDirectory;
    private readonly object _sync = new();

    public TransferRecoveryService(string? journalDirectory = null)
    {
        _journalDirectory = journalDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinThunar",
            "TransferRecovery");
    }

    // Recovery uses the journal-wide process lease, while its instance lock stays separate from
    // live operations. A disconnected provider must not block another window's history cleanup
    // or registration of a new transfer behind the recovery worker's instance lock.
    internal TransferRecoveryService CreateRecoveryWorker() => new(_journalDirectory);

    public string Register(
        string sourcePath,
        string destinationPath,
        string stagedPath,
        string? backupPath = null,
        string? sourceRecoveryPath = null,
        string? backupRoot = null,
        bool finishStagedMoveOnSourceConflict = false)
    {
        var record = new RecoveryRecord(
            Guid.NewGuid().ToString("N"),
            sourcePath,
            destinationPath,
            stagedPath,
            backupPath,
            sourceRecoveryPath,
            backupRoot,
            OwnerProcessId: Environment.ProcessId,
            OwnerStartTicks: ProcessStartTicks,
            FinishStagedMoveOnSourceConflict: finishStagedMoveOnSourceConflict);
        lock (_sync)
        {
            Directory.CreateDirectory(_journalDirectory);
            var path = RecordPath(record.Id);
            var temporaryPath = path + ".new";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(record));
            File.Move(temporaryPath, path, true);
        }

        return record.Id;
    }

    public void CommitRetained(string id, bool retained = true)
    {
        lock (_sync)
        {
            var path = RecordPath(id);
            var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                ?? throw new InvalidDataException("The recovery record is empty.");
            var temporary = path + ".new";
            File.WriteAllText(temporary, JsonSerializer.Serialize(record with { Retained = retained }));
            File.Move(temporary, path, true);
        }
    }

    internal void RecordPreparedDestination(string id, PathStateSnapshot state)
    {
        lock (_sync)
        {
            var path = RecordPath(id);
            var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                ?? throw new InvalidDataException("The recovery record is empty.");
            var temporary = path + ".new";
            File.WriteAllText(temporary, JsonSerializer.Serialize(record with { PreparedDestination = state }));
            File.Move(temporary, path, true);
        }
    }

    internal void ConfirmSourceCleanup(string id)
    {
        lock (_sync)
        {
            var path = RecordPath(id);
            var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                ?? throw new InvalidDataException("The recovery record is empty.");
            var temporary = path + ".new";
            File.WriteAllText(temporary, JsonSerializer.Serialize(record with { SourceRecoveryPath = null }));
            File.Move(temporary, path, true);
        }
    }

    public void Complete(string id)
    {
        lock (_sync)
        {
            var path = RecordPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void ReleaseRetained(string id)
    {
        lock (_sync)
        {
            var path = RecordPath(id);
            if (!File.Exists(path)) return;
            var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                ?? throw new InvalidDataException("The recovery record is empty.");
            if (!record.Retained) throw new IOException("The history operation still needs recovery.");
            Recover(record);
            File.Delete(path);
        }
    }

    public TransferRecoveryResult RecoverPending(bool skipActiveProcesses = false)
    {
        if (!Directory.Exists(_journalDirectory))
        {
            return new TransferRecoveryResult(0, []);
        }

        var recovered = 0;
        var errors = new List<string>();
        var issues = new List<TransferRecoveryIssue>();
        var active = 0;
        // A recovering process also needs exclusive ownership. The originating transfer may
        // already be dead, so its PID alone cannot prevent concurrent restart recovery.
        var journalIdentity = Path.GetFullPath(_journalDirectory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var mutexName = @"Local\WinThunar.Recovery." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(journalIdentity)));
        Mutex? lease = null;
        bool acquired;
        try { lease = new Mutex(false, mutexName); acquired = lease.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            lease?.Dispose();
            return new TransferRecoveryResult(0, [$"Could not acquire the recovery lease: {ex.Message}"]);
        }
        using var ownedLease = lease!;
        if (!acquired) return new TransferRecoveryResult(0, []) { RecoveryBusy = true };
        try
        {
            lock (_sync)
            {
                foreach (var path in Directory.EnumerateFiles(_journalDirectory, "*.json").ToArray())
                {
                    RecoveryRecord? record = null;
                    try
                    {
                        record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                            ?? throw new InvalidDataException("The recovery record is empty.");
                        // The lease prevents overlapping recoveries. A failed recovery claimed by
                        // this process is retryable, while live transfers/history remain protected.
                        var ownRecovery = record.RecoveryClaimed && record.OwnerProcessId == Environment.ProcessId &&
                            record.OwnerStartTicks == ProcessStartTicks;
                        if (skipActiveProcesses && !ownRecovery && IsOwnerAlive(record)) { active++; continue; }
                        // Persist the recovery owner before mutating filesystem items. Atomic
                        // replacement leaves the previous complete record if writing is interrupted.
                        record = record with { OwnerProcessId = Environment.ProcessId, OwnerStartTicks = ProcessStartTicks, RecoveryClaimed = true };
                        var claim = path + ".new";
                        File.WriteAllText(claim, JsonSerializer.Serialize(record));
                        File.Move(claim, path, true);
                        Recover(record);
                        File.Delete(path);
                        recovered++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                        issues.Add(new(path, ex.Message, record?.SourcePath, record?.DestinationPath,
                            record?.StagedPath, record?.BackupPath, record?.SourceRecoveryPath));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            errors.Add($"Could not read recovery records: {ex.Message}");
        }
        finally { ownedLease.ReleaseMutex(); }

        return new TransferRecoveryResult(recovered, errors) { Issues = issues, ActiveOperations = active };
    }

    private static long ReadCurrentProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime().Ticks;
    }

    private static bool IsOwnerAlive(RecoveryRecord record)
    {
        if (record.OwnerProcessId <= 0) return false; // Earlier journal format, from a previous release.
        try
        {
            using var process = Process.GetProcessById(record.OwnerProcessId);
            return process.StartTime.ToUniversalTime().Ticks == record.OwnerStartTicks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; } // Cannot prove the owner exited.
    }

    private static void Recover(RecoveryRecord record)
    {
        ValidateTemporarySibling(record.StagedPath, record.DestinationPath);
        if (!string.IsNullOrWhiteSpace(record.BackupPath))
        {
            if (record.BackupRoot is null) ValidateTemporarySibling(record.BackupPath, record.DestinationPath);
            else ValidateBackupRoot(record.BackupRoot, record.BackupPath, record.DestinationPath);
        }
        if (!string.IsNullOrWhiteSpace(record.SourceRecoveryPath))
        {
            ValidateTemporarySibling(record.SourceRecoveryPath, record.SourcePath);
        }

        // Missing leaves do not establish that an unavailable share/device contains no staging
        // data. Prove the containing folders are accessible before interpreting absence.
        RequireAvailableParent(record.DestinationPath);
        if (!string.IsNullOrWhiteSpace(record.SourceRecoveryPath)) RequireAvailableParent(record.SourcePath);

        // A completed transfer's undo data expires with the process/window history.
        if (record.Retained)
        {
            if (Exists(record.SourceRecoveryPath))
            {
                if (Exists(record.DestinationPath)) Delete(record.SourceRecoveryPath);
                else if (!Exists(record.SourcePath)) Move(record.SourceRecoveryPath!, record.SourcePath);
                else throw new IOException("The retained source needs manual recovery because its destination is missing and its source name is occupied.");
            }
            Delete(record.BackupPath);
            DeleteEmptyBackupRoot(record.BackupRoot);
            return;
        }

        // The destination name alone does not prove promotion completed. Another application
        // may have occupied it or edited it after the crash. Preserve displaced originals until
        // the complete staged content (including streams) is proven at the destination.
        if (Exists(record.DestinationPath) && (Exists(record.BackupPath) || Exists(record.SourceRecoveryPath)))
        {
            if (record.PreparedDestination is null ||
                !FileOperationService.PathMatchesState(record.DestinationPath, record.PreparedDestination))
                throw new IOException("The destination cannot be verified as the completed transfer. All copies and the recovery record were retained for review.");
        }

        if (Exists(record.SourceRecoveryPath) && !Exists(record.DestinationPath))
        {
            if (!Exists(record.SourcePath)) Move(record.SourceRecoveryPath!, record.SourcePath);
            else if (record.FinishStagedMoveOnSourceConflict && Exists(record.StagedPath))
            {
                // Another record can restore the old replacement into this move's source name.
                // A history move stages a complete copy before parking its source. Complete that
                // move into its still-free destination rather than discarding the incoming data.
                var sourceState = FileOperationService.CapturePathState(record.SourceRecoveryPath!);
                if (!FileOperationService.PathMatchesState(record.StagedPath, sourceState))
                    throw new IOException("The staged history move differs from its preserved source. Both copies and the recovery record were retained.");
                Move(record.StagedPath, record.DestinationPath);
            }
            else throw new IOException("The source name is occupied and its recovery item still needs attention. All staged data and the recovery record were retained.");
        }

        if (Exists(record.BackupPath) && !Exists(record.DestinationPath))
        {
            Move(record.BackupPath!, record.DestinationPath);
#if FILEOPS_FAULT_TESTS
            FileOperationFaultHooks.Hit("RecoveryAfterBackupRestore", record.SourcePath, record.DestinationPath);
#endif
        }

        Delete(record.StagedPath);

        if (Exists(record.SourceRecoveryPath))
        {
            if (Exists(record.DestinationPath))
            {
                Delete(record.SourceRecoveryPath!);
            }
            else if (!Exists(record.SourcePath))
            {
                Move(record.SourceRecoveryPath!, record.SourcePath);
            }
        }

        if (Exists(record.BackupPath))
        {
            if (!Exists(record.DestinationPath))
            {
                Move(record.BackupPath!, record.DestinationPath);
            }
            else
            {
                Delete(record.BackupPath!);
            }
        }
        DeleteEmptyBackupRoot(record.BackupRoot);
    }

    private static void DeleteEmptyBackupRoot(string? path)
    {
        if (path is not null && Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
    }

    private static void RequireAvailableParent(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidDataException("A recovery path has no parent folder.");
        if (!File.GetAttributes(parent).HasFlag(FileAttributes.Directory))
            throw new IOException("A recovery parent is not an available folder.");
    }

    private static void ValidateBackupRoot(string rootPath, string backupPath, string destinationPath)
    {
        var root = Path.GetFullPath(rootPath);
        var backup = Path.GetFullPath(backupPath);
        var destination = Path.GetFullPath(destinationPath);
        var parent = Path.GetDirectoryName(root)!;
        if (!Path.GetFileName(root).StartsWith(".winthunar-undo-", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(backup), root, StringComparison.OrdinalIgnoreCase) ||
            !destination.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            (Directory.Exists(root) && LinkFileSystem.IsReparsePoint(root)))
            throw new InvalidDataException("A recovery record contains an invalid undo backup path.");
    }

    private string RecordPath(string id) => Path.Combine(_journalDirectory, id + ".json");

    private static bool Exists(string? path) =>
        !string.IsNullOrWhiteSpace(path) && LinkFileSystem.Exists(path);

    private static void Move(string sourcePath, string destinationPath)
    {
        if (LinkFileSystem.IsDirectory(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private static void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

#if FILEOPS_FAULT_TESTS
        FileOperationFaultHooks.Hit("RecoveryBeforeDelete", path, path);
#endif
        LinkFileSystem.Delete(path);
    }

    private static void ValidateTemporarySibling(string temporaryPath, string relatedPath)
    {
        var temporary = Path.GetFullPath(temporaryPath);
        var related = Path.GetFullPath(relatedPath);
        if (!Path.GetFileName(temporary).StartsWith(".winthunar-", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetDirectoryName(temporary),
                Path.GetDirectoryName(related),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A recovery record contains an invalid staging path.");
        }
    }

    private sealed record RecoveryRecord(
        string Id,
        string SourcePath,
        string DestinationPath,
        string StagedPath,
        string? BackupPath,
        string? SourceRecoveryPath,
        string? BackupRoot = null,
        bool Retained = false,
        int OwnerProcessId = 0,
        long OwnerStartTicks = 0,
        bool FinishStagedMoveOnSourceConflict = false,
        bool RecoveryClaimed = false,
        PathStateSnapshot? PreparedDestination = null);
}
