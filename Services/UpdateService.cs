using Velopack;
using Velopack.Sources;

namespace WinThunar.Services;

public enum UpdateState
{
    /// <summary>A portable or development copy; there is no installation to update.</summary>
    NotInstalled,
    Idle,
    Checking,
    Downloading,
    UpToDate,
    Ready,
    Failed
}

/// <summary>
/// One process-wide updater: every window shares the download, and the update is installed once,
/// after the last window closes. Create it on the UI thread; state changes are raised there.
/// </summary>
public sealed class UpdateService
{
    public const string ReleasesUrl = "https://github.com/TheTriumphantFox/WinThunar/releases";

    /// <summary>A local folder or URL serving a Velopack feed, for testing a release before publishing it.</summary>
    public const string FeedOverrideVariable = "WINTHUNAR_UPDATE_FEED";

    private const string RepositoryUrl = "https://github.com/TheTriumphantFox/WinThunar";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    // A file manager can stay open for days, so the daily check is re-evaluated while it runs.
    private static readonly TimeSpan DueCheckInterval = TimeSpan.FromHours(1);

    private readonly UpdateManager? _manager;
    private readonly UpdateSettingsStore _settingsStore = new();
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private VelopackAsset? _ready;
    private Task? _check;
    private bool _automaticChecksStarted;

    public UpdateService()
    {
        Settings = _settingsStore.Load();
        var manager = new UpdateManager(CreateSource());
        if (manager.IsInstalled)
        {
            _manager = manager;
            _ready = manager.UpdatePendingRestart;
        }

        State = _manager is null ? UpdateState.NotInstalled : _ready is null ? UpdateState.Idle : UpdateState.Ready;
        AvailableVersion = _ready?.Version.ToString();
    }

    public event EventHandler? StateChanged;

    public UpdateSettings Settings { get; }
    public UpdateState State { get; private set; }
    public bool IsSupported => _manager is not null;

    /// <summary>An all-users install in Program Files; installing an update raises a UAC prompt.</summary>
    public bool RequiresAdministrator { get; } = !IsInstallFolderWritable();
    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public string? AvailableVersion { get; private set; }
    public int DownloadProgress { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void SetCheckAutomatically(bool enabled)
    {
        Settings.CheckAutomatically = enabled;
        SaveSettings();
    }

    public async void StartAutomaticChecks()
    {
        if (_manager is null || _automaticChecksStarted)
        {
            return;
        }

        _automaticChecksStarted = true;
        await Task.Delay(StartupDelay);
        using var timer = new PeriodicTimer(DueCheckInterval);
        do
        {
            if (State is not (UpdateState.Ready or UpdateState.Checking or UpdateState.Downloading) &&
                Settings.IsCheckDue(DateTimeOffset.UtcNow))
            {
                await CheckAsync();
            }
        }
        while (State != UpdateState.Ready && await timer.WaitForNextTickAsync());
    }

    /// <summary>Checks the feed and downloads any newer release. Concurrent callers share one check.</summary>
    public Task CheckAsync()
    {
        if (_manager is null || State == UpdateState.Ready)
        {
            return Task.CompletedTask;
        }

        return _check is { IsCompleted: false } ? _check : _check = CheckAndDownloadAsync(_manager);
    }

    /// <summary>
    /// Velopack installs to a root folder holding <c>current\WinThunar.exe</c>; updating replaces
    /// <c>current</c>, so the root must be writable without elevation.
    /// </summary>
    public static bool IsInstallFolderWritable()
    {
        var root = Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))?.FullName;
        if (root is null)
        {
            return true;
        }

        try
        {
            using (File.Create(Path.Combine(root, $".winthunar-write-test-{Guid.NewGuid():N}"), 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Installs the downloaded update after this process exits, without relaunching. An all-users
    /// install is left for the update bar, so closing WinThunar never surprises anyone with UAC.
    /// </summary>
    public void ApplyOnExit()
    {
        if (_manager is null || _ready is null || RequiresAdministrator)
        {
            return;
        }

        try
        {
            _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: false);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The package stays downloaded; Velopack applies it when WinThunar next starts.
        }
    }

    /// <summary>Exits immediately, installs the update, and starts the new version.</summary>
    public void ApplyAndRestart()
    {
        if (_manager is not null && _ready is not null)
        {
            _manager.ApplyUpdatesAndRestart(_ready);
        }
    }

    private async Task CheckAndDownloadAsync(UpdateManager manager)
    {
        ErrorMessage = null;
        SetState(UpdateState.Checking);
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            Settings.LastCheck = DateTimeOffset.UtcNow;
            SaveSettings();
            if (update is null)
            {
                SetState(UpdateState.UpToDate);
                return;
            }

            AvailableVersion = update.TargetFullRelease.Version.ToString();
            DownloadProgress = 0;
            SetState(UpdateState.Downloading);
            await manager.DownloadUpdatesAsync(update, progress => Post(() =>
            {
                DownloadProgress = progress;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }));
            _ready = update.TargetFullRelease;
            SetState(UpdateState.Ready);
        }
        catch (Exception exception)
        {
            // Network, feed, and package failures surface as many exception types; none of them may
            // take down the file manager, and the next check simply tries again.
            ErrorMessage = exception.Message;
            SetState(UpdateState.Failed);
        }
    }

    private static IUpdateSource CreateSource()
    {
        var feed = Environment.GetEnvironmentVariable(FeedOverrideVariable);
        if (string.IsNullOrWhiteSpace(feed))
        {
            return new GithubSource(RepositoryUrl, null, false);
        }

        return Directory.Exists(feed) ? new SimpleFileSource(new DirectoryInfo(feed)) : new SimpleWebSource(feed);
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(Settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unsaved preference or check time only means checking again sooner.
        }
    }

    private void SetState(UpdateState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Post(Action action)
    {
        if (_uiContext is null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }
}
