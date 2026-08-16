using System.Text.Json;
using WinThunar.Models;

namespace WinThunar.Services;

public sealed class AppSessionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _sessionPath;

    public AppSessionService(string? sessionPath = null)
    {
        _sessionPath = sessionPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinThunar",
            "session.json");
    }

    public AppSessionState Load()
    {
        if (!File.Exists(_sessionPath))
        {
            return new AppSessionState();
        }

        try
        {
            var json = File.ReadAllText(_sessionPath);
            return Normalize(JsonSerializer.Deserialize<AppSessionState>(json, SerializerOptions));
        }
        catch (JsonException)
        {
            return new AppSessionState();
        }
        catch (IOException)
        {
            return new AppSessionState();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSessionState();
        }
    }

    public void Save(AppSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var directory = Path.GetDirectoryName(_sessionPath)
            ?? throw new InvalidOperationException("The session file needs a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_sessionPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, SerializerOptions));
            File.Move(temporaryPath, _sessionPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static AppSessionState Normalize(AppSessionState? state)
    {
        state ??= new AppSessionState();
        state.Bookmarks = (state.Bookmarks ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        state.BookmarkItems = (state.BookmarkItems ?? [])
            .Where(bookmark => bookmark is not null && !string.IsNullOrWhiteSpace(bookmark.Path))
            .ToList();
        state.Tabs = (state.Tabs ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        state.FolderViewSettings = new Dictionary<string, FolderViewState>(
            (state.FolderViewSettings ?? new Dictionary<string, FolderViewState>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null),
            StringComparer.OrdinalIgnoreCase);
        state.CustomShortcuts = new Dictionary<string, string>(
            (state.CustomShortcuts ?? new Dictionary<string, string>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null),
            StringComparer.OrdinalIgnoreCase);
        return state;
    }
}
