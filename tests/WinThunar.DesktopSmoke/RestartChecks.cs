using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Velopack;
using WinThunar.Models;
using WinThunar.Services;

namespace WinThunar;

// Test-only replacement. The normal application never exposes an updater setter.
public partial class App
{
    internal static UpdateService SwapSmokeUpdater(UpdateService replacement)
    {
        var previous = Updates;
        Updates = replacement;
        return previous;
    }
}

public sealed partial class MainPage
{
    private async Task CheckRestartSessionAsync(string directory, List<string> checks)
    {
        var first = Directory.CreateDirectory(Path.Combine(directory, "restart-first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(directory, "restart-second")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(directory, "restart-other-window")).FullName;
        var store = new AppSessionService(Path.Combine(directory, "session.json"));
        var backend = new SmokeRestartBackend(() => store.Load());
        var updates = new UpdateService(() => backend,
            new UpdateSettingsStore(Path.Combine(directory, "restart-update-settings.json")), false);
        var original = App.SwapSmokeUpdater(updates);
        MainWindow? firstWindow = null, otherWindow = null;
        var releaseHistory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? pendingHistory = null;
        try
        {
            firstWindow = (MainWindow)App.OpenWindow(LaunchRequest.Empty);
            otherWindow = (MainWindow)App.OpenWindow(LaunchRequest.Empty);
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
                page.ApplyUpdateState();
                page.UpdateLayout();
            }
            await Configure(firstWindow.Page!, [first, second]);
            await Configure(otherWindow.Page!, [other]);
            Require(store.Load().Tabs.SequenceEqual([other]), "Restart fixture reproduces a secondary window replacing the stored session", checks);
            static void Invoke(Button button)
            {
                var peer = new ButtonAutomationPeer(button);
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            }
            firstWindow.Page!._history.Push(new FileHistoryEntry("restart guard fixture", () => releaseHistory.Task, () => Task.CompletedTask));
            pendingHistory = firstWindow.Page._history.UndoAsync();
            Invoke(otherWindow.Page!.RestartToUpdateButton);
            await Task.Delay(100);
            Require(backend.Restarts == 0 && store.Load().Tabs.SequenceEqual([other]),
                "Restart Now waits for another window's history operation without changing the stored session", checks);
            releaseHistory.TrySetResult();
            await pendingHistory;
            pendingHistory = null;
            Invoke(firstWindow.Page.RestartToUpdateButton);
            await Task.Delay(100);
            File.WriteAllText(Path.Combine(directory, "restart-session-observation.json"),
                System.Text.Json.JsonSerializer.Serialize(new { backend.Restarts, backend.SessionAtRestart }));
            Require(backend.Restarts == 1 && backend.SessionAtRestart is { ActiveTabIndex: 1, ViewMode: BrowserViewMode.Compact } session &&
                session.Tabs.SequenceEqual([first, second]) && session.LastPath == second,
                "Restart Now persists the invoking window's tabs, active folder and view before the updater exits", checks);
        }
        finally
        {
            releaseHistory.TrySetResult();
            if (pendingHistory is not null) await pendingHistory;
            App.SwapSmokeUpdater(original);
            otherWindow?.Close();
            firstWindow?.Close();
        }
    }

    private sealed class SmokeRestartBackend(Func<AppSessionState> readSession) : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string CurrentVersion => "0.5.11";
        public VelopackAsset UpdatePendingRestart { get; } = new() { Version = SemanticVersion.Parse("0.5.12") };
        public int Restarts { get; private set; }
        public AppSessionState? SessionAtRestart { get; private set; }
        public Task<UpdateInfo?> CheckForUpdatesAsync() => throw new InvalidOperationException("No real update check is allowed in this regression.");
        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress) => throw new InvalidOperationException("No download is allowed in this regression.");
        public void ApplyOnExit(VelopackAsset update) => throw new InvalidOperationException("No native updater is allowed in this regression.");
        public void ApplyAndRestart(VelopackAsset update)
        {
            Restarts++;
            SessionAtRestart = readSession();
        }
    }
}
