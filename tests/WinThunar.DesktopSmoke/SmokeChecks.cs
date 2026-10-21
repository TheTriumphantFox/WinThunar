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
    private async Task RunRoadmapSmokeAsync()
    {
        _restoringSession = true;
        _sessionReady = false;
        var checks = new List<string>();
        var directory = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_DIRECTORY")
            ?? throw new InvalidOperationException("The desktop smoke test needs an isolated directory.");
        string? failure = null;
        try
        {
            // Page_Loaded never reads the normal session or recovers the user's transfer journal.
            Directory.CreateDirectory(directory);
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
            Require(RequestedTheme == ElementTheme.Default, "Browser follows the Windows theme", checks);
            _showTerminalPanel = true;
            ApplyTerminalPanel();
            foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                RequestedTheme = theme;
                UpdateLayout();
                await Task.Delay(150);
                Require(ActualTheme == theme, $"{theme} theme is applied", checks);
                await CaptureSmokeImageAsync(directory, theme.ToString());
            }
            RequestedTheme = ElementTheme.Default;
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

            Require(TaskbarService.LastError is null, "Windows accepted the jump-list tasks", checks);
            checks.Add(NotificationService.LastError is null ? "Windows notification registration succeeded" : "Notification registration unavailable: " + NotificationService.LastError);
        }
        catch (Exception ex) { failure = ex.ToString(); }
        finally
        {
            try { await CaptureSmokeImageAsync(directory, "Final"); } catch (Exception ex) { checks.Add("Screenshot unavailable: " + ex.Message); }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks, failure }, new JsonSerializerOptions { WriteIndented = true }));
            App.Window.Close();
        }
    }

    private static void Require(bool condition, string description, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description);
    }

    private async Task CaptureSmokeImageAsync(string directory, string name)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(this);
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(directory);
        var file = await folder.CreateFileAsync(name + ".png", CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
