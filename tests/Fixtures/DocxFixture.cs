using System.IO.Compression;
using System.Text;
using System.Xml;

namespace WinThunar.Testing;

// Generated packages only; no copied Office documents or dependency on an installed editor.
public static class DocxFixture
{
    public static void Write(string path, string body, string mainPart = "word/document.xml", bool strict = false,
        string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
        string? targetMode = null)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var xml = XmlWriter.Create(archive.CreateEntry("_rels/.rels").Open(), new XmlWriterSettings { CloseOutput = true }))
        {
            xml.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            xml.WriteStartElement("Relationship");
            xml.WriteAttributeString("Id", "rId1");
            xml.WriteAttributeString("Type", strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"
                : "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            xml.WriteAttributeString("Target", mainPart);
            if (targetMode is not null) xml.WriteAttributeString("TargetMode", targetMode);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        using (var xml = XmlWriter.Create(archive.CreateEntry("[Content_Types].xml").Open(), new XmlWriterSettings { CloseOutput = true }))
        {
            xml.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            xml.WriteStartElement("Default");
            xml.WriteAttributeString("Extension", "rels");
            xml.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
            xml.WriteEndElement();
            xml.WriteStartElement("Override");
            xml.WriteAttributeString("PartName", "/" + mainPart.TrimStart('/'));
            xml.WriteAttributeString("ContentType", contentType);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        using var writer = new StreamWriter(archive.CreateEntry(mainPart.TrimStart('/'), CompressionLevel.SmallestSize).Open(), new UTF8Encoding(false));
        var ns = strict ? "http://purl.oclc.org/ooxml/wordprocessingml/main" : "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        writer.Write($"<?xml version=\"1.0\" encoding=\"utf-8\"?><w:document xmlns:w=\"{ns}\"><w:body>{body}</w:body></w:document>");
    }
}
