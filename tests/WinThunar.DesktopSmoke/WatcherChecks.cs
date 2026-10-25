namespace WinThunar;

public sealed partial class MainPage
{
    private async Task CheckFolderWatcherAsync(string directory, List<string> checks)
    {
        // CurrentPath still names the old folder during asynchronous navigation. A native
        // event there must not reload that folder and cancel the user's new navigation.
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "watcher-files")).FullName;
        await ViewModel.NavigateAsync(fixture);
        using var watcher = new FileSystemWatcher(fixture);
        var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Created += (_, _) => created.TrySetResult();
        watcher.EnableRaisingEvents = true;
        ViewModel.IsBusy = true;
        try
        {
            File.WriteAllText(Path.Combine(fixture, "during-navigation.txt"), "Native watcher fixture");
            await created.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(700);
            Require(ViewModel.IsBusy && ViewModel.Entries.Count == 0,
                "An old folder's watcher cannot interrupt busy navigation", checks);
        }
        finally { ViewModel.IsBusy = false; }
        File.WriteAllText(Path.Combine(fixture, "after-navigation.txt"), "Native watcher fixture");
        for (var attempt = 0; attempt < 100 && ViewModel.Entries.Count != 2; attempt++) await Task.Delay(30);
        Require(ViewModel.Entries.Count == 2,
            "Native folder watching resumes after navigation is no longer busy", checks);
    }
}
