using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinThunar.Models;
using WinThunar.Services;

namespace WinThunar;

public sealed partial class MainPage
{
    private async Task CheckContainerBindingsAsync(string directory, List<string> checks)
    {
        await CheckContainerBindingDeferralAsync(checks);
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "container-binding-files")).FullName;
        File.WriteAllText(Path.Combine(fixture, "first.txt"), "First accessible item");
        File.WriteAllText(Path.Combine(fixture, "second.txt"), "Second accessible item with different metadata");
        foreach (var mode in new[] { BrowserViewMode.Details, BrowserViewMode.Icons, BrowserViewMode.Compact })
        {
            ViewModel.ViewMode = mode;
            await ViewModel.NavigateAsync(fixture);
            await CheckListAsync(ActiveFileList, ViewModel.Entries[0], mode.ToString());
            await ViewModel.SearchAsync("second");
            await CheckListAsync(ActiveFileList, ViewModel.Entries.Single(), mode + " after search");
            await ViewModel.EndSearchAsync();
        }
        await OpenSplitPaneAsync(fixture, false);
        await CheckListAsync(SecondaryFileList, SplitViewModel.Entries[0], "split pane");
        CloseSplitPane();
        ViewModel.ViewMode = BrowserViewMode.Details;
        UpdateLayout();
        FileList.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);

        async Task CheckListAsync(ListViewBase list, FileSystemEntry entry, string phase)
        {
            list.ScrollIntoView(entry);
            UpdateLayout();
            await Task.Delay(100);
            var browser = ReferenceEquals(list, SecondaryFileList) ? SplitViewModel : ViewModel;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            // The real background query can legitimately replace a synthetic Git status.
            // Drain it before testing the binding's PropertyChanged notification.
            while (browser.GitStatusLoading && DateTime.UtcNow < deadline) await Task.Delay(10);
            if (browser.GitStatusLoading) throw new TimeoutException("Background Git status did not finish.");
            await browser.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
            var container = list.ContainerFromItem(entry) as ContentControl
                ?? throw new InvalidOperationException($"Missing {phase} container.");
            Require(container.GetBindingExpression(AutomationProperties.NameProperty) is null,
                $"{phase} accessible name does not use a reflection binding", checks);
            Require(AutomationProperties.GetName(container) == entry.AutomationName,
                $"{phase} accessible name describes the current file and metadata", checks);
            entry.GitStatus = GitFileStatus.Modified;
            await Task.Delay(50);
            Require(AutomationProperties.GetName(container) == entry.AutomationName &&
                AutomationProperties.GetName(container).Contains("Git: Modified"),
                $"{phase} accessible name updates when Git status changes", checks);
            Require(FindSmokeElement<TextBlock>(container, block => block.Text == "M") is not null,
                $"{phase} compiled template updates the visible Git badge", checks);
            entry.GitStatus = GitFileStatus.None;
            var thumbnail = entry.Thumbnail ?? throw new InvalidOperationException("Missing template thumbnail fixture.");
            entry.Thumbnail = null;
            await Task.Delay(50);
            Require(FindSmokeElement<Image>(container, image => image.Source is null && image.Visibility == Visibility.Collapsed) is not null,
                $"{phase} compiled template hides a cleared thumbnail", checks);
            entry.Thumbnail = thumbnail;
            await Task.Delay(50);
            Require(FindSmokeElement<Image>(container, image => ReferenceEquals(image.Source, thumbnail) && image.Visibility == Visibility.Visible) is not null,
                $"{phase} compiled template restores its live thumbnail", checks);
            // Exercise native Content transitions and notifications from both the old
            // and replacement source. No reflection binding may reconnect during layout.
            var replacement = new FileSystemEntry("replacement.txt", "", false, "42 B", "Text file", "", "");
            try
            {
                container.Content = replacement;
                await Task.Delay(50);
                Require(AutomationProperties.GetName(container) == replacement.AutomationName,
                    $"{phase} container content changes cannot leave an old accessible name", checks);
                entry.GitStatus = GitFileStatus.Untracked;
                replacement.GitStatus = GitFileStatus.Added;
                await Task.Delay(50);
                Require(AutomationProperties.GetName(container) == replacement.AutomationName,
                    $"{phase} replacement notifications update the name without stale source updates", checks);
                container.Content = null;
                replacement.GitStatus = GitFileStatus.Modified;
                await Task.Delay(50);
                Require(AutomationProperties.GetName(container) == string.Empty &&
                    container.GetBindingExpression(AutomationProperties.NameProperty) is null,
                    $"{phase} empty container clears its accessible name without a reflection binding", checks);
            }
            finally { container.Content = entry; }
        }
    }

    private async Task CheckContainerBindingDeferralAsync(List<string> checks)
    {
        var first = new FileSystemEntry("queued-first.txt", "", false, "1 B", "Text file", "", "");
        var replacement = new FileSystemEntry("queued-replacement.txt", "", false, "2 B", "Text file", "", "");
        var items = new System.Collections.ObjectModel.ObservableCollection<FileSystemEntry> { first };
        var probe = new ListView { Width = 300, Height = 80, ItemsSource = items, ItemTemplate = FileList.ItemTemplate,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var sawFreshContainer = false;
        var snapshotWithoutBinding = false;
        probe.ContainerContentChanging += FileContainer_Changing;
        probe.ContainerContentChanging += (_, args) =>
        {
            if (sawFreshContainer || args.InRecycleQueue || args.Item is not FileSystemEntry entry) return;
            sawFreshContainer = true;
            snapshotWithoutBinding = args.ItemContainer.GetBindingExpression(AutomationProperties.NameProperty) is null &&
                AutomationProperties.GetName(args.ItemContainer) == entry.AutomationName;
        };
        var root = (Grid)Content;
        Grid.SetRow(probe, 4);
        root.Children.Add(probe);
        try
        {
            UpdateLayout();
            // Supersede the first item before the queued native callback registration.
            items[0] = replacement;
            UpdateLayout();
            ContentControl? container = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                await Task.Delay(20);
                probe.UpdateLayout();
                container = probe.ContainerFromItem(replacement) as ContentControl;
            } while ((container is null || AutomationProperties.GetName(container) != replacement.AutomationName) && DateTime.UtcNow < deadline);
            Require(sawFreshContainer && snapshotWithoutBinding,
                "A fresh native container gets its name without building a binding in the protected callback", checks);
            Require(sawFreshContainer && container is not null &&
                container.GetBindingExpression(AutomationProperties.NameProperty) is null &&
                AutomationProperties.GetName(container) == replacement.AutomationName,
                "Native content tracking names the replacement item without a reflection binding", checks);
        }
        finally { root.Children.Remove(probe); }
    }
}
