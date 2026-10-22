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

    private readonly Func<IUpdateBackend> _createManager;
    private IUpdateBackend? _manager;
    private readonly UpdateSettingsStore _settingsStore;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private VelopackAsset? _ready;
    private Task? _check;
    private bool _automaticChecksStarted;

    public UpdateService() : this(new UpdateSettingsStore())
    {
    }

    // The integration probe uses the real backend with settings inside its owned run directory.
    internal UpdateService(UpdateSettingsStore settingsStore) : this(
        () => new VelopackUpdateBackend(new UpdateManager(CreateSource())),
        settingsStore,
        !IsInstallFolderWritable())
    {
    }

    internal UpdateService(Func<IUpdateBackend> createManager, UpdateSettingsStore settingsStore, bool requiresAdministrator)
    {
        _createManager = createManager;
        _settingsStore = settingsStore;
        RequiresAdministrator = requiresAdministrator;
        Settings = _settingsStore.Load();
        try
        {
            InitializeManager();
        }
        catch (Exception exception)
        {
            // A malformed feed or inaccessible updater data must not prevent opening folders.
            SetFailure(exception);
        }
    }

    public event EventHandler? StateChanged;

    public UpdateSettings Settings { get; }
    public UpdateState State { get; private set; }
    // Failed initialization is retryable; it does not establish that this is a portable copy.
    public bool IsSupported => State != UpdateState.NotInstalled;

    /// <summary>An all-users install in Program Files; installing an update raises a UAC prompt.</summary>
    public bool RequiresAdministrator { get; }
    public string CurrentVersion => _manager?.CurrentVersion ?? typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "unknown";
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
        if (!IsSupported || _automaticChecksStarted)
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
        while (IsSupported && State != UpdateState.Ready && await timer.WaitForNextTickAsync());
    }

    /// <summary>Checks the feed and downloads any newer release. Concurrent callers share one check.</summary>
    public Task CheckAsync()
    {
        if (!IsSupported || State == UpdateState.Ready)
        {
            return Task.CompletedTask;
        }

        return _check is { IsCompleted: false } ? _check : _check = CheckAndDownloadAsync();
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
            _manager.ApplyOnExit(_ready);
        }
        catch (Exception)
        {
            // The package stays downloaded; Velopack applies it when WinThunar next starts.
        }
    }

    /// <summary>Exits immediately, installs the update, and starts the new version.</summary>
    public bool ApplyAndRestart()
    {
        if (_manager is null || _ready is null)
        {
            return false;
        }

        ErrorMessage = null;
        try
        {
            _manager.ApplyAndRestart(_ready);
            return true;
        }
        catch (Exception exception)
        {
            // Keep the downloaded package ready for another attempt. Launch failures must not
            // escape a XAML click handler and terminate the file manager.
            ErrorMessage = exception.Message;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
    }

    private void InitializeManager()
    {
        var manager = _createManager();
        if (!manager.IsInstalled)
        {
            SetState(UpdateState.NotInstalled);
            return;
        }

        // Read pending data before retaining the manager, so a failed initialization can retry.
        var ready = manager.UpdatePendingRestart;
        _manager = manager;
        _ready = ready;
        AvailableVersion = ready?.Version.ToString();
        SetState(ready is null ? UpdateState.Idle : UpdateState.Ready);
    }

    private async Task CheckAndDownloadAsync()
    {
        ErrorMessage = null;
        SetState(UpdateState.Checking);
        try
        {
            if (_manager is null)
            {
                InitializeManager();
                if (_manager is null || _ready is not null)
                {
                    return;
                }

                SetState(UpdateState.Checking);
            }

            var manager = _manager;
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
            SetFailure(exception);
        }
    }

    private void SetFailure(Exception exception)
    {
        ErrorMessage = exception.Message;
        SetState(UpdateState.Failed);
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

    private sealed class VelopackUpdateBackend(UpdateManager manager) : IUpdateBackend
    {
        public bool IsInstalled => manager.IsInstalled;
        public string? CurrentVersion => manager.CurrentVersion?.ToString();
        public VelopackAsset? UpdatePendingRestart => manager.UpdatePendingRestart;
        public Task<UpdateInfo?> CheckForUpdatesAsync() => manager.CheckForUpdatesAsync();
        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress) => manager.DownloadUpdatesAsync(update, progress);
        public void ApplyOnExit(VelopackAsset update) => manager.WaitExitThenApplyUpdates(update, silent: true, restart: false);
        public void ApplyAndRestart(VelopackAsset update) => manager.ApplyUpdatesAndRestart(update);
    }
}

// Keep the real updater behind this boundary so failure tests cannot install packages or exit.
internal interface IUpdateBackend
{
    bool IsInstalled { get; }
    string? CurrentVersion { get; }
    VelopackAsset? UpdatePendingRestart { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress);
    void ApplyOnExit(VelopackAsset update);
    void ApplyAndRestart(VelopackAsset update);
}
