using System.Text.Json;

namespace WinThunar.Services;

public sealed record TransferRecoveryResult(int RecoveredOperations, IReadOnlyList<string> Errors);

public sealed class TransferRecoveryService
{
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
        string? sourceRecoveryPath = null)
    {
        var record = new RecoveryRecord(
            Guid.NewGuid().ToString("N"),
            sourcePath,
            destinationPath,
            stagedPath,
            backupPath,
            sourceRecoveryPath);
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

    public TransferRecoveryResult RecoverPending()
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

    private static void Recover(RecoveryRecord record)
    {
        ValidateTemporarySibling(record.StagedPath, record.DestinationPath);
        if (!string.IsNullOrWhiteSpace(record.BackupPath))
        {
            ValidateTemporarySibling(record.BackupPath, record.DestinationPath);
        }
        if (!string.IsNullOrWhiteSpace(record.SourceRecoveryPath))
        {
            ValidateTemporarySibling(record.SourceRecoveryPath, record.SourcePath);
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
    }

    private string RecordPath(string id) => Path.Combine(_journalDirectory, id + ".json");

    private static bool Exists(string? path) =>
        !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));

    private static void Move(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
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

        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else if (File.Exists(path))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            File.Delete(path);
        }
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
        string? SourceRecoveryPath);
}
