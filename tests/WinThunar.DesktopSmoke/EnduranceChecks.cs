using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinThunar.Models;

namespace WinThunar;

// Included only in the isolated desktop test build, alongside SmokeChecks.cs.
public sealed partial class MainPage
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong time);

    private static ulong EnduranceAwakeTime()
    {
        if (!QueryUnbiasedInterruptTime(out var time)) throw new InvalidOperationException("Could not read Windows awake time.");
        return time;
    }

    private async Task RunOptionalEnduranceAsync(string directory, List<string> checks)
    {
        var setting = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_ENDURANCE_SECONDS");
        if (string.IsNullOrEmpty(setting) || setting == "0") return;
        if (!int.TryParse(setting, out var seconds) || seconds is < 1 or > 86400)
            throw new InvalidOperationException("Desktop endurance duration must be between 1 and 86400 seconds.");
        var requestedFocus = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_ENDURANCE_FOCUS") ?? "Existing";
        if (requestedFocus is not ("Existing" or "Files" or "Menu" or "Alternate"))
            throw new InvalidOperationException("Unknown endurance focus mode.");

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
        var awakeStart = EnduranceAwakeTime();
        double AwakeSeconds() => (EnduranceAwakeTime() - awakeStart) / 10_000_000.0;
        double workloadAwakeSeconds = 0;
        // Keep the observer bounded even for a day-long run. Complete histories are streamed
        // to disk; JSON reports retain the most recent samples and explicit total counters.
        const int historyLimit = 600;
        var phases = new Queue<object>();
        var samples = new Queue<object>();
        var firstEntriesObserved = new List<WeakReference>();
        var entriesObserved = new Queue<WeakReference>();
        using var phaseLog = new StreamWriter(Path.Combine(directory, "endurance-searches.ndjson"));
        using var sampleLog = new StreamWriter(Path.Combine(directory, "endurance-samples.ndjson")) { AutoFlush = true };
        int completedSearches = 0, observedEntries = 0, totalSamples = 0, checkpointSkippedWrites = 0;
        void Retain<T>(Queue<T> queue, T value)
        {
            if (queue.Count == historyLimit) queue.Dequeue();
            queue.Enqueue(value);
        }
        var lastTick = Stopwatch.GetTimestamp();
        var lastAwakeTick = EnduranceAwakeTime();
        double maximumDispatchGapMilliseconds = 0;
        double maximumAwakeDispatchGapMilliseconds = 0;
        int ticks = 0, cycles = 0, thumbnailsObserved = 0, previews = 0;
        string? failure = null;
        var heartbeat = DispatcherQueue.CreateTimer();
        heartbeat.Interval = TimeSpan.FromMilliseconds(100);
        heartbeat.Tick += (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            var awakeNow = EnduranceAwakeTime();
            maximumDispatchGapMilliseconds = Math.Max(maximumDispatchGapMilliseconds, Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds);
            maximumAwakeDispatchGapMilliseconds = Math.Max(maximumAwakeDispatchGapMilliseconds, (awakeNow - lastAwakeTick) / 10_000.0);
            lastTick = now;
            lastAwakeTick = awakeNow;
            ticks++;
        };
        void Sample(string phase = "workload")
        {
            using var process = Process.GetCurrentProcess();
            var memory = GC.GetGCMemoryInfo();
            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            var sample = new { phase, seconds = clock.Elapsed.TotalSeconds, awakeSeconds = AwakeSeconds(), cycles, managedBytes = GC.GetTotalMemory(false),
                allocatedBytes = GC.GetTotalAllocatedBytes(), privateBytes = process.PrivateMemorySize64,
                workingSet = process.WorkingSet64, handles = process.HandleCount, maximumDispatchGapMilliseconds, maximumAwakeDispatchGapMilliseconds,
                gen0Collections = GC.CollectionCount(0), gen1Collections = GC.CollectionCount(1), gen2Collections = GC.CollectionCount(2),
                gcHeapBytes = memory.HeapSizeBytes, gcCommittedBytes = memory.TotalCommittedBytes,
                gcFragmentedBytes = memory.FragmentedBytes, finalizationPendingCount = memory.FinalizationPendingCount,
                observedEntries, observedEntryWindow = firstEntriesObserved.Count + entriesObserved.Count,
                firstEntriesObserved = firstEntriesObserved.Count,
                firstEntriesAlive = firstEntriesObserved.Count(entry => entry.IsAlive),
                observedEntriesAlive = firstEntriesObserved.Concat(entriesObserved).Count(entry => entry.IsAlive),
                nameTrackersObserved = _containerNameTrackers.Count,
                nameTrackersAlive = _containerNameTrackers.Count(reference => reference.TryGetTarget(out _)),
                focusType = focused?.GetType().FullName,
                focusName = (focused as FrameworkElement)?.Name,
                focusInFiles = ContainsFocus(ActiveFileList, focused),
                focusInMenu = ContainsFocus(FileMenuBarItem, focused) };
            Retain(samples, sample);
            totalSamples++;
            sampleLog.WriteLine(JsonSerializer.Serialize(sample));
            phaseLog.Flush();
            // A native termination cannot run finally. Preserve a small checkpoint instead
            // of losing every completed cycle with the final in-memory report.
            var progressPath = Path.Combine(directory, "endurance-progress.json");
            File.WriteAllText(progressPath + ".tmp", JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, requestedSeconds = seconds,
                completedSearches, previews, requestedFocus, latestSample = sample
            }, new JsonSerializerOptions { WriteIndented = true }));
            try { File.Move(progressPath + ".tmp", progressPath, overwrite: true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException &&
                (error.HResult & 0xffff) is 5 or 32 or 33)
            {
                // Readers such as Get-Content may omit FileShare.Delete. Keep running
                // when that reader briefly blocks replacement; the flushed NDJSON above
                // already preserves this sample, and the next checkpoint retries.
                checkpointSkippedWrites++;
            }
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
            if (requestedFocus != "Existing")
            {
                var menu = requestedFocus == "Menu" || (requestedFocus == "Alternate" && cycles % 2 == 1);
                var focusSet = menu ? FileMenuBarItem.Focus(FocusState.Keyboard) : ActiveFileList.Focus(FocusState.Keyboard);
                var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
                if (!focusSet || !(menu ? ContainsFocus(FileMenuBarItem, focused) : ContainsFocus(ActiveFileList, focused)))
                    throw new InvalidOperationException("Endurance focus mode could not acquire its requested region.");
            }
            var renderedThumbnails = ViewModel.Entries.Count(entry => entry.Thumbnail is not null);
            // Preserve an early cohort for the entire run: a sliding window alone could
            // forget a leak just because the retained objects are more than 600 searches old.
            if (firstEntriesObserved.Count < 100) firstEntriesObserved.Add(new WeakReference(ViewModel.Entries[0]));
            else
            {
                if (entriesObserved.Count == historyLimit - 100) entriesObserved.Dequeue();
                entriesObserved.Enqueue(new WeakReference(ViewModel.Entries[0]));
            }
            observedEntries++;
            thumbnailsObserved += renderedThumbnails;
            var phaseFocus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            var result = new { cycle = cycles, query, milliseconds = phase.Elapsed.TotalMilliseconds,
                results = ViewModel.Entries.Count, renderedThumbnails,
                focusInFiles = ContainsFocus(ActiveFileList, phaseFocus),
                focusInMenu = ContainsFocus(FileMenuBarItem, phaseFocus) };
            Retain(phases, result);
            completedSearches++;
            phaseLog.WriteLine(JsonSerializer.Serialize(result));
        }
        // A collection change while a container callback is still on the stack means that
        // callback pumped the dispatcher during layout. Record each one with its managed stack.
        var reentrantChanges = 0;
        using var reentrancyLog = new StreamWriter(Path.Combine(directory, "endurance-reentrancy.ndjson")) { AutoFlush = true };
        void EntriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if (ContainerCallbackDepth == 0) return;
            reentrantChanges++;
            if (reentrantChanges <= 50)
                reentrancyLog.WriteLine(JsonSerializer.Serialize(new { cycle = cycles, action = args.Action.ToString(),
                    depth = ContainerCallbackDepth, stack = Environment.StackTrace }));
        }
        ViewModel.Entries.CollectionChanged += EntriesChanged;
        heartbeat.Start();
        try
        {
            ViewModel.ShowThumbnails = true;
            _showImagePreview = true;
            ApplyImagePreview();
            Sample();
            // Diagnostic stress: repeat the container-binding checks before every cycle, reproducing
            // the state that preceded the one first-cycle WinUI access violation.
            var interleaveContainerChecks = Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_INTERLEAVE_CONTAINER_CHECKS") == "1";
            do
            {
                if (interleaveContainerChecks) await CheckContainerBindingsAsync(directory, []);
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
            } while (AwakeSeconds() < seconds);
            workloadAwakeSeconds = AwakeSeconds();
            Sample();
            Require(cycles > 0 && completedSearches == cycles * 3, "Sustained desktop searches retain all generated results", checks);
            Require(thumbnailsObserved > 0 && previews == cycles * 2, "Sustained desktop work renders thumbnails and image/text previews", checks);
            Require(ticks > 0, "UI dispatcher continues ticking during sustained search and preview work", checks);
            Require(phases.Count == Math.Min(historyLimit, completedSearches) &&
                firstEntriesObserved.Count + entriesObserved.Count <= historyLimit && samples.Count <= historyLimit,
                "Endurance diagnostics remain bounded while retaining the initial weak-entry cohort", checks);
            await CaptureSmokeImageAsync(directory, "EnduranceFinal");
            // Window capture allocates its own render surfaces; keep that apart from cleanup.
            Sample("after-capture");
            // Distinguish pending work/finalization and UI caches from retained directory entries.
            // This collection is test diagnostics only; the application never forces collection.
            await UpdateImagePreviewAsync(null);
            await ViewModel.NavigateAsync(Directory.CreateDirectory(Path.Combine(directory, "endurance-idle")).FullName);
            await Task.Delay(5000);
            Sample("idle");
            await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
            await Task.Delay(1000);
            Sample("after-collection");
            // WinRT wrapper cleanup can post work back to the dispatcher. Sample later
            // collection rounds after that work runs instead of treating one GC as a leak test.
            for (var round = 2; round <= 3; round++)
            {
                await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
                await Task.Delay(1000);
                Sample($"after-collection-{round}");
            }
            Require(_containerNameTrackers.Count(reference => reference.TryGetTarget(out _)) <= (textCount + imageCount) * 4,
                "Native container name trackers remain bounded after the generated workload", checks);
            if (int.TryParse(Environment.GetEnvironmentVariable("WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS"), out var holdSeconds) && holdSeconds is > 0 and <= 600)
            {
                File.WriteAllText(Path.Combine(directory, "endurance-ready-for-dump.json"),
                    JsonSerializer.Serialize(new { processId = Environment.ProcessId, processPath = Environment.ProcessPath, holdSeconds }));
                var holdStart = AwakeSeconds();
                while (AwakeSeconds() - holdStart < holdSeconds) await Task.Delay(1000);
            }
        }
        catch (Exception ex) { failure = ex.ToString(); throw; }
        finally
        {
            heartbeat.Stop();
            ViewModel.Entries.CollectionChanged -= EntriesChanged;
            File.WriteAllText(Path.Combine(directory, "endurance-result.json"), JsonSerializer.Serialize(new
            {
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                requestedSeconds = seconds, elapsedSeconds = clock.Elapsed.TotalSeconds, textCount, imageCount, cycles,
                workloadAwakeSeconds, timingBasis = "Windows unbiased interrupt time; excludes system sleep/hibernation",
                completedSearches, totalSamples, historyLimit, requestedFocus, checkpointSkippedWrites,
                phases, samples, ticks, maximumDispatchGapMilliseconds, maximumAwakeDispatchGapMilliseconds, thumbnailsObserved, previews, reentrantChanges, failure,
                limits = "Generated local UI workload in a separate test build. Dispatch gaps and memory samples are diagnostics, not latency or leak guarantees. Histories/weak references retain at most 600 records; full search/sample histories are NDJSON. No slow-network or physical-device coverage; duration is reported explicitly."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
