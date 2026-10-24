using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinThunar.Models;
using WinThunar.Services;

namespace WinThunar;

// Compiled only with -p:RoadmapSmokeTest=true; never included in a release build.
public sealed partial class MainPage
{
    private static bool _smokeStarted;

    private async Task RunRoadmapSmokeAsync()
    {
        // Additional windows are used to verify application-wide preferences without starting
        // another smoke run, reading the normal session, or navigating the user's folders.
        if (_smokeStarted) return;
        _smokeStarted = true;
        _restoringSession = true;
        _sessionReady = false;
        var checks = new List<string>();
        var directory = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_DIRECTORY")
            ?? throw new InvalidOperationException("The desktop smoke test needs an isolated directory.");
        var themeRestart = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_RESTART_THEME") == "Dark";
        string? failure = null;
        try
        {
            // Page_Loaded never reads the normal session or recovers the user's transfer journal.
            Directory.CreateDirectory(directory);
            if (themeRestart)
            {
                await Task.Delay(200);
                Require(App.ThemePreference == AppThemePreference.Dark && ActualTheme == ElementTheme.Dark &&
                    App.Window.AppWindow.TitleBar.PreferredTheme == Microsoft.UI.Windowing.TitleBarTheme.Dark,
                    "Restart restores the saved dark theme to the browser and title bar", checks);
                var preferences = ShowPreferencesAsync();
                await Task.Delay(150);
                Require(FindThemeSelector().SelectedIndex == (int)AppThemePreference.Dark,
                    "Restart restores the saved theme in Preferences", checks);
                await CaptureSmokeImageAsync(directory, "ThemeRestart", _preferencesDialog);
                await FinishSmokePreferencesAsync(preferences, apply: false);
                return;
            }
            var fixture = Directory.CreateDirectory(Path.Combine(directory, "files")).FullName;
            File.WriteAllText(Path.Combine(fixture, "example.txt"), "Desktop smoke fixture");
            await ViewModel.NavigateAsync(fixture);
            AddBrowserTab(fixture, true);
            await Task.Delay(400);
            UpdateLayout();
            Require(FileList.Items.Count == 1, "Detailed view lists its fixture", checks);
            Require(AutomationProperties.GetName(LocationBox) == "Folder location", "Location has an accessible name", checks);
            Require(AutomationProperties.GetName(FileList).Length > 0, "File view has an accessible name", checks);
            FileList.SelectedIndex = 0;
            FileList.ScrollIntoView(FileList.SelectedItem);
            UpdateLayout();
            await Task.Delay(200);
            var item = FileList.ContainerFromIndex(0) as ListViewItem;
            Require(item is not null && AutomationProperties.GetName(item).Contains("example.txt"), "File container exposes the filename to accessibility", checks);
            LocationBox.Focus(FocusState.Keyboard);
            CycleKeyboardRegion(false);
            Require(ContainsFocus(PlacesList, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject), "F6 moves from location to side pane", checks);
            CycleKeyboardRegion(false);
            Require(ContainsFocus(FileList, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject), "F6 moves from side pane to file list", checks);
            CycleKeyboardRegion(true);
            Require(ContainsFocus(PlacesList, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject), "Shift+F6 reverses focus order", checks);
            var entry = ViewModel.Entries[0];
            entry.GitStatus = GitFileStatus.Modified;
            Require(entry.AutomationName.Contains("Git: Modified"), "Git status is exposed as text", checks);
            foreach (var mode in new[] { BrowserViewMode.Details, BrowserViewMode.Icons, BrowserViewMode.Compact })
            {
                ViewModel.ViewMode = mode;
                UpdateLayout();
                await Task.Delay(150);
                var list = ActiveFileList;
                Require(list.Visibility == Visibility.Visible, $"{mode} view is visible", checks);
                await CaptureSmokeImageAsync(directory, mode.ToString());
            }
            ViewModel.ViewMode = BrowserViewMode.Details;
            Require(App.ThemePreference == AppThemePreference.System && RequestedTheme == ElementTheme.Default, "Browser follows the Windows theme", checks);
            _showTerminalPanel = true;
            ApplyTerminalPanel();
            await CheckThemePreferencesAsync(directory, checks);
            _showTerminalPanel = false;
            ApplyTerminalPanel();
            await ViewModel.SearchAsync("type:txt size:>0 modified:today");
            await Task.Delay(200);
            Require(ViewModel.Entries.Count == 1, "Combined search filters find the fixture", checks);
            await ViewModel.SearchAsync("modified:invalid");
            Require(!ViewModel.IsBusy && ViewModel.StatusText.Contains("modified:"), "Invalid search filters report an error without leaving a busy view", checks);
            await ViewModel.EndSearchAsync();

            var incoming = Directory.CreateDirectory(Path.Combine(directory, "incoming")).FullName;
            var outgoing = Directory.CreateDirectory(Path.Combine(directory, "outgoing")).FullName;
            var sourcePath = Path.Combine(incoming, "replace.txt");
            var destinationPath = Path.Combine(outgoing, "replace.txt");
            File.WriteAllText(sourcePath, "incoming");
            File.WriteAllText(destinationPath, "old destination");
            var transfer = await RunTransferAsync([sourcePath], outgoing, FileTransferMode.Copy,
                _ => Task.FromResult(new ConflictResolution(ConflictAction.Replace)));
            Require(transfer?.Succeeded == true && _history.CanUndo, "Queued replacement is added to window Undo history", checks);
            await _history.UndoAsync();
            Require(File.ReadAllText(destinationPath) == "old destination", "Window Undo restores the displaced file", checks);
            await _history.RedoAsync();
            Require(File.ReadAllText(destinationPath) == "incoming", "Window Redo restores the incoming file", checks);
            _history.Dispose();
            Require(!Directory.EnumerateDirectories(outgoing).Any(), "Expiring window history releases retained data", checks);

            var recoveryJournal = Directory.CreateDirectory(Path.Combine(directory, "recovery")).FullName;
            var brokenRecord = Path.Combine(recoveryJournal, "broken.json");
            File.WriteAllText(brokenRecord, "{ deliberately invalid smoke record");
            await RecoverTransfersAsync();
            Require(RecoveryInfoBar.IsOpen && _recoveryResult.Errors.Count == 1,
                "Recovery issues have a persistent review warning", checks);
            Require(RecoveryDetails(_recoveryResult).Contains(brokenRecord),
                "Recovery details identify the preserved journal", checks);
            var recoveryDialog = ShowRecoveryDialogAsync();
            await Task.Delay(200);
            Require(_recoveryDialog is not null && _recoveryDialogOpen, "Interrupted Transfers dialog opens", checks);
            var recoveryPanel = (StackPanel)_recoveryDialog!.Content;
            var recoveryDetails = (TextBlock)recoveryPanel.Children.OfType<ScrollViewer>().Single().Content;
            Require(recoveryDetails.IsTextSelectionEnabled && AutomationProperties.GetName(recoveryDetails) == "Interrupted transfer details",
                "Recovery details are read-only and have an accessible name", checks);
            await CaptureSmokeImageAsync(directory, "RecoveryDialog", _recoveryDialog);
            _recoveryDialog.Hide();
            await recoveryDialog;
            Require(File.Exists(brokenRecord), "Review leaves ambiguous recovery records intact", checks);
            File.Delete(brokenRecord); // Only this test's intentionally corrupt record.
            await RecoverTransfersAsync();
            Require(!RecoveryInfoBar.IsOpen, "Successful recovery retry clears the warning", checks);

            await ViewModel.NavigateAsync(fixture);
            await ViewModel.SearchAsync("type:txt content:\"Desktop smoke\"");
            await Task.Delay(100);
            Require(ViewModel.Entries.Count == 1, "Text-content search finds a phrase inside the fixture", checks);
            await ViewModel.EndSearchAsync();
            _showImagePreview = true;
            ApplyImagePreview();
            await UpdateImagePreviewAsync(ViewModel.Entries.Single());
            Require(PreviewText.Visibility == Visibility.Visible && PreviewText.IsReadOnly && PreviewText.Text.Contains("Desktop smoke fixture"),
                "Preview pane displays bounded read-only text", checks);
            UpdateLayout();
            await Task.Delay(100);
            var filename = FindSmokeText(FileList.ContainerFromIndex(0), "example.txt");
            Require(filename is not null && filename.ActualWidth >= 48,
                "Detailed filenames keep visible space when the preview narrows the pane", checks);
            await CaptureSmokeImageAsync(directory, "TextPreview");
            await CheckDocxAsync(directory, checks);
            await ViewModel.NavigateAsync(fixture);

            var documents = Directory.CreateDirectory(Path.Combine(directory, "documents")).FullName;
            var pdfPath = Path.Combine(documents, "preview.pdf");
            WriteSmokePdf(pdfPath);
            var pdfEntry = new FileSystemEntry("preview.pdf", pdfPath, false, "", "PDF document", "", "");
            await UpdateImagePreviewAsync(pdfEntry);
            UpdateLayout();
            await Task.Delay(100);
            checks.Add($"PDF render diagnostics: {((PreviewImage.Source as BitmapImage)?.PixelWidth)}x{((PreviewImage.Source as BitmapImage)?.PixelHeight)}, {PdfPageNumber.Text}, {PreviewStatus.Text}");
            Require(PreviewImage.Source is BitmapImage firstPage && firstPage.PixelWidth > 0 && firstPage.PixelWidth <= PdfFilePreview.MaximumRenderPixels &&
                firstPage.PixelHeight <= PdfFilePreview.MaximumRenderPixels && PdfPageNumber.Text == "Page 1 of 2" && !PdfPreviousPage.IsEnabled && PdfNextPage.IsEnabled,
                "Native PDF preview renders the first page within the pixel limit", checks);
            Require(AutomationProperties.GetName(PdfNextPage) == "Next PDF page" && AutomationProperties.GetLiveSetting(PdfPageNumber) == Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite,
                "PDF navigation has accessible names and announces page changes", checks);
            PdfNextPage_Click(PdfNextPage, new RoutedEventArgs());
            for (var attempt = 0; attempt < 100 && (_pdfPageIndex != 1 || _pdfPreviewEntry is null); attempt++) await Task.Delay(30);
            Require(PdfPageNumber.Text == "Page 2 of 2" && PdfPreviousPage.IsEnabled && !PdfNextPage.IsEnabled,
                "Next PDF page reaches the final page and disables Next", checks);
            PdfPreviousPage_Click(PdfPreviousPage, new RoutedEventArgs());
            for (var attempt = 0; attempt < 100 && (_pdfPageIndex != 0 || _pdfPreviewEntry is null); attempt++) await Task.Delay(30);
            Require(PdfPageNumber.Text == "Page 1 of 2" && !PdfPreviousPage.IsEnabled,
                "Previous PDF page returns to the first page", checks);
            UpdateLayout();
            await Task.Delay(100);
            await CaptureSmokeImageAsync(directory, "PdfPreview");
            using (var exclusive = new FileStream(pdfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(exclusive.Length > 0, "PDF preview releases the source handle after rendering", checks);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try { await PdfFilePreview.ReadAsync(pdfPath, 0, cancelled.Token); throw new InvalidOperationException("PDF cancellation was ignored."); }
                catch (OperationCanceledException) { checks.Add("PDF preview observes cancellation"); }
            }
            try { await PdfFilePreview.ReadAsync(pdfPath, 2, default); throw new InvalidOperationException("Invalid PDF page was accepted."); }
            catch (IOException) { checks.Add("PDF preview rejects an unavailable page"); }
            var malformedPdf = Path.Combine(documents, "malformed.pdf");
            File.WriteAllText(malformedPdf, "This is not a PDF document.");
            await UpdateImagePreviewAsync(new FileSystemEntry("malformed.pdf", malformedPdf, false, "", "PDF document", "", ""));
            Require(PreviewImage.Source is null && PdfPageControls.Visibility == Visibility.Collapsed && PreviewStatus.Text.StartsWith("Preview unavailable:"),
                "Malformed PDF reports an error without leaving an old image or page controls", checks);
            var oversizedPdf = Path.Combine(documents, "oversized.pdf");
            using (var oversized = File.Create(oversizedPdf)) oversized.SetLength(PdfFilePreview.MaximumFileBytes + 1L);
            await UpdateImagePreviewAsync(new FileSystemEntry("oversized.pdf", oversizedPdf, false, "", "PDF document", "", ""));
            Require(PreviewStatus.Text.Contains("64 MiB"), "PDF preview rejects oversized input before parsing", checks);
            File.Delete(oversizedPdf); // Only the generated size-limit fixture.
            var pendingPdf = UpdateImagePreviewAsync(pdfEntry);
            await UpdateImagePreviewAsync(ViewModel.Entries.Single());
            await pendingPdf;
            Require(PreviewText.Visibility == Visibility.Visible && PreviewImage.Source is null && PdfPageControls.Visibility == Visibility.Collapsed,
                "A newer selection supersedes an unfinished PDF preview", checks);
            File.Delete(pdfPath);
            File.Delete(malformedPdf);
            await ViewModel.SearchAsync("index:true name:example type:txt");
            Require(!ViewModel.IsBusy && ViewModel.StatusText.StartsWith("Windows index:"),
                "Native read-only Windows index query completes with an explicit coverage warning", checks);
            Require(ViewModel.Entries.All(match => match.FullPath.StartsWith(fixture + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)),
                "Native Windows index results stay inside the requested fixture scope", checks);
            checks.Add($"Generated index fixture matches: {ViewModel.Entries.Count} (index inclusion is controlled by Windows)");
            await ViewModel.EndSearchAsync();
            // Read at most one existing file's metadata to exercise a non-empty native rowset.
            // No filename/path/content is logged or shown, and the index configuration is untouched.
            var indexProbe = await Task.Run(() => new IndexedFileSearchService().Search(
                [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)], FileSearchQuery.Parse("type:file"),
                false, _ => { }, maximumResults: 1));
            checks.Add(indexProbe.ResultCount == 1 ? "Native Windows index returned and validated a filesystem metadata row" :
                "No accessible indexed user-profile file was available for a positive-row probe");
            await ViewModel.SearchAsync("index:true content:\"Desktop smoke\" type:txt");
            Require(ViewModel.StatusText.StartsWith("Windows index:"), "Native index accepts a quoted document-content query", checks);
            await ViewModel.EndSearchAsync();
            _showImagePreview = false;
            ApplyImagePreview();

            // Use generated folders as synthetic drive roots; never recursively search real drives
            // merely to prove This PC's routing and scope retention across successive queries.
            ViewModel.CurrentPath = ViewModels.MainPageViewModel.ComputerPath;
            ViewModel.IsComputerView = true;
            ViewModel.Entries.Clear();
            ViewModel.Entries.Add(new FileSystemEntry("Fixture drive", fixture, true, "", "Drive", "", ""));
            await ViewModel.SearchAsync("content:\"Desktop smoke\" type:txt");
            await Task.Delay(100);
            Require(ViewModel.Entries.Count == 1 && !ViewModel.IsComputerView,
                "This PC search returns actionable filesystem matches from its drive roots", checks);
            await ViewModel.SearchAsync("content:nonexistent-phrase type:txt");
            Require(ViewModel.Entries.Count == 0, "This PC retains its original scope for the next query", checks);
            await ViewModel.EndSearchAsync();
            Require(ViewModel.IsComputerView && !ViewModel.IsSearchMode, "Closing This PC search restores the drive listing", checks);
            await ViewModel.NavigateAsync(fixture);

            await CheckThumbnailReuseAsync(directory, checks);
            await RunOptionalEnduranceAsync(directory, checks);
            await CheckRestartSessionAsync(directory, checks);
            await CheckWindowCloseSessionAsync(directory, checks);
            Require(TaskbarService.LastError is null, "Windows accepted the jump-list tasks", checks);
            checks.Add(NotificationService.LastError is null ? "Windows notification registration succeeded" : "Notification registration unavailable: " + NotificationService.LastError);
        }
        catch (Exception ex) { failure = ex.ToString(); }
        finally
        {
            if (failure is null && !themeRestart) App.SetThemePreference(AppThemePreference.Dark);
            try { await CaptureSmokeImageAsync(directory, "Final"); } catch (Exception ex) { checks.Add("Screenshot unavailable: " + ex.Message); }
            File.WriteAllText(Path.Combine(directory, themeRestart ? "theme-restart-result.json" : "result.json"), JsonSerializer.Serialize(new { checks, failure }, new JsonSerializerOptions { WriteIndented = true }));
            App.Window.Close();
        }
    }

    private async Task CheckWindowCloseSessionAsync(string directory, List<string> checks)
    {
        var firstPath = Directory.CreateDirectory(Path.Combine(directory, "session-first")).FullName;
        var secondPath = Directory.CreateDirectory(Path.Combine(directory, "session-second")).FullName;
        var otherPath = Directory.CreateDirectory(Path.Combine(directory, "session-other-window")).FullName;
        var firstWindow = (MainWindow)App.OpenWindow(LaunchRequest.Empty);
        var otherWindow = (MainWindow)App.OpenWindow(LaunchRequest.Empty);
        var firstClosed = false;
        var otherClosed = false;
        try
        {
            await Task.Delay(150);
            async Task Configure(MainPage page, string[] paths)
            {
                page._restoringSession = true;
                foreach (var path in paths) page.AddBrowserTab(path, false);
                page.BrowserTabs.SelectedIndex = paths.Length - 1;
                await page.ViewModel.NavigateAsync(paths[^1]);
                page.ViewModel.ViewMode = BrowserViewMode.Compact;
                page._restoringSession = false;
                page._sessionReady = true;
                page.SaveSession();
            }
            await Configure(firstWindow.Page!, [firstPath, secondPath]);
            await Configure(otherWindow.Page!, [otherPath]);
            var store = new AppSessionService(Path.Combine(directory, "session.json"));
            Require(store.Load().Tabs.SequenceEqual([otherPath]),
                "An additional window can replace the shared saved session", checks);
            otherWindow.Close();
            otherClosed = true;
            firstWindow.Close();
            firstClosed = true;
            await Task.Delay(100);
            var restored = store.Load();
            Require(restored.Tabs.SequenceEqual([firstPath, secondPath]) && restored.ActiveTabIndex == 1 &&
                restored.LastPath == secondPath && restored.ViewMode == BrowserViewMode.Compact,
                "Closing the remaining window saves its own tabs, active folder and view for restart", checks);
        }
        finally
        {
            if (!otherClosed) otherWindow.Close();
            if (!firstClosed) firstWindow.Close();
        }
    }

    private ComboBox FindThemeSelector() => FindSmokeElement<ComboBox>(_preferencesDialog,
        combo => AutomationProperties.GetName(combo) == "Color theme") ?? throw new InvalidOperationException("Theme selector is missing.");

    private async Task FinishSmokePreferencesAsync(Task preferences, bool apply)
    {
        var button = FindSmokeElement<Button>(_preferencesDialog, candidate => candidate.Content is string text && text == (apply ? "Apply" : "Cancel"))
            ?? throw new InvalidOperationException("Preferences action button is missing.");
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
        var provider = (Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke);
        provider.Invoke();
        await preferences;
    }

    private async Task CheckThemePreferencesAsync(string directory, List<string> checks)
    {
        var cancelled = ShowPreferencesAsync();
        await Task.Delay(150);
        var selector = FindThemeSelector();
        Require(selector.Items.Count == 3 && selector.SelectedIndex == 0, "Preferences exposes accessible System, Light and Dark theme choices", checks);
        selector.SelectedIndex = 2;
        await FinishSmokePreferencesAsync(cancelled, apply: false);
        Require(App.ThemePreference == AppThemePreference.System, "Cancel leaves the color theme unchanged", checks);
        MainWindow? otherWindow = null;
        try
        {
            foreach (var theme in new[] { AppThemePreference.Light, AppThemePreference.Dark, AppThemePreference.System })
            {
                var preferences = ShowPreferencesAsync();
                await Task.Delay(150);
                Require(_preferencesDialog!.ActualTheme == ActualTheme, $"Preferences dialog follows the active theme before choosing {theme}", checks);
                FindThemeSelector().SelectedIndex = (int)theme;
                if (theme != AppThemePreference.System) await CaptureSmokeImageAsync(directory, "Preferences" + theme, _preferencesDialog);
                await FinishSmokePreferencesAsync(preferences, apply: true);
                UpdateLayout();
                await Task.Delay(150);
                var expected = theme switch
                {
                    AppThemePreference.Light => ElementTheme.Light,
                    AppThemePreference.Dark => ElementTheme.Dark,
                    _ => Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light
                };
                var titleTheme = theme switch
                {
                    AppThemePreference.Light => Microsoft.UI.Windowing.TitleBarTheme.Light,
                    AppThemePreference.Dark => Microsoft.UI.Windowing.TitleBarTheme.Dark,
                    _ => Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode
                };
                Require(App.ThemePreference == theme && ActualTheme == expected && App.Window.AppWindow.TitleBar.PreferredTheme == titleTheme,
                    $"Applying {theme} changes the browser and title bar", checks);
                Require(new AppearanceSettingsStore(Path.Combine(directory, "appearance.json")).Load().Theme == theme,
                    $"Applying {theme} persists the choice outside window sessions", checks);
                if (otherWindow is null)
                {
                    otherWindow = (MainWindow)App.OpenWindow(LaunchRequest.Empty);
                    await Task.Delay(150);
                }
                Require(otherWindow.Page?.ActualTheme == expected && otherWindow.AppWindow.TitleBar.PreferredTheme == titleTheme,
                    $"{theme} applies to both new and existing additional windows", checks);
                if (theme != AppThemePreference.System)
                {
                    await CaptureSmokeImageAsync(directory, theme.ToString());
                    var whatsNew = CreateWhatsNewDialog();
                    var showingWhatsNew = whatsNew.ShowAsync();
                    await Task.Delay(150);
                    Require(FindSmokeText(whatsNew.Content as DependencyObject, "Version 0.5.13 — maintenance update") is not null,
                        $"What's New describes the 0.5.13 maintenance release in {theme}", checks);
                    Require(FindSmokeText(whatsNew.Content as DependencyObject, "Version 0.5.12 — read and find DOCX text") is not null,
                        $"What's New describes the 0.5.12 DOCX additions in {theme}", checks);
                    await CaptureSmokeImageAsync(directory, "WhatsNew" + theme, whatsNew);
                    whatsNew.Hide();
                    await showingWhatsNew;
                }
            }
            // A second window changes the theme while an older Preferences dialog remains open.
            var stalePreferences = ShowPreferencesAsync();
            await Task.Delay(150);
            App.SetThemePreference(AppThemePreference.Dark);
            await Task.Delay(100);
            Require(_preferencesDialog!.ActualTheme == ElementTheme.Dark, "An open dialog follows a theme change from another window", checks);
            await FinishSmokePreferencesAsync(stalePreferences, apply: true);
            Require(App.ThemePreference == AppThemePreference.Dark, "Applying an unchanged stale selector preserves another window's theme choice", checks);
            var blockedPreferences = ShowPreferencesAsync();
            await Task.Delay(150);
            FindThemeSelector().SelectedIndex = (int)AppThemePreference.Light;
            using (var locked = new FileStream(Path.Combine(directory, "appearance.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await FinishSmokePreferencesAsync(blockedPreferences, apply: true);
                Require(App.ThemePreference == AppThemePreference.Dark && ActualTheme == ElementTheme.Dark && ViewModel.StatusText.Contains("Could not save the color theme"),
                    "A failed theme save reports the error and preserves the active and stored choice", checks);
            }
            App.SetThemePreference(AppThemePreference.System);
        }
        finally { otherWindow?.Close(); }
    }

    private static T? FindSmokeElement<T>(DependencyObject? root, Func<T, bool> predicate) where T : DependencyObject
    {
        if (root is T element && predicate(element)) return element;
        if (root is null) return null;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var found = FindSmokeElement(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index), predicate);
            if (found is not null) return found;
        }
        return null;
    }

    private static void Require(bool condition, string description, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description);
    }

    private static void WriteSmokePdf(string path)
    {
        static string Page(string label) => $"BT /F1 20 Tf 32 160 Td ({label}) Tj ET";
        var first = Page("WINTHUNAR PDF PREVIEW");
        var second = Page("SECOND PAGE");
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 360 240] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {first.Length} >>\nstream\n{first}\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 360 240] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>",
            $"<< /Length {second.Length} >>\nstream\n{second}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];
        var pdf = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllText(path, pdf.ToString(), System.Text.Encoding.ASCII);
    }

    private static TextBlock? FindSmokeText(DependencyObject? root, string text)
    {
        if (root is TextBlock block && block.Text == text) return block;
        if (root is null) return null;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var found = FindSmokeText(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index), text);
            if (found is not null) return found;
        }
        return null;
    }

    private async Task CaptureSmokeImageAsync(string directory, string name, UIElement? element = null)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element ?? this);
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(directory);
        var file = await folder.CreateFileAsync(name + ".png", CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
