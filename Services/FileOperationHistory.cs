namespace WinThunar.Services;

public sealed record FileHistoryEntry(
    string Description,
    Func<Task> Undo,
    Func<Task> Redo,
    Action? Release = null);

public sealed class FileOperationHistory : IDisposable
{
    private const int MaximumEntries = 10;
    private readonly Stack<FileHistoryEntry> _undo = new();
    private readonly Stack<FileHistoryEntry> _redo = new();
    private bool _disposed;
    private bool _disposeRequested;
    public bool IsReplaying { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoLabel => CanUndo ? $"Undo {_undo.Peek().Description}" : "Undo";
    public string RedoLabel => CanRedo ? $"Redo {_redo.Peek().Description}" : "Redo";

    public event EventHandler? Changed;

    public void Push(FileHistoryEntry entry)
    {
        if (_disposed) { entry.Release?.Invoke(); return; }
        if (IsReplaying) throw new InvalidOperationException("Wait for Undo/Redo before adding another file operation.");
        _undo.Push(entry);
        foreach (var discarded in _redo) discarded.Release?.Invoke();
        _redo.Clear();

        if (_undo.Count > MaximumEntries)
        {
            var retained = _undo.Take(MaximumEntries).Reverse().ToArray();
            foreach (var discarded in _undo.Skip(MaximumEntries)) discarded.Release?.Invoke();
            _undo.Clear();
            foreach (var item in retained)
            {
                _undo.Push(item);
            }
        }

        OnChanged();
    }

    public async Task UndoAsync()
    {
        if (IsReplaying) throw new InvalidOperationException("A history operation is already running.");
        if (!CanUndo)
        {
            return;
        }

        var entry = _undo.Pop();
        IsReplaying = true;
        try
        {
            await entry.Undo();
            _redo.Push(entry);
        }
        catch
        {
            _undo.Push(entry);
            throw;
        }
        finally
        {
            IsReplaying = false;
            if (_disposeRequested) Dispose();
            OnChanged();
        }
    }

    public async Task RedoAsync()
    {
        if (IsReplaying) throw new InvalidOperationException("A history operation is already running.");
        if (!CanRedo)
        {
            return;
        }

        var entry = _redo.Pop();
        IsReplaying = true;
        try
        {
            await entry.Redo();
            _undo.Push(entry);
        }
        catch
        {
            _redo.Push(entry);
            throw;
        }
        finally
        {
            IsReplaying = false;
            if (_disposeRequested) Dispose();
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        if (IsReplaying) { _disposeRequested = true; return; }
        _disposed = true;
        foreach (var entry in _undo.Concat(_redo)) entry.Release?.Invoke();
        _undo.Clear();
        _redo.Clear();
    }
}
