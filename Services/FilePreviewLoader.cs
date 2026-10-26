using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinThunar.Services;

public abstract record FilePreview;

public sealed record PdfPagePreviewImage(ImageSource Image, uint PageIndex, uint PageCount) : FilePreview;

public sealed record TextFilePreviewContent(TextFilePreview Text, bool IsDocx) : FilePreview;

public sealed record ImageFilePreview(ImageSource? Image) : FilePreview;

public sealed record UnsupportedFilePreview : FilePreview;

/// <summary>
/// Chooses and loads the preview for a selected file. Call on the UI thread: image sources
/// are created there, while file reading and parsing run on the thread pool.
/// </summary>
public sealed class FilePreviewLoader
{
    public const uint ImagePreviewSize = 512;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif", ".bmp", ".gif", ".heic", ".heif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };

    private readonly ShellImageService _shellImageService = new();

    public static bool IsPdf(string path) => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<FilePreview> LoadAsync(string path, uint pdfPageIndex, CancellationToken cancellationToken)
    {
        if (IsPdf(path))
        {
            var page = await Task.Run(() => PdfFilePreview.ReadAsync(path, pdfPageIndex, cancellationToken), cancellationToken);
            using var stream = new MemoryStream(page.Image).AsRandomAccessStream();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            return new PdfPagePreviewImage(bitmap, page.PageIndex, page.PageCount);
        }

        var isDocx = DocxFileContent.IsDocument(path);
        var text = await Task.Run(() => isDocx ? DocxFileContent.ReadPreview(path, cancellationToken)
            : TextFileContent.Supports(path, File.GetAttributes(path))
                ? TextFileContent.ReadPreview(path, cancellationToken) : null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (text is not null)
        {
            return new TextFilePreviewContent(text, isDocx);
        }

        if (!ImageExtensions.Contains(Path.GetExtension(path)))
        {
            return new UnsupportedFilePreview();
        }

        var image = await _shellImageService.GetPreviewAsync(path, ImagePreviewSize, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new ImageFilePreview(image);
    }
}
