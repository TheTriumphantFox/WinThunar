using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinThunar.Services;

/// <summary>Operates on the link itself. Never enumerates or opens a link target for copying.</summary>
public static class LinkFileSystem
{
    private const uint SymbolicLinkTag = 0xA000000C;
    private const uint MountPointTag = 0xA0000003;

    public static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    public static bool IsDirectory(string path) => File.GetAttributes(path).HasFlag(FileAttributes.Directory);
    public static bool IsReparsePoint(string path) => Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    public static byte[]? ReadLink(string path)
    {
        if (!IsReparsePoint(path)) return null;
        using var handle = Open(path, 0);
        var buffer = new byte[16 * 1024];
        if (!DeviceIoControl(handle, 0x000900A8, null, 0, buffer, buffer.Length, out var count, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var tag = BitConverter.ToUInt32(buffer, 0);
        // Cloud placeholders and other non-name-surrogate tags are ordinary filesystem content.
        if ((tag & 0x20000000) == 0) return null;
        if (tag is not (SymbolicLinkTag or MountPointTag))
            throw new LinkTransferException("This kind of filesystem link is not supported.");
        return buffer[..count];
    }

    public static bool CopyLink(string source, string destination)
    {
        var data = ReadLink(source);
        if (data is null) return false;
        if (Exists(destination)) throw new IOException("The link destination already exists.");
        var directory = IsDirectory(source);
        if (BitConverter.ToUInt32(data, 0) == SymbolicLinkTag)
        {
            // LinkTarget preserves relative targets as well as dangling targets. .NET requests
            // unprivileged creation when Windows Developer Mode permits it.
            var target = (directory ? (FileSystemInfo)new DirectoryInfo(source) : new FileInfo(source)).LinkTarget
                ?? throw new IOException("The symbolic link target could not be read.");
            if (directory) Directory.CreateSymbolicLink(destination, target);
            else File.CreateSymbolicLink(destination, target);
            return true;
        }

        Directory.CreateDirectory(destination);
        try
        {
            using var handle = Open(destination, 0x40000000);
            if (!DeviceIoControl(handle, 0x000900A4, data, data.Length, null, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch
        {
            Directory.Delete(destination); // Only the empty directory we created.
            throw;
        }
        return true;
    }

    public static void Delete(string path)
    {
        if (!Exists(path)) return;
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                foreach (var child in Directory.EnumerateFileSystemEntries(path)) Delete(child);
            Directory.Delete(path);
        }
        else
        {
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            File.Delete(path);
        }
    }

    private static SafeFileHandle Open(string path, uint access)
    {
        var handle = CreateFile(path, access, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[]? input, int inputSize, byte[]? output, int outputSize, out int returned, IntPtr overlapped);
}
