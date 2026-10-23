using System.IO.Compression;
using System.Text;
using System.Xml;

namespace WinThunar.Services;

/// <summary>Reads bounded DOCX main-body text without Office, extraction to disk, or external resources.</summary>
public static class DocxFileContent
{
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public const int MaximumDocumentBytes = 8 * 1024 * 1024;
    public const int MaximumSearchCharacters = 1024 * 1024;
    private const string Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StrictWord = "http://purl.oclc.org/ooxml/wordprocessingml/main";

    public static bool IsDocument(string path) => Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase);
    public static bool Supports(string path, FileAttributes attributes) => IsDocument(path) &&
        (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x440000)) == 0;

    public static bool Contains(string path, IReadOnlyList<string> terms, CancellationToken cancellationToken, out bool skipped)
    {
        skipped = false;
        try
        {
            var text = Read(path, MaximumSearchCharacters, cancellationToken);
            if (text.Truncated) { skipped = true; return false; }
            return terms.All(term => text.Text.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { skipped = true; return false; }
    }

    public static TextFilePreview ReadPreview(string path, CancellationToken cancellationToken = default) =>
        Read(path, TextFileContent.MaximumPreviewCharacters, cancellationToken);

    private static TextFilePreview Read(string path, int characterLimit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Supports(path, File.GetAttributes(path)))
            throw new IOException("DOCX text needs a downloaded document stored on this device.");
        try
        {
            // Close the source before parsing; the snapshot also bounds a file growing during the read.
            using var snapshot = new MemoryStream();
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (source.Length > MaximumFileBytes) throw new IOException("DOCX text is limited to files of 16 MiB or less.");
                CopyBounded(source, snapshot, MaximumFileBytes, cancellationToken);
            }
            snapshot.Position = 0;
            using var archive = new ZipArchive(snapshot, ZipArchiveMode.Read);
            if (archive.Entries.Count > 4096) throw new IOException("This DOCX contains too many package parts.");
            using var relationships = Part(archive, "_rels/.rels", 128 * 1024, cancellationToken);
            string? main = null;
            using (var reader = Xml(relationships, 128 * 1024))
            {
                reader.MoveToContent();
                if (reader.LocalName != "Relationships" || reader.NamespaceURI != Relationships)
                    throw new IOException("This DOCX has invalid package relationships.");
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1 || reader.LocalName != "Relationship" || reader.NamespaceURI != Relationships) continue;
                    if (reader.GetAttribute("Type") is not ("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" or
                        "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument")) continue;
                    if (main is not null || reader.GetAttribute("TargetMode") is not (null or "Internal"))
                        throw new IOException("This DOCX needs one internal main document part.");
                    main = PartName(reader.GetAttribute("Target") ?? "");
                }
            }
            if (main is null) throw new IOException("This DOCX has no main document part.");
            using var types = Part(archive, "[Content_Types].xml", 128 * 1024, cancellationToken);
            var validType = false;
            using (var reader = Xml(types, 128 * 1024))
            {
                reader.MoveToContent();
                if (reader.LocalName != "Types" || reader.NamespaceURI != ContentTypes)
                    throw new IOException("This DOCX has invalid content types.");
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && reader.LocalName == "Override" &&
                        reader.NamespaceURI == ContentTypes && PartName(reader.GetAttribute("PartName") ?? "") == main)
                    {
                        if (validType || reader.GetAttribute("ContentType") != "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")
                            throw new IOException("Only DOCX document body text is supported.");
                        validType = true;
                    }
                }
            }
            if (!validType) throw new IOException("This DOCX has no supported main document content type.");
            using var document = Part(archive, main, MaximumDocumentBytes, cancellationToken);
            using var xml = Xml(document, MaximumDocumentBytes);
            xml.MoveToContent();
            if (xml.LocalName != "document" || xml.NamespaceURI is not (Word or StrictWord))
                throw new IOException("This DOCX has an invalid document body.");
            var wordNamespace = xml.NamespaceURI;
            var text = new StringBuilder();
            var truncated = false;
            var bodyDepth = -1;
            var textDepth = -1;
            var suppressedDepth = -1;
            var hasBody = false;
            void Append(string value)
            {
                var remaining = characterLimit - text.Length;
                text.Append(value.AsSpan(0, Math.Min(remaining, value.Length)));
                truncated |= value.Length > remaining;
            }
            // Validate the entire bounded XML even after the text cap, so malformed tails cannot
            // masquerade as successful previews or content-search matches.
            while (xml.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (xml.Depth > 128) throw new IOException("This DOCX has excessively nested markup.");
                if (suppressedDepth >= 0)
                {
                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == suppressedDepth) suppressedDepth = -1;
                    continue;
                }
                if (xml.NamespaceURI == wordNamespace && xml.LocalName == "body" && xml.Depth == 1)
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (hasBody) throw new IOException("This DOCX has multiple document bodies.");
                        hasBody = true;
                        bodyDepth = xml.IsEmptyElement ? -1 : xml.Depth;
                    }
                    else if (xml.NodeType == XmlNodeType.EndElement) bodyDepth = -1;
                    continue;
                }
                if (bodyDepth < 0) continue;
                if (xml.NodeType == XmlNodeType.Element && xml.NamespaceURI == wordNamespace)
                {
                    if (xml.LocalName is "del" or "moveFrom")
                        suppressedDepth = xml.IsEmptyElement ? -1 : xml.Depth;
                    else if (xml.LocalName == "t" && !xml.IsEmptyElement) textDepth = xml.Depth;
                    else if (xml.LocalName == "tab") Append("\t");
                    else if (xml.LocalName is "br" or "cr") Append("\n");
                }
                else if (xml.NodeType == XmlNodeType.EndElement && xml.NamespaceURI == wordNamespace)
                {
                    if (xml.LocalName == "t") textDepth = -1;
                    else if (xml.LocalName == "p") Append("\n");
                    else if (xml.LocalName == "tc") Append("\t");
                }
                else if (textDepth >= 0 && xml.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace)
                    Append(xml.Value);
            }
            if (!hasBody) throw new IOException("This DOCX has no document body.");
            return new(text.ToString().TrimEnd('\n', '\t'), truncated);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or NotSupportedException or ArgumentException or FormatException)
        { throw new IOException("This DOCX is malformed, encrypted, or uses an unsupported package format.", ex); }
    }

    private static string PartName(string target)
    {
        if (target.Length == 0 || target.IndexOfAny(['\\', ':', '?', '#']) >= 0 || target.StartsWith("//"))
            throw new IOException("This DOCX has an unsupported document-part path.");
        var segments = new List<string>();
        foreach (var encoded in target.TrimStart('/').Split('/'))
        {
            var segment = Uri.UnescapeDataString(encoded);
            if (segment.IndexOfAny(['/', '\\', ':', '?', '#', '\0']) >= 0 || segment.Length == 0)
                throw new IOException("This DOCX has an invalid document-part path.");
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) throw new IOException("This DOCX document-part path leaves the package.");
                segments.RemoveAt(segments.Count - 1);
            }
            else segments.Add(segment);
        }
        if (segments.Count == 0) throw new IOException("This DOCX has an empty document-part path.");
        return string.Join('/', segments);
    }

    private static MemoryStream Part(ZipArchive archive, string name, int limit, CancellationToken cancellationToken)
    {
        var matches = archive.Entries.Where(entry => entry.FullName == name).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Length > limit) throw new IOException("A required DOCX part is missing, duplicated, or too large.");
        var output = new MemoryStream();
        try
        {
            using var input = matches[0].Open();
            CopyBounded(input, output, limit, cancellationToken);
            output.Position = 0;
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private static void CopyBounded(Stream input, Stream output, int limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        int count;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, limit + 1L - output.Length));
            if (count == 0) return;
            if (output.Length + count > limit) throw new IOException("This DOCX or its document markup exceeds the text-reading size limit.");
            output.Write(buffer, 0, count);
        }
    }

    private static XmlReader Xml(Stream stream, int limit) => XmlReader.Create(stream, new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limit,
        IgnoreComments = true, IgnoreProcessingInstructions = true
    });
}
