using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinThunar.Services;

namespace WinThunar;

public sealed partial class MainPage
{
    private TransferRecoveryResult _recoveryResult = new(0, []);
    private bool _recoveryRunning;
    private bool _recoveryDialogOpen;
    private ContentDialog? _recoveryDialog;

    private async Task RecoverTransfersAsync()
    {
        if (_recoveryRunning) return;
        _recoveryRunning = true;
        try
        {
            // Storage providers can block while disconnected. Keep folder browsing responsive.
            _recoveryResult = await Task.Run(() => _fileOperations.RecoverInterruptedTransfers(skipActiveProcesses: true));
            if (_disposed) return;
            RecoveryInfoBar.IsOpen = _recoveryResult.Errors.Count > 0;
            RecoveryInfoBar.Message = $"{_recoveryResult.Errors.Count} recovery issue(s). Review the retained files and retry when the storage is available.";
            if (_recoveryResult.RecoveredOperations > 0 || _recoveryResult.Errors.Count > 0)
                ViewModel.StatusText = RecoverySummary(_recoveryResult);
        }
        catch (Exception ex)
        {
            _recoveryResult = new(0, [ex.Message]);
            if (!_disposed)
            {
                RecoveryInfoBar.Message = ex.Message;
                RecoveryInfoBar.IsOpen = true;
            }
        }
        finally { _recoveryRunning = false; }
    }

    private static string RecoverySummary(TransferRecoveryResult result) => result.RecoveryBusy
        ? "Another window is recovering interrupted transfers. Try again when it finishes."
        : $"Recovered {result.RecoveredOperations} interrupted {(result.RecoveredOperations == 1 ? "transfer" : "transfers")}. " +
          (result.Errors.Count == 0 ? "No recovery issues need attention." : $"{result.Errors.Count} {(result.Errors.Count == 1 ? "issue needs" : "issues need")} attention.") +
          (result.ActiveOperations > 0 ? " Active transfers and Undo data were left alone." : "");

    private static string RecoveryDetails(TransferRecoveryResult result)
    {
        var text = new System.Text.StringBuilder(RecoverySummary(result));
        foreach (var issue in result.Issues)
        {
            text.AppendLine().AppendLine().AppendLine(issue.Message);
            foreach (var (label, path) in new[] { ("Source", issue.SourcePath), ("Destination", issue.DestinationPath),
                         ("Incoming data", issue.StagedPath), ("Previous destination", issue.BackupPath),
                         ("Retained source", issue.SourceRecoveryPath), ("Recovery record", issue.JournalPath) })
                if (!string.IsNullOrEmpty(path)) text.AppendLine($"{label}: {path}");
        }
        if (result.Issues.Count == 0)
            foreach (var error in result.Errors) text.AppendLine().AppendLine(error);
        return text.ToString();
    }

    private async void InterruptedTransfers_Click(object sender, RoutedEventArgs e) => await ShowRecoveryDialogAsync();

    private async Task ShowRecoveryDialogAsync()
    {
        if (_recoveryDialogOpen) return;
        _recoveryDialogOpen = true;
        try
        {
            var panel = new StackPanel { Spacing = 12, MaxWidth = 650 };
            panel.Children.Add(new TextBlock
            {
                Text = "Reconnect unavailable drives or network shares and close apps that lock these files, then retry. " +
                       "Conflicting copies stay preserved for review. Keep the retained data and recovery records until the issue is resolved.",
                TextWrapping = TextWrapping.Wrap
            });
            var details = new TextBlock { Text = RecoveryDetails(_recoveryResult), IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap, MinWidth = 300 };
            AutomationProperties.SetName(details, "Interrupted transfer details");
            panel.Children.Add(new ScrollViewer { Content = details, Height = 240, MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            var retry = new Button { Content = "Retry recovery" };
            AutomationProperties.SetName(retry, "Retry interrupted transfer recovery");
            panel.Children.Add(retry);
            retry.Click += async (_, _) =>
            {
                if (App.HasPendingTransfers)
                {
                    details.Text = "Finish the running transfers, Undo/Redo, or recovery before retrying.\n\n" + RecoveryDetails(_recoveryResult);
                    return;
                }
                retry.IsEnabled = false;
                details.Text = "Checking interrupted transfers...";
                try
                {
                    await RecoverTransfersAsync();
                    details.Text = RecoveryDetails(_recoveryResult);
                }
                finally { retry.IsEnabled = true; }
            };
            _recoveryDialog = new ContentDialog { Title = "Interrupted Transfers", Content = panel,
                CloseButtonText = "Close", XamlRoot = XamlRoot };
            await _recoveryDialog.ShowAsync();
        }
        finally { _recoveryDialog = null; _recoveryDialogOpen = false; }
    }
}
