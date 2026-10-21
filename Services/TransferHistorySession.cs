namespace WinThunar.Services;

/// <summary>Owns displaced items and exact incoming data until its window history expires.</summary>
public sealed class TransferHistorySession : IDisposable
{
    private readonly FileOperationService _operations;
    private readonly Node[] _roots;
    private readonly HashSet<string> _storage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _parkingRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PathStateSnapshot?> _expected = new(StringComparer.OrdinalIgnoreCase);
    private bool _undone;
    private bool _disposed;
    private bool _needsRecovery;

    private TransferHistorySession(FileOperationService operations, IReadOnlyList<FileTransferRecord> records)
    {
        _operations = operations;
        _roots = records.Select(record => Build(record, Path.GetDirectoryName(record.DestinationPath)!)).ToArray();
    }

    public static async Task<TransferHistorySession> CreateAsync(FileOperationService operations, IReadOnlyList<FileTransferRecord> records)
    {
        var session = new TransferHistorySession(operations, records);
        try
        {
            foreach (var root in session._roots)
            {
                session._expected[root.Record.DestinationPath] = root.Record.DestinationState;
                if (root.Record.Mode == FileTransferMode.Move)
                    session._expected[root.Record.SourcePath] = root.Record.RemainingSourceState;
            }
            foreach (var node in session.AllNodes())
                if (node.Record.BackupPath is { } backup) session._expected[backup] = node.Record.BackupState;
            return await Task.FromResult(session);
        }
        catch { session.Dispose(); throw; }
    }

    public Task UndoAsync() => ReplayAsync(true);
    public Task RedoAsync() => ReplayAsync(false);

    private Node Build(FileTransferRecord record, string storageParent)
    {
        if (record.BackupPath is { } backup) _storage.Add(Path.GetDirectoryName(backup)!);
        return new Node(record,
            record.Children?.Select(child => Build(child, storageParent)).ToArray(),
            storageParent,
            record.Children is not null ? DirectoryState.Read(record.DestinationPath) : null);
    }

    private IEnumerable<Node> AllNodes() => _roots.SelectMany(Descendants);
    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        if (node.Children is not null)
            foreach (var child in node.Children.SelectMany(Descendants)) yield return child;
    }

    private async Task CaptureGuardsAsync()
    {
        _expected.Clear();
        foreach (var root in _roots)
        {
            await GuardAsync(root.Record.DestinationPath);
            if (root.Record.Mode == FileTransferMode.Move) await GuardAsync(root.Record.SourcePath);
        }
        foreach (var node in AllNodes())
        {
            if (node.Record.BackupPath is { } backup) await GuardAsync(backup);
            if (node.ParkedPath is { } parked) await GuardAsync(parked);
        }
    }

    private async Task GuardAsync(string path) => _expected[path] = LinkFileSystem.Exists(path)
        ? await FileOperationService.CapturePathStateAsync(path) : null;

    private async Task ReplayAsync(bool undo)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_needsRecovery) throw new InvalidOperationException("Restart WinThunar to recover the interrupted history operation before trying again.");
        if (_undone == undo) throw new InvalidOperationException("This transfer is already in that history state.");
        foreach (var (path, state) in _expected)
        {
            if (state is null ? LinkFileSystem.Exists(path) : !await FileOperationService.PathMatchesStateAsync(path, state))
                throw new IOException($"Cannot {(undo ? "undo" : "redo")} because '{path}' changed. No items were changed.");
        }
        var rollback = new Stack<Func<Task>>();
        try
        {
            // Recovery must restore an old destination if a replay is interrupted between renames.
            foreach (var node in AllNodes()) _operations.SetHistoryRetention(node.Record.RecoveryId, false);
            foreach (var node in undo ? _roots.Reverse() : _roots)
                await ReplayNodeAsync(node, undo, rollback);
            await CaptureGuardsAsync();
            foreach (var node in AllNodes()) _operations.SetHistoryRetention(node.Record.RecoveryId, true);
            _undone = undo;
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            while (rollback.TryPop(out var restore))
            {
                try { await restore(); } catch (Exception ex) { errors.Add(ex); }
            }
            if (errors.Count == 1)
            {
                try
                {
                    await CaptureGuardsAsync();
                    foreach (var node in AllNodes()) _operations.SetHistoryRetention(node.Record.RecoveryId, true);
                }
                catch (Exception ex) { errors.Add(ex); }
            }
            if (errors.Count > 1)
            {
                _needsRecovery = true;
                throw new AggregateException("History replay could not be fully rolled back. Retained data and recovery records were kept.", errors);
            }
            throw;
        }
    }

    private async Task ReplayNodeAsync(Node node, bool undo, Stack<Func<Task>> rollback)
    {
        var record = node.Record;
        if (node.Children is { } children)
        {
            // Save parent metadata first, so inverse actions restore it after all child renames.
            RememberDirectory(record.SourcePath, rollback);
            RememberDirectory(record.DestinationPath, rollback);
            if (undo && record.RemovedSourceDirectory) CreateDirectory(record.SourcePath, rollback);
            if (!undo && record.CreatedDirectory) CreateDirectory(record.DestinationPath, rollback);
            foreach (var child in undo ? children.Reverse() : children)
                await ReplayNodeAsync(child, undo, rollback);
            if (undo)
            {
                if (record.CreatedDirectory) RemoveDirectory(record.DestinationPath, rollback);
                else record.OriginalDestinationDirectory?.Apply(record.DestinationPath);
                if (record.Mode == FileTransferMode.Move) record.OriginalSourceDirectory?.Apply(record.SourcePath);
            }
            else
            {
                if (record.RemovedSourceDirectory) RemoveDirectory(record.SourcePath, rollback);
                node.CompletedDirectory?.Apply(record.DestinationPath);
            }
            return;
        }

        if (undo)
        {
            if (record.Mode == FileTransferMode.Move)
                await MoveAsync(record.DestinationPath, record.SourcePath, rollback);
            else
            {
                if (node.ParkedPath is null)
                {
                    if (!_parkingRoots.TryGetValue(node.StorageParent, out var storage))
                    {
                        storage = Path.Combine(node.StorageParent, $".winthunar-undo-{Guid.NewGuid():N}.tmp");
                        Directory.CreateDirectory(storage);
                        _storage.Add(storage);
                        _parkingRoots.Add(node.StorageParent, storage);
                    }
                    node.ParkedPath = Path.Combine(storage, Guid.NewGuid().ToString("N"));
                    node.ParkedRecoveryId = _operations.RetainHistoryPath(node.ParkedPath, record.DestinationPath);
                }
                await MoveAsync(record.DestinationPath, node.ParkedPath, rollback);
            }
            if (record.BackupPath is { } backup) await MoveAsync(backup, record.DestinationPath, rollback);
        }
        else
        {
            if (record.BackupPath is { } backup) await MoveAsync(record.DestinationPath, backup, rollback);
            await MoveAsync(record.Mode == FileTransferMode.Move ? record.SourcePath : node.ParkedPath!, record.DestinationPath, rollback);
        }
    }

    private async Task MoveAsync(string source, string destination, Stack<Func<Task>> rollback)
    {
        if (LinkFileSystem.Exists(destination)) throw new IOException($"'{destination}' already exists.");
        // Parent paths must already exist or be recreated from the recorded directory structure.
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException($"The parent of '{destination}' is unavailable.");
        await _operations.MoveHistoryPathAsync(source, destination);
        rollback.Push(() => _operations.MoveHistoryPathAsync(destination, source));
    }

    private static void RememberDirectory(string path, Stack<Func<Task>> rollback)
    {
        if (!Directory.Exists(path)) return;
        var state = DirectoryState.Read(path);
        rollback.Push(() => { state.Apply(path); return Task.CompletedTask; });
    }

    private static void CreateDirectory(string path, Stack<Func<Task>> rollback)
    {
        if (LinkFileSystem.Exists(path)) throw new IOException($"'{path}' already exists.");
        if (!Directory.Exists(Path.GetDirectoryName(path))) throw new IOException($"The parent of '{path}' is unavailable.");
        Directory.CreateDirectory(path);
        rollback.Push(() => { Directory.Delete(path); return Task.CompletedTask; });
    }

    private static void RemoveDirectory(string path, Stack<Func<Task>> rollback)
    {
        var state = DirectoryState.Read(path);
        Directory.Delete(path); // Never recurse: unexpected children must prevent removal.
        rollback.Push(() => { Directory.CreateDirectory(path); state.Apply(path); return Task.CompletedTask; });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_needsRecovery) return;
        foreach (var node in AllNodes())
        {
            // Recovery records remain if any cleanup fails. Startup can finish cleanup later.
            try
            {
                if (node.Record.BackupPath is { } backup) LinkFileSystem.Delete(backup);
                _operations.ForgetRecovery(node.Record.RecoveryId);
                if (node.ParkedPath is { } parked) LinkFileSystem.Delete(parked);
                _operations.ForgetRecovery(node.ParkedRecoveryId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        }
        foreach (var directory in _storage)
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private sealed class Node(FileTransferRecord record, Node[]? children, string storageParent, DirectoryState? completedDirectory)
    {
        public FileTransferRecord Record { get; } = record;
        public Node[]? Children { get; } = children;
        public string StorageParent { get; } = storageParent;
        public DirectoryState? CompletedDirectory { get; } = completedDirectory;
        public string? ParkedPath { get; set; }
        public string? ParkedRecoveryId { get; set; }
    }
}
