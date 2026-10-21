using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class FileSearchQueryTests
{
    private static readonly DateTime Today = new(2026, 10, 21);
    private static FileSearchMatch Match(string name = "Annual report.PDF", long size = 2 * 1024 * 1024, int daysAgo = 0, bool folder = false)
        => new(name, @"C:\fixture\" + name, folder, size, Today.AddDays(-daysAgo), Path.GetExtension(name));

    [Theory]
    [InlineData("report", true)]
    [InlineData("annual report", true)]
    [InlineData("name:\"Annual report\" type:pdf size:>1MB modified:last7days", true)]
    [InlineData("*.pdf size:1MB..3MB modified:2026-10-01..2026-10-21", true)]
    [InlineData("type:file modified:>=2026-10-21", true)]
    [InlineData("ext:.PDF size:<=2MB modified:today", true)]
    [InlineData("type:txt", false)]
    [InlineData("type:folder", false)]
    [InlineData("size:>2MB", false)]
    [InlineData("modified:yesterday", false)]
    [InlineData("name:budget", false)]
    public void CombinesNameAndMetadataFilters(string query, bool expected)
        => Assert.Equal(expected, FileSearchQuery.Parse(query, Today).Matches(Match()));

    [Theory]
    [InlineData("size:lots")]
    [InlineData("size:-1")]
    [InlineData("size:9999999999999999999999TB")]
    [InlineData("size:2MB..1MB")]
    [InlineData("modified:nextweek")]
    [InlineData("modified:2026-11-01..2026-10-01")]
    [InlineData("name:")]
    [InlineData("type:")]
    [InlineData("name:\"unfinished")]
    public void InvalidFiltersProduceUsefulErrors(string query)
        => Assert.Throws<FormatException>(() => FileSearchQuery.Parse(query, Today));

    [Fact]
    public void DateWindowsAreInclusiveAndSizeFiltersExcludeDirectories()
    {
        var query = FileSearchQuery.Parse("modified:last7days", Today);
        Assert.True(query.Matches(Match(daysAgo: 6)));
        Assert.False(query.Matches(Match(daysAgo: 7)));
        Assert.False(query.Matches(Match(daysAgo: -1)));
        Assert.False(FileSearchQuery.Parse("size:>=0").Matches(Match(folder: true)));
    }

    [Fact]
    public void LargeFolderSearchBatchesResultsCapsOutputAndCancelsBetweenBatches()
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests"));
        var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (var index = 0; index < 5100; index++) File.WriteAllText(Path.Combine(root, $"report-{index:D5}.txt"), "fixture");
            var count = 0;
            var service = new FileSearchService();
            var summary = service.Search(root, "report type:txt size:>0", false, batch =>
            {
                Assert.InRange(batch.Count, 1, 40);
                count += batch.Count;
            });
            Assert.Equal(5000, count);
            Assert.True(summary.LimitReached);
            using var cancellation = new CancellationTokenSource();
            count = 0;
            Assert.Throws<OperationCanceledException>(() => service.Search(root, "*.txt", false, batch =>
            {
                count += batch.Count;
                cancellation.Cancel();
            }, cancellation.Token));
            Assert.Equal(40, count);
        }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            Directory.Delete(root, true);
        }
    }
}
