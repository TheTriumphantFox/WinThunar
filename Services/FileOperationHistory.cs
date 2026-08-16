namespace WinThunar.Services;

public sealed record FileHistoryEntry(
    string Description,
    Func<Task> Undo,
    Func<Task> Redo);

public sealed class FileOperationHistory
{
    private const int MaximumEntries = 10;
    private readonly Stack<FileHistoryEntry> _undo = new();
    private readonly Stack<FileHistoryEntry> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoLabel => CanUndo ? $"Undo {_undo.Peek().Description}" : "Undo";
    public string RedoLabel => CanRedo ? $"Redo {_redo.Peek().Description}" : "Redo";

    public event EventHandler? Changed;

    public void Push(FileHistoryEntry entry)
    {
        _undo.Push(entry);
        _redo.Clear();

        if (_undo.Count > MaximumEntries)
        {
            var retained = _undo.Take(MaximumEntries).Reverse().ToArray();
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
        if (!CanUndo)
        {
            return;
        }

        var entry = _undo.Pop();
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
            OnChanged();
        }
    }

    public async Task RedoAsync()
    {
        if (!CanRedo)
        {
            return;
        }

        var entry = _redo.Pop();
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
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
