using Microsoft.Win32;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class ShellRegistrationTests : IDisposable
{
    private readonly string _testRoot = $@"Software\WinThunar.Tests\{Guid.NewGuid():N}";
    private readonly string _executable = Path.Combine(Path.GetTempPath(), "WinThunar Test", "WinThunar.exe");

    private string Classes => _testRoot + @"\Classes";

    private ShellRegistrationService Create(string? executable = null) =>
        new(executable ?? _executable, Registry.CurrentUser, Classes, _testRoot + @"\State");

    [Fact]
    public void RegistersAndRemovesOpenVerbWithoutLeavingKeys()
    {
        var service = Create();
        service.SetOpenVerb(true);

        Assert.True(service.IsOpenVerbRegistered);
        Assert.Equal(_executable, service.RegisteredExecutable);
        Assert.Equal($"\"{_executable}\" \"%V\"", Read(@"Drive\shell\WinThunar\command"));
        Assert.Equal("Open in WinThunar", Read(@"Directory\Background\shell\WinThunar", "MUIVerb"));

        service.SetOpenVerb(false);

        Assert.False(service.IsOpenVerbRegistered);
        using var classes = Registry.CurrentUser.OpenSubKey(Classes);
        Assert.True(classes is null || classes.SubKeyCount == 0);
    }

    [Fact]
    public void DefaultFileManagerRestoresPriorValues()
    {
        Write(@"Directory\shell", string.Empty, "previousVerb");
        var service = Create();

        service.SetDefaultFileManager(true);

        Assert.True(service.IsDefaultFileManager);
        Assert.True(service.IsOpenVerbRegistered);
        Assert.Equal("WinThunar", Read(@"Directory\shell"));
        Assert.Equal("WinThunar", Read(@"Drive\shell"));

        service.SetOpenVerb(false);

        Assert.False(service.IsDefaultFileManager);
        Assert.Equal("previousVerb", Read(@"Directory\shell"));
        Assert.Null(Read(@"Drive\shell"));
    }

    [Fact]
    public void DisablingDefaultKeepsTheOpenVerb()
    {
        var service = Create();
        service.SetDefaultFileManager(true);
        service.SetDefaultFileManager(false);

        Assert.True(service.IsOpenVerbRegistered);
        Assert.Null(Read(@"Directory\shell"));
    }

    [Fact]
    public void DisablingDefaultKeepsAnotherAppsNewerClaim()
    {
        var service = Create();
        service.SetDefaultFileManager(true);
        Write(@"Directory\shell", string.Empty, "otherManager");

        service.SetDefaultFileManager(false);

        Assert.Equal("otherManager", Read(@"Directory\shell"));
    }

    [Fact]
    public void RepairsCommandsOnlyWhenTheRegisteredExecutableIsGone()
    {
        Create().SetDefaultFileManager(true);
        var moved = Path.Combine(Path.GetTempPath(), "Moved WinThunar", "WinThunar.exe");

        var repaired = Create(moved).RepairExecutablePath();

        Assert.True(repaired);
        Assert.Equal(moved, Create().RegisteredExecutable);

        // A registered executable that still exists belongs to another copy; leave it alone.
        var existing = typeof(ShellRegistrationTests).Assembly.Location;
        Create(existing).SetOpenVerb(true);
        Assert.False(Create(moved).RepairExecutablePath());
        Assert.Equal(existing, Create().RegisteredExecutable);
    }

    [Fact]
    public void UninstallRemovesOnlyItsOwnRegistration()
    {
        Write(@"Directory\shell", string.Empty, "previousVerb");
        var portable = Path.Combine(Path.GetTempPath(), "Portable WinThunar", "WinThunar.exe");
        Create(portable).SetDefaultFileManager(true);

        Assert.False(Create().UnregisterIfOwned());
        Assert.Equal(portable, Create().RegisteredExecutable);

        Assert.True(Create(portable.ToUpperInvariant()).UnregisterIfOwned());
        Assert.False(Create().IsOpenVerbRegistered);
        Assert.Equal("previousVerb", Read(@"Directory\shell"));
        Assert.False(Create().UnregisterIfOwned());
    }

    private string? Read(string path, string name = "")
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{Classes}\{path}");
        return key?.GetValue(name) as string;
    }

    private void Write(string path, string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{Classes}\{path}");
        key.SetValue(name, value);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_testRoot, false);
        using var parent = Registry.CurrentUser.OpenSubKey(@"Software\WinThunar.Tests");
        if (parent is { SubKeyCount: 0, ValueCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\WinThunar.Tests", false);
        }
    }
}
