using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class MsiMigrationStateGuardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupStopsForAnyHoldingEntryAndLeavesOriginalAndCompetingStateUntouched(bool fileEntry)
    {
        var root = Path.Combine(Path.GetTempPath(), "WinThunar.MigrationGuard.Tests", Guid.NewGuid().ToString("N"));
        var held = Path.Combine(root, "WinThunar.MsiMigrationState");
        var state = Path.Combine(root, "WinThunar");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "session.json"), "competing state");
        Assert.Null(MsiMigrationStateGuard.GetStartupBlockReason(root));
        if (fileEntry) File.WriteAllText(held, "unexpected holding entry");
        else
        {
            Directory.CreateDirectory(held);
            File.WriteAllText(Path.Combine(held, "session.json"), "original state");
        }
        var message = MsiMigrationStateGuard.GetStartupBlockReason(root);
        Assert.NotNull(message);
        Assert.Contains(held, message);
        Assert.Equal("competing state", File.ReadAllText(Path.Combine(state, "session.json")));
        if (!fileEntry) Assert.Equal("original state", File.ReadAllText(Path.Combine(held, "session.json")));
        // Retain the tiny fixture; never recursively delete a computed path in this regression.
    }
}
