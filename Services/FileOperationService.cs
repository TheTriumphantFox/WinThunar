using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace WinThunar.Services;

public enum FileTransferMode
{
    Copy,
    Move
}

public enum ConflictAction
{
    Replace,
    Skip,
    Rename,
    Cancel
}

public sealed record FileConflict(string SourcePath, string DestinationPath, bool SourceIsDirectory);

public sealed record ConflictResolution(ConflictAction Action, string? NewName = null);

public sealed record FileOperationProgress(
    string ItemName,
    int CompletedItems,
    int TotalItems,
    long BytesTransferred = 0,
    long TotalBytes = 0);

public sealed record PathStateEntry(
    string RelativePath,
    bool IsDirectory,
    long Length,
    long LastWriteTimeUtcTicks,
    FileAttributes Attributes,
    string? ContentHash);

public sealed record PathStateSnapshot(IReadOnlyList<PathStateEntry> Entries);

public sealed record FileTransferRecord(
    string SourcePath,
    string DestinationPath,
    FileTransferMode Mode,
    bool ReplacedExistingItem,
    PathStateSnapshot DestinationState);

public sealed record FileOperationResult(
    int CompletedItems,
    int SkippedItems,
    IReadOnlyList<string> Errors,
    bool Cancelled,
    IReadOnlyList<FileTransferRecord> Transfers)
{
    public bool Succeeded => !Cancelled && Errors.Count == 0;
}

public sealed class FileOperationService
{
    private readonly TransferRecoveryService _recovery;

    public FileOperationService(TransferRecoveryService? recovery = null)
    {
        _recovery = recovery ?? new TransferRecoveryService();
    }

    public TransferRecoveryResult RecoverInterruptedTransfers() => _recovery.RecoverPending();

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string? ValidateLeafName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "The name cannot be empty.";
        }

        if (name is "." or "..")
        {
            return "That name is reserved by the filesystem.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "The name contains a character Windows does not allow.";
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return "Windows names cannot end with a space or period.";
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        if (ReservedDeviceNames.Contains(stem))
        {
            return "That name is reserved by Windows.";
        }

        return null;
    }

    public static string? ValidateUncPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Enter a network path.";
        }

        var value = path.Trim();
        if (!value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"Enter a UNC path such as \\server\share.";
        }

        try
        {
            _ = Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "The network path is not valid.";
        }

        var parts = value.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? null : @"Include both a server and share, such as \\server\share.";
    }

    public static string GetAvailableLeafName(string parentDirectory, string preferredName, bool isDirectory = false)
    {
        EnsureDirectoryExists(parentDirectory);
        EnsureValidLeafName(preferredName);

        if (!PathExists(Path.Combine(parentDirectory, preferredName)))
        {
            return preferredName;
        }

        var extension = isDirectory ? string.Empty : Path.GetExtension(preferredName);
        var stem = isDirectory ? preferredName : Path.GetFileNameWithoutExtension(preferredName);
        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{stem} {suffix}{extension}";
            if (!PathExists(Path.Combine(parentDirectory, candidate)))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find an available name based on '{preferredName}'.");
    }

    public async Task<string> CreateDirectoryAsync(
        string parentDirectory,
        string name,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(parentDirectory);
        EnsureValidLeafName(name);
        var path = Path.Combine(parentDirectory, name);
        EnsurePathDoesNotExist(path);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
        }, cancellationToken);

        return path;
    }

    public async Task<string> CreateFileAsync(
        string parentDirectory,
        string name,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(parentDirectory);
        EnsureValidLeafName(name);
        var path = Path.Combine(parentDirectory, name);
        EnsurePathDoesNotExist(path);

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1,
            FileOptions.Asynchronous);
        await stream.FlushAsync(cancellationToken);
        return path;
    }

    public async Task<string> RenameAsync(
        string sourcePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        EnsurePathExists(sourcePath);
        EnsureValidLeafName(newName);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(sourcePath))
            ?? throw new InvalidOperationException("The filesystem root cannot be renamed here.");
        var destinationPath = Path.Combine(parent, newName);

        if (PathsExactlyEqual(sourcePath, destinationPath))
        {
            return sourcePath;
        }

        var caseOnlyRename = PathsEqual(sourcePath, destinationPath);
        if (!caseOnlyRename)
        {
            EnsurePathDoesNotExist(destinationPath);
        }
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!caseOnlyRename)
            {
                MovePath(sourcePath, destinationPath);
                return;
            }

            var temporaryPath = GetTemporarySiblingPath(sourcePath, "case-rename");
            MovePath(sourcePath, temporaryPath);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                MovePath(temporaryPath, destinationPath);
            }
            catch
            {
                if (PathExists(temporaryPath) && !PathExists(sourcePath))
                {
                    MovePath(temporaryPath, sourcePath);
                }

                throw;
            }
        }, cancellationToken);

        return destinationPath;
    }

    public async Task<FileOperationResult> TransferAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(conflictResolver);
        EnsureDirectoryExists(destinationDirectory);

        var sources = RemoveNestedSelections(sourcePaths);
        var errors = new List<string>();
        var completed = 0;
        var skipped = 0;
        var cancelled = false;
        var transfers = new List<FileTransferRecord>();

        foreach (var sourcePath in sources)
        {
            var itemJournal = new List<FileTransferRecord>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsurePathExists(sourcePath);

                if (Directory.Exists(sourcePath) && IsSameOrDescendantPath(destinationDirectory, sourcePath))
                {
                    throw new IOException("A folder cannot be copied or moved into itself.");
                }

                var destinationPath = Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath)));

                var outcome = await TransferPathAsync(
                    sourcePath,
                    destinationPath,
                    mode,
                    conflictResolver,
                    cancellationToken,
                    itemJournal,
                    (itemPath, bytesTransferred, totalBytes) =>
                        progress?.Report(new FileOperationProgress(
                            Path.GetFileName(Path.TrimEndingDirectorySeparator(itemPath)),
                            completed + skipped,
                            sources.Count,
                            bytesTransferred,
                            totalBytes)));

                if (outcome.Outcome == TransferOutcome.Cancelled)
                {
                    transfers.AddRange(itemJournal);
                    cancelled = true;
                    break;
                }

                if (outcome.Outcome == TransferOutcome.Skipped)
                {
                    transfers.AddRange(itemJournal);
                    skipped++;
                }
                else
                {
                    completed++;
                    var capturedState = FindCapturedState(itemJournal, outcome.DestinationPath)
                        ?? await CapturePathStateAsync(outcome.DestinationPath, cancellationToken);
                    transfers.Add(new FileTransferRecord(
                        sourcePath,
                        outcome.DestinationPath,
                        mode,
                        outcome.ReplacedExistingItem,
                        capturedState));
                }

                progress?.Report(new FileOperationProgress(
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath)),
                    completed + skipped,
                    sources.Count));
            }
            catch (OperationCanceledException)
            {
                transfers.AddRange(itemJournal);
                cancelled = true;
                break;
            }
            catch (Exception ex)
            {
                transfers.AddRange(itemJournal);
                errors.Add($"{sourcePath}: {ex.Message}");
            }
        }

        return new FileOperationResult(completed, skipped, errors, cancelled, transfers);
    }

    public async Task<FileOperationResult> TransferExactAsync(
        string sourcePath,
        string destinationPath,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflictResolver);
        EnsurePathExists(sourcePath);
        var destinationDirectory = Path.GetDirectoryName(
            Path.TrimEndingDirectorySeparator(destinationPath))
            ?? throw new InvalidOperationException("A filesystem root cannot be a transfer destination.");
        EnsureDirectoryExists(destinationDirectory);

        if (Directory.Exists(sourcePath) && IsSameOrDescendantPath(destinationDirectory, sourcePath))
        {
            throw new IOException("A folder cannot be copied or moved into itself.");
        }

        var journal = new List<FileTransferRecord>();
        try
        {
            var outcome = await TransferPathAsync(
                sourcePath,
                destinationPath,
                mode,
                conflictResolver,
                cancellationToken,
                journal,
                null);
            if (outcome.Outcome == TransferOutcome.Completed)
            {
                var capturedState = FindCapturedState(journal, outcome.DestinationPath)
                    ?? await CapturePathStateAsync(outcome.DestinationPath, cancellationToken);
                return new FileOperationResult(
                    1,
                    0,
                    [],
                    false,
                    [new FileTransferRecord(
                        sourcePath,
                        outcome.DestinationPath,
                        mode,
                        outcome.ReplacedExistingItem,
                        capturedState)]);
            }

            return outcome.Outcome == TransferOutcome.Skipped
                ? new FileOperationResult(0, 1, [], false, journal)
                : new FileOperationResult(0, 0, [], true, journal);
        }
        catch (OperationCanceledException)
        {
            return new FileOperationResult(0, 0, [], true, journal);
        }
        catch (Exception ex)
        {
            return new FileOperationResult(0, 0, [ex.Message], false, journal);
        }
    }

    public async Task MoveToTrashAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var selectedPaths = RemoveNestedSelections(paths);
        await Task.Run(() =>
        {
            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsurePathExists(path);

                if (Directory.Exists(path))
                {
                    FileSystem.DeleteDirectory(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.ThrowException);
                }
                else
                {
                    FileSystem.DeleteFile(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.ThrowException);
                }
            }
        }, cancellationToken);
    }

    public async Task<FileOperationResult> RestoreRecycleItemAsync(
        string recycledPath,
        string originalPath,
        string metadataPath,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recycledPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);
        ArgumentNullException.ThrowIfNull(conflictResolver);

        EnsurePathExists(recycledPath);
        var destinationDirectory = Path.GetDirectoryName(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(originalPath)))
            ?? throw new InvalidOperationException("A filesystem root cannot be restored from the Recycle Bin.");
        Directory.CreateDirectory(destinationDirectory);

        var result = await TransferExactAsync(
            recycledPath,
            originalPath,
            FileTransferMode.Move,
            conflictResolver,
            cancellationToken);
        if (result.Succeeded && result.CompletedItems == 1 && !PathExists(recycledPath))
        {
            try
            {
                File.Delete(metadataPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return result with
                {
                    Errors = [$"The item was restored, but its Recycle Bin record could not be removed: {ex.Message}"]
                };
            }
        }

        return result;
    }

    public async Task DeletePermanentlyAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var selectedPaths = RemoveNestedSelections(paths);
        await Task.Run(() =>
        {
            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsurePathExists(path);

                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                    File.Delete(path);
                }
            }
        }, cancellationToken);
    }

    public static string GetDuplicatePath(string sourcePath)
    {
        EnsurePathExists(sourcePath);
        var isDirectory = Directory.Exists(sourcePath);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(sourcePath))
            ?? throw new InvalidOperationException("The filesystem root cannot be duplicated.");
        var originalName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath));
        var stem = isDirectory ? originalName : Path.GetFileNameWithoutExtension(originalName);
        var extension = isDirectory ? string.Empty : Path.GetExtension(originalName);

        for (var copyNumber = 1; copyNumber < int.MaxValue; copyNumber++)
        {
            var candidate = Path.Combine(parent, $"{stem} (copy {copyNumber}){extension}");
            if (!PathExists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No available duplicate name could be generated.");
    }

    private async Task<TransferPathResult> TransferPathAsync(
        string sourcePath,
        string requestedDestinationPath,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        CancellationToken cancellationToken,
        ICollection<FileTransferRecord> journal,
        Action<string, long, long>? byteProgress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceIsDirectory = Directory.Exists(sourcePath);

        if (sourceIsDirectory && File.GetAttributes(sourcePath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("Directory links and junctions are not supported yet.");
        }

        var resolution = await ResolveDestinationAsync(
            sourcePath,
            requestedDestinationPath,
            sourceIsDirectory,
            conflictResolver,
            cancellationToken);

        if (resolution.Outcome is TransferOutcome.Skipped or TransferOutcome.Cancelled)
        {
            return new TransferPathResult(
                resolution.Outcome,
                resolution.DestinationPath,
                resolution.ReplacedExistingItem);
        }

        var destinationPath = resolution.DestinationPath;
        if (resolution.ReplacedExistingItem &&
            !(sourceIsDirectory && Directory.Exists(destinationPath)))
        {
            await ReplacePathAsync(
                sourcePath,
                destinationPath,
                sourceIsDirectory,
                mode,
                cancellationToken,
                byteProgress);
            var destinationState = await CapturePathStateAsync(destinationPath, cancellationToken);
            journal.Add(new FileTransferRecord(
                sourcePath,
                destinationPath,
                mode,
                true,
                destinationState));
            return new TransferPathResult(TransferOutcome.Completed, destinationPath, true);
        }

        if (sourceIsDirectory)
        {
            var directoryOutcome = await TransferDirectoryAsync(
                sourcePath,
                destinationPath,
                mode,
                conflictResolver,
                cancellationToken,
                journal,
                byteProgress);
            return new TransferPathResult(
                directoryOutcome,
                destinationPath,
                resolution.ReplacedExistingItem);
        }

        await TransferFileAsync(sourcePath, destinationPath, mode, cancellationToken, byteProgress);
        var fileState = await CapturePathStateAsync(destinationPath, cancellationToken);
        journal.Add(new FileTransferRecord(
            sourcePath,
            destinationPath,
            mode,
            resolution.ReplacedExistingItem,
            fileState));
        return new TransferPathResult(
            TransferOutcome.Completed,
            destinationPath,
            resolution.ReplacedExistingItem);
    }

    private async Task<TransferOutcome> TransferDirectoryAsync(
        string sourcePath,
        string destinationPath,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        CancellationToken cancellationToken,
        ICollection<FileTransferRecord> journal,
        Action<string, long, long>? byteProgress)
    {
        var sourceAttributes = File.GetAttributes(sourcePath);
        var sourceCreationTime = Directory.GetCreationTimeUtc(sourcePath);
        var sourceLastAccessTime = Directory.GetLastAccessTimeUtc(sourcePath);
        var sourceLastWriteTime = Directory.GetLastWriteTimeUtc(sourcePath);

        if (mode == FileTransferMode.Move && !Directory.Exists(destinationPath))
        {
            try
            {
                Directory.Move(sourcePath, destinationPath);
                var movedState = await CapturePathStateAsync(destinationPath, cancellationToken);
                journal.Add(new FileTransferRecord(
                    sourcePath,
                    destinationPath,
                    mode,
                    false,
                    movedState));
                return TransferOutcome.Completed;
            }
            catch (IOException)
            {
                // A cross-volume move falls back to copy-then-delete.
            }
        }

        var destinationWasCreated = !Directory.Exists(destinationPath);
        Directory.CreateDirectory(destinationPath);
        var fullyMoved = true;
        var childCount = 0;

        foreach (var childPath in Directory.EnumerateFileSystemEntries(sourcePath).ToArray())
        {
            childCount++;
            cancellationToken.ThrowIfCancellationRequested();
            var childDestination = Path.Combine(
                destinationPath,
                Path.GetFileName(Path.TrimEndingDirectorySeparator(childPath)));
            var childOutcome = await TransferPathAsync(
                childPath,
                childDestination,
                mode,
                conflictResolver,
                cancellationToken,
                journal,
                byteProgress);

            if (childOutcome.Outcome == TransferOutcome.Cancelled)
            {
                return TransferOutcome.Cancelled;
            }

            if (childOutcome.Outcome != TransferOutcome.Completed)
            {
                fullyMoved = false;
            }
        }

        if (destinationWasCreated)
        {
            Directory.SetCreationTimeUtc(destinationPath, sourceCreationTime);
            Directory.SetLastAccessTimeUtc(destinationPath, sourceLastAccessTime);
            Directory.SetLastWriteTimeUtc(destinationPath, sourceLastWriteTime);
            File.SetAttributes(destinationPath, sourceAttributes);
        }

        if (mode == FileTransferMode.Move && fullyMoved && !Directory.EnumerateFileSystemEntries(sourcePath).Any())
        {
            Directory.Delete(sourcePath);
        }

        if (childCount == 0 && destinationWasCreated)
        {
            var emptyDirectoryState = await CapturePathStateAsync(destinationPath, cancellationToken);
            journal.Add(new FileTransferRecord(
                sourcePath,
                destinationPath,
                mode,
                false,
                emptyDirectoryState));
        }

        return fullyMoved ? TransferOutcome.Completed : TransferOutcome.Skipped;
    }

    private async Task TransferFileAsync(
        string sourcePath,
        string destinationPath,
        FileTransferMode mode,
        CancellationToken cancellationToken,
        Action<string, long, long>? byteProgress)
    {
        if (mode == FileTransferMode.Move)
        {
            try
            {
                File.Move(sourcePath, destinationPath);
                return;
            }
            catch (IOException) when (!File.Exists(destinationPath))
            {
                // A cross-volume move falls back to copy-then-delete.
            }
        }

        var temporaryPath = GetTemporarySiblingPath(destinationPath, "partial");
        var recoveryId = _recovery.Register(sourcePath, destinationPath, temporaryPath);
        try
        {
            await CopyFilePreservingMetadataAsync(sourcePath, temporaryPath, cancellationToken, byteProgress);
            File.Move(temporaryPath, destinationPath);

            if (mode == FileTransferMode.Move)
            {
                File.SetAttributes(sourcePath, File.GetAttributes(sourcePath) & ~FileAttributes.ReadOnly);
                File.Delete(sourcePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            _recovery.Complete(recoveryId);
        }
    }

    private static async Task<DestinationResolution> ResolveDestinationAsync(
        string sourcePath,
        string requestedDestinationPath,
        bool sourceIsDirectory,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver,
        CancellationToken cancellationToken)
    {
        var destinationPath = requestedDestinationPath;
        while (PathExists(destinationPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AreSameFileSystemObject(sourcePath, destinationPath))
            {
                throw new IOException("The source and destination are the same filesystem item. Choose another destination.");
            }

            var conflict = new FileConflict(sourcePath, destinationPath, sourceIsDirectory);
            var resolution = await conflictResolver(conflict);

            switch (resolution.Action)
            {
                case ConflictAction.Skip:
                    return new DestinationResolution(destinationPath, TransferOutcome.Skipped, false);
                case ConflictAction.Cancel:
                    return new DestinationResolution(destinationPath, TransferOutcome.Cancelled, false);
                case ConflictAction.Rename:
                    EnsureValidLeafName(resolution.NewName);
                    destinationPath = Path.Combine(
                        Path.GetDirectoryName(destinationPath)!,
                        resolution.NewName!);
                    continue;
                case ConflictAction.Replace:
                    if (PathsEqual(sourcePath, destinationPath))
                    {
                        throw new IOException("The source and destination are the same item. Choose Rename instead.");
                    }

                    if (sourceIsDirectory && Directory.Exists(destinationPath))
                    {
                        return new DestinationResolution(destinationPath, TransferOutcome.Completed, true);
                    }

                    return new DestinationResolution(destinationPath, TransferOutcome.Completed, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return new DestinationResolution(destinationPath, TransferOutcome.Completed, false);
    }

    public static PathStateSnapshot CapturePathState(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePathExists(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var entries = new List<PathStateEntry>();
        CaptureStateEntry(root, root, entries, cancellationToken);
        return new PathStateSnapshot(entries
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public static Task<PathStateSnapshot> CapturePathStateAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => CapturePathState(path, cancellationToken), cancellationToken);

    public static bool PathMatchesState(
        string path,
        PathStateSnapshot expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!PathExists(path))
        {
            return false;
        }

        try
        {
            return CapturePathState(path, cancellationToken).Entries.SequenceEqual(expected.Entries);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static Task<bool> PathMatchesStateAsync(
        string path,
        PathStateSnapshot expected,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => PathMatchesState(path, expected, cancellationToken), cancellationToken);

    public async Task DeleteCreatedPathIfUnchangedAsync(
        string path,
        bool expectedDirectory,
        PathStateSnapshot expectedState,
        CancellationToken cancellationToken = default)
    {
        EnsurePathExists(path);
        if (Directory.Exists(path) != expectedDirectory ||
            !await PathMatchesStateAsync(path, expectedState, cancellationToken))
        {
            throw new IOException(
                "WinThunar will not undo creation because the item was changed or replaced.");
        }

        await DeletePermanentlyAsync([path], cancellationToken);
    }

    private static void CaptureStateEntry(
        string root,
        string path,
        ICollection<PathStateEntry> entries,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attributes = File.GetAttributes(path);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        var relativePath = PathsExactlyEqual(root, path) ? string.Empty : Path.GetRelativePath(root, path);
        entries.Add(new PathStateEntry(
            relativePath,
            isDirectory,
            isDirectory ? 0 : new FileInfo(path).Length,
            isDirectory ? Directory.GetLastWriteTimeUtc(path).Ticks : File.GetLastWriteTimeUtc(path).Ticks,
            attributes,
            isDirectory ? null : ComputeFileHash(path, cancellationToken)));

        if (!isDirectory || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            CaptureStateEntry(root, child, entries, cancellationToken);
        }
    }

    private static string ComputeFileHash(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            1024 * 1024,
            FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, bytesRead);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private async Task ReplacePathAsync(
        string sourcePath,
        string destinationPath,
        bool sourceIsDirectory,
        FileTransferMode mode,
        CancellationToken cancellationToken,
        Action<string, long, long>? byteProgress)
    {
        var stagedPath = GetTemporarySiblingPath(destinationPath, "incoming");
        var backupPath = GetTemporarySiblingPath(destinationPath, "replaced");
        var sourceRecoveryPath = GetTemporarySiblingPath(sourcePath, "move-source");
        var recoveryId = _recovery.Register(
            sourcePath,
            destinationPath,
            stagedPath,
            backupPath,
            sourceRecoveryPath);
        var destinationBackedUp = false;
        var promoted = false;
        var sourceMovedAside = false;

        try
        {
            if (sourceIsDirectory)
            {
                await CopyDirectoryPreservingMetadataAsync(sourcePath, stagedPath, cancellationToken, byteProgress);
            }
            else
            {
                await CopyFilePreservingMetadataAsync(sourcePath, stagedPath, cancellationToken, byteProgress);
            }

            cancellationToken.ThrowIfCancellationRequested();
            MovePath(destinationPath, backupPath);
            destinationBackedUp = true;
            MovePath(stagedPath, destinationPath);
            promoted = true;

            if (mode == FileTransferMode.Move)
            {
                MovePath(sourcePath, sourceRecoveryPath);
                sourceMovedAside = true;
            }

            DeleteExistingPath(backupPath);
            destinationBackedUp = false;
            if (sourceMovedAside)
            {
                DeleteExistingPath(sourceRecoveryPath);
                sourceMovedAside = false;
            }
        }
        catch
        {
            if (sourceMovedAside && !PathExists(sourcePath))
            {
                TryMovePath(sourceRecoveryPath, sourcePath);
            }

            if (promoted && destinationBackedUp)
            {
                TryMovePath(destinationPath, stagedPath);
            }

            if (destinationBackedUp && !PathExists(destinationPath))
            {
                TryMovePath(backupPath, destinationPath);
            }

            throw;
        }
        finally
        {
            TryDeletePath(stagedPath);
            if (!destinationBackedUp)
            {
                TryDeletePath(backupPath);
            }
            if (!sourceMovedAside)
            {
                TryDeletePath(sourceRecoveryPath);
            }
            if (!PathExists(stagedPath) &&
                !PathExists(backupPath) &&
                !PathExists(sourceRecoveryPath))
            {
                _recovery.Complete(recoveryId);
            }
        }
    }

    private static async Task CopyDirectoryPreservingMetadataAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action<string, long, long>? byteProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attributes = File.GetAttributes(sourcePath);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Directory links and junctions cannot be copied: {sourcePath}");
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var child in Directory.EnumerateFileSystemEntries(sourcePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationChild = Path.Combine(destinationPath, Path.GetFileName(child));
            if (Directory.Exists(child))
            {
                await CopyDirectoryPreservingMetadataAsync(child, destinationChild, cancellationToken, byteProgress);
            }
            else
            {
                await CopyFilePreservingMetadataAsync(child, destinationChild, cancellationToken, byteProgress);
            }
        }

        Directory.SetLastWriteTimeUtc(destinationPath, Directory.GetLastWriteTimeUtc(sourcePath));
        File.SetAttributes(destinationPath, attributes);
    }

    private static async Task CopyFilePreservingMetadataAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action<string, long, long>? byteProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Run(() =>
        {
            var cancelPointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(cancelPointer, 0);
                using var registration = cancellationToken.Register(
                    () => Marshal.WriteInt32(cancelPointer, 1));
                CopyProgressRoutine? callback = byteProgress is null
                    ? null
                    : (totalFileSize, totalBytesTransferred, _, _, _, _, _, _, _) =>
                    {
                        byteProgress(sourcePath, totalBytesTransferred, totalFileSize);
                        return 0;
                    };
                if (!CopyFileEx(sourcePath, destinationPath, callback, IntPtr.Zero, cancelPointer, CopyFileFlags.FailIfExists))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (cancellationToken.IsCancellationRequested || error == ErrorRequestAborted)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new Win32Exception(error, $"Windows could not copy '{Path.GetFileName(sourcePath)}'.");
                }
                GC.KeepAlive(callback);
            }
            finally
            {
                Marshal.FreeHGlobal(cancelPointer);
            }
        }, CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string GetTemporarySiblingPath(string path, string purpose)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.GetDirectoryName(trimmed)
            ?? throw new InvalidOperationException("A filesystem root cannot be used as a staged transfer item.");
        return Path.Combine(parent, $".winthunar-{purpose}-{Guid.NewGuid():N}.tmp");
    }

    private static void MovePath(string sourcePath, string destinationPath)
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

    private static void TryMovePath(string sourcePath, string destinationPath)
    {
        try
        {
            if (PathExists(sourcePath) && !PathExists(destinationPath))
            {
                MovePath(sourcePath, destinationPath);
            }
        }
        catch (Exception)
        {
            // Keep the recovery item in place when automatic rollback is not possible.
        }
    }

    private static void TryDeletePath(string path)
    {
        try
        {
            DeleteExistingPath(path);
        }
        catch (Exception)
        {
            // A staged or recovery item is safer to leave behind than to broaden cleanup.
        }
    }

    private static List<string> RemoveNestedSelections(IEnumerable<string> paths)
    {
        var normalized = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path.Length)
            .ToList();

        return normalized
            .Where(candidate => !normalized.Any(parent =>
                !PathsEqual(parent, candidate) &&
                Directory.Exists(parent) &&
                IsDescendantPath(candidate, parent)))
            .ToList();
    }

    private static PathStateSnapshot? FindCapturedState(
        IEnumerable<FileTransferRecord> journal,
        string destinationPath) =>
        journal.LastOrDefault(record => PathsEqual(record.DestinationPath, destinationPath))
            ?.DestinationState;

    private static bool IsSameOrDescendantPath(string candidatePath, string parentPath)
    {
        if (TryGetFinalPath(candidatePath, out var finalCandidate) &&
            TryGetFinalPath(parentPath, out var finalParent))
        {
            return PathsEqual(finalCandidate, finalParent) || IsDescendantPath(finalCandidate, finalParent);
        }

        return PathsEqual(candidatePath, parentPath) || IsDescendantPath(candidatePath, parentPath);
    }

    private static bool AreSameFileSystemObject(string left, string right)
    {
        if (!TryGetFileIdentity(left, out var leftIdentity) ||
            !TryGetFileIdentity(right, out var rightIdentity))
        {
            return PathsEqual(left, right);
        }

        return leftIdentity == rightIdentity;
    }

    private static bool TryGetFileIdentity(string path, out FileIdentity identity)
    {
        identity = default;
        using var handle = OpenPathHandle(path);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var information))
        {
            return false;
        }

        identity = new FileIdentity(
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
        return true;
    }

    private static bool TryGetFinalPath(string path, out string finalPath)
    {
        finalPath = string.Empty;
        using var handle = OpenPathHandle(path);
        if (handle.IsInvalid)
        {
            return false;
        }

        var buffer = new StringBuilder(512);
        var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0)
        {
            return false;
        }
        if (length >= (uint)buffer.Capacity)
        {
            buffer.EnsureCapacity((int)length + 1);
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0 || length >= (uint)buffer.Capacity)
            {
                return false;
            }
        }

        finalPath = buffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            finalPath = @"\\" + finalPath[8..];
        }
        else if (finalPath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            finalPath = finalPath[4..];
        }

        finalPath = Path.TrimEndingDirectorySeparator(finalPath);
        return true;
    }

    private static SafeFileHandle OpenPathHandle(string path) =>
        CreateFile(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);

    private static bool IsDescendantPath(string candidatePath, string parentPath)
    {
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath));
        return candidate.StartsWith(
            parent + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static bool PathsExactlyEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.Ordinal);

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void EnsurePathExists(string path)
    {
        if (!PathExists(path))
        {
            throw new FileNotFoundException("The selected item no longer exists.", path);
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Folder not found: {path}");
        }
    }

    private static void EnsurePathDoesNotExist(string path)
    {
        if (PathExists(path))
        {
            throw new IOException($"An item named '{Path.GetFileName(path)}' already exists.");
        }
    }

    private static void EnsureValidLeafName(string? name)
    {
        var error = ValidateLeafName(name);
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(name));
        }
    }

    private static void DeleteExistingPath(string path)
    {
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

    private enum TransferOutcome
    {
        Completed,
        Skipped,
        Cancelled
    }

    private sealed record DestinationResolution(
        string DestinationPath,
        TransferOutcome Outcome,
        bool ReplacedExistingItem);

    private sealed record TransferPathResult(
        TransferOutcome Outcome,
        string DestinationPath,
        bool ReplacedExistingItem);

    private readonly record struct FileIdentity(uint VolumeSerialNumber, ulong FileIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private const int ErrorRequestAborted = 1235;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [Flags]
    private enum CopyFileFlags : uint
    {
        FailIfExists = 0x00000001
    }

    private delegate uint CopyProgressRoutine(
        long totalFileSize,
        long totalBytesTransferred,
        long streamSize,
        long streamBytesTransferred,
        uint streamNumber,
        uint callbackReason,
        IntPtr sourceFile,
        IntPtr destinationFile,
        IntPtr data);

    [DllImport("kernel32.dll", EntryPoint = "CopyFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileEx(
        string existingFileName,
        string newFileName,
        CopyProgressRoutine? progressRoutine,
        IntPtr data,
        IntPtr cancel,
        CopyFileFlags copyFlags);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);
}
