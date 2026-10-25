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
            var deadline = DateTime.UtcNow.AddSeconds(5);
            // The real background query can legitimately replace a synthetic Git status.
            // Drain it before testing the binding's PropertyChanged notification.
            while (browser.GitStatusLoading && DateTime.UtcNow < deadline) await Task.Delay(10);
            if (browser.GitStatusLoading) throw new TimeoutException("Background Git status did not finish.");
            var container = list.ContainerFromItem(entry) as ContentControl
                ?? throw new InvalidOperationException($"Missing {phase} container.");
            var binding = container.GetBindingExpression(AutomationProperties.NameProperty);
            Require(AutomationProperties.GetName(container) == entry.AutomationName,
                $"{phase} accessible name describes the current file and metadata", checks);
            entry.GitStatus = GitFileStatus.Modified;
            await Task.Delay(50);
            Require(AutomationProperties.GetName(container) == entry.AutomationName &&
                AutomationProperties.GetName(container).Contains("Git: Modified"),
                $"{phase} accessible name updates when Git status changes", checks);
            entry.GitStatus = GitFileStatus.None;
            // Exercise the native content transition used during recycling. The name
            // must follow the new object and clear on null without installing a new binding.
            var replacement = new FileSystemEntry("replacement.txt", "", false, "42 B", "Text file", "", "");
            try
            {
                container.Content = replacement;
                await Task.Delay(50);
                Require(AutomationProperties.GetName(container) == replacement.AutomationName,
                    $"{phase} container content changes cannot leave an old accessible name", checks);
                container.Content = null;
                await Task.Delay(50);
                Require(AutomationProperties.GetName(container) == string.Empty &&
                    ReferenceEquals(binding, container.GetBindingExpression(AutomationProperties.NameProperty)),
                    $"{phase} empty container clears its accessible name and reuses its binding", checks);
            }
            finally { container.Content = entry; }
        }
    }
}
