using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class ShellFileOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinThunar.FileOps.Tests",
        Guid.NewGuid().ToString("N"));

    public ShellFileOperationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void CopiesMovesRenamesCreatesAndDeletesThroughTheWindowsEngine()
    {
        var source = Path.Combine(_root, "item.txt");
        File.WriteAllText(source, "contents");
        var copies = Directory.CreateDirectory(Path.Combine(_root, "copies")).FullName;
        var moved = Directory.CreateDirectory(Path.Combine(_root, "moved")).FullName;

        RunOnSta(() =>
        {
            Assert.True(ShellFileOperation.Run(0, ShellFileOperation.Kind.Copy, [source], copies, false));
            Assert.True(ShellFileOperation.Run(0, ShellFileOperation.Kind.Move, [Path.Combine(copies, "item.txt")], moved, false));
            Assert.True(ShellFileOperation.Rename(0, Path.Combine(moved, "item.txt"), "renamed.txt", false));
            Assert.True(ShellFileOperation.CreateFolder(0, _root, "made", false));
            Assert.True(ShellFileOperation.Run(0, ShellFileOperation.Kind.Delete, [Path.Combine(moved, "renamed.txt")], null, false));
        });

        Assert.Equal("contents", File.ReadAllText(source));
        Assert.False(File.Exists(Path.Combine(copies, "item.txt")));
        Assert.False(File.Exists(Path.Combine(moved, "renamed.txt")));
        Assert.True(Directory.Exists(Path.Combine(_root, "made")));
    }

    [Fact]
    public void CopiesAJunctionThatWinThunarHandsToWindows()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        File.WriteAllText(Path.Combine(target, "inside.txt"), "contents");
        var junction = Path.Combine(_root, "junction");
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", junction, target },
            CreateNoWindow = true,
            UseShellExecute = false
        })!)
        {
            mklink.WaitForExit();
        }

        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        RunOnSta(() => Assert.True(ShellFileOperation.Run(0, ShellFileOperation.Kind.Copy, [junction], destination, false)));

        Assert.True(File.Exists(Path.Combine(destination, "junction", "inside.txt")));
    }

    // The shell copy engine expects a single-threaded apartment, as on WinThunar's UI thread.
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }

    public void Dispose()
    {
        // rmdir removes junctions without following them.
        using var cleanup = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "rmdir", "/s", "/q", _root },
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        cleanup.WaitForExit();
    }
}
