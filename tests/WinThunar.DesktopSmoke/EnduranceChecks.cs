using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinThunar.Models;

namespace WinThunar;

// Included only in the isolated desktop test build, alongside SmokeChecks.cs.
public sealed partial class MainPage
{
    private async Task RunOptionalEnduranceAsync(string directory, List<string> checks)
    {
        var setting = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_ENDURANCE_SECONDS");
        if (string.IsNullOrEmpty(setting) || setting == "0") return;
        if (!int.TryParse(setting, out var seconds) || seconds is < 1 or > 3600)
            throw new InvalidOperationException("Desktop endurance duration must be between 1 and 3600 seconds.");

        const int textCount = 300, imageCount = 60;
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "endurance-files")).FullName;
        var template = await (await StorageFolder.GetFolderFromPathAsync(directory))
            .CreateFileAsync("endurance-template.png", CreationCollisionOption.FailIfExists);
        using (var stream = await template.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            var pixels = new byte[32 * 32 * 4];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = 180;
                pixels[offset + 1] = 100;
                pixels[offset + 2] = 40;
                pixels[offset + 3] = 255;
            }
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 32, 32, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        await Task.Run(() =>
        {
            for (var index = 0; index < textCount; index++)
                File.WriteAllText(Path.Combine(fixture, $"report-{index:D4}.txt"), "Synthetic endurance marker " + index);
            for (var index = 0; index < imageCount; index++)
                File.Copy(template.Path, Path.Combine(fixture, $"image-{index:D4}.png"));
        });

        var clock = Stopwatch.StartNew();
        var phases = new List<object>();
        var samples = new List<object>();
        var entriesObserved = new List<WeakReference>();
        var lastTick = Stopwatch.GetTimestamp();
        double maximumDispatchGapMilliseconds = 0;
        int ticks = 0, cycles = 0, thumbnailsObserved = 0, previews = 0;
        string? failure = null;
        var heartbeat = DispatcherQueue.CreateTimer();
        heartbeat.Interval = TimeSpan.FromMilliseconds(100);
        heartbeat.Tick += (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            maximumDispatchGapMilliseconds = Math.Max(maximumDispatchGapMilliseconds, Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds);
            lastTick = now;
            ticks++;
        };
        void Sample(string phase = "workload")
        {
            using var process = Process.GetCurrentProcess();
            samples.Add(new { phase, seconds = clock.Elapsed.TotalSeconds, cycles, managedBytes = GC.GetTotalMemory(false),
                allocatedBytes = GC.GetTotalAllocatedBytes(), privateBytes = process.PrivateMemorySize64,
                workingSet = process.WorkingSet64, handles = process.HandleCount, maximumDispatchGapMilliseconds,
                gen0Collections = GC.CollectionCount(0), gen1Collections = GC.CollectionCount(1), gen2Collections = GC.CollectionCount(2),
                observedEntriesAlive = entriesObserved.Count(entry => entry.IsAlive) });
        }
        async Task Search(string query, int expected)
        {
            var phase = Stopwatch.StartNew();
            await ViewModel.SearchAsync(query);
            if (ViewModel.Entries.Count != expected)
                throw new InvalidOperationException($"Endurance query {query} returned {ViewModel.Entries.Count}; expected {expected}.");
            UpdateLayout();
            if (ActiveFileList.Items.Count > 0)
                ActiveFileList.ScrollIntoView(ActiveFileList.Items[(cycles * 17) % ActiveFileList.Items.Count]);
            await Task.Delay(100);
            var renderedThumbnails = ViewModel.Entries.Count(entry => entry.Thumbnail is not null);
            entriesObserved.Add(new WeakReference(ViewModel.Entries[0]));
            thumbnailsObserved += renderedThumbnails;
            phases.Add(new { cycle = cycles, query, milliseconds = phase.Elapsed.TotalMilliseconds,
                results = ViewModel.Entries.Count, renderedThumbnails });
        }
        heartbeat.Start();
        try
        {
            ViewModel.ShowThumbnails = true;
            _showImagePreview = true;
            ApplyImagePreview();
            Sample();
            do
            {
                await ViewModel.NavigateAsync(fixture);
                ViewModel.ViewMode = BrowserViewMode.Icons;
                ViewModel.ZoomLevel = cycles % 4 + 1;
                await Search("type:png", imageCount);
                var selectedImage = ViewModel.Entries[cycles % imageCount];
                await UpdateImagePreviewAsync(selectedImage);
                if (PreviewImage.Source is null || PreviewImage.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("Generated image preview failed during endurance work.");
                previews++;
                if (cycles == 0) await CaptureSmokeImageAsync(directory, "EnduranceIcons");
                ViewModel.ViewMode = BrowserViewMode.Details;
                await Search("report type:txt", textCount);
                ViewModel.ViewMode = BrowserViewMode.Compact;
                await Search("type:txt content:\"endurance marker\"", textCount);
                await UpdateImagePreviewAsync(ViewModel.Entries[cycles % textCount]);
                if (!PreviewText.Text.Contains("endurance marker"))
                    throw new InvalidOperationException("Text preview failed during endurance work.");
                previews++;
                await ViewModel.EndSearchAsync();
                cycles++;
                if (cycles % 10 == 0) Sample();
            } while (clock.Elapsed.TotalSeconds < seconds);
            Sample();
            Require(cycles > 0 && phases.Count == cycles * 3, "Sustained desktop searches retain all generated results", checks);
            Require(thumbnailsObserved > 0 && previews == cycles * 2, "Sustained desktop work renders thumbnails and image/text previews", checks);
            Require(ticks > 0, "UI dispatcher continues ticking during sustained search and preview work", checks);
            await CaptureSmokeImageAsync(directory, "EnduranceFinal");
            // Distinguish pending work/finalization and UI caches from retained directory entries.
            // This collection is test diagnostics only; the application never forces collection.
            await UpdateImagePreviewAsync(null);
            await ViewModel.NavigateAsync(Directory.CreateDirectory(Path.Combine(directory, "endurance-idle")).FullName);
            await Task.Delay(5000);
            Sample("idle");
            await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
            await Task.Delay(1000);
            Sample("after-collection");
            if (int.TryParse(Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS"), out var holdSeconds) && holdSeconds is > 0 and <= 600)
            {
                File.WriteAllText(Path.Combine(directory, "endurance-ready-for-dump.json"),
                    JsonSerializer.Serialize(new { processId = Environment.ProcessId, processPath = Environment.ProcessPath, holdSeconds }));
                await Task.Delay(TimeSpan.FromSeconds(holdSeconds));
            }
        }
        catch (Exception ex) { failure = ex.ToString(); throw; }
        finally
        {
            heartbeat.Stop();
            File.WriteAllText(Path.Combine(directory, "endurance-result.json"), JsonSerializer.Serialize(new
            {
                requestedSeconds = seconds, elapsedSeconds = clock.Elapsed.TotalSeconds, textCount, imageCount, cycles,
                phases, samples, ticks, maximumDispatchGapMilliseconds, thumbnailsObserved, previews, failure,
                limits = "Generated local UI workload in a separate test build. Dispatch gaps and memory samples are diagnostics, not latency or leak guarantees. No slow network, physical device, or day-long soak coverage."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
