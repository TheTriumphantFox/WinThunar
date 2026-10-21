using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class AppearanceSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_root, "appearance.json");

    [Theory]
    [InlineData(AppThemePreference.System)]
    [InlineData(AppThemePreference.Light)]
    [InlineData(AppThemePreference.Dark)]
    public void ThemeSurvivesReloadWithoutChangingWindowSession(AppThemePreference theme)
    {
        var store = new AppearanceSettingsStore(SettingsPath);
        Assert.Equal(AppThemePreference.System, store.Load().Theme);
        store.Save(new AppearanceSettings { Theme = theme });
        Assert.Equal(theme, new AppearanceSettingsStore(SettingsPath).Load().Theme);
        Assert.Contains($"\"{theme}\"", File.ReadAllText(SettingsPath));
        new AppSessionService(Path.Combine(_root, "session.json")).Save(new());
        Assert.Equal(theme, store.Load().Theme);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{ invalid")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Theme\":\"Unknown\"}")]
    [InlineData("{\"Theme\":999}")]
    public void MissingOrUnrecognizedThemeFallsBackToSystem(string json)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsPath, json);
        Assert.Equal(AppThemePreference.System, new AppearanceSettingsStore(SettingsPath).Load().Theme);
    }

    [Fact]
    public void FailedReplacementPreservesTheExistingChoiceAndRemovesTemporaryFile()
    {
        var store = new AppearanceSettingsStore(SettingsPath);
        store.Save(new AppearanceSettings { Theme = AppThemePreference.Dark });
        using (var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = Record.Exception(() => store.Save(new AppearanceSettings { Theme = AppThemePreference.Light }));
            Assert.True(failure is IOException or UnauthorizedAccessException);
            Assert.Equal(AppThemePreference.Dark, store.Load().Theme);
            Assert.Single(Directory.GetFiles(_root));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(new AppearanceSettings { Theme = (AppThemePreference)999 }));
        Assert.Equal(AppThemePreference.Dark, store.Load().Theme);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
