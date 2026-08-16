namespace WinThunar.Models;

public enum BrowserViewMode
{
    Details,
    Icons,
    Compact
}

public enum BrowserSortColumn
{
    Name,
    Size,
    Type,
    Modified
}

public sealed class BrowserTabState
{
    private const string RecycleBinVirtualPath = "shell:RecycleBinFolder";
    private readonly List<string> _history;
    private int _historyIndex;

    public BrowserTabState(string path)
    {
        Path = path;
        _history = [path];
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string Path { get; set; }
    public string Title => GetTitle(Path);
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;

    public void RecordNavigation(string path)
    {
        if (PathsEqual(Path, path))
        {
            Path = path;
            return;
        }

        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(path);
        _historyIndex = _history.Count - 1;
        Path = path;
    }

    public string? GoBack()
    {
        if (!CanGoBack)
        {
            return null;
        }

        Path = _history[--_historyIndex];
        return Path;
    }

    public string? GoForward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        Path = _history[++_historyIndex];
        return Path;
    }

    public static string GetTitle(string path)
    {
        if (string.Equals(path, RecycleBinVirtualPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Trash";
        }

        var trimmed = System.IO.Path.TrimEndingDirectorySeparator(path);
        var title = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(title) ? path : title;
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.Equals(left, RecycleBinVirtualPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(right, RecycleBinVirtualPath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(left)),
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AppSessionState
{
    public string? LastPath { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public BrowserViewMode ViewMode { get; set; } = BrowserViewMode.Details;
    public BrowserSortColumn SortColumn { get; set; } = BrowserSortColumn.Name;
    public bool SortDescending { get; set; }
    public bool FoldersFirst { get; set; } = true;
    public bool ShowThumbnails { get; set; } = true;
    public int ZoomLevel { get; set; } = 2;
    public bool IncludeSubfolders { get; set; } = true;
    public bool ConfirmMoveToTrash { get; set; }
    public bool RestoreTabs { get; set; } = true;
    public bool TreeSidePane { get; set; }
    public bool ShowSizeColumn { get; set; } = true;
    public bool ShowTypeColumn { get; set; } = true;
    public bool ShowModifiedColumn { get; set; } = true;
    public double SizeColumnWidth { get; set; } = 110;
    public double TypeColumnWidth { get; set; } = 140;
    public double ModifiedColumnWidth { get; set; } = 160;
    public double SplitSizeColumnWidth { get; set; } = 85;
    public double SplitTypeColumnWidth { get; set; } = 105;
    public bool UsePathBar { get; set; }
    public bool SingleClickActivation { get; set; }
    public bool RememberFolderViews { get; set; }
    public bool ShowImagePreview { get; set; }
    public bool ShowTerminalPanel { get; set; }
    public bool ShowBackToolbarButton { get; set; } = true;
    public bool ShowForwardToolbarButton { get; set; } = true;
    public bool ShowUpToolbarButton { get; set; } = true;
    public bool ShowHomeToolbarButton { get; set; } = true;
    public bool ShowReloadToolbarButton { get; set; } = true;
    public bool ShowSearchToolbarButton { get; set; } = true;
    public Dictionary<string, FolderViewState> FolderViewSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CustomShortcuts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Bookmarks { get; set; } = [];
    public List<BookmarkState> BookmarkItems { get; set; } = [];
    public List<string> Tabs { get; set; } = [];
    public int ActiveTabIndex { get; set; }
    public bool SplitPaneOpen { get; set; }
    public string? SplitPath { get; set; }
}

public sealed class BookmarkState
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class FolderViewState
{
    public BrowserViewMode ViewMode { get; set; } = BrowserViewMode.Details;
    public BrowserSortColumn SortColumn { get; set; } = BrowserSortColumn.Name;
    public bool SortDescending { get; set; }
    public int ZoomLevel { get; set; } = 2;
}
