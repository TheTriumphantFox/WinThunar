using System.Text.Json;
using System.Diagnostics;

namespace WinThunar.Services;

public sealed record TransferRecoveryResult(int RecoveredOperations, IReadOnlyList<string> Errors);

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

    public string Register(
        string sourcePath,
        string destinationPath,
        string stagedPath,
        string? backupPath = null,
        string? sourceRecoveryPath = null,
        string? backupRoot = null)
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
            OwnerStartTicks: ProcessStartTicks);
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
        lock (_sync)
        {
            foreach (var path in Directory.EnumerateFiles(_journalDirectory, "*.json").ToArray())
            {
                try
                {
                    var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path))
                        ?? throw new InvalidDataException("The recovery record is empty.");
                    if (skipActiveProcesses && IsOwnerAlive(record)) continue;
                    Recover(record);
                    File.Delete(path);
                    recovered++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }
        }

        return new TransferRecoveryResult(recovered, errors);
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

        if (Exists(record.SourceRecoveryPath) && !Exists(record.SourcePath) && !Exists(record.DestinationPath))
        {
            Move(record.SourceRecoveryPath!, record.SourcePath);
        }

        if (Exists(record.BackupPath) && !Exists(record.DestinationPath))
        {
            Move(record.BackupPath!, record.DestinationPath);
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
        long OwnerStartTicks = 0);
}
