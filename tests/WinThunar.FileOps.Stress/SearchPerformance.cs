using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal static class SearchPerformance
{
    private sealed record Measurement(string Query, int Iteration, double FirstBatchMilliseconds, double TotalMilliseconds,
        int Results, long ManagedAllocatedBytes, long ManagedRetainedBytes, long PrivateBytes);

    internal static int Run(string output)
    {
        var root = Path.GetFullPath(output);
        Fixture.Check(!Directory.Exists(root) && !File.Exists(root), "Performance output must be new.");
        Directory.CreateDirectory(root);
        var token = Guid.NewGuid().ToString("N");
        var marker = Path.Combine(root, "performance-owner.txt");
        File.WriteAllText(marker, token);
        var fixture = Directory.CreateDirectory(Path.Combine(root, "files")).FullName;
        const int files = 10000;
        var measurements = new List<Measurement>();
        string? failure = null;
        double cancellationMilliseconds = 0;
        try
        {
            // Include both a single large directory and recursive traversal, using generated data.
            var nested = Directory.CreateDirectory(Path.Combine(fixture, "nested")).FullName;
            for (var index = 0; index < files; index++)
                File.WriteAllText(Path.Combine(index < files / 2 ? fixture : nested, $"report-{index:D5}.txt"),
                    "Synthetic annual report. Approved fixture " + index);
            var search = new FileSearchService();
            foreach (var query in new[] { "report type:txt", "type:txt content:\"annual report\" content:approved" })
                for (var iteration = 1; iteration <= 5; iteration++)
                {
                    var allocation = GC.GetTotalAllocatedBytes(precise: true);
                    var before = GC.GetTotalMemory(forceFullCollection: true);
                    var timer = Stopwatch.StartNew();
                    double firstBatch = -1;
                    var observed = 0;
                    var summary = search.Search(fixture, query, false, batch =>
                    {
                        if (firstBatch < 0) firstBatch = timer.Elapsed.TotalMilliseconds;
                        observed += batch.Count;
                    }, maximumResults: files + 1);
                    var elapsed = timer.Elapsed.TotalMilliseconds;
                    Fixture.Check(summary.ResultCount == files && observed == files && !summary.LimitReached && summary.SkippedContentFiles == 0,
                        "Performance run lost results or skipped generated files.");
                    using var process = Process.GetCurrentProcess();
                    measurements.Add(new(query, iteration, firstBatch, elapsed, observed,
                        GC.GetTotalAllocatedBytes(precise: true) - allocation, GC.GetTotalMemory(forceFullCollection: true) - before,
                        process.PrivateMemorySize64));
                }
            using var cancellation = new CancellationTokenSource();
            var cancelTimer = Stopwatch.StartNew();
            try
            {
                search.Search(fixture, "report type:txt", false, _ => cancellation.Cancel(), cancellation.Token, maximumResults: files + 1);
                throw new InvalidOperationException("The cancelled search did not stop.");
            }
            catch (OperationCanceledException) { cancellationMilliseconds = cancelTimer.Elapsed.TotalMilliseconds; }

            // Verify the exact ordinary fixture and marker before deleting only generated files.
            Fixture.Check(File.ReadAllText(marker) == token && Path.GetDirectoryName(fixture) == root &&
                !LinkFileSystem.IsReparsePoint(root) && !LinkFileSystem.IsReparsePoint(fixture), "Performance fixture ownership changed.");
            LinkFileSystem.Delete(fixture);
        }
        catch (Exception ex) { failure = ex.ToString(); }
        File.WriteAllText(Path.Combine(root, "report.json"), JsonSerializer.Serialize(new
        {
            capturedUtc = DateTimeOffset.UtcNow, framework = RuntimeInformation.FrameworkDescription, files,
            measurements, cancellationMilliseconds, failure,
            limits = "Generated local service-search workload; warmed filesystem caches. No UI rendering, thumbnails, remote-provider latency, day-long soak, or physical-device coverage. Retained managed-byte differences are diagnostic, not a leak assertion."
        }, Fixture.JsonOptions));
        Console.WriteLine($"Search performance report: {Path.Combine(root, "report.json")}");
        if (failure is not null) Console.Error.WriteLine(failure);
        return failure is null ? 0 : 1;
    }
}
