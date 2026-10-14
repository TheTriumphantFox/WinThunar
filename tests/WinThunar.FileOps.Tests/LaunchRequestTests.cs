using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class LaunchRequestTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinThunar.FileOps.Tests",
        Guid.NewGuid().ToString("N"),
        "with space, comma");

    private readonly string _file;

    public LaunchRequestTests()
    {
        Directory.CreateDirectory(_root);
        _file = Path.Combine(_root, "report, final.txt");
        File.WriteAllText(_file, "contents");
    }

    [Fact]
    public void EmptyArgumentsRequestNothing()
    {
        Assert.True(LaunchRequest.Parse([]).IsEmpty);
        Assert.True(LaunchRequest.Parse([Path.Combine(_root, "missing")]).IsEmpty);
    }

    [Fact]
    public void OpensPlainFolderPaths()
    {
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse([_root]));
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse([_root + Path.DirectorySeparatorChar]));
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse([new Uri(_root).AbsoluteUri]));
    }

    [Fact]
    public void KeepsDriveRootSeparator()
    {
        var drive = Path.GetPathRoot(_root)!;
        Assert.Equal(new LaunchRequest(drive, null), LaunchRequest.Parse([drive]));
    }

    [Fact]
    public void RecoversDriveRootsMangledByTrailingBackslashQuotes()
    {
        var drive = Path.GetPathRoot(_root)!;
        var mangled = LaunchRequest.SplitCommandLine($"\"C:\\WinThunar.exe\" \"{drive}\"")[1];
        Assert.Equal(drive[..2] + "\"", mangled);
        Assert.Equal(new LaunchRequest(drive, null), LaunchRequest.Parse([mangled]));
    }

    [Fact]
    public void OpeningAFileSelectsItInItsFolder()
    {
        Assert.Equal(new LaunchRequest(_root, _file), LaunchRequest.Parse([_file]));
    }

    [Theory]
    [InlineData("/select,{0}")]
    [InlineData("/e,/select,{0}")]
    [InlineData("/select|{0}")]
    [InlineData("/select,|{0}")]
    [InlineData("--select={0}")]
    [InlineData("--select|{0}")]
    public void ParsesExplorerAndLongSelectSwitches(string pattern)
    {
        var arguments = string.Format(pattern, _file).Split('|');
        Assert.Equal(new LaunchRequest(_root, _file), LaunchRequest.Parse(arguments));
    }

    [Fact]
    public void ParsesChainedExplorerSwitchesWithAFolder()
    {
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse([$"/n,{_root}"]));
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse(["/e,", _root]));
        Assert.Equal(new LaunchRequest(_root, null), LaunchRequest.Parse([$"/root,{_root}"]));
    }

    [Fact]
    public void MapsRecycleBinAliases()
    {
        var expected = new LaunchRequest(LaunchRequest.RecycleBinPath, null);
        Assert.Equal(expected, LaunchRequest.Parse(["shell:RecycleBinFolder"]));
        Assert.Equal(expected, LaunchRequest.Parse(["::{645FF040-5081-101B-9F08-08002B30309D}"]));
    }

    [Fact]
    public void SplitsCommandLinesLikeWindows()
    {
        Assert.Equal(
            ["C:\\Program Files\\WinThunar\\WinThunar.exe", "/select,C:\\a b\\c.txt", "x\"y", "C:\\dir\\"],
            LaunchRequest.SplitCommandLine("\"C:\\Program Files\\WinThunar\\WinThunar.exe\" /select,\"C:\\a b\\c.txt\" x\\\"y \"C:\\dir\\\\\""));
    }

    [Fact]
    public void ParsesRedirectedCommandLinesWithOrWithoutTheExecutable()
    {
        const string executable = "C:\\Apps\\WinThunar\\WinThunar.exe";
        var expected = new LaunchRequest(_root, _file);
        Assert.Equal(expected, LaunchRequest.ParseCommandLine($"\"{executable}\" /select,\"{_file}\"", executable));
        Assert.Equal(expected, LaunchRequest.ParseCommandLine($"/select,\"{_file}\"", executable));
        Assert.True(LaunchRequest.ParseCommandLine($"\"{executable}\"", executable).IsEmpty);
    }

    public void Dispose()
    {
        var testRoot = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, true);
        }
    }
}
