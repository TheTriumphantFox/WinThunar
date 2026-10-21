using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class IndexedSearchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.Index.Tests", Guid.NewGuid().ToString("N"));
    public IndexedSearchTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ExplicitIndexModePreservesOrdinaryFiltersAndRejectsInvalidValues()
    {
        var query = FileSearchQuery.Parse("index:true report type:pdf size:>1KB");
        Assert.True(query.UseWindowsIndex);
        Assert.True(query.Matches(new("report.pdf", "", false, 2048, DateTime.Now, ".pdf")));
        Assert.False(query.Matches(new("report.txt", "", false, 2048, DateTime.Now, ".txt")));
        Assert.False(FileSearchQuery.Parse("index:false").UseWindowsIndex);
        Assert.Throws<FormatException>(() => FileSearchQuery.Parse("index:maybe"));
    }

    [Theory]
    [InlineData("a_b%[x]", "%a[_]b[%][[]x[]]%")]
    [InlineData("a*?.txt", "a%_.txt")]
    [InlineData("O'Brien", "%O''Brien%")]
    public void SqlEscapesNameLiteralsAndConvertsOnlyOurWildcards(string name, string expectedPattern)
    {
        var sql = IndexedFileSearchService.BuildSql([@"C:\A'B"], FileSearchQuery.Parse("name:" + name), true);
        Assert.Contains("SCOPE='file:C:/A''B'", sql);
        Assert.Contains("System.FileName LIKE '" + expectedPattern + "'", sql);
        Assert.StartsWith("SELECT TOP 50001 System.ItemPathDisplay FROM SYSTEMINDEX WHERE ", sql);
    }

    [Fact]
    public void SqlContentIsQuotedAndMultipleScopesAreGrouped()
    {
        var sql = IndexedFileSearchService.BuildSql([@"C:\one", @"D:\two"], FileSearchQuery.Parse("content:\"O'Brien OR approved\" content:second"), false);
        Assert.Contains("(DIRECTORY='file:C:/one' OR DIRECTORY='file:D:/two') AND CONTAINS", sql);
        Assert.Contains("CONTAINS(System.Search.Contents,'\"O''Brien OR approved\"') AND CONTAINS(System.Search.Contents,'\"second\"')", sql);
        Assert.Throws<FormatException>(() => IndexedFileSearchService.BuildSql(["C:\\bad\nroot"], FileSearchQuery.Parse("example"), true));
    }

    [Fact]
    public void CandidatesAreValidatedAgainstCurrentMetadataScopeAndDuplicates()
    {
        var good = Write("report.txt");
        var wrongType = Write("report.pdf");
        var other = Write("other.txt");
        var nested = Write(Path.Combine("nested", "report.txt"));
        var hidden = Write(Path.Combine(".hidden", "report.txt"));
        var stale = Path.Combine(_root, "removed.txt");
        string[] candidates = [good, good, wrongType, other, nested, hidden, stale, _root + "-adjacent\\report.txt", "relative.txt"];
        var service = new IndexedFileSearchService((_, _) => candidates);
        var matches = new List<FileSearchMatch>();
        var result = service.Search([_root], FileSearchQuery.Parse("report type:txt size:>0"), false, batch => matches.AddRange(batch));
        Assert.Equal(new[] { good, nested }, matches.Select(match => match.FullPath));
        Assert.True(result.UsedWindowsIndex);
        Assert.Equal(4, result.SkippedIndexEntries);
        Assert.Equal(2, result.ResultCount);
        matches.Clear();
        service.Search([_root], FileSearchQuery.Parse("report type:txt"), false, batch => matches.AddRange(batch), includeSubfolders: false);
        Assert.Equal(good, Assert.Single(matches).FullPath);
    }

    [Fact]
    public void AnExplicitNestedRootRemainsSearchableWhenItsAncestorIsHidden()
    {
        var path = Write(Path.Combine(".hidden", "one.txt"));
        var matches = new List<FileSearchMatch>();
        new IndexedFileSearchService((_, _) => [path]).Search([_root, Path.GetDirectoryName(path)!],
            FileSearchQuery.Parse("one"), false, batch => matches.AddRange(batch));
        Assert.Equal(path, Assert.Single(matches).FullPath);
    }

    [Fact]
    public void IndexedContentsDoNotOpenOrHydrateDocumentBytes()
    {
        var path = Write("document.pdf");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var matches = new List<FileSearchMatch>();
        var result = new IndexedFileSearchService((sql, _) =>
        {
            Assert.Contains("CONTAINS(System.Search.Contents", sql);
            return [path];
        }).Search([_root], FileSearchQuery.Parse("content:approved type:pdf"), false, batch => matches.AddRange(batch));
        Assert.Equal(path, Assert.Single(matches).FullPath);
        Assert.Equal(1, result.ResultCount);
    }

    [Fact]
    public void ResultLimitStopsProviderAndDisposesItsEnumerator()
    {
        var path = Write("one.txt");
        bool disposed = false;
        IEnumerable<string> Provider(string _, CancellationToken cancellation)
        {
            try { yield return path; throw new InvalidOperationException("Read beyond result limit"); }
            finally { disposed = true; }
        }
        var result = new IndexedFileSearchService(Provider).Search([_root], FileSearchQuery.Parse("one"), false, _ => { }, maximumResults: 1);
        Assert.True(result.LimitReached);
        Assert.True(disposed);
    }

    [Fact]
    public void CandidateLimitIsReportedEvenWhenAllEntriesAreStale()
    {
        var result = new IndexedFileSearchService((_, _) => Enumerable.Repeat("relative", IndexedFileSearchService.MaximumCandidates + 1))
            .Search([_root], FileSearchQuery.Parse("anything"), false, _ => throw new InvalidOperationException());
        Assert.True(result.IndexCandidateLimitReached);
        Assert.False(result.LimitReached);
        Assert.Equal(IndexedFileSearchService.MaximumCandidates, result.SkippedIndexEntries);
    }

    [Fact]
    public void CancellationPreventsPublishingPendingBatchAndDisposesProvider()
    {
        using var cancellation = new CancellationTokenSource();
        var path = Write("one.txt");
        bool disposed = false;
        IEnumerable<string> Provider(string _, CancellationToken token)
        {
            try { yield return path; cancellation.Cancel(); yield return path; }
            finally { disposed = true; }
        }
        Assert.Throws<OperationCanceledException>(() => new IndexedFileSearchService(Provider)
            .Search([_root], FileSearchQuery.Parse("one"), false, _ => throw new InvalidOperationException("Stale batch published"), cancellation.Token));
        Assert.True(disposed);
    }

    [Fact]
    public void EmptyScopesNeverOpenTheProvider()
    {
        var result = new IndexedFileSearchService((_, _) => throw new InvalidOperationException("Unexpected provider call"))
            .Search([], FileSearchQuery.Parse("one"), false, _ => { });
        Assert.True(result.UsedWindowsIndex);
        Assert.Equal(0, result.ResultCount);
    }

    private string Write(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture");
        return path;
    }
    public void Dispose() => Directory.Delete(_root, true);
}
