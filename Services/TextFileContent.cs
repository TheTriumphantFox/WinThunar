using System.Text;

namespace WinThunar.Services;

public sealed record TextFilePreview(string Text, bool Truncated);

/// <summary>Bounded local text reads and content search, including DOCX body search. Excludes placeholders and file links.</summary>
public static class TextFileContent
{
    public const long MaximumSearchBytes = 16 * 1024 * 1024;
    public const int MaximumPreviewCharacters = 32 * 1024;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".config",
        ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx", ".cs", ".csproj", ".props", ".targets", ".sln", ".slnx",
        ".xaml", ".py", ".ps1", ".psm1", ".psd1", ".sh", ".bat", ".cmd", ".sql", ".c", ".h", ".cpp", ".hpp", ".rs", ".go", ".java"
    };

    public static bool Supports(string path, FileAttributes attributes) => Extensions.Contains(Path.GetExtension(path)) &&
        (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x440000)) == 0;

    public static bool Contains(string path, IReadOnlyList<string> terms, CancellationToken cancellationToken, out bool skipped)
    {
        skipped = false;
        if (terms.Count == 0) return true;
        if (DocxFileContent.IsDocument(path)) return DocxFileContent.Contains(path, terms, cancellationToken, out skipped);
        try
        {
            var attributes = File.GetAttributes(path);
            if (!Supports(path, attributes) || new FileInfo(path).Length > MaximumSearchBytes) { skipped = true; return false; }
            using var stream = Open(path);
            if (stream.Length > MaximumSearchBytes) { skipped = true; return false; }
            using var reader = Reader(stream);
            var buffer = new char[8192];
            var found = new bool[terms.Count];
            var tail = string.Empty;
            var tailLength = terms.Max(term => term.Length) - 1;
            long characters = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = reader.Read(buffer, 0, buffer.Length);
                if (count == 0) return found.All(value => value);
                characters += count;
                // Bound a file that grows while it is being searched as well as its initial size.
                if (characters > MaximumSearchBytes || stream.Position > MaximumSearchBytes || buffer.AsSpan(0, count).Contains('\0'))
                { skipped = true; return false; }
                var text = tail + new string(buffer, 0, count);
                for (var index = 0; index < terms.Count; index++)
                    found[index] |= text.Contains(terms[index], StringComparison.OrdinalIgnoreCase);
                if (found.All(value => value)) return true;
                tail = text[^Math.Min(tailLength, text.Length)..];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        { skipped = true; return false; }
    }

    public static TextFilePreview ReadPreview(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Supports(path, File.GetAttributes(path))) throw new IOException("Text preview is unavailable for this file or online-only item.");
        using var stream = Open(path);
        using var reader = Reader(stream);
        var text = new StringBuilder();
        var buffer = new char[4096];
        while (text.Length <= MaximumPreviewCharacters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = reader.Read(buffer, 0, Math.Min(buffer.Length, MaximumPreviewCharacters + 1 - text.Length));
            if (count == 0) return new(text.ToString(), false);
            if (buffer.AsSpan(0, count).Contains('\0')) throw new IOException("This file contains binary data; text preview is unavailable.");
            text.Append(buffer, 0, count);
        }
        return new(text.ToString(0, MaximumPreviewCharacters), true);
    }

    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
    private static StreamReader Reader(Stream stream) => new(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
}
