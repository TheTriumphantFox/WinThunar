using System.Runtime.InteropServices;

namespace WinThunar.Services;

/// <summary>Top-down, premultiplied BGRA pixels ready for a XAML <c>WriteableBitmap</c>.</summary>
public sealed record ShellBitmapPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// Reads shell thumbnails and icons in-process through <c>IShellItemImageFactory</c>.
/// Windows.Storage's WinRT <c>StorageFile</c> path duplicated an impersonation token on each
/// brokered call and kept many of them, so long browsing sessions accumulated Token handles.
/// </summary>
public static class ShellThumbnailReader
{
    private const int ResizeToFit = 0x0;
    private const int IconOnly = 0x4;
    private static readonly Guid ImageFactoryId = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    /// <summary>Returns null when the shell has no image for the item or it is inaccessible.</summary>
    public static ShellBitmapPixels? Read(string path, int size, bool iconOnly)
    {
        var interfaceId = ImageFactoryId;
        if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref interfaceId, out var factory) < 0 || factory == IntPtr.Zero)
        {
            return null;
        }

        var bitmap = IntPtr.Zero;
        try
        {
            // IShellItemImageFactory::GetImage is the fourth vtable slot, after IUnknown.
            var getImage = Marshal.GetDelegateForFunctionPointer<GetImageMethod>(
                Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), 3 * IntPtr.Size));
            if (getImage(factory, new NativeSize { Width = size, Height = size }, iconOnly ? IconOnly : ResizeToFit, out bitmap) < 0 ||
                bitmap == IntPtr.Zero)
            {
                return null;
            }

            return CopyPixels(bitmap);
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            Marshal.Release(factory);
        }
    }

    private static ShellBitmapPixels? CopyPixels(IntPtr bitmap)
    {
        if (GetObject(bitmap, Marshal.SizeOf<NativeBitmap>(), out var info) == 0 || info.Width <= 0 || info.Height == 0)
        {
            return null;
        }

        var width = info.Width;
        var height = Math.Abs(info.Height);
        var header = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height, // Negative height requests top-down rows, matching XAML.
            Planes = 1,
            BitCount = 32
        };
        var pixels = new byte[width * height * 4];
        var screen = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(screen, bitmap, 0, (uint)height, pixels, ref header, 0) != height)
            {
                return null;
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screen);
        }

        NormalizeAlpha(pixels);
        return new ShellBitmapPixels(width, height, pixels);
    }

    // Shell bitmaps arrive in three forms: opaque images with an unused zero alpha channel,
    // premultiplied icons, and occasionally straight alpha. WriteableBitmap needs premultiplied.
    internal static void NormalizeAlpha(byte[] pixels)
    {
        var anyAlpha = false;
        var straight = false;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var alpha = pixels[offset + 3];
            anyAlpha |= alpha != 0;
            straight |= pixels[offset] > alpha || pixels[offset + 1] > alpha || pixels[offset + 2] > alpha;
        }

        if (!anyAlpha)
        {
            for (var offset = 3; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = 255;
            }
        }
        else if (straight)
        {
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                var alpha = pixels[offset + 3];
                pixels[offset] = (byte)(pixels[offset] * alpha / 255);
                pixels[offset + 1] = (byte)(pixels[offset + 1] * alpha / 255);
                pixels[offset + 2] = (byte)(pixels[offset + 2] * alpha / 255);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetImageMethod(IntPtr self, NativeSize size, int flags, out IntPtr bitmap);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IntPtr item);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(IntPtr bitmap, int size, out NativeBitmap info);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
}
