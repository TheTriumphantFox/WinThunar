using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class UpdateSettingsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinThunar.FileOps.Tests",
        Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_root, "updates.json");

    [Fact]
    public void ChecksAreDueDailyAndOnlyWhenEnabled()
    {
        Assert.True(new UpdateSettings().IsCheckDue(Now));
        Assert.False(new UpdateSettings { LastCheck = Now.AddHours(-23) }.IsCheckDue(Now));
        Assert.True(new UpdateSettings { LastCheck = Now.AddDays(-1) }.IsCheckDue(Now));
        Assert.False(new UpdateSettings { CheckAutomatically = false }.IsCheckDue(Now));
    }

    [Fact]
    public void ACheckTimeInTheFutureIsDue()
    {
        Assert.True(new UpdateSettings { LastCheck = Now.AddDays(3) }.IsCheckDue(Now));
    }

    [Fact]
    public void SettingsRoundTripAndDefaultWhenMissingOrCorrupt()
    {
        var store = new UpdateSettingsStore(SettingsPath);
        Assert.True(store.Load().CheckAutomatically);

        store.Save(new UpdateSettings { CheckAutomatically = false, LastCheck = Now });
        var loaded = store.Load();
        Assert.False(loaded.CheckAutomatically);
        Assert.Equal(Now, loaded.LastCheck);
        Assert.Single(Directory.GetFiles(_root));

        File.WriteAllText(SettingsPath, "{ not json");
        Assert.True(store.Load().CheckAutomatically);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
