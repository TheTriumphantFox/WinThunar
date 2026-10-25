using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinThunar.Models;

namespace WinThunar;

// Exercises real WinUI focus, including descendants of each keyboard region.
public sealed partial class MainPage
{
    private async Task CheckKeyboardRegionsAsync(string directory, List<string> checks)
    {
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "keyboard-files")).FullName;
        var textPath = Path.Combine(fixture, "keyboard.txt");
        File.WriteAllText(textPath, "Keyboard preview fixture");
        ViewModel.ViewMode = BrowserViewMode.Details;
        await ViewModel.NavigateAsync(fixture);
        _showImagePreview = false;
        ApplyImagePreview();
        _showTerminalPanel = false;
        ApplyTerminalPanel();
        SetTreeSidePane(false);
        App.Window.Activate();
        var bookmark = new NavigationLocation("Keyboard fixture", fixture, "\uE8A5");
        ViewModel.Bookmarks.Add(bookmark);
        UpdateLayout();
        await Task.Delay(100);
        bool Focused(DependencyObject region) => ContainsFocus(region, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);
        void CheckButtonName(DependencyObject region, string content, string expected)
        {
            var button = FindSmokeElement<Button>(region, candidate => candidate.Content is string text && text == content)
                ?? throw new InvalidOperationException($"Missing button: {content}");
            var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            Require(peer.GetName() == expected, $"Button automation announces {expected}", checks);
        }
        async Task FocusAndWaitAsync(Control target)
        {
            // GotFocus is asynchronous. Wait for the native notification rather than a
            // fixed sleep, and ignore older notifications queued by earlier focus moves.
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFocus(object? sender, FocusManagerGotFocusEventArgs args)
            {
                if (ContainsFocus(target, args.NewFocusedElement) && Focused(target)) completed.TrySetResult();
            }
            FocusManager.GotFocus += OnFocus;
            try
            {
                Require(target.Focus(FocusState.Keyboard) && Focused(target), $"{target.Name} accepts keyboard focus", checks);
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { FocusManager.GotFocus -= OnFocus; }
        }

        Search_Click(this, new RoutedEventArgs());
        CycleKeyboardRegion(false);
        Require(Focused(PlacesList), "F6 leaves the search input for the side pane", checks);
        CycleKeyboardRegion(true);
        Require(Focused(SearchBox), "Shift+F6 returns to the visible search input", checks);
        await CloseSearchAsync();
        Require(Focused(ActiveFileList), "Closing search returns keyboard focus to its file pane", checks);

        Require(DevicesList.Focus(FocusState.Keyboard) && Focused(DevicesList), "Devices accepts keyboard focus", checks);
        CycleKeyboardRegion(false);
        Require(Focused(ActiveFileList), "F6 recognizes Devices as part of the side pane", checks);
        Require(BookmarksList.Focus(FocusState.Keyboard) && Focused(BookmarksList), "Generated bookmark accepts keyboard focus", checks);
        CycleKeyboardRegion(true);
        Require(Focused(LocationBox), "Shift+F6 recognizes Bookmarks as part of the side pane", checks);

        _usePathBar = true;
        ApplyLocationSelector();
        PathBarEdit_Click(this, new RoutedEventArgs());
        CycleKeyboardRegion(false);
        Require(Focused(PlacesList), "F6 recognizes the edited pathbar location", checks);
        CycleKeyboardRegion(true);
        Require(Focused(LocationBox), "Shift+F6 returns to the visible pathbar editor", checks);
        _usePathBar = false;
        ApplyLocationSelector();

        SetTreeSidePane(true);
        UpdateLayout();
        LocationBox.Focus(FocusState.Keyboard);
        CycleKeyboardRegion(false);
        Require(Focused(DirectoryTree), "F6 reaches the visible folder tree", checks);
        SetTreeSidePane(false);
        foreach (var mode in new[] { BrowserViewMode.Details, BrowserViewMode.Icons, BrowserViewMode.Compact })
        {
            ViewModel.ViewMode = mode;
            UpdateLayout();
            PlacesList.Focus(FocusState.Keyboard);
            CycleKeyboardRegion(false);
            Require(Focused(ActiveFileList), $"F6 reaches the {mode} file layout", checks);
        }
        ViewModel.ViewMode = BrowserViewMode.Details;

        await OpenSplitPaneAsync(fixture, false);
        UpdateLayout();
        await Task.Delay(100);
        CheckButtonName(SecondaryPaneBorder, "\u2190", "Back in split pane");
        CheckButtonName(SecondaryPaneBorder, "\u2191", "Parent folder in split pane");
        CheckButtonName(SecondaryPaneBorder, "\u2302", "Home in split pane");
        CheckButtonName(SecondaryPaneBorder, "Go", "Open entered split pane folder");
        await FocusAndWaitAsync(SplitLocationBox);
        Require(ReferenceEquals(ActiveBrowser, SplitViewModel), "Focusing the split location activates split-pane commands", checks);
        CycleKeyboardRegion(true);
        Require(Focused(FileList), "Shift+F6 recognizes split navigation as part of the split pane", checks);
        CycleKeyboardRegion(false);
        Require(Focused(SecondaryFileList), "F6 reaches the split file pane", checks);

        _showImagePreview = true;
        ApplyImagePreview();
        await UpdateImagePreviewAsync(new FileSystemEntry("keyboard.txt", textPath, false, "", "Text document", "", ""));
        await FocusAndWaitAsync(SplitLocationBox);
        await FocusAndWaitAsync(SecondaryFileList);
        CycleKeyboardRegion(false);
        Require(Focused(PreviewText), "F6 reaches selectable read-only preview text", checks);

        var pdfPath = Path.Combine(fixture, "keyboard.pdf");
        WriteSmokePdf(pdfPath);
        await UpdateImagePreviewAsync(new FileSystemEntry("keyboard.pdf", pdfPath, false, "", "PDF document", "", ""));
        await FocusAndWaitAsync(SecondaryFileList);
        CycleKeyboardRegion(false);
        Require(Focused(PdfNextPage), "F6 reaches the enabled Next button on the first PDF page", checks);
        PdfNextPage_Click(PdfNextPage, new RoutedEventArgs());
        for (var attempt = 0; attempt < 100 && (_pdfPageIndex != 1 || _pdfPreviewEntry is null); attempt++) await Task.Delay(30);
        await FocusAndWaitAsync(SecondaryFileList);
        CycleKeyboardRegion(false);
        Require(Focused(PdfPreviousPage), "F6 reaches the enabled Previous button on the last PDF page", checks);
        await UpdateImagePreviewAsync(null);
        await FocusAndWaitAsync(SecondaryFileList);
        CycleKeyboardRegion(false);
        Require(Focused(PreviewScroller), "F6 reaches the preview scroller when no text or PDF controls are visible", checks);
        await UpdateImagePreviewAsync(new FileSystemEntry("keyboard.txt", textPath, false, "", "Text document", "", ""));
        _showTerminalPanel = true;
        ApplyTerminalPanel();
        CheckButtonName(TerminalPane, "Close", "Close command panel");
        CheckButtonName(TerminalPane, "Run", "Run PowerShell command");
        TerminalOutput.Focus(FocusState.Keyboard);
        CycleKeyboardRegion(true);
        Require(Focused(PreviewText), "Shift+F6 leaves command output for the preview region", checks);
        TerminalCommand.IsEnabled = false;
        CycleKeyboardRegion(false);
        Require(Focused(TerminalOutput), "F6 reaches command output while command input is disabled", checks);
        HomeToolbarButton.Focus(FocusState.Keyboard);
        CycleKeyboardRegion(true);
        Require(Focused(TerminalOutput), "Shift+F6 from outside the regions starts at the final available region", checks);
        HomeToolbarButton.Focus(FocusState.Keyboard);
        CycleKeyboardRegion(false);
        Require(Focused(BrowserTabs), "F6 from outside the regions starts at folder tabs", checks);
        TerminalOutput.Focus(FocusState.Keyboard);
        TerminalCommand.IsEnabled = true;
        CloseTerminalPanel_Click(this, new RoutedEventArgs());
        Require(Focused(SecondaryFileList), "Closing the command panel restores active-pane focus", checks);

        PreviewText.Focus(FocusState.Keyboard);
        _showImagePreview = false;
        ApplyImagePreview();
        Require(Focused(SecondaryFileList), "Hiding the preview restores active-pane focus", checks);
        SplitLocationBox.Focus(FocusState.Keyboard);
        CloseSplitPane();
        Require(Focused(ActiveFileList), "Closing a focused split pane restores primary-pane focus", checks);
        ViewModel.Bookmarks.Remove(bookmark);
        await CaptureSmokeImageAsync(directory, "KeyboardRegions");
    }
}
