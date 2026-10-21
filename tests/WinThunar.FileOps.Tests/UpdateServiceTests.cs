using Velopack;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

[Collection("Updater environment")]
public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.UpdateTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void InvalidFeedDoesNotPreventApplicationStartup()
    {
        var originalFeed = Environment.GetEnvironmentVariable(UpdateService.FeedOverrideVariable);
        try
        {
            Environment.SetEnvironmentVariable(UpdateService.FeedOverrideVariable, "http://[");
            var updates = new UpdateService();

            Assert.Equal(UpdateState.Failed, updates.State);
            Assert.False(string.IsNullOrWhiteSpace(updates.ErrorMessage));
        }
        finally
        {
            Environment.SetEnvironmentVariable(UpdateService.FeedOverrideVariable, originalFeed);
        }
    }

    [Fact]
    public async Task InitializationFailureCanBeRetriedWithoutRestartingTheApplication()
    {
        var backend = new FakeBackend();
        var attempts = 0;
        var updates = Create(() => ++attempts == 1 ? throw new IOException("Updater data is locked") : backend);

        Assert.Equal(UpdateState.Failed, updates.State);
        Assert.True(updates.IsSupported);
        Assert.Equal("Updater data is locked", updates.ErrorMessage);

        await updates.CheckAsync();

        Assert.Equal(2, attempts);
        Assert.Equal(UpdateState.UpToDate, updates.State);
        Assert.Null(updates.ErrorMessage);
        Assert.Equal(1, backend.Checks);
    }

    [Fact]
    public async Task InaccessiblePendingPackageDoesNotLeaveAPartiallyInitializedManager()
    {
        var backend = new FakeBackend { PendingError = new UnauthorizedAccessException("Package is unavailable") };
        var attempts = 0;
        var updates = Create(() => { attempts++; return backend; });
        Assert.Equal(UpdateState.Failed, updates.State);

        backend.PendingError = null;
        backend.Pending = Release();
        await updates.CheckAsync();

        Assert.Equal(2, attempts);
        Assert.Equal(UpdateState.Ready, updates.State);
        Assert.Equal("0.5.3", updates.AvailableVersion);
        Assert.Equal(0, backend.Checks);
    }

    [Fact]
    public async Task PortableCopiesDoNotCheckOrApplyUpdates()
    {
        var backend = new FakeBackend { IsInstalled = false };
        var updates = Create(() => backend);

        await updates.CheckAsync();
        updates.ApplyOnExit();

        Assert.Equal(UpdateState.NotInstalled, updates.State);
        Assert.False(updates.IsSupported);
        Assert.False(updates.ApplyAndRestart());
        Assert.Equal(0, backend.Checks);
        Assert.Equal(0, backend.Restarts);
        Assert.Equal(0, backend.ExitApplications);
    }

    [Fact]
    public async Task ConcurrentChecksShareTheDownloadAndAFailedDownloadCanRetry()
    {
        var feed = new TaskCompletionSource<UpdateInfo?>();
        var backend = new FakeBackend
        {
            Check = () => feed.Task,
            Download = (_, _) => Task.FromException(new IOException("Connection lost"))
        };
        var updates = Create(() => backend);
        var first = updates.CheckAsync();
        Assert.Same(first, updates.CheckAsync());
        Assert.Equal(UpdateState.Checking, updates.State);
        feed.SetResult(new UpdateInfo(Release(), false, null, []));
        await first;

        Assert.Equal(UpdateState.Failed, updates.State);
        Assert.Equal("Connection lost", updates.ErrorMessage);
        Assert.False(updates.ApplyAndRestart());
        Assert.Equal(1, backend.Checks);
        Assert.Equal(1, backend.Downloads);

        backend.Download = (_, _) => Task.CompletedTask;
        await updates.CheckAsync();

        Assert.Equal(UpdateState.Ready, updates.State);
        Assert.Null(updates.ErrorMessage);
        Assert.Equal("0.5.3", updates.AvailableVersion);
        Assert.Equal(2, backend.Checks);
        Assert.Equal(2, backend.Downloads);
        await updates.CheckAsync();
        Assert.Equal(2, backend.Checks);
    }

    [Fact]
    public async Task FailedFeedCheckDoesNotPostponeTheNextAutomaticCheck()
    {
        var backend = new FakeBackend { Check = () => Task.FromException<UpdateInfo?>(new IOException("Offline")) };
        var updates = Create(() => backend);

        await updates.CheckAsync();

        Assert.Equal(UpdateState.Failed, updates.State);
        Assert.Null(updates.Settings.LastCheck);
        Assert.True(updates.Settings.IsCheckDue(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void InstallerLaunchFailureKeepsThePackageReadyForRetry()
    {
        var backend = new FakeBackend { Pending = Release(), ApplyError = new System.ComponentModel.Win32Exception(5) };
        var updates = Create(() => backend);

        Assert.False(updates.ApplyAndRestart());
        Assert.Equal(UpdateState.Ready, updates.State);
        Assert.False(string.IsNullOrWhiteSpace(updates.ErrorMessage));
        Assert.Equal("0.5.3", updates.AvailableVersion);

        backend.ApplyError = null;
        Assert.True(updates.ApplyAndRestart());
        Assert.Null(updates.ErrorMessage);
        Assert.Equal(2, backend.Restarts);
    }

    [Fact]
    public void AllUsersInstallDoesNotLaunchAnInstallerOnExit()
    {
        var backend = new FakeBackend { Pending = Release() };
        var updates = Create(() => backend, requiresAdministrator: true);

        updates.ApplyOnExit();

        Assert.Equal(0, backend.ExitApplications);
        Assert.True(updates.ApplyAndRestart());
        Assert.Equal(1, backend.Restarts);
    }

    [Fact]
    public void ApplyOnExitFailureDoesNotEscapeTheLastWindowClose()
    {
        var backend = new FakeBackend { Pending = Release(), ApplyError = new System.Security.SecurityException("Execution blocked") };
        var updates = Create(() => backend);

        updates.ApplyOnExit();

        Assert.Equal(1, backend.ExitApplications);
        Assert.Equal(UpdateState.Ready, updates.State);
    }

    private UpdateService Create(Func<IUpdateBackend> factory, bool requiresAdministrator = false) =>
        new(factory, new UpdateSettingsStore(Path.Combine(_root, "updates.json")), requiresAdministrator);

    private static VelopackAsset Release() => new() { Version = SemanticVersion.Parse("0.5.3") };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class FakeBackend : IUpdateBackend
    {
        public bool IsInstalled { get; set; } = true;
        public string? CurrentVersion => "0.5.0";
        public VelopackAsset? Pending { get; set; }
        public Exception? PendingError { get; set; }
        public Exception? ApplyError { get; set; }
        public VelopackAsset? UpdatePendingRestart => PendingError is { } error ? throw error : Pending;
        public Func<Task<UpdateInfo?>> Check { get; set; } = () => Task.FromResult<UpdateInfo?>(null);
        public Func<UpdateInfo, Action<int>, Task> Download { get; set; } = (_, _) => Task.CompletedTask;
        public int Checks { get; private set; }
        public int Downloads { get; private set; }
        public int Restarts { get; private set; }
        public int ExitApplications { get; private set; }

        public Task<UpdateInfo?> CheckForUpdatesAsync() { Checks++; return Check(); }
        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress) { Downloads++; return Download(update, progress); }
        public void ApplyAndRestart(VelopackAsset update) { Restarts++; Apply(); }
        public void ApplyOnExit(VelopackAsset update) { ExitApplications++; Apply(); }

        private void Apply()
        {
            if (ApplyError is { } error)
            {
                throw error;
            }
        }
    }
}

[CollectionDefinition("Updater environment", DisableParallelization = true)]
public sealed class UpdaterEnvironmentCollection;
