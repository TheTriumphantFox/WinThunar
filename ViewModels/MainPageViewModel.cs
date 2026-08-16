using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using WinThunar.Models;
using WinThunar.Services;

namespace WinThunar.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly List<string> _history = [];
    private readonly FileSearchService _searchService = new();
    private readonly ShellImageService _shellImageService = new();
    private readonly object _watcherRefreshLock = new();
    private readonly RequestGeneration _navigationGeneration = new();
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _imageCancellation;
    private CancellationTokenSource? _navigationCancellation;
    private CancellationTokenSource? _watcherRefreshCancellation;
    private FileSystemWatcher? _folderWatcher;
    private int _searchVersion;
    private int _imageVersion;
    private int _historyIndex = -1;

    public ObservableCollection<FileSystemEntry> Entries { get; } = [];
    public ObservableCollection<NavigationLocation> Places { get; } = [];
    public ObservableCollection<NavigationLocation> Devices { get; } = [];
    public ObservableCollection<NavigationLocation> Bookmarks { get; } = [];
    public ObservableCollection<NavigationLocation> NetworkLocations { get; } = [];

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool ShowHiddenFiles { get; set; }

    [ObservableProperty]
    public partial bool IsSearchMode { get; set; }

    [ObservableProperty]
    public partial BrowserViewMode ViewMode { get; set; } = BrowserViewMode.Details;

    [ObservableProperty]
    public partial BrowserSortColumn SortColumn { get; set; } = BrowserSortColumn.Name;

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    [ObservableProperty]
    public partial bool FoldersFirst { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowThumbnails { get; set; } = true;

    [ObservableProperty]
    public partial int ZoomLevel { get; set; } = 2;

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSizeColumn { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTypeColumn { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowModifiedColumn { get; set; } = true;

    [ObservableProperty]
    public partial Visibility TransferPanelVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string ActiveTransferTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveTransferDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ActiveTransferProgress { get; set; }

    [ObservableProperty]
    public partial string QueuedTransferText { get; set; } = string.Empty;

    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    public Visibility DetailsViewVisibility => ViewMode == BrowserViewMode.Details
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility IconViewVisibility => ViewMode == BrowserViewMode.Icons
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility CompactViewVisibility => ViewMode == BrowserViewMode.Compact
        ? Visibility.Visible
        : Visibility.Collapsed;
    public string HomePath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public double IconItemWidth => 80 + (ZoomLevel * 16);
    public double IconItemHeight => 68 + (ZoomLevel * 13);
    public double IconImageSize => 28 + (ZoomLevel * 10);
    public double IconNameWidth => IconItemWidth - 16;
    public double CompactItemWidth => 190 + (ZoomLevel * 25);

    public MainPageViewModel()
    {
        AddPlace("Home", HomePath, "\uE80F");
        AddPlace("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "\uE8FC");
        AddPlace("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "\uE8A5");
        AddPlace("Downloads", Path.Combine(HomePath, "Downloads"), "\uE896");
        AddPlace("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "\uE8D6");
        AddPlace("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "\uEB9F");
        AddPlace("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "\uE8B2");
        Places.Add(new NavigationLocation("Trash", "shell:RecycleBinFolder", "\uE74D", NavigationLocationKind.RecycleBin));

        NetworkLocations.Add(new NavigationLocation(
            "Browse Network",
            "shell:NetworkPlacesFolder",
            "\uE968",
            NavigationLocationKind.NetworkBrowser));

        RefreshDevices();
    }

    public async Task InitializeAsync(string? initialPath = null) =>
        await NavigateAsync(Directory.Exists(initialPath) ? initialPath! : HomePath);

    public void SetBookmarks(IEnumerable<string> paths)
    {
        Bookmarks.Clear();
        foreach (var path in paths
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Bookmarks.Add(new NavigationLocation(
                BrowserTabState.GetTitle(path),
                path,
                "\uE734"));
        }
    }

    public void SetBookmarks(IEnumerable<BookmarkState> bookmarks)
    {
        Bookmarks.Clear();
        foreach (var bookmark in bookmarks
            .Where(bookmark => Directory.Exists(bookmark.Path))
            .GroupBy(bookmark => Path.GetFullPath(bookmark.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()))
        {
            var path = Path.GetFullPath(bookmark.Path);
            Bookmarks.Add(new NavigationLocation(
                string.IsNullOrWhiteSpace(bookmark.Name) ? BrowserTabState.GetTitle(path) : bookmark.Name,
                path,
                "\uE734"));
        }
    }

    public bool AddBookmark(string path)
    {
        var normalized = Path.GetFullPath(path);
        if (!Directory.Exists(normalized) || Bookmarks.Any(bookmark => PathEquals(bookmark.Path, normalized)))
        {
            return false;
        }

        Bookmarks.Add(new NavigationLocation(
            BrowserTabState.GetTitle(normalized),
            normalized,
            "\uE734"));
        return true;
    }

    public bool RemoveBookmark(string path)
    {
        var bookmark = Bookmarks.FirstOrDefault(item => PathEquals(item.Path, path));
        return bookmark is not null && Bookmarks.Remove(bookmark);
    }

    public bool RenameBookmark(string path, string name)
    {
        var bookmark = Bookmarks.FirstOrDefault(item => PathEquals(item.Path, path));
        if (bookmark is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var index = Bookmarks.IndexOf(bookmark);
        Bookmarks[index] = bookmark with { Name = name.Trim() };
        return true;
    }

    public bool MoveBookmark(string path, int offset)
    {
        var bookmark = Bookmarks.FirstOrDefault(item => PathEquals(item.Path, path));
        if (bookmark is null)
        {
            return false;
        }

        var oldIndex = Bookmarks.IndexOf(bookmark);
        var newIndex = Math.Clamp(oldIndex + offset, 0, Bookmarks.Count - 1);
        if (newIndex == oldIndex)
        {
            return false;
        }

        Bookmarks.Move(oldIndex, newIndex);
        return true;
    }

    partial void OnViewModeChanged(BrowserViewMode value)
    {
        OnPropertyChanged(nameof(DetailsViewVisibility));
        OnPropertyChanged(nameof(IconViewVisibility));
        OnPropertyChanged(nameof(CompactViewVisibility));
        BeginLoadingImages(Entries.ToList());
    }

    partial void OnSortColumnChanged(BrowserSortColumn value) => ResortEntries();
    partial void OnSortDescendingChanged(bool value) => ResortEntries();
    partial void OnFoldersFirstChanged(bool value) => ResortEntries();
    partial void OnShowThumbnailsChanged(bool value) => BeginLoadingImages(Entries.ToList());
    partial void OnShowSizeColumnChanged(bool value) => ConfigureEntryColumns();
    partial void OnShowTypeColumnChanged(bool value) => ConfigureEntryColumns();
    partial void OnShowModifiedColumnChanged(bool value) => ConfigureEntryColumns();

    partial void OnZoomLevelChanged(int value)
    {
        OnPropertyChanged(nameof(IconItemWidth));
        OnPropertyChanged(nameof(IconItemHeight));
        OnPropertyChanged(nameof(IconImageSize));
        OnPropertyChanged(nameof(IconNameWidth));
        OnPropertyChanged(nameof(CompactItemWidth));
        foreach (var entry in Entries)
        {
            entry.ZoomLevel = value;
        }
        BeginLoadingImages(Entries.ToList());
    }

    public async Task NavigateAsync(string requestedPath, bool addToHistory = true)
    {
        var navigationVersion = _navigationGeneration.Next();
        _navigationCancellation?.Cancel();
        _navigationCancellation?.Dispose();
        _navigationCancellation = new CancellationTokenSource();
        var cancellationToken = _navigationCancellation.Token;

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            IsBusy = false;
            return;
        }

        string path;
        try
        {
            path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(requestedPath.Trim().Trim('"')));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            IsBusy = false;
            return;
        }

        if (!Directory.Exists(path))
        {
            StatusText = $"Folder not found: {path}";
            IsBusy = false;
            return;
        }

        var isRefresh = !IsSearchMode && PathEquals(path, CurrentPath);

        CancelSearchCore();
        IsSearchMode = false;

        if (!isRefresh)
        {
            IsBusy = true;
            StatusText = $"Loading {path}...";
        }

        try
        {
            var entries = await Task.Run(() => ReadDirectory(
                path,
                ShowHiddenFiles,
                SortColumn,
                SortDescending,
                FoldersFirst), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (!_navigationGeneration.IsCurrent(navigationVersion))
            {
                return;
            }

            foreach (var entry in entries)
            {
                entry.ZoomLevel = ZoomLevel;
                entry.ConfigureColumns(ShowSizeColumn, ShowTypeColumn, ShowModifiedColumn);
            }

            var entriesChanged = SynchronizeEntries(entries);

            CurrentPath = path;
            StatusText = FormatStatus(entries, path);
            ConfigureFolderWatcher(path);
            if (entriesChanged)
            {
                BeginLoadingImages(Entries.ToList());
            }

            if (addToHistory)
            {
                if (_historyIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                }

                if (_history.Count == 0 || !PathEquals(_history[^1], path))
                {
                    _history.Add(path);
                }

                _historyIndex = _history.Count - 1;
            }

            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
        catch (UnauthorizedAccessException)
        {
            if (_navigationGeneration.IsCurrent(navigationVersion))
            {
                StatusText = $"Permission denied: {path}";
            }
        }
        catch (OperationCanceledException)
        {
            // A newer navigation owns the view now.
        }
        catch (Exception ex)
        {
            if (_navigationGeneration.IsCurrent(navigationVersion))
            {
                StatusText = $"Could not open folder: {ex.Message}";
            }
        }
        finally
        {
            if (_navigationGeneration.IsCurrent(navigationVersion))
            {
                IsBusy = false;
            }
        }
    }

    public async Task GoBackAsync()
    {
        if (!CanGoBack)
        {
            return;
        }

        _historyIndex--;
        await NavigateAsync(_history[_historyIndex], false);
    }

    public async Task GoForwardAsync()
    {
        if (!CanGoForward)
        {
            return;
        }

        _historyIndex++;
        await NavigateAsync(_history[_historyIndex], false);
    }

    public async Task GoUpAsync()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent is not null)
        {
            await NavigateAsync(parent.FullName);
        }
    }

    public async Task RefreshAsync()
    {
        RefreshDevices();
        await NavigateAsync(CurrentPath, false);
    }

    public async Task ToggleHiddenFilesAsync()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        await NavigateAsync(CurrentPath, false);
    }

    public async Task SearchAsync(string query)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            await EndSearchAsync();
            return;
        }

        CancelSearchCore();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;
        var version = ++_searchVersion;
        IsSearchMode = true;
        IsBusy = true;
        Entries.Clear();
        StatusText = $"Searching {CurrentPath} for '{normalizedQuery}'...";

        var progress = new Progress<IReadOnlyList<FileSearchMatch>>(batch =>
        {
            if (version != _searchVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var match in batch)
            {
                var entry = new FileSystemEntry(
                    match.Name,
                    match.FullPath,
                    match.IsDirectory,
                    match.IsDirectory ? string.Empty : FormatSize(match.Size),
                    match.IsDirectory ? "Folder" : FileType(match.Extension),
                    match.Modified.ToString("g"),
                    match.IsDirectory ? "\uE8B7" : "\uE8A5",
                    match.Size,
                    match.Modified)
                {
                    ZoomLevel = ZoomLevel
                };
                entry.ConfigureColumns(ShowSizeColumn, ShowTypeColumn, ShowModifiedColumn);
                Entries.Add(entry);
            }

            BeginLoadingImages(batch
                .Select(match => Entries.FirstOrDefault(entry => PathEquals(entry.FullPath, match.FullPath)))
                .Where(entry => entry is not null)
                .Cast<FileSystemEntry>()
                .ToList());

            StatusText = $"Searching... {Entries.Count} match{(Entries.Count == 1 ? string.Empty : "es")}";
        });
        var progressReporter = (IProgress<IReadOnlyList<FileSearchMatch>>)progress;

        try
        {
            var summary = await Task.Run(() => _searchService.Search(
                CurrentPath,
                normalizedQuery,
                ShowHiddenFiles,
                progressReporter.Report,
                cancellationToken,
                includeSubfolders: IncludeSubfolders), cancellationToken);
            if (version != _searchVersion)
            {
                return;
            }

            StatusText = $"{summary.ResultCount} match{(summary.ResultCount == 1 ? string.Empty : "es")} for '{normalizedQuery}'";
            if (summary.LimitReached)
            {
                StatusText += $" (showing the first {FileSearchService.DefaultMaximumResults})";
            }

            if (summary.SkippedDirectories > 0)
            {
                StatusText += $"; {summary.SkippedDirectories} inaccessible folder{(summary.SkippedDirectories == 1 ? string.Empty : "s")} skipped";
            }
        }
        catch (OperationCanceledException)
        {
            // A new query or explicit close superseded this search.
        }
        finally
        {
            if (version == _searchVersion)
            {
                IsBusy = false;
            }
        }
    }

    public async Task EndSearchAsync()
    {
        var wasSearching = IsSearchMode;
        CancelSearchCore();
        IsSearchMode = false;
        if (wasSearching && Directory.Exists(CurrentPath))
        {
            await NavigateAsync(CurrentPath, false);
        }
    }

    public void SetSelectionStatus(IReadOnlyCollection<FileSystemEntry> selected)
    {
        if (selected.Count == 0)
        {
            StatusText = FormatStatus(Entries, CurrentPath);
            return;
        }

        var fileBytes = selected
            .Where(entry => !entry.IsDirectory)
            .Select(entry => new FileInfo(entry.FullPath))
            .Where(file => file.Exists)
            .Sum(file => file.Length);

        StatusText = $"{selected.Count} item{(selected.Count == 1 ? string.Empty : "s")} selected";
        if (fileBytes > 0)
        {
            StatusText += $" ({FormatSize(fileBytes)})";
        }
    }

    private void AddPlace(string name, string path, string glyph)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            Places.Add(new NavigationLocation(name, path, glyph));
        }
    }

    private void RefreshDevices()
    {
        Devices.Clear();
        var mappedNetworks = new List<NavigationLocation>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            var name = drive.Name;
            try
            {
                if (drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel))
                {
                    name = $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                }
            }
            catch
            {
                // The drive can disappear between discovery and inspection.
            }

            Devices.Add(new NavigationLocation(name, drive.RootDirectory.FullName, "\uEDA2"));
            if (drive.DriveType == DriveType.Network)
            {
                mappedNetworks.Add(new NavigationLocation(name, drive.RootDirectory.FullName, "\uE968"));
            }
        }


        while (NetworkLocations.Count > 1)
        {
            NetworkLocations.RemoveAt(NetworkLocations.Count - 1);
        }
        foreach (var network in mappedNetworks)
        {
            NetworkLocations.Add(network);
        }
    }

    private static List<FileSystemEntry> ReadDirectory(
        string path,
        bool showHiddenFiles,
        BrowserSortColumn sortColumn,
        bool sortDescending,
        bool foldersFirst)
    {
        var results = new List<FileSystemEntry>();
        foreach (var childPath in Directory.EnumerateFileSystemEntries(path))
        {
            try
            {
                var attributes = File.GetAttributes(childPath);
                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                var info = isDirectory ? (FileSystemInfo)new DirectoryInfo(childPath) : new FileInfo(childPath);

                if (!showHiddenFiles &&
                    (info.Name.StartsWith('.') ||
                     attributes.HasFlag(FileAttributes.Hidden) ||
                     attributes.HasFlag(FileAttributes.System)))
                {
                    continue;
                }

                results.Add(new FileSystemEntry(
                    info.Name,
                    info.FullName,
                    isDirectory,
                    isDirectory ? string.Empty : FormatSize(((FileInfo)info).Length),
                    isDirectory ? "Folder" : FileType(info.Extension),
                    info.LastWriteTime.ToString("g"),
                    isDirectory ? "\uE8B7" : "\uE8A5",
                    isDirectory ? 0 : ((FileInfo)info).Length,
                    info.LastWriteTime));
            }
            catch
            {
                // A single inaccessible or disappearing entry should not hide the folder.
            }
        }

        return SortEntries(results, sortColumn, sortDescending, foldersFirst).ToList();
    }

    private static string FormatStatus(IEnumerable<FileSystemEntry> entries, string path)
    {
        var list = entries as ICollection<FileSystemEntry> ?? entries.ToList();
        var folders = list.Count(entry => entry.IsDirectory);
        var files = list.Count - folders;
        var status = $"{folders} folder{(folders == 1 ? string.Empty : "s")}, {files} file{(files == 1 ? string.Empty : "s")}";

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    status += $" — {FormatSize(drive.AvailableFreeSpace)} free";
                }
            }
        }
        catch
        {
            // Network and transient volumes may not expose free-space information.
        }

        return status;
    }

    private static string FileType(string extension) =>
        string.IsNullOrWhiteSpace(extension) ? "File" : $"{extension.TrimStart('.').ToUpperInvariant()} file";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private void CancelSearchCore()
    {
        _searchVersion++;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
    }

    private void BeginLoadingImages(IReadOnlyCollection<FileSystemEntry> entries)
    {
        _imageCancellation?.Cancel();
        _imageCancellation?.Dispose();
        _imageCancellation = new CancellationTokenSource();
        var cancellationToken = _imageCancellation.Token;
        var version = ++_imageVersion;
        var requestedSize = ViewMode == BrowserViewMode.Icons
            ? (uint)Math.Clamp((int)IconImageSize, 32, 96)
            : 32u;

        _ = LoadImagesAsync(entries, requestedSize, version, cancellationToken);
    }

    private async Task LoadImagesAsync(
        IReadOnlyCollection<FileSystemEntry> entries,
        uint requestedSize,
        int version,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(6);
        var tasks = entries.Select(async entry =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var image = await _shellImageService.GetImageAsync(
                    entry.FullPath,
                    entry.IsDirectory,
                    ShowThumbnails,
                    requestedSize,
                    cancellationToken);

                if (version == _imageVersion && !cancellationToken.IsCancellationRequested)
                {
                    entry.Thumbnail = image;
                }
            }
            finally
            {
                gate.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Navigation, view changes, and newer search batches supersede older image work.
        }
    }

    private void ResortEntries()
    {
        if (Entries.Count < 2 || IsSearchMode)
        {
            return;
        }

        var sorted = SortEntries(Entries, SortColumn, SortDescending, FoldersFirst).ToList();
        ReconcileEntries(sorted);

        StatusText = FormatStatus(Entries, CurrentPath);
    }

    private void ConfigureEntryColumns()
    {
        foreach (var entry in Entries)
        {
            entry.ConfigureColumns(ShowSizeColumn, ShowTypeColumn, ShowModifiedColumn);
        }
    }

    private static IOrderedEnumerable<FileSystemEntry> SortEntries(
        IEnumerable<FileSystemEntry> entries,
        BrowserSortColumn sortColumn,
        bool sortDescending,
        bool foldersFirst)
    {
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        Func<FileSystemEntry, object> keySelector = sortColumn switch
        {
            BrowserSortColumn.Size => entry => entry.ByteSize,
            BrowserSortColumn.Type => entry => entry.Type,
            BrowserSortColumn.Modified => entry => entry.ModifiedTime,
            _ => entry => entry.Name
        };

        IOrderedEnumerable<FileSystemEntry> ordered;
        if (foldersFirst)
        {
            ordered = entries.OrderByDescending(entry => entry.IsDirectory);
            ordered = sortDescending
                ? ordered.ThenByDescending(keySelector, ObjectKeyComparer.Instance)
                : ordered.ThenBy(keySelector, ObjectKeyComparer.Instance);
        }
        else
        {
            ordered = sortDescending
                ? entries.OrderByDescending(keySelector, ObjectKeyComparer.Instance)
                : entries.OrderBy(keySelector, ObjectKeyComparer.Instance);
        }

        return ordered.ThenBy(entry => entry.Name, comparer);
    }

    private sealed class ObjectKeyComparer : IComparer<object>
    {
        public static ObjectKeyComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (x is string leftString && y is string rightString)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(leftString, rightString);
            }

            return Comparer<object>.Default.Compare(x, y);
        }
    }

    private void ConfigureFolderWatcher(string path)
    {
        if (_folderWatcher is { EnableRaisingEvents: true } existingWatcher && PathEquals(existingWatcher.Path, path))
        {
            return;
        }

        _folderWatcher?.Dispose();
        _folderWatcher = null;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size
            };
            watcher.Created += FolderWatcher_Changed;
            watcher.Deleted += FolderWatcher_Changed;
            watcher.Changed += FolderWatcher_Changed;
            watcher.Renamed += FolderWatcher_Changed;
            watcher.EnableRaisingEvents = true;
            _folderWatcher = watcher;
        }
        catch
        {
            // Some remote or protected locations do not support change notifications.
        }
    }

    private bool SynchronizeEntries(IReadOnlyList<FileSystemEntry> refreshedEntries)
    {
        var existingByPath = Entries.ToDictionary(
            entry => Path.TrimEndingDirectorySeparator(entry.FullPath),
            StringComparer.OrdinalIgnoreCase);
        var targetEntries = new List<FileSystemEntry>(refreshedEntries.Count);
        var contentChanged = refreshedEntries.Count != Entries.Count;

        foreach (var refreshed in refreshedEntries)
        {
            var key = Path.TrimEndingDirectorySeparator(refreshed.FullPath);
            if (existingByPath.TryGetValue(key, out var existing) && EntriesEquivalent(existing, refreshed))
            {
                targetEntries.Add(existing);
                continue;
            }

            contentChanged = true;
            targetEntries.Add(refreshed);
        }

        return ReconcileEntries(targetEntries) || contentChanged;
    }

    private bool ReconcileEntries(IReadOnlyList<FileSystemEntry> targetEntries)
        => ObservableCollectionReconciler.Reconcile(
            Entries,
            targetEntries,
            (current, target) => PathEquals(current.FullPath, target.FullPath));

    private static bool EntriesEquivalent(FileSystemEntry left, FileSystemEntry right) =>
        PathEquals(left.FullPath, right.FullPath) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.IsDirectory == right.IsDirectory &&
        left.ByteSize == right.ByteSize &&
        left.ModifiedTime == right.ModifiedTime &&
        string.Equals(left.Size, right.Size, StringComparison.Ordinal) &&
        string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
        string.Equals(left.Modified, right.Modified, StringComparison.Ordinal) &&
        string.Equals(left.Glyph, right.Glyph, StringComparison.Ordinal);

    private void FolderWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        CancellationTokenSource refreshCancellation;
        lock (_watcherRefreshLock)
        {
            _watcherRefreshCancellation?.Cancel();
            _watcherRefreshCancellation?.Dispose();
            _watcherRefreshCancellation = new CancellationTokenSource();
            refreshCancellation = _watcherRefreshCancellation;
        }

        _ = DebouncedWatcherRefreshAsync(refreshCancellation.Token);
    }

    private async Task DebouncedWatcherRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            App.DispatcherQueue.TryEnqueue(async () =>
            {
                if (!cancellationToken.IsCancellationRequested && !IsSearchMode && Directory.Exists(CurrentPath))
                {
                    await NavigateAsync(CurrentPath, false);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer file-system event restarted the debounce window.
        }
    }
}
