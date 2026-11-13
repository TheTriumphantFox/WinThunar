using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

/// <summary>A program planted in a browsed or current folder must never be the one WinThunar starts.</summary>
public sealed class ExecutableLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _trusted;
    private readonly string _browsed;

    public ExecutableLocatorTests()
    {
        _trusted = Directory.CreateDirectory(Path.Combine(_root, "trusted bin")).FullName;
        _browsed = Directory.CreateDirectory(Path.Combine(_root, "browsed")).FullName;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Plant(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "not a program");
        return path;
    }

    [Fact]
    public void PathSearchSkipsEmptyRelativeAndDriveRelativeEntries()
    {
        var trustedGit = Plant(_trusted, "git.exe");
        Plant(_browsed, "git.exe");
        var relativeBrowsed = Path.GetRelativePath(Environment.CurrentDirectory, _browsed);
        var driveRelative = _browsed[..2] + _browsed[3..];
        Assert.False(Path.IsPathFullyQualified(relativeBrowsed), relativeBrowsed);

        Assert.Null(ExecutableLocator.Search(["git.exe"], $";.;{relativeBrowsed};{driveRelative}"));
        Assert.Equal(trustedGit, ExecutableLocator.Search(["git.exe"], $";{relativeBrowsed};\"{_trusted}\";{_browsed}"));
    }

    [Fact]
    public void FixedHelpersAreFullyQualifiedAndPresent()
    {
        Assert.True(Path.IsPathFullyQualified(ExecutableLocator.PowerShell));
        Assert.True(File.Exists(ExecutableLocator.PowerShell), ExecutableLocator.PowerShell);
        Assert.True(Path.IsPathFullyQualified(ExecutableLocator.Explorer));
        Assert.True(File.Exists(ExecutableLocator.Explorer), ExecutableLocator.Explorer);
    }

    [Fact]
    public void PluginCommandOnPathResolvesToItsFullPath()
    {
        var tool = Plant(_trusted, "mytool.cmd");
        Plant(_browsed, "mytool.cmd");

        // PATHEXT supplies the extension's case; Windows file names ignore it.
        Assert.Equal(tool, ExecutableLocator.ResolveShellCommand("mytool", _browsed, _trusted, ".EXE;.CMD"), ignoreCase: true);
        Assert.Equal(tool, ExecutableLocator.ResolveShellCommand("mytool.cmd", _browsed, _trusted, ".EXE;.CMD"));
    }

    [Fact]
    public void PluginCommandPlantedInBrowsedFolderIsRefused()
    {
        Plant(_browsed, "mytool.exe");

        Assert.Throws<InvalidOperationException>(() => ExecutableLocator.ResolveShellCommand("mytool", _browsed, _trusted, ".EXE;.CMD"));
        Assert.Throws<InvalidOperationException>(() => ExecutableLocator.ResolveShellCommand("mytool.exe", _browsed, _trusted, ".EXE;.CMD"));
        Assert.Throws<InvalidOperationException>(() => ExecutableLocator.ResolveShellCommand(@"tools\mytool.exe", _browsed, _trusted, ".EXE"));
        Assert.Throws<InvalidOperationException>(() => ExecutableLocator.ResolveShellCommand(@".\mytool.exe", _browsed, _trusted, ".EXE"));
    }

    [Fact]
    public void OtherPluginCommandsAreLeftForTheShell()
    {
        var fullPath = Plant(_browsed, "full.exe");

        // Absent everywhere: the shell may still find it through App Paths, and the browsed folder has no match.
        Assert.Equal("chrome", ExecutableLocator.ResolveShellCommand("chrome", _browsed, _trusted, ".EXE"));
        Assert.Equal(fullPath, ExecutableLocator.ResolveShellCommand(fullPath, _browsed, _trusted, ".EXE"));
        Assert.Equal("https://example.invalid/", ExecutableLocator.ResolveShellCommand("https://example.invalid/", _browsed, _trusted, ".EXE"));
    }
}
