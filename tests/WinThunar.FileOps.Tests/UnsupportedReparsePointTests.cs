using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class UnsupportedReparsePointTests : IDisposable
{
    // A synthetic third-party name-surrogate tag with its own GUID. No provider or driver is installed.
    private const uint FixtureTag = 0x2000FFFF;
    private static readonly Guid FixtureGuid = new("c69f4a40-9df0-46ae-b7fd-d7d814389763");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.Reparse.Tests", Guid.NewGuid().ToString("N"));
    private string? _link;

    [Theory]
    [InlineData(false, FileTransferMode.Copy, false)]
    [InlineData(false, FileTransferMode.Copy, true)]
    [InlineData(false, FileTransferMode.Move, false)]
    [InlineData(false, FileTransferMode.Move, true)]
    [InlineData(true, FileTransferMode.Copy, false)]
    [InlineData(true, FileTransferMode.Copy, true)]
    [InlineData(true, FileTransferMode.Move, false)]
    [InlineData(true, FileTransferMode.Move, true)]
    public async Task UnknownNameSurrogateIsRefusedWithoutChangingSourceOrDestination(bool directory, FileTransferMode mode, bool replace)
    {
        Directory.CreateDirectory(_root);
        _link = Path.Combine(_root, "unknown");
        if (directory) Directory.CreateDirectory(_link);
        else File.WriteAllText(_link, "original source bytes");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var target = Path.Combine(destination, "unknown");
        if (replace) File.WriteAllText(target, "keep destination bytes");
        SetFixtureTag(_link);

        Assert.True(LinkFileSystem.IsReparsePoint(_link));
        Assert.Throws<LinkTransferException>(() => LinkFileSystem.ReadLink(_link));
        Assert.Throws<LinkTransferException>(() => LinkFileSystem.CopyLink(_link, Path.Combine(destination, "direct")));
        Assert.False(LinkFileSystem.Exists(Path.Combine(destination, "direct")));

        var recovery = new TransferRecoveryService(Path.Combine(_root, "journal"));
        var result = await new FileOperationService(recovery).TransferAsync([_link], destination, mode,
            _ => Task.FromResult(new ConflictResolution(ConflictAction.Replace)));

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.CompletedItems);
        Assert.Empty(result.Transfers);
        Assert.Contains(_link, result.WindowsAssistSources);
        Assert.False(result.WindowsAssistNeedsElevation);
        Assert.True(LinkFileSystem.IsReparsePoint(_link));
        Assert.Equal(replace ? ["unknown"] : Array.Empty<string>(), Directory.GetFileSystemEntries(destination).Select(Path.GetFileName).ToArray());
        if (replace) Assert.Equal("keep destination bytes", File.ReadAllText(target));
        Assert.Empty(recovery.RecoverPending().Errors);

        RemoveFixtureTag(_link);
        if (!directory) Assert.Equal("original source bytes", File.ReadAllText(_link));
        else Assert.Empty(Directory.GetFileSystemEntries(_link));
    }

    private static byte[] Buffer(bool includeData)
    {
        // REPARSE_GUID_DATA_BUFFER: DWORD tag, WORD data length, WORD reserved, GUID, payload.
        var buffer = new byte[includeData ? 25 : 24];
        BitConverter.GetBytes(FixtureTag).CopyTo(buffer, 0);
        BitConverter.GetBytes((ushort)(includeData ? 1 : 0)).CopyTo(buffer, 4);
        FixtureGuid.ToByteArray().CopyTo(buffer, 8);
        if (includeData) buffer[24] = 42;
        return buffer;
    }

    private static SafeFileHandle Open(string path)
    {
        var handle = CreateFile(LinkFileSystem.ToNativePath(path), 0x40000000, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }

    private static void SetFixtureTag(string path)
    {
        using var handle = Open(path);
        var buffer = Buffer(true);
        if (!DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, null, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the synthetic NTFS reparse fixture.");
    }

    private static void RemoveFixtureTag(string path)
    {
        if (!LinkFileSystem.IsReparsePoint(path)) return;
        using var handle = Open(path);
        var buffer = Buffer(false);
        if (!DeviceIoControl(handle, 0x000900AC, buffer, buffer.Length, null, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the fixture's own reparse tag.");
    }

    public void Dispose()
    {
        if (_link is not null) RemoveFixtureTag(_link);
        // Cleanup is confined to this generated GUID directory and never follows a reparse target.
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WinThunar.Reparse.Tests"));
        if (Path.GetDirectoryName(Path.GetFullPath(_root)) != parent || LinkFileSystem.IsReparsePoint(_root))
            throw new IOException("Reparse fixture cleanup ownership changed.");
        LinkFileSystem.Delete(_root);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, byte[]? output, int outputSize, out int returned, IntPtr overlapped);
}
