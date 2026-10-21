using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WinThunar.Services;

public sealed record PdfPagePreview(byte[] Image, uint PageIndex, uint PageCount);

/// <summary>Renders a single PDF page from a bounded snapshot, without retaining an open file.</summary>
public static class PdfFilePreview
{
    public const int MaximumFileBytes = 64 * 1024 * 1024;
    public const uint MaximumRenderPixels = 1024;

    public static async Task<PdfPagePreview> ReadAsync(string path, uint pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attributes = File.GetAttributes(path);
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x440000)) != 0)
            throw new IOException("PDF preview needs a downloaded PDF file stored on this device.");

        // A fixed snapshot bounds both the initial file and a file growing during the read.
        // The source handle closes before parsing, so browsing pages does not lock the file.
        using var snapshot = new MemoryStream();
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (file.Length > MaximumFileBytes) throw new IOException("PDF preview is limited to files of 64 MiB or less.");
            var buffer = new byte[65536];
            int count;
            while ((count = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaximumFileBytes + 1L - snapshot.Length)), cancellationToken)) != 0)
            {
                if (snapshot.Length + count > MaximumFileBytes) throw new IOException("PDF preview is limited to files of 64 MiB or less.");
                snapshot.Write(buffer, 0, count);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        snapshot.Position = 0;
        using var input = snapshot.AsRandomAccessStream();
        var document = await PdfDocument.LoadFromStreamAsync(input).AsTask(cancellationToken);
        if (document.PageCount == 0) throw new IOException("This PDF has no pages to preview.");
        if (pageIndex >= document.PageCount) throw new IOException("That page is no longer available; select the PDF again.");
        using var page = document.GetPage(pageIndex);
        var size = page.Size;
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width <= 0 || size.Height <= 0)
            throw new IOException("This PDF has an invalid page size.");
        var scale = MaximumRenderPixels / Math.Max(size.Width, size.Height);
        using var output = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(output, new PdfPageRenderOptions
        {
            BitmapEncoderId = BitmapEncoder.PngEncoderId,
            DestinationWidth = Math.Max(1, (uint)Math.Round(size.Width * scale)),
            DestinationHeight = Math.Max(1, (uint)Math.Round(size.Height * scale))
        }).AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Windows PDF destinations are DIPs. Normalize physical pixels on scaled displays too.
        output.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(output).AsTask(cancellationToken);
        using var normalized = new InMemoryRandomAccessStream();
        IRandomAccessStream rendered = output;
        if (Math.Max(decoder.PixelWidth, decoder.PixelHeight) > MaximumRenderPixels)
        {
            var pixelScale = MaximumRenderPixels / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight);
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(normalized, decoder).AsTask(cancellationToken);
            encoder.BitmapTransform.ScaledWidth = Math.Max(1, (uint)Math.Floor(decoder.PixelWidth * pixelScale));
            encoder.BitmapTransform.ScaledHeight = Math.Max(1, (uint)Math.Floor(decoder.PixelHeight * pixelScale));
            await encoder.FlushAsync().AsTask(cancellationToken);
            rendered = normalized;
        }
        rendered.Seek(0);
        using var reader = new DataReader(rendered);
        var length = checked((uint)rendered.Size);
        await reader.LoadAsync(length).AsTask(cancellationToken);
        var image = new byte[length];
        reader.ReadBytes(image);
        return new(image, pageIndex, document.PageCount);
    }
}
