using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace WinThunar.Services;

public sealed class ShellImageService
{
    public async Task<BitmapImage?> GetPreviewAsync(
        string path,
        uint requestedSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
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

    public async Task<BitmapImage?> GetImageAsync(
        string path,
        bool isDirectory,
        bool showContentThumbnails,
        uint requestedSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            StorageItemThumbnail? thumbnail;
            if (isDirectory)
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(path);
                thumbnail = await folder.GetThumbnailAsync(
                    ThumbnailMode.SingleItem,
                    requestedSize,
                    ThumbnailOptions.UseCurrentScale);
            }
            else
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                thumbnail = await file.GetThumbnailAsync(
                    showContentThumbnails && requestedSize >= 48 ? ThumbnailMode.PicturesView : ThumbnailMode.ListView,
                    requestedSize,
                    ThumbnailOptions.UseCurrentScale);
            }

            using (thumbnail)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (thumbnail is null || thumbnail.Size == 0)
                {
                    return null;
                }

                var image = new BitmapImage
                {
                    DecodePixelWidth = (int)requestedSize
                };
                await image.SetSourceAsync(thumbnail);
                cancellationToken.ThrowIfCancellationRequested();
                return image;
            }
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
    }
}
