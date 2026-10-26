using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinThunar.Services;

/// <summary>
/// Thumbnails and image previews without Windows.Storage's brokered <c>StorageFile</c> calls,
/// which retained impersonation Token handles across long sessions.
/// </summary>
public sealed class ShellImageService
{
    public async Task<ImageSource?> GetPreviewAsync(
        string path,
        uint requestedSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reading an online-only cloud file downloads it. The provider's thumbnail does not.
        if (IsCloudPlaceholder(path))
        {
            return await GetImageAsync(path, false, true, requestedSize, cancellationToken);
        }

        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous);
            using var stream = file.AsRandomAccessStream();
            cancellationToken.ThrowIfCancellationRequested();

            var image = new BitmapImage
            {
                DecodePixelWidth = (int)requestedSize
            };
            await image.SetSourceAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            return image;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Some codecs can only be rendered by the Windows shell thumbnail provider.
            return await GetImageAsync(
                path,
                false,
                true,
                requestedSize,
                cancellationToken);
        }
    }

    public async Task<ImageSource?> GetImageAsync(
        string path,
        bool isDirectory,
        bool showContentThumbnails,
        uint requestedSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Small list sizes show type icons; folders keep the shell's own folder image.
        var iconOnly = !isDirectory && !(showContentThumbnails && requestedSize >= 48);
        ShellBitmapPixels? pixels;
        try
        {
            pixels = await Task.Run(() => ShellThumbnailReader.Read(path, (int)requestedSize, iconOnly), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Inaccessible and transient shell items retain the built-in fallback glyph.
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (pixels is null)
        {
            return null;
        }

        var image = new WriteableBitmap(pixels.Width, pixels.Height);
        pixels.Bgra.CopyTo(image.PixelBuffer);
        image.Invalidate();
        return image;
    }

    private static bool IsCloudPlaceholder(string path)
    {
        const System.IO.FileAttributes RecallOnOpen = (System.IO.FileAttributes)0x00040000;
        const System.IO.FileAttributes RecallOnDataAccess = (System.IO.FileAttributes)0x00400000;
        try
        {
            return (File.GetAttributes(path) & (RecallOnOpen | RecallOnDataAccess | System.IO.FileAttributes.Offline)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
