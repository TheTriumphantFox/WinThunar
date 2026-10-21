using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

// Only the opt-in provisioning script creates this capability. A normal --second-volume never
// authorizes filling a volume. Recheck the mount/identity/bounds immediately before allocating.
internal sealed record DisposableVolume(string MountPath, string VolumePath, string ImagePath, string Token, string Label)
{
    internal static DisposableVolume Open(string manifestPath)
    {
        var volume = JsonSerializer.Deserialize<DisposableVolume>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("Missing disposable-volume manifest.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        Fixture.Check(Path.GetFullPath(volume.ImagePath) == Path.Combine(parent, "disposable.vhd") && File.Exists(volume.ImagePath),
            "The disposable image must be beside its manifest.");
        Fixture.Check(Path.TrimEndingDirectorySeparator(Path.GetFullPath(volume.MountPath)) == Path.Combine(parent, "volume"),
            "The disposable mount must be in the provisioned run directory.");
        volume.Validate(parent);
        return volume;
    }

    internal ulong Validate(string hostPath)
    {
        Fixture.Check(Guid.TryParseExact(Token, "N", out _) && Label == "WT-" + Token[..12], "Invalid disposable-volume ownership token.");
        var mount = Path.TrimEndingDirectorySeparator(Path.GetFullPath(MountPath)) + Path.DirectorySeparatorChar;
        var actualMount = new StringBuilder(1024);
        if (!GetVolumePathName(mount, actualMount, actualMount.Capacity)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Fixture.Check(string.Equals(mount, actualMount.ToString(), StringComparison.OrdinalIgnoreCase), "The disposable volume is not mounted at its recorded path.");
        var identity = new StringBuilder(1024);
        if (!GetVolumeNameForVolumeMountPoint(mount, identity, identity.Capacity)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Fixture.Check(string.Equals(identity.ToString(), VolumePath, StringComparison.OrdinalIgnoreCase), "Disposable volume identity changed.");
        var label = new StringBuilder(256);
        var fileSystem = new StringBuilder(256);
        if (!GetVolumeInformation(mount, label, label.Capacity, out _, out _, out _, fileSystem, fileSystem.Capacity))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        Fixture.Check(label.ToString() == Label && fileSystem.ToString() == "NTFS", "Disposable volume label/filesystem changed.");
        Fixture.Check(File.ReadAllText(Path.Combine(mount, "disposable-owner.txt")) == Token, "Disposable volume ownership marker changed.");
        Fixture.Check(!FileOperationService.ArePathsOnSameVolume([hostPath], mount), "Refusing to fill the host/source volume.");
        if (!GetDiskFreeSpaceEx(mount, out var available, out var total, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Fixture.Check(total >= 64UL * 1024 * 1024 && total <= 512UL * 1024 * 1024, "Refusing to fill a volume outside the 64–512 MiB disposable size limit.");
        return available;
    }

    internal long Fill(Fixture fixture)
    {
        Fixture.Check(fixture.State.ExternalRoot is { } external &&
            string.Equals(Path.GetDirectoryName(external), Path.TrimEndingDirectorySeparator(MountPath), StringComparison.OrdinalIgnoreCase),
            "The disk-full fixture is outside its disposable mount.");
        var fillerPath = Path.Combine(fixture.State.ExternalRoot!, "capacity-filler.bin");
        Fixture.Check(!LinkFileSystem.IsReparsePoint(fixture.State.ExternalRoot!), "The disk-full fixture cannot be a link.");
        var available = Validate(fixture.Root);
        const ulong reserve = 1024 * 1024;
        Fixture.Check(available > 2 * reserve, "Disposable volume lacks space to prepare a controlled disk-full check.");
        using var filler = new FileStream(fillerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var bytes = new byte[1024 * 1024];
        new Random(91724).NextBytes(bytes); // Allocate real, non-sparse data.
        long written = 0;
        while (available > 2 * reserve)
        {
            Fixture.Check(written < 512L * 1024 * 1024, "Disposable volume allocation exceeded its bound.");
            filler.Write(bytes);
            filler.Flush(flushToDisk: true);
            written += bytes.Length;
            available = Validate(fixture.Root);
        }
        Fixture.Check(available < Fixture.PayloadBytes / 2, "The disposable target still has room for the incoming file.");
        return written;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, int bufferLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(string root, StringBuilder label, int labelSize, out uint serial,
        out uint maxComponent, out uint flags, StringBuilder fileSystem, int fileSystemSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong free);
}
