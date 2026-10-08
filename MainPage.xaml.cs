using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Storage;
using WinThunar.Models;
using WinThunar.Services;
using WinThunar.ViewModels;

namespace WinThunar;

public sealed partial class MainPage : Page
{
    private readonly FileOperationService _fileOperations = new();
    private readonly BulkRenameService _bulkRenameService = new();
    private readonly ShellImageService _shellImageService = new();
    private readonly PluginService _pluginService = new();
    private readonly FileTransferQueue _transferQueue;
    private readonly FileOperationHistory _history = new();
    private readonly AppSessionService _sessionService = new();
    private readonly ArchiveService _archiveService = new();
    private bool _restoringSession;
    private bool _sessionReady;
    private bool _splitPaneOpen;
    private BrowserPane _activePane = BrowserPane.Primary;
    private BrowserTabState? _splitTab;
    private CancellationTokenSource? _searchInputDelay;
    private MainPageViewModel? _searchBrowser;
    private string? _launchPath;
    private readonly List<Window> _ownedWindows = [];
    private bool _confirmMoveToTrash;
    private bool _restoreTabs = true;
    private bool _treeSidePane;
    private bool _usePathBar;
    private bool _singleClickActivation;
    private bool _rememberFolderViews;
    private bool _showImagePreview;
    private bool _showTerminalPanel;
    private Dictionary<string, FolderViewState> _folderViewSettings = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _previewCancellation;
    private Dictionary<string, string> _customShortcuts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TreeViewNode, NavigationLocation> _treeLocations = [];
    private readonly List<MenuFlyoutItemBase> _generatedPluginMenuItems = [];
    private readonly List<MenuFlyoutItemBase> _generatedFolderPluginMenuItems = [];
    private readonly List<Button> _generatedToolbarPluginButtons = [];
    private FileSystemWatcher? _templatesWatcher;
    private List<string> _toolbarOrder = ["Back", "Forward", "Up", "Home", "Reload", "Search"];
    private HashSet<string> _toolbarCustomActionIds = new(StringComparer.OrdinalIgnoreCase);
    private int _tabSelectionVersion;
    private double _dragSizeStart;
    private double _dragTypeStart;
    private double _dragModifiedStart;
    private double _dragSplitSizeStart;
    private double _dragSplitTypeStart;
    private int _dragPointerStartX;
    private bool _disposed;

    public MainPageViewModel ViewModel { get; } = new();
    public MainPageViewModel SplitViewModel { get; } = new();
    public bool HasPendingTransfers =>
        _transferQueue.ActiveJob is not null || _transferQueue.QueuedCount > 0;

    public void CancelTransfersForClose() => _transferQueue.Dispose();

    public MainPage()
    {
        _transferQueue = new FileTransferQueue(_fileOperations);
        _transferQueue.StateChanged += TransferQueue_StateChanged;
        _history.Changed += History_Changed;
        InitializeComponent();
        AddHandler(KeyDownEvent, new KeyEventHandler(Page_KeyDown), true);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        SplitViewModel.PropertyChanged += SplitViewModel_PropertyChanged;
        UpdateHistoryMenu();
        UpdateActivePaneVisuals();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _launchPath = e.Parameter as string;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var recovery = _fileOperations.RecoverInterruptedTransfers();
        _restoringSession = true;
        _pluginService.Reload();
        RefreshCreateDocumentMenus();
        StartTemplatesWatcher();
        var session = _sessionService.Load();
        ViewModel.ShowHiddenFiles = session.ShowHiddenFiles;
        ShowHiddenMenuItem.IsChecked = session.ShowHiddenFiles;
        ViewModel.ViewMode = Enum.IsDefined(session.ViewMode)
            ? session.ViewMode
            : BrowserViewMode.Details;
        ViewModel.SortColumn = Enum.IsDefined(session.SortColumn)
            ? session.SortColumn
            : BrowserSortColumn.Name;
        ViewModel.SortDescending = session.SortDescending;
        ViewModel.FoldersFirst = session.FoldersFirst;
        ViewModel.ShowThumbnails = session.ShowThumbnails;
        ViewModel.ZoomLevel = Math.Clamp(session.ZoomLevel, 0, 4);
        ViewModel.IncludeSubfolders = session.IncludeSubfolders;
        _confirmMoveToTrash = session.ConfirmMoveToTrash;
        _restoreTabs = session.RestoreTabs;
        _treeSidePane = session.TreeSidePane;
        ViewModel.ShowSizeColumn = session.ShowSizeColumn;
        ViewModel.ShowTypeColumn = session.ShowTypeColumn;
        ViewModel.ShowModifiedColumn = session.ShowModifiedColumn;
        ViewModel.SetColumnWidths(session.SizeColumnWidth, session.TypeColumnWidth, session.ModifiedColumnWidth);
        _usePathBar = session.UsePathBar;
        _singleClickActivation = session.SingleClickActivation;
        _rememberFolderViews = session.RememberFolderViews;
        _showImagePreview = session.ShowImagePreview;
        _showTerminalPanel = session.ShowTerminalPanel;
        _folderViewSettings = new Dictionary<string, FolderViewState>(session.FolderViewSettings, StringComparer.OrdinalIgnoreCase);
        _customShortcuts = new Dictionary<string, string>(session.CustomShortcuts, StringComparer.OrdinalIgnoreCase);
        _toolbarOrder = NormalizeToolbarOrder(session.ToolbarOrder);
        _toolbarCustomActionIds = new HashSet<string>(session.ToolbarCustomActions ?? [], StringComparer.OrdinalIgnoreCase);
        ApplyToolbarVisibility(session);
        ApplyToolbarOrder();
        if (session.BookmarkItems.Count > 0)
        {
            ViewModel.SetBookmarks(session.BookmarkItems);
        }
        else
        {
            ViewModel.SetBookmarks(session.Bookmarks);
        }
        SplitViewModel.ShowHiddenFiles = session.ShowHiddenFiles;
        SplitViewModel.ViewMode = BrowserViewMode.Details;
        SplitViewModel.SortColumn = ViewModel.SortColumn;
        SplitViewModel.SortDescending = ViewModel.SortDescending;
        SplitViewModel.FoldersFirst = ViewModel.FoldersFirst;
        SplitViewModel.ShowThumbnails = ViewModel.ShowThumbnails;
        SplitViewModel.ZoomLevel = ViewModel.ZoomLevel;
        SplitViewModel.IncludeSubfolders = ViewModel.IncludeSubfolders;
        SplitViewModel.ShowSizeColumn = ViewModel.ShowSizeColumn;
        SplitViewModel.ShowTypeColumn = ViewModel.ShowTypeColumn;
        SplitViewModel.ShowModifiedColumn = ViewModel.ShowModifiedColumn;
        SplitViewModel.SetColumnWidths(session.SplitSizeColumnWidth, session.SplitTypeColumnWidth, session.ModifiedColumnWidth);
        UpdateViewMenu();
        SetTreeSidePane(_treeSidePane);
        ApplyColumnVisibility();
        ApplyLocationSelector();
        ApplyImagePreview();
        ApplyTerminalPanel();

        var tabPaths = (_restoreTabs ? session.Tabs : [])
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .ToList();
        if (tabPaths.Count == 0)
        {
            tabPaths.Add(Directory.Exists(_launchPath)
                ? Path.GetFullPath(_launchPath!)
                : Directory.Exists(session.LastPath) ? session.LastPath! : ViewModel.HomePath);
        }

        foreach (var path in tabPaths)
        {
            AddBrowserTab(path, false);
        }

        BrowserTabs.SelectedIndex = Math.Clamp(session.ActiveTabIndex, 0, BrowserTabs.TabItems.Count - 1);
        var initialPath = ActiveTab?.Path ?? ViewModel.HomePath;
        ApplyFolderViewSettings(ViewModel, initialPath);
        await ViewModel.InitializeAsync(initialPath);
        ApplyToolbarOrder();
        if (session.SplitPaneOpen && Directory.Exists(session.SplitPath))
        {
            await OpenSplitPaneAsync(session.SplitPath!, false);
        }
        _restoringSession = false;
        _sessionReady = true;
        UpdateCommandState();
        SaveSession();
        if (recovery.RecoveredOperations > 0 || recovery.Errors.Count > 0)
        {
            ViewModel.StatusText = recovery.Errors.Count == 0
                ? $"Recovered {recovery.RecoveredOperations} interrupted transfer{(recovery.RecoveredOperations == 1 ? string.Empty : "s")}."
                : $"Recovered {recovery.RecoveredOperations} interrupted transfers; {recovery.Errors.Count} need attention.";
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _searchInputDelay?.Cancel();
        _searchInputDelay?.Dispose();
        _searchInputDelay = null;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        _templatesWatcher?.Dispose();
        _templatesWatcher = null;
        _transferQueue.StateChanged -= TransferQueue_StateChanged;
        _history.Changed -= History_Changed;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SplitViewModel.PropertyChanged -= SplitViewModel_PropertyChanged;
        _transferQueue.Dispose();
        ViewModel.Dispose();
        SplitViewModel.Dispose();
        foreach (var window in _ownedWindows.ToArray())
        {
            window.Close();
        }
        _ownedWindows.Clear();
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        var path = _activePane == BrowserPane.Secondary
            ? _splitTab?.GoBack()
            : ActiveTab?.GoBack();
        if (path is not null)
        {
            ApplyFolderViewSettings(ActiveBrowser, path);
            await ActiveBrowser.NavigateAsync(path, false);
        }
    }

    private async void Forward_Click(object sender, RoutedEventArgs e)
    {
        var path = _activePane == BrowserPane.Secondary
            ? _splitTab?.GoForward()
            : ActiveTab?.GoForward();
        if (path is not null)
        {
            ApplyFolderViewSettings(ActiveBrowser, path);
            await ActiveBrowser.NavigateAsync(path, false);
        }
    }

    private void NavigationButton_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not Button { Tag: string direction } button)
        {
            return;
        }

        var navigation = _activePane == BrowserPane.Secondary && _splitPaneOpen ? _splitTab : ActiveTab;
        var entries = string.Equals(direction, "Back", StringComparison.OrdinalIgnoreCase)
            ? navigation?.BackHistory
            : navigation?.ForwardHistory;
        var menu = new MenuFlyout();
        if (entries is null || entries.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = $"No {direction.ToLowerInvariant()} history", IsEnabled = false });
        }
        else
        {
            foreach (var entry in entries)
            {
                var item = new MenuFlyoutItem
                {
                    Text = BrowserTabState.GetTitle(entry.Path),
                    Tag = entry
                };
                ToolTipService.SetToolTip(item, entry.Path);
                item.Click += NavigationHistoryItem_Click;
                menu.Items.Add(item);
            }
        }

        e.Handled = true;
        if (e.TryGetPosition(button, out var position))
        {
            menu.ShowAt(button, position);
        }
        else
        {
            menu.ShowAt(button);
        }
    }

    private async void NavigationHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: NavigationHistoryEntry entry })
        {
            return;
        }

        var navigation = _activePane == BrowserPane.Secondary && _splitPaneOpen ? _splitTab : ActiveTab;
        var path = navigation?.GoToHistoryIndex(entry.Index);
        if (path is null)
        {
            return;
        }

        ApplyFolderViewSettings(ActiveBrowser, path);
        await ActiveBrowser.NavigateAsync(path, false);
        SaveSession();
    }

    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveBrowser.IsRecycleBinView)
        {
            return;
        }

        if (Directory.GetParent(ActiveBrowser.CurrentPath) is { } parent)
        {
            await NavigateActivePaneAsync(parent.FullName);
        }
    }

    private async void Home_Click(object sender, RoutedEventArgs e) => await NavigateActivePaneAsync(ViewModel.HomePath);

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ActiveBrowser.RefreshAsync();

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        _searchBrowser = ActiveBrowser;
        LocationLabel.Visibility = Visibility.Collapsed;
        LocationBox.Visibility = Visibility.Collapsed;
        LocationGoButton.Visibility = Visibility.Collapsed;
        SearchLabel.Visibility = Visibility.Visible;
        SearchBox.Visibility = Visibility.Visible;
        CloseSearchButton.Visibility = Visibility.Visible;
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_searchBrowser is null || SearchBox.Visibility != Visibility.Visible)
        {
            return;
        }

        _searchInputDelay?.Cancel();
        _searchInputDelay?.Dispose();
        _searchInputDelay = new CancellationTokenSource();
        var cancellationToken = _searchInputDelay.Token;
        try
        {
            await Task.Delay(350, cancellationToken);
            await _searchBrowser.SearchAsync(SearchBox.Text);
        }
        catch (OperationCanceledException)
        {
            // More typing superseded this debounce delay.
        }
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && _searchBrowser is not null)
        {
            e.Handled = true;
            _searchInputDelay?.Cancel();
            await _searchBrowser.SearchAsync(SearchBox.Text);
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            await CloseSearchAsync();
        }
    }

    private async void CloseSearch_Click(object sender, RoutedEventArgs e) => await CloseSearchAsync();

    private async void ShowHiddenFiles_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleHiddenFilesAsync();
        SplitViewModel.ShowHiddenFiles = ViewModel.ShowHiddenFiles;
        if (_splitPaneOpen)
        {
            await SplitViewModel.RefreshAsync();
        }
    }

    private async void SplitView_Click(object sender, RoutedEventArgs e)
    {
        if (_splitPaneOpen)
        {
            CloseSplitPane();
        }
        else
        {
            await OpenSplitPaneAsync(ActiveBrowser.CurrentPath, true);
        }
    }

    private void PrimaryPane_PointerPressed(object sender, PointerRoutedEventArgs e) => SetActivePane(BrowserPane.Primary);

    private void SecondaryPane_PointerPressed(object sender, PointerRoutedEventArgs e) => SetActivePane(BrowserPane.Secondary);

    private async void SplitBack_Click(object sender, RoutedEventArgs e)
    {
        SetActivePane(BrowserPane.Secondary);
        if (_splitTab?.GoBack() is { } path)
        {
            await SplitViewModel.NavigateAsync(path, false);
        }
    }

    private async void SplitUp_Click(object sender, RoutedEventArgs e)
    {
        SetActivePane(BrowserPane.Secondary);
        if (SplitViewModel.IsRecycleBinView)
        {
            return;
        }

        if (Directory.GetParent(SplitViewModel.CurrentPath) is { } parent)
        {
            await NavigateSplitPaneAsync(parent.FullName);
        }
    }

    private async void SplitHome_Click(object sender, RoutedEventArgs e)
    {
        SetActivePane(BrowserPane.Secondary);
        await NavigateSplitPaneAsync(ViewModel.HomePath);
    }

    private async void SplitGo_Click(object sender, RoutedEventArgs e)
    {
        SetActivePane(BrowserPane.Secondary);
        await NavigateSplitPaneAsync(SplitLocationBox.Text);
    }

    private async void SplitLocationBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            SetActivePane(BrowserPane.Secondary);
            await NavigateSplitPaneAsync(SplitLocationBox.Text);
        }
    }

    private void DetailsView_Click(object sender, RoutedEventArgs e) => SetViewMode(BrowserViewMode.Details);

    private void IconView_Click(object sender, RoutedEventArgs e) => SetViewMode(BrowserViewMode.Icons);

    private void CompactView_Click(object sender, RoutedEventArgs e) => SetViewMode(BrowserViewMode.Compact);

    private void SortName_Click(object sender, RoutedEventArgs e) => SetSortColumn(BrowserSortColumn.Name);

    private void SortSize_Click(object sender, RoutedEventArgs e) => SetSortColumn(BrowserSortColumn.Size);

    private void SortType_Click(object sender, RoutedEventArgs e) => SetSortColumn(BrowserSortColumn.Type);

    private void SortModified_Click(object sender, RoutedEventArgs e) => SetSortColumn(BrowserSortColumn.Modified);

    private void SortDescending_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SortDescending = SortDescendingMenuItem.IsChecked;
        SplitViewModel.SortDescending = ViewModel.SortDescending;
        UpdateViewMenu();
        SaveSession();
    }

    private void FoldersFirst_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.FoldersFirst = FoldersFirstMenuItem.IsChecked;
        SplitViewModel.FoldersFirst = ViewModel.FoldersFirst;
        UpdateViewMenu();
        SaveSession();
    }

    private void ShowThumbnails_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowThumbnails = ShowThumbnailsMenuItem.IsChecked;
        SplitViewModel.ShowThumbnails = ViewModel.ShowThumbnails;
        SaveSession();
    }

    private void ImagePreview_Click(object sender, RoutedEventArgs e)
    {
        _showImagePreview = ImagePreviewMenuItem.IsChecked;
        ApplyImagePreview();
        SaveSession();
    }

    private void ApplyImagePreview()
    {
        ImagePreviewMenuItem.IsChecked = _showImagePreview;
        ImagePreviewColumn.Width = _showImagePreview ? new GridLength(260) : new GridLength(0);
        ImagePreviewPane.Visibility = _showImagePreview ? Visibility.Visible : Visibility.Collapsed;
        if (_showImagePreview)
        {
            _ = UpdateImagePreviewAsync(SelectedEntries().FirstOrDefault());
        }
    }

    private void TerminalPanel_Click(object sender, RoutedEventArgs e)
    {
        _showTerminalPanel = TerminalPanelMenuItem.IsChecked;
        ApplyTerminalPanel();
        SaveSession();
    }

    private void CloseTerminalPanel_Click(object sender, RoutedEventArgs e)
    {
        _showTerminalPanel = false;
        ApplyTerminalPanel();
        SaveSession();
    }

    private void ApplyTerminalPanel()
    {
        TerminalPanelMenuItem.IsChecked = _showTerminalPanel;
        TerminalPane.Visibility = _showTerminalPanel ? Visibility.Visible : Visibility.Collapsed;
        TerminalPathLabel.Text = ActiveBrowser.CurrentPath;
        if (_showTerminalPanel)
        {
            TerminalCommand.Focus(FocusState.Programmatic);
        }
    }

    private async void RunTerminalCommand_Click(object sender, RoutedEventArgs e) => await RunTerminalCommandAsync();

    private async void TerminalCommand_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await RunTerminalCommandAsync();
        }
    }

    private async Task RunTerminalCommandAsync()
    {
        var command = TerminalCommand.Text.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var workingDirectory = ActiveBrowser.CurrentPath;
        TerminalCommand.Text = string.Empty;
        TerminalCommand.IsEnabled = false;
        TerminalOutput.Text += $"PS {workingDirectory}> {command}{Environment.NewLine}";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("PowerShell could not be started.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;
            if (!string.IsNullOrEmpty(output))
            {
                TerminalOutput.Text += output;
            }
            if (!string.IsNullOrEmpty(error))
            {
                TerminalOutput.Text += error;
            }
            TerminalOutput.Text += $"[exit {process.ExitCode}]{Environment.NewLine}";
            TerminalOutput.SelectionStart = TerminalOutput.Text.Length;
        }
        catch (Exception ex)
        {
            TerminalOutput.Text += $"Terminal error: {ex.Message}{Environment.NewLine}";
        }
        finally
        {
            TerminalCommand.IsEnabled = true;
            TerminalCommand.Focus(FocusState.Programmatic);
        }
    }

    private void ShortcutsPane_Click(object sender, RoutedEventArgs e) => SetTreeSidePane(false);

    private void TreePane_Click(object sender, RoutedEventArgs e) => SetTreeSidePane(true);

    private void SetTreeSidePane(bool useTree)
    {
        _treeSidePane = useTree;
        ShortcutsSidePane.Visibility = useTree ? Visibility.Collapsed : Visibility.Visible;
        DirectoryTree.Visibility = useTree ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsPaneMenuItem.IsChecked = !useTree;
        TreePaneMenuItem.IsChecked = useTree;
        if (useTree && DirectoryTree.RootNodes.Count == 0)
        {
            InitializeDirectoryTree();
        }

        SaveSession();
    }

    private void LocationEntry_Click(object sender, RoutedEventArgs e)
    {
        _usePathBar = false;
        ApplyLocationSelector();
        SaveSession();
    }

    private void PathBar_Click(object sender, RoutedEventArgs e)
    {
        _usePathBar = true;
        ApplyLocationSelector();
        SaveSession();
    }

    private void PathBarEdit_Click(object sender, RoutedEventArgs e)
    {
        PathBarScroller.Visibility = Visibility.Collapsed;
        PathBarEditButton.Visibility = Visibility.Collapsed;
        LocationBox.Visibility = Visibility.Visible;
        LocationGoButton.Visibility = Visibility.Visible;
        LocationBox.Focus(FocusState.Programmatic);
        LocationBox.SelectAll();
    }

    private void ApplyLocationSelector()
    {
        if (SearchBox.Visibility == Visibility.Visible)
        {
            return;
        }

        LocationEntryMenuItem.IsChecked = !_usePathBar;
        PathBarMenuItem.IsChecked = _usePathBar;
        LocationBox.Visibility = _usePathBar ? Visibility.Collapsed : Visibility.Visible;
        LocationGoButton.Visibility = _usePathBar ? Visibility.Collapsed : Visibility.Visible;
        PathBarScroller.Visibility = _usePathBar ? Visibility.Visible : Visibility.Collapsed;
        PathBarEditButton.Visibility = _usePathBar ? Visibility.Visible : Visibility.Collapsed;
        BuildPathBar(ViewModel.CurrentPath);
    }

    private void BuildPathBar(string path)
    {
        PathBarPanel.Children.Clear();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var segments = new Stack<string>();
        try
        {
            for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            {
                segments.Push(current.FullName);
            }
        }
        catch
        {
            segments.Push(path);
        }

        foreach (var segment in segments)
        {
            var label = BrowserTabState.GetTitle(segment);
            var button = new Button
            {
                Content = string.IsNullOrWhiteSpace(label) ? segment : label,
                Tag = segment,
                Padding = new Thickness(9, 4, 9, 4)
            };
            button.Click += PathSegment_Click;
            PathBarPanel.Children.Add(button);
        }
    }

    private async void PathSegment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            await NavigateCurrentTabAsync(path);
        }
    }

    private async void ConfigureColumns_Click(object sender, RoutedEventArgs e)
    {
        var size = new CheckBox { Content = "Size", IsChecked = ViewModel.ShowSizeColumn };
        var type = new CheckBox { Content = "Type", IsChecked = ViewModel.ShowTypeColumn };
        var modified = new CheckBox { Content = "Date Modified", IsChecked = ViewModel.ShowModifiedColumn };
        var content = new StackPanel { Spacing = 8, MinWidth = 300 };
        content.Children.Add(new TextBlock { Text = "Name is always shown. Select the additional columns:" });
        content.Children.Add(size);
        content.Children.Add(type);
        content.Children.Add(modified);
        var dialog = new ContentDialog
        {
            Title = "Configure Columns",
            Content = content,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.ShowSizeColumn = size.IsChecked == true;
        ViewModel.ShowTypeColumn = type.IsChecked == true;
        ViewModel.ShowModifiedColumn = modified.IsChecked == true;
        SplitViewModel.ShowSizeColumn = ViewModel.ShowSizeColumn;
        SplitViewModel.ShowTypeColumn = ViewModel.ShowTypeColumn;
        SplitViewModel.ShowModifiedColumn = ViewModel.ShowModifiedColumn;
        ApplyColumnVisibility();
        SaveSession();
    }

    private void DetailsHeader_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not FrameworkElement header)
        {
            return;
        }

        var menu = new MenuFlyout();
        foreach (var (name, visible) in new[]
                 {
                     ("Size", ViewModel.ShowSizeColumn),
                     ("Type", ViewModel.ShowTypeColumn),
                     ("Date Modified", ViewModel.ShowModifiedColumn)
                 })
        {
            var item = new ToggleMenuFlyoutItem { Text = name, IsChecked = visible, Tag = name };
            item.Click += ColumnVisibility_Click;
            menu.Items.Add(item);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        var configure = new MenuFlyoutItem { Text = "Configure Columns..." };
        configure.Click += ConfigureColumns_Click;
        menu.Items.Add(configure);

        e.Handled = true;
        if (e.TryGetPosition(header, out var position))
        {
            menu.ShowAt(header, position);
        }
        else
        {
            menu.ShowAt(header);
        }
    }

    private void ColumnVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem { Tag: string column } item)
        {
            return;
        }

        switch (column)
        {
            case "Size": ViewModel.ShowSizeColumn = item.IsChecked; break;
            case "Type": ViewModel.ShowTypeColumn = item.IsChecked; break;
            case "Date Modified": ViewModel.ShowModifiedColumn = item.IsChecked; break;
        }
        SplitViewModel.ShowSizeColumn = ViewModel.ShowSizeColumn;
        SplitViewModel.ShowTypeColumn = ViewModel.ShowTypeColumn;
        SplitViewModel.ShowModifiedColumn = ViewModel.ShowModifiedColumn;
        ApplyColumnVisibility();
        SaveSession();
    }

    private async void ConfigureToolbar_Click(object sender, RoutedEventArgs e)
    {
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MinHeight = 230
        };
        foreach (var name in _toolbarOrder)
        {
            var button = GetToolbarButton(name);
            list.Items.Add(new CheckBox
            {
                Content = name,
                Tag = name,
                IsChecked = button?.Visibility == Visibility.Visible,
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }

        var moveUp = new Button { Content = "Move Up", IsEnabled = false };
        var moveDown = new Button { Content = "Move Down", IsEnabled = false };
        void UpdateMoveButtons()
        {
            moveUp.IsEnabled = list.SelectedIndex > 0;
            moveDown.IsEnabled = list.SelectedIndex >= 0 && list.SelectedIndex < list.Items.Count - 1;
        }
        list.SelectionChanged += (_, _) => UpdateMoveButtons();
        moveUp.Click += (_, _) => MoveToolbarChoice(-1);
        moveDown.Click += (_, _) => MoveToolbarChoice(1);
        void MoveToolbarChoice(int offset)
        {
            var oldIndex = list.SelectedIndex;
            var newIndex = oldIndex + offset;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= list.Items.Count)
            {
                return;
            }
            var item = list.Items[oldIndex];
            list.Items.RemoveAt(oldIndex);
            list.Items.Insert(newIndex, item);
            list.SelectedIndex = newIndex;
        }

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        controls.Children.Add(moveUp);
        controls.Children.Add(moveDown);
        var content = new StackPanel { Spacing = 8, MinWidth = 360 };
        content.Children.Add(new TextBlock { Text = "Choose visible commands and arrange their order:" });
        content.Children.Add(list);
        content.Children.Add(controls);
        var customActionChoices = GetCurrentFolderPluginActions()
            .Select(available => (Available: available, Choice: new CheckBox
            {
                Content = available.Action.Name,
                IsChecked = _toolbarCustomActionIds.Contains(GetPluginActionKey(available))
            }))
            .ToArray();
        if (customActionChoices.Length > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Folder custom actions",
                Margin = new Thickness(0, 8, 0, 0),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            foreach (var (_, choice) in customActionChoices)
            {
                content.Children.Add(choice);
            }
        }

        var dialog = new ContentDialog
        {
            Title = "Configure Toolbar",
            Content = content,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _toolbarOrder = list.Items
            .OfType<CheckBox>()
            .Select(item => (string)item.Tag)
            .ToList();
        foreach (var item in list.Items.OfType<CheckBox>())
        {
            if (GetToolbarButton((string)item.Tag) is { } button)
            {
                button.Visibility = item.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        _toolbarCustomActionIds = customActionChoices
            .Where(pair => pair.Choice.IsChecked == true)
            .Select(pair => GetPluginActionKey(pair.Available))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ApplyToolbarOrder();
        SaveSession();
    }

    private void ApplyToolbarVisibility(AppSessionState session)
    {
        BackToolbarButton.Visibility = session.ShowBackToolbarButton ? Visibility.Visible : Visibility.Collapsed;
        ForwardToolbarButton.Visibility = session.ShowForwardToolbarButton ? Visibility.Visible : Visibility.Collapsed;
        UpToolbarButton.Visibility = session.ShowUpToolbarButton ? Visibility.Visible : Visibility.Collapsed;
        HomeToolbarButton.Visibility = session.ShowHomeToolbarButton ? Visibility.Visible : Visibility.Collapsed;
        ReloadToolbarButton.Visibility = session.ShowReloadToolbarButton ? Visibility.Visible : Visibility.Collapsed;
        SearchToolbarButton.Visibility = session.ShowSearchToolbarButton ? Visibility.Visible : Visibility.Collapsed;
    }

    private static List<string> NormalizeToolbarOrder(IEnumerable<string>? order)
    {
        string[] defaults = ["Back", "Forward", "Up", "Home", "Reload", "Search"];
        var normalized = (order ?? [])
            .Where(name => defaults.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        normalized.AddRange(defaults.Where(name => !normalized.Contains(name, StringComparer.OrdinalIgnoreCase)));
        return normalized;
    }

    private Button? GetToolbarButton(string name) => name switch
    {
        "Back" => BackToolbarButton,
        "Forward" => ForwardToolbarButton,
        "Up" => UpToolbarButton,
        "Home" => HomeToolbarButton,
        "Reload" => ReloadToolbarButton,
        "Search" => SearchToolbarButton,
        _ => null
    };

    private void ApplyToolbarOrder()
    {
        ToolbarPanel.Children.Clear();
        _generatedToolbarPluginButtons.Clear();
        for (var index = 0; index < _toolbarOrder.Count; index++)
        {
            if (index == 4)
            {
                ToolbarPanel.Children.Add(ToolbarSeparator);
            }
            if (GetToolbarButton(_toolbarOrder[index]) is { } button)
            {
                ToolbarPanel.Children.Add(button);
            }
        }
        foreach (var available in GetCurrentFolderPluginActions()
                     .Where(available => _toolbarCustomActionIds.Contains(GetPluginActionKey(available))))
        {
            var button = new Button
            {
                Content = available.Action.Name,
                Tag = available,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 5, 8, 5)
            };
            ToolTipService.SetToolTip(button, available.Action.Description);
            button.Click += PluginAction_Click;
            ToolbarPanel.Children.Add(button);
            _generatedToolbarPluginButtons.Add(button);
        }
        var visibleBeforeSeparator = _toolbarOrder.Take(4)
            .Select(GetToolbarButton)
            .Any(button => button?.Visibility == Visibility.Visible);
        var visibleAfterSeparator = _toolbarOrder.Skip(4)
            .Select(GetToolbarButton)
            .Any(button => button?.Visibility == Visibility.Visible) ||
            _generatedToolbarPluginButtons.Count > 0;
        ToolbarSeparator.Visibility = visibleBeforeSeparator && visibleAfterSeparator
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyColumnVisibility()
    {
        SizeColumn.MinWidth = ViewModel.ShowSizeColumn ? 55 : 0;
        TypeColumn.MinWidth = ViewModel.ShowTypeColumn ? 70 : 0;
        ModifiedColumn.MinWidth = ViewModel.ShowModifiedColumn ? 100 : 0;
        SizeColumn.Width = new GridLength(ViewModel.ShowSizeColumn ? ViewModel.SizeColumnWidth : 0);
        TypeColumn.Width = new GridLength(ViewModel.ShowTypeColumn ? ViewModel.TypeColumnWidth : 0);
        ModifiedColumn.Width = new GridLength(ViewModel.ShowModifiedColumn ? ViewModel.ModifiedColumnWidth : 0);
        NameSizeGripColumn.Width = new GridLength(ViewModel.ShowSizeColumn ? 7 : 0);
        SizeTypeGripColumn.Width = new GridLength(ViewModel.ShowSizeColumn && ViewModel.ShowTypeColumn ? 7 : 0);
        TypeModifiedGripColumn.Width = new GridLength(ViewModel.ShowTypeColumn && ViewModel.ShowModifiedColumn ? 7 : 0);
        NameSizeColumnGrip.Visibility = ViewModel.ShowSizeColumn ? Visibility.Visible : Visibility.Collapsed;
        SizeTypeColumnGrip.Visibility = ViewModel.ShowSizeColumn && ViewModel.ShowTypeColumn ? Visibility.Visible : Visibility.Collapsed;
        TypeModifiedColumnGrip.Visibility = ViewModel.ShowTypeColumn && ViewModel.ShowModifiedColumn ? Visibility.Visible : Visibility.Collapsed;

        SplitSizeColumn.MinWidth = SplitViewModel.ShowSizeColumn ? 55 : 0;
        SplitTypeColumn.MinWidth = SplitViewModel.ShowTypeColumn ? 70 : 0;
        SplitSizeColumn.Width = new GridLength(SplitViewModel.ShowSizeColumn ? SplitViewModel.SizeColumnWidth : 0);
        SplitTypeColumn.Width = new GridLength(SplitViewModel.ShowTypeColumn ? SplitViewModel.TypeColumnWidth : 0);
        SplitNameSizeGripColumn.Width = new GridLength(SplitViewModel.ShowSizeColumn ? 7 : 0);
        SplitSizeTypeGripColumn.Width = new GridLength(SplitViewModel.ShowSizeColumn && SplitViewModel.ShowTypeColumn ? 7 : 0);
        SplitNameSizeColumnGrip.Visibility = SplitViewModel.ShowSizeColumn ? Visibility.Visible : Visibility.Collapsed;
        SplitSizeTypeColumnGrip.Visibility = SplitViewModel.ShowSizeColumn && SplitViewModel.ShowTypeColumn
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ColumnGrip_DragStarted(object sender, DragStartedEventArgs e)
    {
        _dragSizeStart = SizeColumn.ActualWidth;
        _dragTypeStart = TypeColumn.ActualWidth;
        _dragModifiedStart = ModifiedColumn.ActualWidth;
        _dragSplitSizeStart = SplitSizeColumn.ActualWidth;
        _dragSplitTypeStart = SplitTypeColumn.ActualWidth;
        if (GetCursorPos(out var point))
        {
            _dragPointerStartX = point.X;
        }
    }

    private void ColumnGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is FrameworkElement grip)
        {
            var delta = e.HorizontalChange;
            if (GetCursorPos(out var point))
            {
                var scale = XamlRoot?.RasterizationScale ?? 1;
                delta = (point.X - _dragPointerStartX) / scale;
            }

            ApplyColumnGripDelta(grip, delta);
        }
    }

    private void ColumnGrip_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        ViewModel.SetColumnWidths(SizeColumn.ActualWidth, TypeColumn.ActualWidth, ModifiedColumn.ActualWidth);
        SplitViewModel.SetColumnWidths(SplitSizeColumn.ActualWidth, SplitTypeColumn.ActualWidth, ViewModel.ModifiedColumnWidth);
        SaveSession();
    }

    private void ApplyColumnGripDelta(FrameworkElement grip, double delta)
    {
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        if (ReferenceEquals(grip, NameSizeColumnGrip))
        {
            ResizeRightColumn(SizeColumn, _dragSizeStart, delta, 55, 420);
        }
        else if (ReferenceEquals(grip, SizeTypeColumnGrip))
        {
            ResizeColumnPair(SizeColumn, TypeColumn, _dragSizeStart, _dragTypeStart, delta, 55, 70);
        }
        else if (ReferenceEquals(grip, TypeModifiedColumnGrip))
        {
            ResizeColumnPair(TypeColumn, ModifiedColumn, _dragTypeStart, _dragModifiedStart, delta, 70, 100);
        }
        else if (ReferenceEquals(grip, SplitNameSizeColumnGrip))
        {
            ResizeRightColumn(SplitSizeColumn, _dragSplitSizeStart, delta, 55, 420);
        }
        else if (ReferenceEquals(grip, SplitSizeTypeColumnGrip))
        {
            ResizeColumnPair(SplitSizeColumn, SplitTypeColumn, _dragSplitSizeStart, _dragSplitTypeStart, delta, 55, 70);
        }
    }

    private static void ResizeRightColumn(
        ColumnDefinition right,
        double startWidth,
        double delta,
        double minimum,
        double maximum)
    {
        right.Width = new GridLength(Math.Clamp(startWidth - delta, minimum, maximum));
    }

    private static void ResizeColumnPair(
        ColumnDefinition left,
        ColumnDefinition right,
        double leftStart,
        double rightStart,
        double delta,
        double leftMinimum,
        double rightMinimum)
    {
        var total = leftStart + rightStart;
        var leftWidth = Math.Clamp(leftStart + delta, leftMinimum, total - rightMinimum);
        left.Width = new GridLength(leftWidth);
        right.Width = new GridLength(total - leftWidth);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private void InitializeDirectoryTree()
    {
        DirectoryTree.RootNodes.Clear();
        _treeLocations.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            var node = new TreeViewNode
            {
                Content = drive.Name,
                HasUnrealizedChildren = drive.IsReady
            };
            _treeLocations[node] = new NavigationLocation(drive.Name, drive.RootDirectory.FullName, "\uEDA2");
            DirectoryTree.RootNodes.Add(node);
        }
    }

    private async void DirectoryTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Node.Children.Count > 0 || !_treeLocations.TryGetValue(args.Node, out var location))
        {
            return;
        }

        string[] folders;
        try
        {
            folders = await Task.Run(() => Directory.EnumerateDirectories(location.Path)
                .Where(path =>
                {
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        return ViewModel.ShowHiddenFiles ||
                               (!attributes.HasFlag(System.IO.FileAttributes.Hidden) &&
                                !attributes.HasFlag(System.IO.FileAttributes.System));
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
        }
        catch
        {
            args.Node.HasUnrealizedChildren = false;
            return;
        }

        foreach (var folder in folders)
        {
            var childNode = new TreeViewNode
            {
                Content = Path.GetFileName(folder),
                HasUnrealizedChildren = true
            };
            _treeLocations[childNode] = new NavigationLocation(Path.GetFileName(folder), folder, "\uE8B7");
            args.Node.Children.Add(childNode);
        }

        args.Node.HasUnrealizedChildren = false;
    }

    private async void DirectoryTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (sender.SelectedNode is { } node && _treeLocations.TryGetValue(node, out var location))
        {
            await NavigateCurrentTabAsync(location.Path);
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoomLevel(ViewModel.ZoomLevel + 1);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoomLevel(ViewModel.ZoomLevel - 1);

    private void ZoomNormal_Click(object sender, RoutedEventArgs e) => SetZoomLevel(2);

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StatusText = ViewModel.AddBookmark(ActiveBrowser.CurrentPath)
            ? $"Bookmarked {BrowserTabState.GetTitle(ActiveBrowser.CurrentPath)}."
            : "The current folder is already bookmarked.";
        SaveSession();
    }

    private void RemoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StatusText = ViewModel.RemoveBookmark(ActiveBrowser.CurrentPath)
            ? "Bookmark removed."
            : "The current folder is not bookmarked.";
        SaveSession();
    }

    private async void RenameBookmark_Click(object sender, RoutedEventArgs e)
    {
        var bookmark = ViewModel.Bookmarks.FirstOrDefault(item => PathEquals(item.Path, ActiveBrowser.CurrentPath));
        if (bookmark is null)
        {
            ViewModel.StatusText = "The current folder is not bookmarked.";
            return;
        }

        var name = await PromptForNameAsync("Rename Bookmark", "Bookmark name:", bookmark.Name);
        if (name is not null && ViewModel.RenameBookmark(bookmark.Path, name))
        {
            ViewModel.StatusText = $"Bookmark renamed to '{name.Trim()}'.";
            SaveSession();
        }
    }

    private void MoveBookmarkUp_Click(object sender, RoutedEventArgs e) => MoveCurrentBookmark(-1);

    private void MoveBookmarkDown_Click(object sender, RoutedEventArgs e) => MoveCurrentBookmark(1);

    private void MoveCurrentBookmark(int offset)
    {
        ViewModel.StatusText = ViewModel.MoveBookmark(ActiveBrowser.CurrentPath, offset)
            ? "Bookmark order updated."
            : "The current folder's bookmark cannot move in that direction.";
        SaveSession();
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) =>
        AddBrowserTab(string.IsNullOrWhiteSpace(ActiveBrowser.CurrentPath) ? ViewModel.HomePath : ActiveBrowser.CurrentPath, true);

    private void NewWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new MainWindow(ActiveBrowser.CurrentPath);
        _ownedWindows.Add(window);
        window.Closed += (_, _) => _ownedWindows.Remove(window);
        window.Activate();
    }

    private void OpenSelectedInNewWindow_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntries() is not [{ IsDirectory: true } folder])
        {
            return;
        }

        var window = new MainWindow(folder.FullPath);
        _ownedWindows.Add(window);
        window.Closed += (_, _) => _ownedWindows.Remove(window);
        window.Activate();
    }

    private void OpenSelectedTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntries() is [{ IsDirectory: true } folder])
        {
            ShellIntegrationService.OpenTerminal(folder.FullPath);
        }
    }

    private void OpenInNewTab_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveBrowser.IsRecycleBinView)
        {
            ActiveBrowser.StatusText = "Restore the folder before opening it in a tab.";
            return;
        }

        var folder = SelectedEntries().FirstOrDefault(entry => entry.IsDirectory);
        if (folder is null)
        {
            ViewModel.StatusText = "Select a folder to open in a new tab.";
            return;
        }

        AddBrowserTab(folder.FullPath, true);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserTabs.SelectedItem is TabViewItem tab)
        {
            CloseBrowserTab(tab);
        }
    }

    private void BrowserTabs_AddTabButtonClick(TabView sender, object args) => NewTab_Click(sender, new RoutedEventArgs());

    private void BrowserTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        CloseBrowserTab(args.Tab);

    private async void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_restoringSession || ActiveTab is not { } tab)
        {
            return;
        }

        var selectionVersion = Interlocked.Increment(ref _tabSelectionVersion);
        await ViewModel.NavigateAsync(tab.Path, false);
        if (selectionVersion == _tabSelectionVersion &&
            ActiveTab is { } selectedTab &&
            selectedTab.Id == tab.Id &&
            PathEquals(ViewModel.CurrentPath, tab.Path))
        {
            SaveSession();
        }
    }

    private async void Go_Click(object sender, RoutedEventArgs e) => await NavigateCurrentTabAsync(LocationBox.Text);

    private async void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        if (RejectDestinationMutation("Create Folder"))
        {
            return;
        }

        var suggestedName = FileOperationService.GetAvailableLeafName(ActiveBrowser.CurrentPath, "New Folder", true);
        var name = await PromptForNameAsync(
            "Create Folder",
            "Enter the new folder name:",
            suggestedName,
            value => ValidateNewItemName(ActiveBrowser.CurrentPath, value));
        if (name is null)
        {
            return;
        }

        string? createdPath = null;
        if (await RunFileOperationAsync(
            $"Creating folder '{name}'...",
            async () => createdPath = await _fileOperations.CreateDirectoryAsync(ActiveBrowser.CurrentPath, name)) &&
            createdPath is not null)
        {
            await PushCreateHistoryAsync(createdPath, true);
        }
    }

    private async void CreateDocumentTemplate_Click(object sender, RoutedEventArgs e)
    {
        var templatePath = (sender as MenuFlyoutItem)?.Tag as string;
        await CreateDocumentAsync(string.IsNullOrEmpty(templatePath) ? null : templatePath);
    }

    private async Task CreateDocumentAsync(string? selectedTemplate)
    {
        if (RejectDestinationMutation("Create Document"))
        {
            return;
        }

        var preferredName = selectedTemplate is null ? "New Empty File" : Path.GetFileName(selectedTemplate);
        var suggestedName = FileOperationService.GetAvailableLeafName(ActiveBrowser.CurrentPath, preferredName);
        var title = selectedTemplate is null
            ? "Create Empty File"
            : $"Create Document from Template '{Path.GetFileName(selectedTemplate)}'";
        var name = await PromptForNameAsync(
            title,
            "Enter the new document name:",
            suggestedName,
            value => ValidateNewItemName(ActiveBrowser.CurrentPath, value));
        if (name is null)
        {
            return;
        }

        string? createdPath = null;
        if (await RunFileOperationAsync(
            $"Creating document '{name}'...",
            async () =>
            {
                if (selectedTemplate is null)
                {
                    createdPath = await _fileOperations.CreateFileAsync(ActiveBrowser.CurrentPath, name);
                    return;
                }

                var validationError = FileOperationService.ValidateLeafName(name);
                if (validationError is not null)
                {
                    throw new ArgumentException(validationError, nameof(name));
                }

                createdPath = Path.Combine(ActiveBrowser.CurrentPath, name);
                await Task.Run(() => File.Copy(selectedTemplate, createdPath, false));
            }) &&
            createdPath is not null)
        {
            await PushCreateHistoryAsync(createdPath, false);
        }
    }

    private void RefreshCreateDocumentMenus()
    {
        var templatesFolder = GetTemplatesFolder();

        PopulateCreateDocumentMenu(FileCreateDocumentMenu, templatesFolder);
        PopulateCreateDocumentMenu(FolderCreateDocumentMenu, templatesFolder);
    }

    private string GetTemplatesFolder()
    {
        var templatesFolder = Environment.GetFolderPath(Environment.SpecialFolder.Templates);
        return string.IsNullOrWhiteSpace(templatesFolder)
            ? Path.Combine(ViewModel.HomePath, "Templates")
            : templatesFolder;
    }

    private void StartTemplatesWatcher()
    {
        _templatesWatcher?.Dispose();
        _templatesWatcher = null;
        var templatesFolder = GetTemplatesFolder();
        if (!Directory.Exists(templatesFolder))
        {
            return;
        }

        try
        {
            _templatesWatcher = new FileSystemWatcher(templatesFolder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            FileSystemEventHandler refresh = (_, _) => DispatcherQueue.TryEnqueue(RefreshCreateDocumentMenus);
            RenamedEventHandler renamed = (_, _) => DispatcherQueue.TryEnqueue(RefreshCreateDocumentMenus);
            _templatesWatcher.Created += refresh;
            _templatesWatcher.Deleted += refresh;
            _templatesWatcher.Changed += refresh;
            _templatesWatcher.Renamed += renamed;
            _templatesWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Templates will refresh when their menu is reopened: {ex.Message}";
        }
    }

    private void PopulateCreateDocumentMenu(MenuFlyoutSubItem menu, string templatesFolder)
    {
        menu.Items.Clear();
        if (!AppendDocumentTemplates(menu.Items, templatesFolder))
        {
            menu.Items.Add(new MenuFlyoutItem
            {
                Text = $"No templates installed in\n'{templatesFolder}'",
                IsEnabled = false
            });
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        var emptyFileItem = new MenuFlyoutItem
        {
            Text = "Empty File",
            Tag = string.Empty
        };
        emptyFileItem.Click += CreateDocumentTemplate_Click;
        menu.Items.Add(emptyFileItem);
    }

    private bool AppendDocumentTemplates(IList<MenuFlyoutItemBase> items, string folder)
    {
        string[] directories;
        string[] files;
        try
        {
            if (!Directory.Exists(folder))
            {
                return false;
            }

            directories = Directory.EnumerateDirectories(folder)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            files = Directory.EnumerateFiles(folder)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        var added = false;
        foreach (var directory in directories)
        {
            var submenu = new MenuFlyoutSubItem { Text = Path.GetFileName(directory) };
            if (AppendDocumentTemplates(submenu.Items, directory))
            {
                items.Add(submenu);
                added = true;
            }
        }

        foreach (var file in files)
        {
            var item = new MenuFlyoutItem
            {
                Text = Path.GetFileName(file),
                Tag = file
            };
            item.Click += CreateDocumentTemplate_Click;
            items.Add(item);
            added = true;
        }

        return added;
    }

    private async void Cut_Click(object sender, RoutedEventArgs e) => await CopySelectionToClipboardAsync(true);

    private async void Copy_Click(object sender, RoutedEventArgs e) => await CopySelectionToClipboardAsync(false);

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        await PasteIntoAsync(ActiveBrowser.CurrentPath);
    }

    private async void PasteIntoSelectedFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntries() is [{ IsDirectory: true } folder])
        {
            await PasteIntoAsync(folder.FullPath);
        }
    }

    private async Task PasteIntoAsync(string destinationPath)
    {
        if (RejectDestinationMutation("Paste", destinationPath))
        {
            return;
        }

        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.StorageItems))
            {
                ViewModel.StatusText = "The clipboard does not contain files or folders.";
                return;
            }

            var storageItems = await content.GetStorageItemsAsync();
            var paths = storageItems
                .Select(item => item.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();

            if (paths.Length == 0)
            {
                ViewModel.StatusText = "The clipboard items do not expose filesystem paths.";
                return;
            }

            var mode = content.RequestedOperation == DataPackageOperation.Move
                ? FileTransferMode.Move
                : FileTransferMode.Copy;

            var result = await RunTransferAsync(paths, destinationPath, mode);
            if (mode == FileTransferMode.Move && result is { Succeeded: true })
            {
                Clipboard.Clear();
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Paste Failed", ex.Message);
        }
    }

    private async void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (RejectDestinationMutation("Duplicate"))
        {
            return;
        }

        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            ViewModel.StatusText = "Select one or more items to duplicate.";
            return;
        }

        await RunTransferAsync(
            selected.Select(entry => entry.FullPath).ToArray(),
            ActiveBrowser.CurrentPath,
            FileTransferMode.Copy,
            conflict => Task.FromResult(new ConflictResolution(
                ConflictAction.Rename,
                Path.GetFileName(FileOperationService.GetDuplicatePath(conflict.SourcePath)))),
            "Duplicate");
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (RejectRecycleBinMutation("Rename"))
        {
            return;
        }

        var selected = SelectedEntries();
        if (selected.Count > 1)
        {
            await BulkRenameAsync(selected);
            return;
        }

        if (selected.Count == 0)
        {
            ViewModel.StatusText = "Select one item to rename.";
            return;
        }

        var entry = selected[0];
        var newName = await PromptForNameAsync(
            "Rename",
            "Enter the new name:",
            entry.Name,
            value => ValidateRenameName(entry.FullPath, value));
        if (newName is null || string.Equals(newName, entry.Name, StringComparison.Ordinal))
        {
            return;
        }

        string? destinationPath = null;
        if (await RunFileOperationAsync(
            $"Renaming '{entry.Name}'...",
            async () => destinationPath = await _fileOperations.RenameAsync(entry.FullPath, newName)) &&
            destinationPath is not null)
        {
            PushRenameHistory(entry.FullPath, destinationPath);
        }
    }

    private async Task BulkRenameAsync(IReadOnlyList<FileSystemEntry> selected)
    {
        var mode = new ComboBox
        {
            Header = "Rename method",
            ItemsSource = new[]
            {
                "Search and Replace", "Add Prefix", "Add Suffix", "Numbering",
                "UPPERCASE", "lowercase", "Title Case"
            },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var firstValue = new TextBox { Header = "Find", PlaceholderText = "Text to find" };
        var secondValue = new TextBox { Header = "Replace with", PlaceholderText = "Replacement text" };
        var preserveExtension = new CheckBox { Content = "Preserve file extensions", IsChecked = true };
        var preview = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            MaxHeight = 250
        };
        var previewScroll = new ScrollViewer
        {
            Content = preview,
            MaxHeight = 250,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var content = new StackPanel { Spacing = 9, MinWidth = 520 };
        content.Children.Add(mode);
        content.Children.Add(firstValue);
        content.Children.Add(secondValue);
        content.Children.Add(preserveExtension);
        content.Children.Add(new TextBlock { Text = "Preview", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(previewScroll);

        var dialog = new ContentDialog
        {
            Title = $"Bulk Rename {selected.Count} Items",
            Content = content,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        IReadOnlyList<BulkRenamePlanItem>? currentPlan = null;
        void UpdatePreview()
        {
            var renameMode = (BulkRenameMode)Math.Clamp(mode.SelectedIndex, 0, 6);
            firstValue.Visibility = renameMode is BulkRenameMode.Uppercase or BulkRenameMode.Lowercase or BulkRenameMode.TitleCase
                ? Visibility.Collapsed
                : Visibility.Visible;
            secondValue.Visibility = renameMode is BulkRenameMode.SearchAndReplace or BulkRenameMode.Numbering
                ? Visibility.Visible
                : Visibility.Collapsed;
            firstValue.Header = renameMode switch
            {
                BulkRenameMode.Prefix => "Prefix",
                BulkRenameMode.Suffix => "Suffix",
                BulkRenameMode.Numbering => "Base name",
                _ => "Find"
            };
            secondValue.Header = renameMode == BulkRenameMode.Numbering ? "Start number" : "Replace with";

            try
            {
                currentPlan = _bulkRenameService.BuildPlan(
                    selected.Select(entry => entry.FullPath).ToArray(),
                    new BulkRenameOptions(
                        renameMode,
                        firstValue.Text,
                        secondValue.Text,
                        preserveExtension.IsChecked == true));
                preview.Text = string.Join(Environment.NewLine, currentPlan
                    .Take(30)
                    .Select(item => $"{item.OriginalName}  →  {item.NewName}"));
                if (currentPlan.Count > 30)
                {
                    preview.Text += $"{Environment.NewLine}…and {currentPlan.Count - 30} more";
                }

                dialog.IsPrimaryButtonEnabled = BulkRenameService.HasChanges(currentPlan);
            }
            catch (Exception ex)
            {
                currentPlan = null;
                preview.Text = $"Cannot rename: {ex.Message}";
                dialog.IsPrimaryButtonEnabled = false;
            }
        }

        mode.SelectionChanged += (_, _) => UpdatePreview();
        firstValue.TextChanged += (_, _) => UpdatePreview();
        secondValue.TextChanged += (_, _) => UpdatePreview();
        preserveExtension.Checked += (_, _) => UpdatePreview();
        preserveExtension.Unchecked += (_, _) => UpdatePreview();
        UpdatePreview();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || currentPlan is null)
        {
            return;
        }

        var plan = currentPlan.ToArray();
        if (await RunFileOperationAsync(
            $"Renaming {plan.Length} items...",
            () => _bulkRenameService.ApplyAsync(plan)))
        {
            var reverse = _bulkRenameService.Reverse(plan);
            _history.Push(new FileHistoryEntry(
                $"Bulk Rename ({plan.Length} items)",
                () => _bulkRenameService.ApplyAsync(reverse),
                () => _bulkRenameService.ApplyAsync(plan)));
        }
    }

    private async void MoveToTrash_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveBrowser.IsRecycleBinView)
        {
            ViewModel.StatusText = "Items are already in Trash.";
            return;
        }

        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            ViewModel.StatusText = "Select one or more items to move to Trash.";
            return;
        }

        if (_confirmMoveToTrash)
        {
            var confirmation = new ContentDialog
            {
                Title = "Move to Recycle Bin?",
                Content = $"Move {selected.Count} selected item{(selected.Count == 1 ? string.Empty : "s")} to the Windows Recycle Bin?",
                PrimaryButtonText = "Move",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        await RunFileOperationAsync(
            $"Moving {selected.Count} item{(selected.Count == 1 ? string.Empty : "s")} to Trash...",
            () => _fileOperations.MoveToTrashAsync(selected.Select(entry => entry.FullPath)));
    }

    private async void DeletePermanently_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            ViewModel.StatusText = "Select one or more items to delete.";
            return;
        }

        var itemNames = string.Join(", ", selected.Take(3).Select(entry => entry.Name));
        if (selected.Count > 3)
        {
            itemNames += $", and {selected.Count - 3} more";
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Permanently Delete?",
            Content = $"This bypasses Trash and cannot be undone.\n\n{itemNames}",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var pathsToDelete = selected
            .SelectMany(entry => entry.IsRecycleBinItem
                ? new[] { entry.FullPath, entry.RecycleMetadataPath! }
                : new[] { entry.FullPath })
            .ToArray();
        await RunFileOperationAsync(
            $"Permanently deleting {selected.Count} item{(selected.Count == 1 ? string.Empty : "s")}...",
            () => _fileOperations.DeletePermanentlyAsync(pathsToDelete));
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ActiveFileList.SelectAll();

    private async void SelectPattern_Click(object sender, RoutedEventArgs e)
    {
        var pattern = await PromptForNameAsync(
            "Select by Pattern",
            "Wildcard pattern:",
            "*.txt",
            value => string.IsNullOrWhiteSpace(value) ? "Enter a pattern." : null);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        var expression = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        var matcher = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        ActiveFileList.SelectedItems.Clear();
        foreach (var entry in ActiveBrowser.Entries.Where(entry => matcher.IsMatch(entry.Name)))
        {
            ActiveFileList.SelectedItems.Add(entry);
        }

        ActiveBrowser.SetSelectionStatus(SelectedEntries());
    }

    private void InvertSelection_Click(object sender, RoutedEventArgs e)
    {
        var selected = ActiveFileList.SelectedItems.Cast<FileSystemEntry>().ToHashSet();
        ActiveFileList.SelectedItems.Clear();
        foreach (var entry in ActiveBrowser.Entries.Where(entry => !selected.Contains(entry)))
        {
            ActiveFileList.SelectedItems.Add(entry);
        }

        ActiveBrowser.SetSelectionStatus(SelectedEntries());
    }

    private async void Undo_Click(object sender, RoutedEventArgs e) =>
        await RunHistoryOperationAsync("Undo", _history.UndoAsync);

    private async void Redo_Click(object sender, RoutedEventArgs e) =>
        await RunHistoryOperationAsync("Redo", _history.RedoAsync);

    private void CancelTransfer_Click(object sender, RoutedEventArgs e) => _transferQueue.CancelActive();

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntries().FirstOrDefault() is { } entry)
        {
            await OpenEntryAsync(entry);
        }
    }

    private async void OpenWith_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntries().FirstOrDefault(entry => !entry.IsDirectory);
        if (entry is null)
        {
            ViewModel.StatusText = "Select a file to choose an application.";
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(entry.FullPath);
            var options = new Windows.System.LauncherOptions { DisplayApplicationPicker = true };
            if (!await Windows.System.Launcher.LaunchFileAsync(file, options))
            {
                ViewModel.StatusText = $"Windows could not open an application picker for {entry.Name}.";
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Open With failed: {ex.Message}";
        }
    }

    private async void MakeLink_Click(object sender, RoutedEventArgs e)
    {
        if (RejectDestinationMutation("Make Link"))
        {
            return;
        }

        var entry = SelectedEntries().FirstOrDefault();
        if (entry is null)
        {
            ViewModel.StatusText = "Select an item to create a link.";
            return;
        }

        var defaultName = $"{Path.GetFileNameWithoutExtension(entry.Name)} - Link{(entry.IsDirectory ? string.Empty : Path.GetExtension(entry.Name))}";
        var linkName = await PromptForNameAsync(
            "Make Link",
            "Link name:",
            FileOperationService.GetAvailableLeafName(ActiveBrowser.CurrentPath, defaultName, entry.IsDirectory),
            value => ValidateNewItemName(ActiveBrowser.CurrentPath, value));
        if (string.IsNullOrWhiteSpace(linkName))
        {
            return;
        }

        var linkPath = Path.Combine(ActiveBrowser.CurrentPath, linkName);
        await RunFileOperationAsync($"Creating link to {entry.Name}...", () => Task.Run(() =>
        {
            if (entry.IsDirectory)
            {
                Directory.CreateSymbolicLink(linkPath, entry.FullPath);
            }
            else
            {
                File.CreateSymbolicLink(linkPath, entry.FullPath);
            }
        }));
    }

    private async void Properties_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedEntries();
        if (selected.Count > 1)
        {
            await ShowCombinedPropertiesAsync(selected);
            return;
        }

        var path = selected.FirstOrDefault()?.FullPath ?? ActiveBrowser.CurrentPath;
        if (!ShellIntegrationService.ShowProperties(path, App.WindowHandle))
        {
            await ShowMessageAsync("Properties", "Windows could not open the Properties dialog for this item.");
        }
    }

    private async Task ShowCombinedPropertiesAsync(IReadOnlyList<FileSystemEntry> selected)
    {
        var fileCount = selected.Count(entry => !entry.IsDirectory);
        var folderCount = selected.Count - fileCount;
        long totalFileBytes = 0;
        foreach (var entry in selected.Where(entry => !entry.IsDirectory))
        {
            try
            {
                totalFileBytes += new FileInfo(entry.FullPath).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A changing selection should not prevent the remaining common properties from being shown.
            }
        }

        var parents = selected
            .Select(entry => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(entry.FullPath)) ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var extensions = selected
            .Where(entry => !entry.IsDirectory)
            .Select(entry => Path.GetExtension(entry.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var type = folderCount == selected.Count
            ? "Folders"
            : fileCount == selected.Count && extensions.Length == 1
                ? string.IsNullOrWhiteSpace(extensions[0]) ? "Files" : $"{extensions[0]} files"
                : "Mixed files and folders";
        var details = new StackPanel { Spacing = 8, MinWidth = 420 };
        details.Children.Add(new TextBlock { Text = $"{selected.Count} selected items", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        details.Children.Add(new TextBlock { Text = $"Type: {type}" });
        details.Children.Add(new TextBlock { Text = $"Files: {fileCount}\nFolders: {folderCount}" });
        if (fileCount > 0)
        {
            details.Children.Add(new TextBlock { Text = $"Combined file size: {FormatByteCount(totalFileBytes)}" });
        }
        details.Children.Add(new TextBlock
        {
            Text = parents.Length == 1 ? $"Location: {parents[0]}" : "Location: Multiple folders",
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Properties",
            Content = details,
            CloseButtonText = "Close"
        };
        await dialog.ShowAsync();
    }

    private static string FormatByteCount(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:N0} {units[unit]}" : $"{value:N1} {units[unit]}";
    }

    private async void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var thumbnails = new CheckBox { Content = "Show Windows thumbnails", IsChecked = ViewModel.ShowThumbnails };
        var foldersFirst = new CheckBox { Content = "Sort folders before files", IsChecked = ViewModel.FoldersFirst };
        var recursiveSearch = new CheckBox { Content = "Include subfolders in search", IsChecked = ViewModel.IncludeSubfolders };
        var confirmTrash = new CheckBox { Content = "Confirm before moving items to the Recycle Bin", IsChecked = _confirmMoveToTrash };
        var restoreTabs = new CheckBox { Content = "Restore tabs on startup", IsChecked = _restoreTabs };
        var singleClick = new CheckBox { Content = "Single click opens files and folders", IsChecked = _singleClickActivation };
        var rememberFolders = new CheckBox { Content = "Remember view, zoom, and sorting for each folder", IsChecked = _rememberFolderViews };
        var previewImages = new CheckBox { Content = "Show image preview side pane", IsChecked = _showImagePreview };
        var defaultView = new ComboBox
        {
            Header = "Default/last active view",
            ItemsSource = new[] { "Icon View", "Detailed List", "Compact List" },
            SelectedIndex = ViewModel.ViewMode switch
            {
                BrowserViewMode.Icons => 0,
                BrowserViewMode.Compact => 2,
                _ => 1
            }
        };

        var content = new StackPanel { Spacing = 10, MinWidth = 430 };
        content.Children.Add(new TextBlock { Text = "Display", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(defaultView);
        content.Children.Add(thumbnails);
        content.Children.Add(foldersFirst);
        content.Children.Add(previewImages);
        content.Children.Add(new TextBlock { Text = "Behavior", Margin = new Thickness(0, 8, 0, 0), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(recursiveSearch);
        content.Children.Add(confirmTrash);
        content.Children.Add(restoreTabs);
        content.Children.Add(singleClick);
        content.Children.Add(rememberFolders);

        var dialog = new ContentDialog
        {
            Title = "WinThunar Preferences",
            Content = content,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.ShowThumbnails = thumbnails.IsChecked == true;
        ViewModel.FoldersFirst = foldersFirst.IsChecked == true;
        ViewModel.IncludeSubfolders = recursiveSearch.IsChecked == true;
        _confirmMoveToTrash = confirmTrash.IsChecked == true;
        _restoreTabs = restoreTabs.IsChecked == true;
        _singleClickActivation = singleClick.IsChecked == true;
        _rememberFolderViews = rememberFolders.IsChecked == true;
        _showImagePreview = previewImages.IsChecked == true;
        ApplyImagePreview();
        SplitViewModel.ShowThumbnails = ViewModel.ShowThumbnails;
        SplitViewModel.FoldersFirst = ViewModel.FoldersFirst;
        SplitViewModel.IncludeSubfolders = ViewModel.IncludeSubfolders;
        SetViewMode(defaultView.SelectedIndex switch
        {
            0 => BrowserViewMode.Icons,
            2 => BrowserViewMode.Compact,
            _ => BrowserViewMode.Details
        });
        UpdateViewMenu();
        SaveSession();
    }

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 8, MinWidth = 390 };
        content.Children.Add(new TextBlock { Text = "WinThunar", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "A Windows-native recreation of the Thunar file manager experience." });
        content.Children.Add(new TextBlock
        {
            Text = "Clean-room implementation for Windows 11 using C#, .NET, WinUI 3, and the Windows App SDK.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "WinThunar is not affiliated with or endorsed by the Xfce project.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 90, 90, 90))
        });
        var dialog = new ContentDialog
        {
            Title = "About WinThunar",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var actions = new[]
        {
            "New Tab", "Close Tab", "Search", "Show Hidden Files", "Rename", "Split View",
            "Terminal Panel", "Create Folder", "Properties", "Reload", "Home", "Focus Location"
        };
        var shortcutChoices = new[]
        {
            "None", "Ctrl+T", "Ctrl+W", "Ctrl+F", "Ctrl+H", "Ctrl+L", "Ctrl+D",
            "Ctrl+Shift+N", "Ctrl+Shift+T", "Alt+Return", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9"
        };
        var editors = new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);
        var rows = new StackPanel { Spacing = 6, MinWidth = 480 };
        rows.Children.Add(new TextBlock
        {
            Text = "Assign an alternate shortcut. Standard Thunar shortcuts remain available.",
            TextWrapping = TextWrapping.Wrap
        });
        foreach (var action in actions)
        {
            var editor = new ComboBox
            {
                ItemsSource = shortcutChoices,
                SelectedItem = _customShortcuts.TryGetValue(action, out var current) ? current : "None",
                Width = 170
            };
            editors[action] = editor;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = action, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);
            rows.Children.Add(row);
        }

        var scroll = new ScrollViewer { Content = rows, MaxHeight = 430, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dialog = new ContentDialog
        {
            Title = "Keyboard Shortcuts",
            Content = scroll,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var assignments = editors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.SelectedItem as string ?? "None",
            StringComparer.OrdinalIgnoreCase);
        var duplicate = assignments
            .Where(pair => pair.Value != "None")
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            await ShowMessageAsync("Keyboard Shortcuts", $"'{duplicate.Key}' was assigned to more than one action. No changes were saved.");
            return;
        }

        _customShortcuts = assignments
            .Where(pair => pair.Value != "None")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        SaveSession();
    }

    private async void CustomActions_Click(object sender, RoutedEventArgs e)
    {
        _pluginService.Reload();
        var plugin = _pluginService.Plugins.FirstOrDefault(item => item.Id == "user.custom-actions")
            ?? new PluginManifest
            {
                Id = "user.custom-actions",
                Name = "Custom Actions",
                Description = "User-created WinThunar context-menu actions."
            };

        while (true)
        {
            var list = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                MinHeight = 130,
                MaxHeight = 330
            };
            foreach (var action in plugin.Actions)
            {
                list.Items.Add(new ListViewItem
                {
                    Content = $"{action.Name}\n{action.Command} {string.Join(' ', action.Arguments)}".TrimEnd(),
                    Tag = action
                });
            }

            var removeButton = new Button
            {
                Content = "Remove Selected",
                IsEnabled = plugin.Actions.Count > 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            list.SelectionChanged += (_, _) => removeButton.IsEnabled = list.SelectedItem is ListViewItem;
            var content = new StackPanel { Spacing = 8, MinWidth = 520 };
            if (plugin.Actions.Count == 0)
            {
                content.Children.Add(new TextBlock { Text = "No custom actions have been created." });
            }
            content.Children.Add(list);
            content.Children.Add(removeButton);

            var removeRequested = false;
            var dialog = new ContentDialog
            {
                Title = "Custom Actions",
                Content = content,
                PrimaryButtonText = "Add",
                SecondaryButtonText = "Edit Selected",
                CloseButtonText = "Done",
                XamlRoot = XamlRoot
            };
            removeButton.Click += (_, _) =>
            {
                removeRequested = true;
                dialog.Hide();
            };

            var result = await dialog.ShowAsync();
            var selected = (list.SelectedItem as ListViewItem)?.Tag as PluginActionManifest;
            if (removeRequested)
            {
                if (selected is null)
                {
                    continue;
                }
                var confirmation = new ContentDialog
                {
                    Title = $"Remove {selected.Name}?",
                    Content = "This removes the custom action from WinThunar's context menu.",
                    PrimaryButtonText = "Remove",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                if (await confirmation.ShowAsync() == ContentDialogResult.Primary)
                {
                    plugin.Actions.Remove(selected);
                    _pluginService.SaveUserPlugin(plugin);
                    ViewModel.StatusText = $"Custom action '{selected.Name}' removed.";
                }
                continue;
            }
            if (result == ContentDialogResult.None)
            {
                return;
            }
            if (result == ContentDialogResult.Secondary && selected is null)
            {
                await ShowMessageAsync("Custom Actions", "Select an action to edit.");
                continue;
            }

            var edited = await ShowCustomActionEditorAsync(result == ContentDialogResult.Secondary ? selected : null);
            if (edited is null)
            {
                continue;
            }
            if (selected is null || result == ContentDialogResult.Primary)
            {
                plugin.Actions.Add(edited);
            }
            else
            {
                plugin.Actions[plugin.Actions.IndexOf(selected)] = edited;
            }
            try
            {
                _pluginService.SaveUserPlugin(plugin);
                ViewModel.StatusText = $"Custom action '{edited.Name}' saved.";
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Custom Actions", ex.Message);
            }
        }
    }

    private async Task<PluginActionManifest?> ShowCustomActionEditorAsync(PluginActionManifest? existing)
    {
        var name = new TextBox { Header = "Action name", PlaceholderText = "Open in my editor" };
        var description = new TextBox { Header = "Description", PlaceholderText = "Shown as context-menu help" };
        var command = new TextBox { Header = "Program", PlaceholderText = "notepad.exe" };
        var arguments = new TextBox
        {
            Header = "Arguments (one argument per line)",
            PlaceholderText = "{selected}",
            AcceptsReturn = true,
            Height = 90
        };
        var patterns = new TextBox { Header = "File patterns (semicolon separated)", Text = "*" };
        var targets = new ComboBox
        {
            Header = "Appears for",
            ItemsSource = new[] { "Files", "Folders", "Files and folders" },
            SelectedIndex = 2
        };
        var confirmation = new CheckBox { Content = "Ask for confirmation before running" };
        if (existing is not null)
        {
            name.Text = existing.Name;
            description.Text = existing.Description;
            command.Text = existing.Command;
            arguments.Text = string.Join(Environment.NewLine, existing.Arguments);
            patterns.Text = string.Join(';', existing.FilePatterns);
            targets.SelectedIndex = existing.Targets switch
            {
                PluginTargetKind.Files => 0,
                PluginTargetKind.Folders => 1,
                _ => 2
            };
            confirmation.IsChecked = existing.RequiresConfirmation;
        }
        var content = new StackPanel { Spacing = 8, MinWidth = 500 };
        content.Children.Add(new TextBlock
        {
            Text = "Tokens: {file}, {name}, {directory}, or {selected} as its own line for every selected path.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(name);
        content.Children.Add(description);
        content.Children.Add(command);
        content.Children.Add(arguments);
        content.Children.Add(patterns);
        content.Children.Add(targets);
        content.Children.Add(confirmation);

        var dialog = new ContentDialog
        {
            Title = existing is null ? "Add Custom Action" : "Edit Custom Action",
            Content = content,
            PrimaryButtonText = existing is null ? "Add" : "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(command.Text))
        {
            await ShowMessageAsync("Custom Actions", "An action name and program are required.");
            return null;
        }

        return new PluginActionManifest
        {
            Id = existing?.Id ?? $"action-{Guid.NewGuid():N}",
            Name = name.Text.Trim(),
            Description = description.Text.Trim(),
            Command = command.Text.Trim(),
            Arguments = arguments.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FilePatterns = patterns.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).DefaultIfEmpty("*").ToList(),
            Targets = targets.SelectedIndex switch
            {
                0 => PluginTargetKind.Files,
                1 => PluginTargetKind.Folders,
                _ => PluginTargetKind.Mixed
            },
            RequiresConfirmation = confirmation.IsChecked == true
        };
    }

    private async void Plugins_Click(object sender, RoutedEventArgs e)
    {
        _pluginService.Reload();
        var editors = new Dictionary<PluginManifest, CheckBox>();
        var content = new StackPanel { Spacing = 8, MinWidth = 520 };
        if (_pluginService.Plugins.Count == 0)
        {
            content.Children.Add(new TextBlock { Text = "No plugins are installed." });
        }
        foreach (var plugin in _pluginService.Plugins)
        {
            var enabled = new CheckBox
            {
                Content = $"{plugin.Name} {plugin.Version}",
                IsChecked = plugin.Enabled
            };
            editors[plugin] = enabled;
            content.Children.Add(enabled);
            content.Children.Add(new TextBlock
            {
                Text = $"{plugin.Description}\n{plugin.Actions.Count} action{(plugin.Actions.Count == 1 ? string.Empty : "s")}",
                Margin = new Thickness(28, -5, 0, 4),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 85, 85, 85)),
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (_pluginService.Diagnostics.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Diagnostics:\n" + string.Join(Environment.NewLine, _pluginService.Diagnostics),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 45, 45)),
                TextWrapping = TextWrapping.Wrap
            });
        }
        content.Children.Add(new TextBlock
        {
            Text = $"User plugin folder:\n{_pluginService.UserDirectory}",
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = "WinThunar Plugins",
            Content = new ScrollViewer { Content = content, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "Apply",
            SecondaryButtonText = "Open Plugin Folder",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            Directory.CreateDirectory(_pluginService.UserDirectory);
            ShellIntegrationService.OpenShellLocation(_pluginService.UserDirectory);
            return;
        }
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            foreach (var pair in editors.Where(pair => pair.Key.Enabled != (pair.Value.IsChecked == true)))
            {
                pair.Key.Enabled = pair.Value.IsChecked == true;
                _pluginService.SaveUserPlugin(pair.Key);
            }
            _pluginService.Reload();
            ViewModel.StatusText = "Plugin settings updated.";
            ApplyToolbarOrder();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Plugins", ex.Message);
        }
    }

    private void FileContextMenu_Opened(object sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        UpdateCommandState();

        foreach (var generated in _generatedPluginMenuItems)
        {
            menu.Items.Remove(generated);
        }
        _generatedPluginMenuItems.Clear();

        if (ActiveBrowser.IsRecycleBinView)
        {
            return;
        }

        var actions = _pluginService.GetApplicableActions(SelectedEntries()
            .Select(entry => new PluginSelectionItem(entry.Name, entry.FullPath, entry.IsDirectory))
            .ToArray());
        if (actions.Count == 0)
        {
            return;
        }

        var separator = new MenuFlyoutSeparator();
        menu.Items.Add(separator);
        _generatedPluginMenuItems.Add(separator);
        foreach (var available in actions)
        {
            var item = new MenuFlyoutItem
            {
                Text = available.Action.Name,
                Tag = available
            };
            ToolTipService.SetToolTip(item, available.Action.Description);
            item.Click += PluginAction_Click;
            menu.Items.Add(item);
            _generatedPluginMenuItems.Add(item);
        }
    }

    private void FolderContextMenu_Opened(object sender, object e)
    {
        RefreshCreateDocumentMenus();
        UpdateCommandState();
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        foreach (var generated in _generatedFolderPluginMenuItems)
        {
            menu.Items.Remove(generated);
        }
        _generatedFolderPluginMenuItems.Clear();

        if (ActiveBrowser.IsRecycleBinView)
        {
            return;
        }

        var actions = GetCurrentFolderPluginActions();
        if (actions.Count == 0)
        {
            return;
        }

        var separator = new MenuFlyoutSeparator();
        menu.Items.Add(separator);
        _generatedFolderPluginMenuItems.Add(separator);
        foreach (var available in actions)
        {
            var item = new MenuFlyoutItem { Text = available.Action.Name, Tag = available };
            ToolTipService.SetToolTip(item, available.Action.Description);
            item.Click += PluginAction_Click;
            menu.Items.Add(item);
            _generatedFolderPluginMenuItems.Add(item);
        }
    }

    private void FileMenu_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        RefreshCreateDocumentMenus();
        UpdateCommandState();
    }

    private void FileMenu_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        RefreshCreateDocumentMenus();
        UpdateCommandState();
    }

    private void FileMenu_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
    {
        RefreshCreateDocumentMenus();
        UpdateCommandState();
    }

    private void EditMenu_PointerEntered(object sender, PointerRoutedEventArgs e) => UpdateCommandState();

    private void EditMenu_GettingFocus(UIElement sender, GettingFocusEventArgs args) => UpdateCommandState();

    private void EditMenu_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args) => UpdateCommandState();

    private async void PluginAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AvailablePluginAction available })
        {
            return;
        }

        var selected = SelectedEntries();
        if ((sender is Button || selected.Count == 0) && !ActiveBrowser.IsRecycleBinView)
        {
            selected = [CreateCurrentFolderEntry()];
        }
        if (available.Action.RequiresConfirmation)
        {
            var dialog = new ContentDialog
            {
                Title = $"Run {available.Action.Name}?",
                Content = $"Plugin: {available.Plugin.Name}\nSelected items: {selected.Count}",
                PrimaryButtonText = "Run",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            if (available.Action.Command.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteBuiltInPluginActionAsync(available.Action, selected);
            }
            else
            {
                var invocation = _pluginService.BuildInvocation(
                    available.Action,
                    selected.Select(entry => entry.FullPath).ToArray(),
                    ActiveBrowser.CurrentPath);
                _pluginService.Execute(invocation);
                ViewModel.StatusText = $"Started '{available.Action.Name}'.";
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"{available.Action.Name} Failed", ex.Message);
        }
    }

    private IReadOnlyList<AvailablePluginAction> GetCurrentFolderPluginActions()
    {
        if (ActiveBrowser.IsRecycleBinView || !Directory.Exists(ActiveBrowser.CurrentPath))
        {
            return [];
        }

        return _pluginService.GetApplicableActions([
            new PluginSelectionItem(
                BrowserTabState.GetTitle(ActiveBrowser.CurrentPath),
                ActiveBrowser.CurrentPath,
                true)
        ]);
    }

    private FileSystemEntry CreateCurrentFolderEntry() => new(
        BrowserTabState.GetTitle(ActiveBrowser.CurrentPath),
        ActiveBrowser.CurrentPath,
        true,
        string.Empty,
        "Folder",
        string.Empty,
        "\uE8B7");

    private static string GetPluginActionKey(AvailablePluginAction available) =>
        $"{available.Plugin.Id}/{available.Action.Id}";

    private async Task ExecuteBuiltInPluginActionAsync(
        PluginActionManifest action,
        IReadOnlyList<FileSystemEntry> selected)
    {
        switch (action.Command.ToLowerInvariant())
        {
            case "builtin:archive-create":
                await CreateArchiveAsync(selected);
                break;
            case "builtin:archive-extract":
                await ExtractArchivesAsync(selected);
                break;
            case "builtin:media-tags":
                await EditMediaTagsAsync(selected[0]);
                break;
            case "builtin:git-status":
                await RunGitPluginAsync("status", selected);
                break;
            case "builtin:git-diff":
                await RunGitPluginAsync("diff", selected);
                break;
            case "builtin:git-log":
                await RunGitPluginAsync("log", selected);
                break;
            case "builtin:share-manage":
                ShellIntegrationService.ShowProperties(selected[0].FullPath, App.WindowHandle);
                break;
            default:
                throw new NotSupportedException($"Unknown built-in plugin action: {action.Command}");
        }
    }

    private async Task CreateArchiveAsync(IReadOnlyList<FileSystemEntry> selected)
    {
        var suggested = selected.Count == 1
            ? $"{Path.GetFileNameWithoutExtension(selected[0].Name)}.zip"
            : "Archive.zip";
        var name = await PromptForNameAsync("Create Archive", "Archive name:", suggested);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            name += ".zip";
        }

        var destination = Path.Combine(ActiveBrowser.CurrentPath, name);
        if (File.Exists(destination))
        {
            throw new IOException($"'{name}' already exists.");
        }

        if (await RunFileOperationAsync(
                $"Creating {name}...",
                () => _archiveService.CreateAsync(selected.Select(entry => entry.FullPath).ToArray(), destination)))
        {
            await PushCreateHistoryAsync(destination, false);
        }
    }

    private async Task ExtractArchivesAsync(IReadOnlyList<FileSystemEntry> selected)
    {
        await RunFileOperationAsync($"Extracting {selected.Count} archive{(selected.Count == 1 ? string.Empty : "s")}...", async () =>
        {
            foreach (var entry in selected)
            {
                var baseDestination = Path.Combine(ActiveBrowser.CurrentPath, Path.GetFileNameWithoutExtension(entry.Name));
                var destination = baseDestination;
                for (var copy = 1; Directory.Exists(destination) || File.Exists(destination); copy++)
                {
                    destination = $"{baseDestination} ({copy})";
                }
                await _archiveService.ExtractAsync(entry.FullPath, destination);
            }
        });
    }

    private async Task EditMediaTagsAsync(FileSystemEntry entry)
    {
        var file = await StorageFile.GetFileFromPathAsync(entry.FullPath);
        var music = await file.Properties.GetMusicPropertiesAsync();
        var title = new TextBox { Header = "Title", Text = music.Title };
        var artist = new TextBox { Header = "Artist", Text = music.Artist };
        var album = new TextBox { Header = "Album", Text = music.Album };
        var track = new NumberBox { Header = "Track number", Minimum = 0, Maximum = 999, Value = music.TrackNumber };
        var year = new NumberBox { Header = "Year", Minimum = 0, Maximum = 9999, Value = music.Year };
        var content = new StackPanel { Spacing = 8, MinWidth = 430 };
        content.Children.Add(title);
        content.Children.Add(artist);
        content.Children.Add(album);
        content.Children.Add(track);
        content.Children.Add(year);
        var dialog = new ContentDialog
        {
            Title = $"Edit Media Tags — {entry.Name}",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var properties = new Dictionary<string, object>
        {
            ["System.Title"] = title.Text,
            ["System.Music.Artist"] = new[] { artist.Text },
            ["System.Music.AlbumTitle"] = album.Text,
            ["System.Music.TrackNumber"] = (uint)Math.Max(0, double.IsNaN(track.Value) ? 0 : track.Value),
            ["System.Media.Year"] = (uint)Math.Max(0, double.IsNaN(year.Value) ? 0 : year.Value)
        };
        await file.Properties.SavePropertiesAsync(properties);
        ViewModel.StatusText = $"Media tags saved for {entry.Name}.";
    }

    private async Task RunGitPluginAsync(string operation, IReadOnlyList<FileSystemEntry> selected)
    {
        var repository = FindGitRepository(ActiveBrowser.CurrentPath)
            ?? throw new InvalidOperationException("The current folder is not inside a Git repository.");
        var startInfo = new ProcessStartInfo
        {
            FileName = "git.exe",
            WorkingDirectory = repository,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        switch (operation)
        {
            case "status":
                startInfo.ArgumentList.Add("status");
                startInfo.ArgumentList.Add("--short");
                break;
            case "diff":
                startInfo.ArgumentList.Add("diff");
                startInfo.ArgumentList.Add("--");
                foreach (var entry in selected)
                {
                    startInfo.ArgumentList.Add(Path.GetRelativePath(repository, entry.FullPath));
                }
                break;
            case "log":
                startInfo.ArgumentList.Add("log");
                startInfo.ArgumentList.Add("--oneline");
                startInfo.ArgumentList.Add("-20");
                startInfo.ArgumentList.Add("--");
                foreach (var entry in selected)
                {
                    startInfo.ArgumentList.Add(Path.GetRelativePath(repository, entry.FullPath));
                }
                break;
        }

        _showTerminalPanel = true;
        ApplyTerminalPanel();
        TerminalOutput.Text += $"git {operation} ({repository}){Environment.NewLine}";
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Git could not be started. Install Git for Windows and ensure git.exe is on PATH.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        TerminalOutput.Text += await outputTask;
        TerminalOutput.Text += await errorTask;
        TerminalOutput.Text += $"[exit {process.ExitCode}]{Environment.NewLine}";
    }

    private static string? FindGitRepository(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
        }
        return null;
    }

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || _customShortcuts.Count == 0)
        {
            return;
        }

        var pressed = GetShortcutText(e.Key);
        var action = _customShortcuts.FirstOrDefault(pair =>
            string.Equals(pair.Value, pressed, StringComparison.OrdinalIgnoreCase)).Key;
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        e.Handled = true;
        var args = new RoutedEventArgs();
        switch (action)
        {
            case "New Tab": NewTab_Click(this, args); break;
            case "Close Tab": CloseTab_Click(this, args); break;
            case "Search": Search_Click(this, args); break;
            case "Show Hidden Files": ShowHiddenFiles_Click(this, args); break;
            case "Rename": Rename_Click(this, args); break;
            case "Split View": SplitView_Click(this, args); break;
            case "Terminal Panel":
                _showTerminalPanel = !_showTerminalPanel;
                ApplyTerminalPanel();
                SaveSession();
                break;
            case "Create Folder": CreateFolder_Click(this, args); break;
            case "Properties": Properties_Click(this, args); break;
            case "Reload": Refresh_Click(this, args); break;
            case "Home": Home_Click(this, args); break;
            case "Focus Location":
                PathBarEdit_Click(this, args);
                break;
        }
    }

    private static string GetShortcutText(Windows.System.VirtualKey key)
    {
        var modifiers = new List<string>();
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
        if (control.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers.Add("Ctrl");
        if (shift.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers.Add("Shift");
        if (alt.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers.Add("Alt");
        modifiers.Add(key == Windows.System.VirtualKey.Enter ? "Return" : key.ToString());
        return string.Join("+", modifiers);
    }

    private void RememberCurrentFolderView()
    {
        if (!_rememberFolderViews || string.IsNullOrWhiteSpace(ViewModel.CurrentPath) ||
            !Directory.Exists(ViewModel.CurrentPath))
        {
            return;
        }

        _folderViewSettings[Path.GetFullPath(ViewModel.CurrentPath)] = new FolderViewState
        {
            ViewMode = ViewModel.ViewMode,
            SortColumn = ViewModel.SortColumn,
            SortDescending = ViewModel.SortDescending,
            ZoomLevel = ViewModel.ZoomLevel
        };
    }

    private void ApplyFolderViewSettings(MainPageViewModel browser, string path)
    {
        if (!_rememberFolderViews)
        {
            return;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch
        {
            return;
        }

        if (_folderViewSettings.TryGetValue(normalized, out var settings))
        {
            browser.ViewMode = settings.ViewMode;
            browser.SortColumn = settings.SortColumn;
            browser.SortDescending = settings.SortDescending;
            browser.ZoomLevel = Math.Clamp(settings.ZoomLevel, 0, 4);
            if (ReferenceEquals(browser, ViewModel))
            {
                UpdateViewMenu();
            }
        }
    }

    private async void LocationBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await NavigateCurrentTabAsync(LocationBox.Text);
        }
    }

    private async void Location_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NavigationLocation location)
        {
            if (location.Kind == NavigationLocationKind.NetworkBrowser)
            {
                ShellIntegrationService.OpenShellLocation(location.Path);
            }
            else
            {
                await NavigateCurrentTabAsync(location.Path);
            }
        }
    }

    private void OpenNetwork_Click(object sender, RoutedEventArgs e) =>
        ShellIntegrationService.OpenShellLocation("shell:NetworkPlacesFolder");

    private async void ConnectToServer_Click(object sender, RoutedEventArgs e)
    {
        var path = await PromptForNameAsync(
            "Connect to Server",
            "UNC path (for example, \\\\server\\share):",
            @"\\server\share",
            FileOperationService.ValidateUncPath);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await NavigateActivePaneAsync(path);
        }
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e) =>
        ShellIntegrationService.OpenTerminal(ActiveBrowser.CurrentPath);

    private async void EjectDrive_Click(object sender, RoutedEventArgs e)
    {
        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType is DriveType.Removable or DriveType.CDRom)
            .Select(drive => drive.RootDirectory.FullName)
            .ToArray();
        if (drives.Length == 0)
        {
            await ShowMessageAsync("Eject Drive", "No removable drives are currently available.");
            return;
        }

        var choices = new ComboBox { Header = "Drive", ItemsSource = drives, SelectedIndex = 0, MinWidth = 280 };
        var dialog = new ContentDialog
        {
            Title = "Eject Removable Drive",
            Content = choices,
            PrimaryButtonText = "Eject",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.StatusText = ShellIntegrationService.EjectDrive((string)choices.SelectedItem)
                ? "Windows received the eject request."
                : "Windows could not eject the selected drive.";
        }
    }

    private async void EmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Empty Recycle Bin?",
            Content = "All items in the Windows Recycle Bin will be permanently deleted.",
            PrimaryButtonText = "Empty",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var result = ShellIntegrationService.EmptyRecycleBin(App.WindowHandle);
            ViewModel.StatusText = result == 0 ? "Recycle Bin emptied." : $"Windows could not empty the Recycle Bin (error 0x{result:X8}).";
        }
    }

    private async void RestoreFromTrash_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedEntries().Where(entry => entry.IsRecycleBinItem).ToArray();
        if (selected.Length == 0)
        {
            ActiveBrowser.StatusText = "Select one or more Trash items to restore.";
            return;
        }

        var restored = 0;
        var skipped = 0;
        var errors = new List<string>();
        foreach (var entry in selected)
        {
            var result = await _fileOperations.RestoreRecycleItemAsync(
                entry.FullPath,
                entry.RecycleOriginalPath!,
                entry.RecycleMetadataPath!,
                ResolveConflictAsync);
            restored += result.CompletedItems;
            skipped += result.SkippedItems;
            errors.AddRange(result.Errors);
            if (result.Cancelled)
            {
                break;
            }
        }

        await ActiveBrowser.RefreshAsync();
        ActiveBrowser.StatusText = errors.Count > 0
            ? $"Restored {restored}, skipped {skipped}. {errors[0]}"
            : $"Restored {restored} item{(restored == 1 ? string.Empty : "s")}; skipped {skipped}.";
    }

    private async void FileList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        SetActivePaneForList(sender);
        var entry = EntryFromInteractionSource(e.OriginalSource)
            ?? (sender as ListViewBase)?.SelectedItem as FileSystemEntry;
        if (entry is not null)
        {
            e.Handled = true;
            await OpenEntryAsync(entry);
        }
    }

    private static FileSystemEntry? EntryFromInteractionSource(object? source)
    {
        var current = source as DependencyObject;
        while (current is not null && current is not ListViewBase)
        {
            if (current is FrameworkElement { DataContext: FileSystemEntry entry })
            {
                return entry;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private async void FileList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_singleClickActivation && e.ClickedItem is FileSystemEntry entry)
        {
            SetActivePaneForList(sender);
            await OpenEntryAsync(entry);
        }
    }

    private void FileList_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListViewBase list)
        {
            return;
        }

        SetActivePaneForList(sender);
        var hasPosition = e.TryGetPosition(list, out var position);
        var entry = hasPosition
            ? VisualTreeHelper.FindElementsInHostCoordinates(position, list)
                .OfType<FrameworkElement>()
                .Select(element => element.DataContext)
                .OfType<FileSystemEntry>()
                .FirstOrDefault()
            : list.SelectedItems.Cast<FileSystemEntry>().FirstOrDefault();

        MenuFlyout? menu;
        if (entry is null)
        {
            list.SelectedItems.Clear();
            menu = Resources["FolderContextMenu"] as MenuFlyout;
        }
        else
        {
            if (!list.SelectedItems.Contains(entry))
            {
                list.SelectedItems.Clear();
                list.SelectedItems.Add(entry);
            }
            menu = Resources["FileContextMenu"] as MenuFlyout;
        }

        e.Handled = true;
        if (menu is not null)
        {
            if (hasPosition)
            {
                menu.ShowAt(list, position);
            }
            else
            {
                menu.ShowAt(list);
            }
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListViewBase list && list.SelectedItems.Count > 0)
        {
            SetActivePaneForList(sender);
        }
        ActiveBrowser.SetSelectionStatus(SelectedEntries());
        UpdateCommandState();
        if (_activePane == BrowserPane.Secondary)
        {
            ViewModel.StatusText = SplitViewModel.StatusText;
        }
        if (_showImagePreview)
        {
            _ = UpdateImagePreviewAsync(SelectedEntries().FirstOrDefault());
        }
    }

    private async Task UpdateImagePreviewAsync(FileSystemEntry? entry)
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();
        var cancellationToken = _previewCancellation.Token;

        PreviewImage.Source = null;
        PreviewTitle.Text = entry?.Name ?? "No item selected";
        PreviewMetadata.Text = entry is null
            ? string.Empty
            : $"{entry.Type}{(string.IsNullOrWhiteSpace(entry.Size) ? string.Empty : $" • {entry.Size}")}\nModified {entry.Modified}";
        PreviewPath.Text = entry?.FullPath ?? string.Empty;
        if (entry is null || entry.IsDirectory)
        {
            return;
        }

        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".avif", ".bmp", ".gif", ".heic", ".heif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
        };
        if (!imageExtensions.Contains(Path.GetExtension(entry.FullPath)))
        {
            return;
        }

        try
        {
            var image = await _shellImageService.GetPreviewAsync(
                entry.FullPath,
                512,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PreviewImage.Source = image;
        }
        catch (OperationCanceledException)
        {
            // A newer selection superseded this preview.
        }
    }

    private void FileList_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        SetActivePaneForList(sender);
        if (ActiveBrowser.IsRecycleBinView)
        {
            args.Cancel = true;
            ActiveBrowser.StatusText = "Drag is not available while viewing Trash.";
            return;
        }

        var dragEntries = args.Items.OfType<FileSystemEntry>().ToArray();
        if (dragEntries.Length == 0)
        {
            args.Cancel = true;
            return;
        }

        ActiveBrowser.StatusText = $"Dragging {dragEntries.Length} item{(dragEntries.Length == 1 ? string.Empty : "s")}...";
        args.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;
        args.Data.SetDataProvider(
            StandardDataFormats.StorageItems,
            request => ProvideDragStorageItems(request, dragEntries));
    }

    private async void ProvideDragStorageItems(
        DataProviderRequest request,
        IReadOnlyList<FileSystemEntry> dragEntries)
    {
        var deferral = request.GetDeferral();
        try
        {
            var storageItems = await GetStorageItemsAsync(dragEntries);
            if (storageItems.Count == 0)
            {
                return;
            }

            request.SetData(storageItems);
        }
        catch (Exception ex)
        {
            App.DispatcherQueue.TryEnqueue(() =>
                ViewModel.StatusText = $"Could not start drag: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void FileList_DragEnter(object sender, DragEventArgs e)
    {
        if (IsFilePaneList(sender))
        {
            SetActivePaneForList(sender);
        }
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var destination = GetDropDestination(e, sender);
            if (destination is null)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }
            var items = await e.DataView.GetStorageItemsAsync();
            var mode = DetermineDropMode(items.Select(item => item.Path), destination, e.Modifiers);
            e.AcceptedOperation = mode == FileTransferMode.Move
                ? DataPackageOperation.Move
                : DataPackageOperation.Copy;
            var destinationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(destination));
            if (string.IsNullOrWhiteSpace(destinationName))
            {
                destinationName = destination;
            }
            e.DragUIOverride.Caption = mode == FileTransferMode.Move
                ? $"Move to {destinationName}"
                : $"Copy to {destinationName}";
            e.Handled = true;
        }
        catch
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void FileList_Drop(object sender, DragEventArgs e)
    {
        if (IsFilePaneList(sender))
        {
            SetActivePaneForList(sender);
        }
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        Task<FileOperationResult?>? transfer = null;
        try
        {
            var destination = GetDropDestination(e, sender);
            if (destination is null)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items
                .Select(item => item.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            var mode = DetermineDropMode(paths, destination, e.Modifiers);
            e.AcceptedOperation = mode == FileTransferMode.Move
                ? DataPackageOperation.Move
                : DataPackageOperation.Copy;
            e.Handled = true;
            transfer = RunTransferAsync(paths, destination, mode);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Drop Failed", ex.Message);
        }
        finally
        {
            deferral.Complete();
        }

        if (transfer is not null)
        {
            await transfer;
        }
    }

    private async Task OpenEntryAsync(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (ActiveBrowser.IsRecycleBinView)
            {
                ActiveBrowser.StatusText = "Restore the folder before browsing it.";
                return;
            }

            await NavigateActivePaneAsync(entry.FullPath);
            return;
        }

        if (ShellIntegrationService.IsShortcutFile(entry.FullPath))
        {
            if (ShellIntegrationService.TryResolveShortcutTarget(entry.FullPath, out var targetPath) &&
                Directory.Exists(targetPath))
            {
                await NavigateActivePaneAsync(targetPath);
            }
            else if (!ShellIntegrationService.OpenPath(entry.FullPath))
            {
                ViewModel.StatusText = $"Windows could not open shortcut '{entry.Name}'.";
            }

            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(entry.FullPath);
            if (!await Windows.System.Launcher.LaunchFileAsync(file))
            {
                ViewModel.StatusText = $"Windows could not find an app to open '{entry.Name}'.";
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Could not open file: {ex.Message}";
        }
    }

    private async Task CopySelectionToClipboardAsync(bool move)
    {
        if (ActiveBrowser.IsRecycleBinView)
        {
            ActiveBrowser.StatusText = "Restore the item before copying or moving it.";
            return;
        }

        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            ViewModel.StatusText = "Select one or more items first.";
            return;
        }

        try
        {
            var storageItems = await GetStorageItemsAsync(selected);

            var package = new DataPackage
            {
                RequestedOperation = move ? DataPackageOperation.Move : DataPackageOperation.Copy
            };
            package.SetStorageItems(storageItems);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            ViewModel.StatusText = $"{selected.Count} item{(selected.Count == 1 ? string.Empty : "s")} ready to {(move ? "move" : "copy")}.";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(move ? "Cut Failed" : "Copy Failed", ex.Message);
        }
    }

    private static async Task<IReadOnlyList<IStorageItem>> GetStorageItemsAsync(
        IEnumerable<FileSystemEntry> entries)
    {
        var storageItems = new List<IStorageItem>();
        foreach (var entry in entries)
        {
            storageItems.Add(entry.IsDirectory
                ? await StorageFolder.GetFolderFromPathAsync(entry.FullPath)
                : await StorageFile.GetFileFromPathAsync(entry.FullPath));
        }

        return storageItems;
    }

    private static FileTransferMode DetermineDropMode(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        DragDropModifiers modifiers)
    {
        if ((modifiers & DragDropModifiers.Control) == DragDropModifiers.Control)
        {
            return FileTransferMode.Copy;
        }

        if ((modifiers & DragDropModifiers.Shift) == DragDropModifiers.Shift)
        {
            return FileTransferMode.Move;
        }

        var destinationRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));
        var sameVolume = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetPathRoot(Path.GetFullPath(path)))
            .All(root => string.Equals(root, destinationRoot, StringComparison.OrdinalIgnoreCase));
        return sameVolume ? FileTransferMode.Move : FileTransferMode.Copy;
    }

    private string? GetDropDestination(DragEventArgs args, object sender)
    {
        if (sender is UIElement dropSurface)
        {
            var pointerPosition = args.GetPosition(null!);
            foreach (var hit in VisualTreeHelper.FindElementsInHostCoordinates(
                         pointerPosition,
                         dropSurface,
                         true))
            {
                var current = hit as DependencyObject;
                while (current is not null && !ReferenceEquals(current, dropSurface))
                {
                    if (dropSurface is TreeView tree && current is TreeViewItem)
                    {
                        var node = tree.NodeFromContainer(current);
                        if (node is not null && _treeLocations.TryGetValue(node, out var treeLocation))
                        {
                            return treeLocation.Path;
                        }
                    }

                    var destination = current switch
                    {
                        FrameworkElement { DataContext: FileSystemEntry { IsDirectory: true } item } => item.FullPath,
                        ContentControl { Content: FileSystemEntry { IsDirectory: true } item } => item.FullPath,
                        FrameworkElement { DataContext: NavigationLocation { Kind: NavigationLocationKind.FileSystem } location } => location.Path,
                        ContentControl { Content: NavigationLocation { Kind: NavigationLocationKind.FileSystem } location } => location.Path,
                        FrameworkElement { DataContext: TreeViewNode node } when _treeLocations.TryGetValue(node, out var location) => location.Path,
                        ContentControl { Content: TreeViewNode node } when _treeLocations.TryGetValue(node, out var location) => location.Path,
                        _ => null
                    };
                    if (!string.IsNullOrWhiteSpace(destination))
                    {
                        return destination;
                    }

                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        return sender switch
        {
            _ when ReferenceEquals(sender, SecondaryFileList) => SplitViewModel.CurrentPath,
            _ when ReferenceEquals(sender, FileList) ||
                   ReferenceEquals(sender, IconFileList) ||
                   ReferenceEquals(sender, CompactFileList) => ViewModel.CurrentPath,
            _ => null
        };
    }

    private bool IsFilePaneList(object sender) =>
        ReferenceEquals(sender, FileList) ||
        ReferenceEquals(sender, IconFileList) ||
        ReferenceEquals(sender, CompactFileList) ||
        ReferenceEquals(sender, SecondaryFileList);

    private async Task<FileOperationResult?> RunTransferAsync(
        IReadOnlyCollection<string> paths,
        string destination,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>>? conflictResolver = null,
        string? historyDescription = null)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        try
        {
            var result = await _transferQueue.EnqueueAsync(
                paths,
                destination,
                mode,
                conflictResolver ?? ResolveConflictAsync);
            var operationName = historyDescription ?? (mode == FileTransferMode.Move ? "Move" : "Copy");
            await FinishTransferAsync(result, operationName);
            PushTransferHistory(result.Transfers, operationName);
            return result;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Transfer Failed", ex.Message);
            return null;
        }
    }

    private void TransferQueue_StateChanged(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => TransferQueue_StateChanged(sender, e));
            return;
        }

        var active = _transferQueue.ActiveJob;
        ViewModel.TransferPanelVisibility = active is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        ViewModel.ActiveTransferTitle = active?.Title ?? string.Empty;
        ViewModel.ActiveTransferDetail = active?.StatusText ?? string.Empty;
        ViewModel.ActiveTransferProgress = active?.ProgressPercent ?? 0;
        ViewModel.QueuedTransferText = _transferQueue.QueuedCount > 0
            ? $"{_transferQueue.QueuedCount} queued"
            : string.Empty;

        if (active is not null)
        {
            ViewModel.StatusText = $"{active.Title}: {active.StatusText}";
        }
    }

    private async Task FinishTransferAsync(FileOperationResult result, string operationName)
    {
        await RefreshVisiblePanesAsync();
        if (result.Errors.Count > 0)
        {
            var details = string.Join(Environment.NewLine, result.Errors.Take(6));
            if (result.Errors.Count > 6)
            {
                details += $"{Environment.NewLine}...and {result.Errors.Count - 6} more error(s).";
            }

            await ShowMessageAsync($"{operationName} Incomplete", details);
        }

        if (result.Cancelled)
        {
            ViewModel.StatusText = $"{operationName} cancelled.";
        }
        else if (result.Errors.Count == 0)
        {
            ViewModel.StatusText = $"{operationName} complete: {result.CompletedItems} completed, {result.SkippedItems} skipped.";
        }
    }

    private void History_Changed(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateHistoryMenu);
            return;
        }

        UpdateHistoryMenu();
    }

    private void UpdateHistoryMenu()
    {
        UndoMenuItem.IsEnabled = _history.CanUndo;
        UndoMenuItem.Text = _history.UndoLabel;
        RedoMenuItem.IsEnabled = _history.CanRedo;
        RedoMenuItem.Text = _history.RedoLabel;
    }

    private async Task PushCreateHistoryAsync(string createdPath, bool isDirectory)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(createdPath))!;
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(createdPath));
        var description = isDirectory ? $"Create Folder '{name}'" : $"Create Document '{name}'";
        var expectedState = await FileOperationService.CapturePathStateAsync(createdPath);
        var contents = isDirectory ? null : await File.ReadAllBytesAsync(createdPath);
        _history.Push(new FileHistoryEntry(
            description,
            () => UndoCreateAsync(createdPath, isDirectory, expectedState),
            async () =>
            {
                if (isDirectory)
                {
                    await _fileOperations.CreateDirectoryAsync(parent, name);
                }
                else
                {
                    var recreatedPath = await _fileOperations.CreateFileAsync(parent, name);
                    if (contents is { Length: > 0 })
                    {
                        await File.WriteAllBytesAsync(recreatedPath, contents);
                    }
                }

                expectedState = await FileOperationService.CapturePathStateAsync(createdPath);
            }));
    }

    private Task UndoCreateAsync(
        string createdPath,
        bool isDirectory,
        PathStateSnapshot expectedState) =>
        _fileOperations.DeleteCreatedPathIfUnchangedAsync(createdPath, isDirectory, expectedState);

    private void PushRenameHistory(string sourcePath, string destinationPath)
    {
        var sourceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath));
        var destinationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(destinationPath));
        _history.Push(new FileHistoryEntry(
            $"Rename to '{destinationName}'",
            () => _fileOperations.RenameAsync(destinationPath, sourceName),
            () => _fileOperations.RenameAsync(sourcePath, destinationName)));
    }

    private void PushTransferHistory(
        IReadOnlyList<FileTransferRecord> transfers,
        string description)
    {
        if (transfers.Count == 0)
        {
            return;
        }

        if (transfers.Any(transfer => transfer.ReplacedExistingItem))
        {
            ViewModel.StatusText += " Undo is unavailable because an existing item was replaced or merged.";
            return;
        }

        var snapshot = transfers.ToArray();
        _history.Push(new FileHistoryEntry(
            description,
            () => ReplayTransfersAsync(snapshot, true),
            () => ReplayTransfersAsync(snapshot, false)));
    }

    private async Task ReplayTransfersAsync(
        IReadOnlyList<FileTransferRecord> transfers,
        bool undo)
    {
        foreach (var transfer in transfers)
        {
            var guardedPath = undo ? transfer.DestinationPath : transfer.SourcePath;
            if (!await FileOperationService.PathMatchesStateAsync(guardedPath, transfer.DestinationState))
            {
                throw new IOException(
                    $"WinThunar will not {(undo ? "undo" : "redo")} '{Path.GetFileName(guardedPath)}' because it was changed, replaced, or now contains different items.");
            }
        }

        if (undo && transfers.All(transfer => transfer.Mode == FileTransferMode.Copy))
        {
            await _fileOperations.MoveToTrashAsync(
                transfers.Reverse().Select(transfer => transfer.DestinationPath));
            return;
        }

        var ordered = undo ? transfers.Reverse() : transfers;
        foreach (var transfer in ordered)
        {
            if (undo && transfer.Mode == FileTransferMode.Copy)
            {
                await _fileOperations.MoveToTrashAsync([transfer.DestinationPath]);
                continue;
            }

            var source = undo ? transfer.DestinationPath : transfer.SourcePath;
            var destination = undo ? transfer.SourcePath : transfer.DestinationPath;
            var destinationParent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(destination));
            if (!string.IsNullOrWhiteSpace(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }
            var result = await _fileOperations.TransferExactAsync(
                source,
                destination,
                transfer.Mode,
                _ => Task.FromResult(new ConflictResolution(ConflictAction.Cancel)));
            if (!result.Succeeded || result.CompletedItems != 1)
            {
                var reason = result.Errors.FirstOrDefault()
                    ?? (result.Cancelled ? "a destination conflict was found" : "the item could not be restored");
                throw new IOException($"Could not {(undo ? "undo" : "redo")} '{Path.GetFileName(source)}': {reason}.");
            }
        }
    }

    private async Task RunHistoryOperationAsync(string operationName, Func<Task> operation)
    {
        if (ViewModel.IsBusy || _transferQueue.ActiveJob is not null)
        {
            ViewModel.StatusText = $"Wait for the active file operation before choosing {operationName}.";
            return;
        }

        ViewModel.IsBusy = true;
        ViewModel.StatusText = $"{operationName} in progress...";
        try
        {
            await operation();
            await RefreshVisiblePanesAsync();
            ViewModel.StatusText = $"{operationName} complete.";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"{operationName} Failed", ex.Message);
            await RefreshVisiblePanesAsync();
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async Task<ConflictResolution> ResolveConflictAsync(FileConflict conflict)
    {
        var sourceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(conflict.SourcePath));
        var destinationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(conflict.DestinationPath));
        var extension = conflict.SourceIsDirectory ? string.Empty : Path.GetExtension(destinationName);
        var stem = conflict.SourceIsDirectory ? destinationName : Path.GetFileNameWithoutExtension(destinationName);

        var choices = new RadioButtons
        {
            Header = "Choose what to do:",
            SelectedIndex = 0
        };
        choices.Items.Add("Replace the existing item");
        choices.Items.Add("Skip this item");
        choices.Items.Add("Rename the incoming item");

        var renameBox = new TextBox
        {
            Header = "New name (used when Rename is selected)",
            Text = $"{stem} copy{extension}"
        };

        var content = new StackPanel { Spacing = 12, MaxWidth = 480 };
        content.Children.Add(new TextBlock
        {
            Text = $"An item named '{destinationName}' already exists while transferring '{sourceName}'.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(choices);
        content.Children.Add(renameBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "File Conflict",
            Content = content,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel Remaining",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return new ConflictResolution(ConflictAction.Cancel);
        }

        return choices.SelectedIndex switch
        {
            0 => new ConflictResolution(ConflictAction.Replace),
            1 => new ConflictResolution(ConflictAction.Skip),
            2 => new ConflictResolution(ConflictAction.Rename, renameBox.Text),
            _ => new ConflictResolution(ConflictAction.Cancel)
        };
    }

    private async Task<string?> PromptForNameAsync(
        string title,
        string prompt,
        string initialName,
        Func<string?, string?>? validator = null)
    {
        var validate = validator ?? FileOperationService.ValidateLeafName;
        var textBox = new TextBox
        {
            Text = initialName,
            MinWidth = 360
        };
        var validationMessage = new TextBlock
        {
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 32, 32)),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        textBox.Loaded += (_, _) =>
        {
            textBox.Focus(FocusState.Programmatic);
            textBox.SelectAll();
        };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(textBox);
        content.Children.Add(validationMessage);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        void UpdateValidation()
        {
            var error = validate(textBox.Text);
            dialog.IsPrimaryButtonEnabled = error is null;
            validationMessage.Text = error ?? string.Empty;
            validationMessage.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        }
        textBox.TextChanged += (_, _) => UpdateValidation();
        UpdateValidation();

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? textBox.Text : null;
    }

    private static string? ValidateNewItemName(string parentDirectory, string? name)
    {
        var validationError = FileOperationService.ValidateLeafName(name);
        if (validationError is not null)
        {
            return validationError;
        }

        var path = Path.Combine(parentDirectory, name!);
        return File.Exists(path) || Directory.Exists(path)
            ? $"An item named '{name}' already exists."
            : null;
    }

    private static string? ValidateRenameName(string sourcePath, string? name)
    {
        var validationError = FileOperationService.ValidateLeafName(name);
        if (validationError is not null)
        {
            return validationError;
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(sourcePath));
        if (string.IsNullOrWhiteSpace(parent))
        {
            return "The filesystem root cannot be renamed here.";
        }

        var destination = Path.Combine(parent, name!);
        if (PathEquals(sourcePath, destination))
        {
            return null;
        }

        return File.Exists(destination) || Directory.Exists(destination)
            ? $"An item named '{name}' already exists."
            : null;
    }

    private async Task<bool> RunFileOperationAsync(string status, Func<Task> operation)
    {
        if (ViewModel.IsBusy)
        {
            return false;
        }

        ViewModel.IsBusy = true;
        ViewModel.StatusText = status;
        try
        {
            await operation();
            await RefreshVisiblePanesAsync();
            return true;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("File Operation Failed", ex.Message);
            await RefreshVisiblePanesAsync();
            return false;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            },
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private MainPageViewModel ActiveBrowser =>
        _activePane == BrowserPane.Secondary && _splitPaneOpen ? SplitViewModel : ViewModel;

    private ListViewBase ActiveFileList => _activePane == BrowserPane.Secondary && _splitPaneOpen
        ? SecondaryFileList
        : ViewModel.ViewMode switch
        {
            BrowserViewMode.Icons => IconFileList,
            BrowserViewMode.Compact => CompactFileList,
            _ => FileList
        };

    private BrowserTabState? ActiveTab =>
        (BrowserTabs.SelectedItem as TabViewItem)?.Tag as BrowserTabState;

    private IReadOnlyList<FileSystemEntry> SelectedEntries() =>
        ActiveFileList.SelectedItems.Cast<FileSystemEntry>().ToArray();

    private void UpdateCommandState()
    {
        var selected = SelectedEntries();
        var hasSelection = selected.Count > 0;
        var singleSelection = selected.Count == 1;
        var singleFolder = singleSelection && selected[0].IsDirectory;
        var singleFile = singleSelection && !selected[0].IsDirectory;
        var inTrash = ActiveBrowser.IsRecycleBinView;
        var canCreate = CanWriteToCurrentFolder();
        var canModifySelection = hasSelection && !inTrash && CanWriteSelectionParents(selected);
        var hasClipboardItems = ClipboardContainsStorageItems();
        var canPaste = canCreate && hasClipboardItems;
        var canPasteIntoFolder = singleFolder &&
            !inTrash &&
            !ActiveBrowser.IsSearchMode &&
            IsDirectoryPotentiallyWritable(selected[0].FullPath) &&
            hasClipboardItems;

        RestoreMenuItem.Visibility = inTrash && selected.Any(entry => entry.IsRecycleBinItem)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ContextOpenMenuItem.Visibility = singleSelection && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextOpenWithMenuItem.Visibility = singleFile && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextOpenInNewTabMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextOpenInNewWindowMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextOpenTerminalMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextPasteIntoMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        ContextPasteIntoMenuItem.IsEnabled = canPasteIntoFolder;
        ContextCutMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextCopyMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextCutMenuItem.IsEnabled = canModifySelection;
        ContextCopyMenuItem.IsEnabled = hasSelection && !inTrash;
        ContextDuplicateMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextDuplicateMenuItem.IsEnabled = hasSelection && canCreate;
        ContextRenameMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextRenameMenuItem.IsEnabled = canModifySelection;
        ContextMakeLinkMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextMakeLinkMenuItem.IsEnabled = singleSelection && canCreate;
        ContextMoveToTrashMenuItem.Visibility = inTrash ? Visibility.Collapsed : Visibility.Visible;
        ContextMoveToTrashMenuItem.IsEnabled = canModifySelection;
        ContextDeletePermanentlyMenuItem.IsEnabled = hasSelection;

        FileOpenMenuItem.Visibility = singleSelection && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileOpenWithMenuItem.Visibility = singleFile && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileOpenInNewTabMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileOpenInNewWindowMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileOpenTerminalMenuItem.Visibility = singleFolder && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileSelectionSeparator.Visibility = singleSelection && !inTrash ? Visibility.Visible : Visibility.Collapsed;
        FileCreateFolderMenuItem.IsEnabled = canCreate;
        FileCreateDocumentMenu.IsEnabled = canCreate;
        FileEmptyTrashMenuItem.Visibility = inTrash ? Visibility.Visible : Visibility.Collapsed;

        FolderCreateFolderMenuItem.IsEnabled = canCreate;
        FolderCreateDocumentMenu.IsEnabled = canCreate;
        FolderPasteMenuItem.IsEnabled = canPaste;

        EditCutMenuItem.IsEnabled = canModifySelection;
        EditCopyMenuItem.IsEnabled = hasSelection && !inTrash;
        EditPasteMenuItem.IsEnabled = canPaste;
        EditMoveToTrashMenuItem.IsEnabled = canModifySelection;
        EditDeletePermanentlyMenuItem.IsEnabled = hasSelection;
        EditDuplicateMenuItem.IsEnabled = hasSelection && canCreate;
        EditRenameMenuItem.IsEnabled = canModifySelection;
    }

    private bool CanWriteToCurrentFolder() =>
        !ActiveBrowser.IsRecycleBinView &&
        !ActiveBrowser.IsSearchMode &&
        IsDirectoryPotentiallyWritable(ActiveBrowser.CurrentPath);

    private static bool CanWriteSelectionParents(IEnumerable<FileSystemEntry> entries) => entries
        .Select(entry => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(entry.FullPath)))
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .All(path => IsDirectoryPotentiallyWritable(path!));

    private static bool IsDirectoryPotentiallyWritable(string path)
    {
        try
        {
            return Directory.Exists(path) &&
                (File.GetAttributes(path) & System.IO.FileAttributes.ReadOnly) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool ClipboardContainsStorageItems()
    {
        try
        {
            return Clipboard.GetContent().Contains(StandardDataFormats.StorageItems);
        }
        catch
        {
            return false;
        }
    }

    private void SetViewMode(BrowserViewMode mode)
    {
        ViewModel.ViewMode = mode;
        UpdateViewMenu();
        ViewModel.SetSelectionStatus(SelectedEntries());
        RememberCurrentFolderView();
        SaveSession();
    }

    private void SetSortColumn(BrowserSortColumn column)
    {
        if (ViewModel.SortColumn == column)
        {
            ViewModel.SortDescending = !ViewModel.SortDescending;
        }
        else
        {
            ViewModel.SortColumn = column;
            ViewModel.SortDescending = false;
        }

        SplitViewModel.SortColumn = ViewModel.SortColumn;
        SplitViewModel.SortDescending = ViewModel.SortDescending;
        UpdateViewMenu();
        RememberCurrentFolderView();
        SaveSession();
    }

    private void SetZoomLevel(int level)
    {
        ViewModel.ZoomLevel = Math.Clamp(level, 0, 4);
        SplitViewModel.ZoomLevel = ViewModel.ZoomLevel;
        RememberCurrentFolderView();
        SaveSession();
    }

    private void UpdateViewMenu()
    {
        DetailsViewMenuItem.IsChecked = ViewModel.ViewMode == BrowserViewMode.Details;
        IconViewMenuItem.IsChecked = ViewModel.ViewMode == BrowserViewMode.Icons;
        CompactViewMenuItem.IsChecked = ViewModel.ViewMode == BrowserViewMode.Compact;
        SortNameMenuItem.IsChecked = ViewModel.SortColumn == BrowserSortColumn.Name;
        SortSizeMenuItem.IsChecked = ViewModel.SortColumn == BrowserSortColumn.Size;
        SortTypeMenuItem.IsChecked = ViewModel.SortColumn == BrowserSortColumn.Type;
        SortModifiedMenuItem.IsChecked = ViewModel.SortColumn == BrowserSortColumn.Modified;
        SortDescendingMenuItem.IsChecked = ViewModel.SortDescending;
        FoldersFirstMenuItem.IsChecked = ViewModel.FoldersFirst;
        ShowThumbnailsMenuItem.IsChecked = ViewModel.ShowThumbnails;
    }

    private void AddBrowserTab(string path, bool select)
    {
        var state = new BrowserTabState(path);
        var tab = new TabViewItem
        {
            Header = state.Title,
            Tag = state,
            IsClosable = true
        };
        BrowserTabs.TabItems.Add(tab);
        if (select)
        {
            BrowserTabs.SelectedItem = tab;
        }

        SaveSession();
    }

    private void CloseBrowserTab(TabViewItem tab)
    {
        if (BrowserTabs.TabItems.Count <= 1)
        {
            ViewModel.StatusText = "WinThunar keeps one browser tab open.";
            return;
        }

        BrowserTabs.TabItems.Remove(tab);
        SaveSession();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageViewModel.CurrentPath) &&
            ActiveTab is { } tab &&
            PathEquals(tab.Path, ViewModel.CurrentPath) &&
            BrowserTabs.SelectedItem is TabViewItem tabItem)
        {
            tabItem.Header = tab.Title;
        }

        if (e.PropertyName is nameof(MainPageViewModel.CurrentPath) or
            nameof(MainPageViewModel.ShowHiddenFiles) or
            nameof(MainPageViewModel.ViewMode))
        {
            SaveSession();
        }

        if (e.PropertyName == nameof(MainPageViewModel.CurrentPath) && _usePathBar)
        {
            BuildPathBar(ViewModel.CurrentPath);
        }
        if (e.PropertyName == nameof(MainPageViewModel.CurrentPath) && _showTerminalPanel && _activePane == BrowserPane.Primary)
        {
            TerminalPathLabel.Text = ViewModel.CurrentPath;
        }
        if (e.PropertyName is nameof(MainPageViewModel.CurrentPath) or nameof(MainPageViewModel.IsSearchMode))
        {
            UpdateCommandState();
        }
    }

    private void SplitViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageViewModel.StatusText) && _activePane == BrowserPane.Secondary)
        {
            ViewModel.StatusText = SplitViewModel.StatusText;
        }

        if (e.PropertyName == nameof(MainPageViewModel.CurrentPath))
        {
            SaveSession();
            UpdateCommandState();
        }
    }

    private async Task NavigateCurrentTabAsync(string requestedPath)
    {
        var initiatingTab = ActiveTab;
        var initiatingTabItem = BrowserTabs.SelectedItem as TabViewItem;
        if (initiatingTab is null)
        {
            return;
        }

        ApplyFolderViewSettings(ViewModel, requestedPath);
        await ViewModel.NavigateAsync(requestedPath, false);
        if (ActiveTab is { } activeTab &&
            activeTab.Id == initiatingTab.Id &&
            ReferenceEquals(BrowserTabs.SelectedItem, initiatingTabItem) &&
            PathEquals(ViewModel.CurrentPath, requestedPath))
        {
            initiatingTab.RecordNavigation(ViewModel.CurrentPath);
            if (initiatingTabItem is not null)
            {
                initiatingTabItem.Header = initiatingTab.Title;
            }

            SaveSession();
        }
    }

    private async Task NavigateActivePaneAsync(string requestedPath)
    {
        if (_searchBrowser is not null && ReferenceEquals(_searchBrowser, ActiveBrowser))
        {
            HideSearchBar();
        }

        if (_activePane == BrowserPane.Secondary && _splitPaneOpen)
        {
            await NavigateSplitPaneAsync(requestedPath);
        }
        else
        {
            await NavigateCurrentTabAsync(requestedPath);
        }
    }

    private async Task NavigateSplitPaneAsync(string requestedPath)
    {
        ApplyFolderViewSettings(SplitViewModel, requestedPath);
        await SplitViewModel.NavigateAsync(requestedPath, false);
        if (_splitTab is not null && PathEquals(SplitViewModel.CurrentPath, requestedPath))
        {
            _splitTab.RecordNavigation(SplitViewModel.CurrentPath);
            SaveSession();
        }
    }

    private async Task OpenSplitPaneAsync(string path, bool activate)
    {
        _splitPaneOpen = true;
        SplitViewMenuItem.IsChecked = true;
        SplitDividerColumn.Width = new GridLength(5);
        SecondaryPaneColumn.Width = new GridLength(1, GridUnitType.Star);
        SplitDivider.Visibility = Visibility.Visible;
        SecondaryPaneBorder.Visibility = Visibility.Visible;
        _splitTab = new BrowserTabState(path);
        SplitViewModel.ShowHiddenFiles = ViewModel.ShowHiddenFiles;
        await SplitViewModel.InitializeAsync(path);
        SetActivePane(activate ? BrowserPane.Secondary : BrowserPane.Primary);
        SaveSession();
    }

    private void CloseSplitPane()
    {
        _splitPaneOpen = false;
        SplitViewMenuItem.IsChecked = false;
        SplitDividerColumn.Width = new GridLength(0);
        SecondaryPaneColumn.Width = new GridLength(0);
        SplitDivider.Visibility = Visibility.Collapsed;
        SecondaryPaneBorder.Visibility = Visibility.Collapsed;
        _splitTab = null;
        SetActivePane(BrowserPane.Primary);
        SaveSession();
    }

    private void SetActivePaneForList(object sender)
    {
        SetActivePane(ReferenceEquals(sender, SecondaryFileList)
            ? BrowserPane.Secondary
            : BrowserPane.Primary);
    }

    private void SetActivePane(BrowserPane pane)
    {
        _activePane = pane == BrowserPane.Secondary && _splitPaneOpen
            ? BrowserPane.Secondary
            : BrowserPane.Primary;
        UpdateActivePaneVisuals();
    }

    private void UpdateActivePaneVisuals()
    {
        PrimaryPaneBorder.BorderThickness = _activePane == BrowserPane.Primary
            ? new Thickness(2)
            : new Thickness(0);
        SecondaryPaneBorder.BorderThickness = _activePane == BrowserPane.Secondary
            ? new Thickness(2)
            : new Thickness(0);
    }

    private async Task RefreshVisiblePanesAsync()
    {
        await RefreshBrowserAsync(ViewModel);
        if (_splitPaneOpen)
        {
            await RefreshBrowserAsync(SplitViewModel);
        }
    }

    private async Task RefreshBrowserAsync(MainPageViewModel browser)
    {
        if (browser.IsSearchMode &&
            ReferenceEquals(browser, _searchBrowser) &&
            !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            await browser.SearchAsync(SearchBox.Text);
        }
        else
        {
            await browser.RefreshAsync();
        }
    }

    private async Task CloseSearchAsync()
    {
        _searchInputDelay?.Cancel();
        _searchInputDelay?.Dispose();
        _searchInputDelay = null;
        var browser = _searchBrowser;
        HideSearchBar();
        if (browser is not null)
        {
            await browser.EndSearchAsync();
        }
    }

    private void HideSearchBar()
    {
        LocationLabel.Visibility = Visibility.Visible;
        LocationBox.Visibility = Visibility.Visible;
        LocationGoButton.Visibility = Visibility.Visible;
        SearchLabel.Visibility = Visibility.Collapsed;
        SearchBox.Visibility = Visibility.Collapsed;
        CloseSearchButton.Visibility = Visibility.Collapsed;
        _searchBrowser = null;
        ApplyLocationSelector();
    }

    private void SaveSession()
    {
        if (!_sessionReady || _restoringSession)
        {
            return;
        }

        try
        {
            _sessionService.Save(new AppSessionState
            {
                LastPath = ViewModel.CurrentPath,
                ShowHiddenFiles = ViewModel.ShowHiddenFiles,
                ViewMode = ViewModel.ViewMode,
                SortColumn = ViewModel.SortColumn,
                SortDescending = ViewModel.SortDescending,
                FoldersFirst = ViewModel.FoldersFirst,
                ShowThumbnails = ViewModel.ShowThumbnails,
                ZoomLevel = ViewModel.ZoomLevel,
                IncludeSubfolders = ViewModel.IncludeSubfolders,
                ConfirmMoveToTrash = _confirmMoveToTrash,
                RestoreTabs = _restoreTabs,
                TreeSidePane = _treeSidePane,
                ShowSizeColumn = ViewModel.ShowSizeColumn,
                ShowTypeColumn = ViewModel.ShowTypeColumn,
                ShowModifiedColumn = ViewModel.ShowModifiedColumn,
                SizeColumnWidth = ViewModel.SizeColumnWidth,
                TypeColumnWidth = ViewModel.TypeColumnWidth,
                ModifiedColumnWidth = ViewModel.ModifiedColumnWidth,
                SplitSizeColumnWidth = SplitViewModel.SizeColumnWidth,
                SplitTypeColumnWidth = SplitViewModel.TypeColumnWidth,
                UsePathBar = _usePathBar,
                SingleClickActivation = _singleClickActivation,
                RememberFolderViews = _rememberFolderViews,
                ShowImagePreview = _showImagePreview,
                ShowTerminalPanel = _showTerminalPanel,
                ShowBackToolbarButton = BackToolbarButton.Visibility == Visibility.Visible,
                ShowForwardToolbarButton = ForwardToolbarButton.Visibility == Visibility.Visible,
                ShowUpToolbarButton = UpToolbarButton.Visibility == Visibility.Visible,
                ShowHomeToolbarButton = HomeToolbarButton.Visibility == Visibility.Visible,
                ShowReloadToolbarButton = ReloadToolbarButton.Visibility == Visibility.Visible,
                ShowSearchToolbarButton = SearchToolbarButton.Visibility == Visibility.Visible,
                ToolbarOrder = _toolbarOrder.ToList(),
                ToolbarCustomActions = _toolbarCustomActionIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                FolderViewSettings = _folderViewSettings,
                CustomShortcuts = _customShortcuts,
                Bookmarks = ViewModel.Bookmarks.Select(bookmark => bookmark.Path).ToList(),
                BookmarkItems = ViewModel.Bookmarks.Select(bookmark => new BookmarkState
                {
                    Name = bookmark.Name,
                    Path = bookmark.Path
                }).ToList(),
                Tabs = BrowserTabs.TabItems
                    .OfType<TabViewItem>()
                    .Select(tab => (tab.Tag as BrowserTabState)?.Path)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Cast<string>()
                    .ToList(),
                ActiveTabIndex = Math.Max(0, BrowserTabs.SelectedIndex),
                SplitPaneOpen = _splitPaneOpen,
                SplitPath = _splitPaneOpen ? SplitViewModel.CurrentPath : null
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Could not save the WinThunar session: {ex.Message}";
        }
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left, RecycleBinService.VirtualPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(right, RecycleBinService.VirtualPath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool RejectRecycleBinMutation(string action)
    {
        if (!ActiveBrowser.IsRecycleBinView)
        {
            return false;
        }

        ViewModel.StatusText = $"{action} is not available while viewing Trash.";
        return true;
    }

    private bool RejectDestinationMutation(string action, string? destinationPath = null)
    {
        if (ActiveBrowser.IsRecycleBinView)
        {
            ViewModel.StatusText = $"{action} is not available while viewing Trash.";
            return true;
        }
        if (ActiveBrowser.IsSearchMode)
        {
            ViewModel.StatusText = $"{action} is not available while viewing search results.";
            return true;
        }
        if (!IsDirectoryPotentiallyWritable(destinationPath ?? ActiveBrowser.CurrentPath))
        {
            ViewModel.StatusText = $"{action} is not available because this folder is read-only.";
            return true;
        }

        return false;
    }

    private enum BrowserPane
    {
        Primary,
        Secondary
    }
}
