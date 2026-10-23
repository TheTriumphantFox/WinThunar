using System.IO.Compression;
using WinThunar.Services;
using WinThunar.Testing;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class DocxContentTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "WinThunar.DocxTests", Guid.NewGuid().ToString("N"))).FullName;
    private string Document(string body, string name = "report.docx")
    {
        var path = Path.Combine(_root, name);
        DocxFixture.Write(path, body);
        return path;
    }
    private static string Paragraph(string text) => $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";
    private static void ReplacePart(string path, string part, string contents)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.GetEntry(part)!.Delete();
        using var writer = new StreamWriter(archive.CreateEntry(part).Open());
        writer.Write(contents);
    }

    [Theory]
    [InlineData(false, "word/document.xml")]
    [InlineData(false, "/word/document.xml")]
    [InlineData(true, "content/main.xml")]
    public void ReadsUnicodeRunsParagraphsTablesAndBreaks(bool strict, string mainPart)
    {
        var path = Path.Combine(_root, "report.docx");
        DocxFixture.Write(path, "<w:p><w:r><w:t>Annual </w:t></w:r><w:r><w:t>report &amp; José</w:t><w:tab/><w:t>✓</w:t><w:br/><w:t>Next</w:t></w:r></w:p>" +
            "<w:tbl><w:tr><w:tc>" + Paragraph("Cell one") + "</w:tc><w:tc>" + Paragraph("Cell two") + "</w:tc></w:tr></w:tbl>", mainPart, strict);
        var preview = DocxFileContent.ReadPreview(path);
        Assert.StartsWith("Annual report & José\t✓\nNext\n", preview.Text);
        Assert.Contains("Cell one", preview.Text);
        Assert.Contains("Cell two", preview.Text);
        Assert.False(preview.Truncated);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    [Fact]
    public void SearchesCurrentBodyAcrossRunsWithoutOfficeOrAnIndex()
    {
        var path = Document("<w:p><w:r><w:t>annual </w:t></w:r><w:r><w:t>report approved by José</w:t></w:r></w:p>");
        var matches = new List<FileSearchMatch>();
        var service = new FileSearchService();
        var summary = service.Search(_root, "type:docx content:\"ANNUAL REPORT\" content:José", false, batch => matches.AddRange(batch));
        Assert.Single(matches);
        Assert.False(summary.UsedWindowsIndex);
        Assert.Equal(0, summary.SkippedContentFiles);
        File.Delete(path);
        DocxFixture.Write(path, Paragraph("New current bytes"));
        Assert.False(TextFileContent.Contains(path, ["annual report"], default, out var skipped));
        Assert.False(skipped);
        Assert.True(TextFileContent.Contains(path, ["current bytes"], default, out skipped));
        Assert.False(skipped);
    }

    [Fact]
    public void IgnoresDeletedContentInstructionsAndUnrelatedParts()
    {
        var path = Document(Paragraph("Visible") + "<w:del>" + Paragraph("Deleted needle") + "</w:del><w:moveFrom>" + Paragraph("Moved needle") +
            "</w:moveFrom><w:p><w:r><w:instrText>Secret instruction</w:instrText><w:t>Field result</w:t></w:r></w:p>");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(archive.CreateEntry("word/header1.xml").Open())) writer.Write("Unrelated needle");
        Assert.Equal("Visible\nField result", DocxFileContent.ReadPreview(path).Text);
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.False(skipped);
    }

    [Fact]
    public void PreviewCapDoesNotLimitSearchAndSearchReportsItsOwnCap()
    {
        var path = Document(Paragraph(new string('a', TextFileContent.MaximumPreviewCharacters + 100) + "needle"));
        var preview = DocxFileContent.ReadPreview(path);
        Assert.Equal(TextFileContent.MaximumPreviewCharacters, preview.Text.Length);
        Assert.True(preview.Truncated);
        Assert.True(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.False(skipped);
        var large = Document(Paragraph(new string('a', DocxFileContent.MaximumSearchCharacters + 1)), "large.docx");
        Assert.False(DocxFileContent.Contains(large, ["aaa"], default, out skipped));
        Assert.True(skipped);
    }

    [Theory]
    [InlineData("document")]
    [InlineData("package")]
    [InlineData("entries")]
    public void RejectsSizeAndPartCountLimits(string limit)
    {
        var path = limit == "document" ? Document(Paragraph(new string('x', DocxFileContent.MaximumDocumentBytes))) : Document(Paragraph("needle"));
        if (limit == "package")
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Write);
            file.SetLength(DocxFileContent.MaximumFileBytes + 1L);
        }
        if (limit == "entries")
        {
            using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
            for (var i = 0; i < 4096; i++) archive.CreateEntry($"extra/{i}");
        }
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.True(skipped);
    }

    [Theory]
    [InlineData("not a zip")]
    [InlineData("<wrong/>")]
    [InlineData("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>needle</w:t></w:r></w:p></w:body>")]
    [InlineData("<!DOCTYPE document [<!ENTITY external SYSTEM 'file:///not-read'>]><document>&external;</document>")]
    public void MalformedOrDtdContentCannotProduceASearchMatch(string contents)
    {
        var path = Document(Paragraph("fixture"));
        ReplacePart(path, "word/document.xml", contents);
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.True(skipped);
    }

    [Theory]
    [InlineData("../outside.xml", null)]
    [InlineData("https://example.invalid/document.xml", "External")]
    [InlineData("word/document.xml", "External")]
    [InlineData("word/%2e%2e/%2e%2e/outside.xml", null)]
    public void RejectsExternalOrEscapingMainPartTargets(string target, string? mode)
    {
        var path = Path.Combine(_root, "bad.docx");
        DocxFixture.Write(path, Paragraph("needle"), target, targetMode: mode);
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
    }

    [Fact]
    public void RejectsMissingDuplicateMacroAndNonZipDocuments()
    {
        var missing = Document(Paragraph("fixture"), "missing.docx");
        using (var archive = ZipFile.Open(missing, ZipArchiveMode.Update)) archive.GetEntry("word/document.xml")!.Delete();
        var duplicate = Document(Paragraph("fixture"), "duplicate.docx");
        using (var archive = ZipFile.Open(duplicate, ZipArchiveMode.Update)) archive.CreateEntry("word/document.xml");
        var macro = Path.Combine(_root, "macro.docx");
        DocxFixture.Write(macro, Paragraph("fixture"), contentType: "application/vnd.ms-word.document.macroEnabled.main+xml");
        var encrypted = Path.Combine(_root, "encrypted.docx");
        File.WriteAllBytes(encrypted, [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1]);
        foreach (var path in new[] { missing, duplicate, macro, encrypted }) Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
    }

    [Theory]
    [InlineData(FileAttributes.ReparsePoint)]
    [InlineData(FileAttributes.Offline)]
    [InlineData((FileAttributes)0x40000)]
    [InlineData((FileAttributes)0x400000)]
    [InlineData(FileAttributes.Directory)]
    public void ExcludesLinksPlaceholdersAndDirectories(FileAttributes attributes) => Assert.False(DocxFileContent.Supports("test.docx", attributes));

    [Fact]
    public void CancellationPropagatesToPreviewAndSearch()
    {
        var path = Document(Paragraph("needle"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DocxFileContent.ReadPreview(path, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => TextFileContent.Contains(path, ["needle"], cancellation.Token, out _));
    }

    [Fact]
    public void LockedInputIsSkippedAndReadingNeverChangesPackageBytes()
    {
        var path = Document(Paragraph("needle"));
        var before = File.ReadAllBytes(path);
        Assert.Contains("needle", DocxFileContent.ReadPreview(path).Text);
        Assert.True(DocxFileContent.Contains(path, ["needle"], default, out _));
        Assert.Equal(before, File.ReadAllBytes(path));
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.True(skipped);
    }

    [Fact]
    public void ExcessiveMarkupDepthIsRefused()
    {
        var path = Document(string.Concat(Enumerable.Repeat("<w:sdt>", 129)) + Paragraph("needle") + string.Concat(Enumerable.Repeat("</w:sdt>", 129)));
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.True(skipped);
    }

    [Fact]
    public void MalformedTailAfterPreviewLimitIsStillRejected()
    {
        var path = Document(Paragraph("fixture"));
        ReplacePart(path, "word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            Paragraph(new string('a', TextFileContent.MaximumPreviewCharacters + 100) + "needle") + "</w:body>");
        Assert.Throws<IOException>(() => DocxFileContent.ReadPreview(path));
        Assert.False(DocxFileContent.Contains(path, ["needle"], default, out var skipped));
        Assert.True(skipped);
    }

    public void Dispose()
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WinThunar.DocxTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(_root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        Directory.Delete(_root, true);
    }
}
