using System.Text;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class TextContentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));

    public TextContentTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public void TextSearchFindsPhrasesAcrossReadBoundariesInSupportedEncodings(string encodingName)
    {
        var path = Path.Combine(_root, "report.txt");
        var encoding = encodingName switch { "utf16le" => Encoding.Unicode, "utf16be" => Encoding.BigEndianUnicode, _ => new UTF8Encoding(false) };
        File.WriteAllText(path, new string('x', 8190) + "annual REPORT\nApproved by José", encoding);
        var matches = new List<FileSearchMatch>();

        var summary = new FileSearchService().Search(_root, "name:report type:txt content:\"annual report\" content:José", false,
            batch => matches.AddRange(batch));

        Assert.Equal(path, Assert.Single(matches).FullPath);
        Assert.Equal(1, summary.ResultCount);
        Assert.Equal(0, summary.SkippedContentFiles);
        Assert.Contains("Approved by José", TextFileContent.ReadPreview(path).Text);
    }

    [Fact]
    public void ContentTermsMustAllMatchAndNeverActAsFilenameTerms()
    {
        File.WriteAllText(Path.Combine(_root, "notes.md"), "annual report has been approved");
        File.WriteAllText(Path.Combine(_root, "annual report approved.md"), "unrelated text");
        var matches = new List<FileSearchMatch>();
        new FileSearchService().Search(_root, "type:md content:\"annual report\" content:approved", false, batch => matches.AddRange(batch));
        Assert.Equal("notes.md", Assert.Single(matches).Name);
        matches.Clear();
        new FileSearchService().Search(_root, "type:md content:annual content:missing", false, batch => matches.AddRange(batch));
        Assert.Empty(matches);
        Assert.Throws<FormatException>(() => FileSearchQuery.Parse("content:"));
        Assert.Throws<FormatException>(() => FileSearchQuery.Parse("content:" + new string('x', 1025)));
    }

    [Fact]
    public void ContentSearchCountsBinaryInvalidUnsupportedLockedAndOversizedFilesAsSkipped()
    {
        File.WriteAllBytes(Path.Combine(_root, "binary.txt"), [0, 1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_root, "invalid.txt"), [0xff, 0xff, 0xff]);
        File.WriteAllText(Path.Combine(_root, "document.pdf"), "needle");
        using (var large = File.Create(Path.Combine(_root, "large.txt"))) large.SetLength(TextFileContent.MaximumSearchBytes + 1);
        var lockedPath = Path.Combine(_root, "locked.txt");
        File.WriteAllText(lockedPath, "needle");
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var summary = new FileSearchService().Search(_root, "content:needle", false, _ => throw new InvalidOperationException("Unexpected content match"));
        Assert.Equal(0, summary.ResultCount);
        Assert.Equal(5, summary.SkippedContentFiles);
        Assert.Throws<IOException>(() => TextFileContent.ReadPreview(Path.Combine(_root, "binary.txt")));
    }

    [Fact]
    public void FilenameSearchDoesNotOpenLockedFileContents()
    {
        var path = Path.Combine(_root, "locked.txt");
        File.WriteAllText(path, "private contents");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var summary = new FileSearchService().Search(_root, "locked type:txt", false, _ => { });
        Assert.Equal(1, summary.ResultCount);
        Assert.Equal(0, summary.SkippedContentFiles);
    }

    [Fact]
    public void PreviewIsReadOnlyBoundedAndHonorsCancellation()
    {
        var path = Path.Combine(_root, "notes.txt");
        var original = new string('a', TextFileContent.MaximumPreviewCharacters + 1000);
        File.WriteAllText(path, original);
        var preview = TextFileContent.ReadPreview(path);
        Assert.True(preview.Truncated);
        Assert.Equal(TextFileContent.MaximumPreviewCharacters, preview.Text.Length);
        Assert.Equal(original, File.ReadAllText(path));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => TextFileContent.ReadPreview(path, cancellation.Token));
    }

    [Fact]
    public void MultipleRootsShareALimitDeduplicateTraversalAndReportUnavailableRoots()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        File.WriteAllText(Path.Combine(first, "one.txt"), "needle");
        File.WriteAllText(Path.Combine(second, "two.txt"), "needle");
        var roots = new[] { first, _root, Path.Combine(_root, "unavailable") };
        var matches = new List<FileSearchMatch>();
        var summary = new FileSearchService().SearchRoots(roots, "type:txt content:needle", false, batch => matches.AddRange(batch));
        Assert.Equal(2, summary.ResultCount);
        Assert.Equal(2, matches.Select(match => match.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(1, summary.SkippedDirectories);
        var limited = new FileSearchService().SearchRoots(roots, "type:txt", false, _ => { }, maximumResults: 1);
        Assert.Equal(1, limited.ResultCount);
        Assert.True(limited.LimitReached);
    }

    public void Dispose() => LinkFileSystem.Delete(_root);
}
