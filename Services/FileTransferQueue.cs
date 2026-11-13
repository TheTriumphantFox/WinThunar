using System.Collections.ObjectModel;
using System.Diagnostics;
using WinThunar.Models;

namespace WinThunar.Services;

public sealed class FileTransferQueue : IDisposable
{
    private readonly FileOperationService _fileOperations;
    private readonly Queue<FileTransferJob> _pending = new();
    private bool _isProcessing;
    private bool _disposed;

    public FileTransferQueue(FileOperationService fileOperations)
    {
        _fileOperations = fileOperations;
    }

    public ObservableCollection<FileTransferJob> Jobs { get; } = [];
    public FileTransferJob? ActiveJob { get; private set; }
    public int QueuedCount => _pending.Count;

    public event EventHandler? StateChanged;

    public Task<FileOperationResult> EnqueueAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var sources = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sources.Length == 0)
        {
            throw new ArgumentException("A transfer job needs at least one source path.", nameof(sourcePaths));
        }

        var job = new FileTransferJob(sources, destinationDirectory, mode, conflictResolver);
        _pending.Enqueue(job);
        Jobs.Add(job);
        TrimCompletedJobs();
        OnStateChanged();
        _ = ProcessQueueAsync();
        return job.Completion.Task;
    }

    public void CancelActive()
    {
        ActiveJob?.Cancellation.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ActiveJob?.Cancellation.Cancel();
        while (_pending.TryDequeue(out var job))
        {
            var result = new FileOperationResult(0, 0, [], true, []);
            job.Result = result;
            job.State = FileTransferJobState.Cancelled;
            job.StatusText = "Cancelled because WinThunar is closing";
            job.Completion.TrySetResult(result);
            job.Cancellation.Dispose();
        }

        StateChanged = null;
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            while (_pending.TryDequeue(out var job))
            {
                ActiveJob = job;
                job.State = FileTransferJobState.Running;
                job.StatusText = "Starting...";
                OnStateChanged();

                var progressClock = Stopwatch.StartNew();
                string? progressItem = null;
                var progress = new Progress<FileOperationProgress>(current =>
                {
                    // Reports already posted to the UI must not revive a finished job.
                    if (!ReferenceEquals(ActiveJob, job) || job.State != FileTransferJobState.Running) return;
                    // Byte counts describe the current file, including files inside a selected folder.
                    // They cannot be used as a fraction of the selected top-level items.
                    job.IsProgressIndeterminate = current.TotalBytes <= 0;
                    job.ProgressPercent = current.TotalBytes > 0
                        ? Math.Clamp(current.BytesTransferred * 100d / current.TotalBytes, 0, 100)
                        : 0;
                    if (current.TotalBytes > 0)
                    {
                        if (!string.Equals(progressItem, current.ItemName, StringComparison.Ordinal))
                        {
                            progressItem = current.ItemName;
                            progressClock.Restart();
                        }

                        var seconds = Math.Max(0.001, progressClock.Elapsed.TotalSeconds);
                        var bytesPerSecond = current.BytesTransferred / seconds;
                        var remainingSeconds = bytesPerSecond > 0
                            ? (current.TotalBytes - current.BytesTransferred) / bytesPerSecond
                            : 0;
                        job.StatusText = $"{current.ItemName} — {FormatBytes(current.BytesTransferred)} of {FormatBytes(current.TotalBytes)} — {FormatBytes((long)bytesPerSecond)}/s" +
                            (remainingSeconds >= 1 ? $" — {FormatDuration(remainingSeconds)} remaining for this file" : string.Empty);
                    }
                    else
                    {
                        job.StatusText = current.TotalItems > 0
                            ? $"{current.ItemName} ({current.CompletedItems} of {current.TotalItems} items finished)"
                            : current.ItemName;
                    }
                    OnStateChanged();
                });

                FileOperationResult result;
                try
                {
                    var context = SynchronizationContext.Current;
                    // Run the whole operation on a worker: metadata checks, directory enumeration,
                    // renames and recovery bookkeeping can block just as the byte copy can.
                    result = await Task.Run(() => _fileOperations.TransferAsync(
                        job.SourcePaths,
                        job.DestinationDirectory,
                        job.Mode,
                        conflict => ResolveConflictAsync(job, conflict, context),
                        new ThrottledProgress(progress),
                        job.Cancellation.Token,
                        retainReplacedItems: true));
                }
                catch (Exception ex)
                {
                    result = new FileOperationResult(
                        0,
                        0,
                        [ex.Message],
                        false,
                        []);
                }

                job.Result = result;
                job.ProgressPercent = result.Cancelled ? job.ProgressPercent : 100;
                job.State = result.Cancelled
                    ? FileTransferJobState.Cancelled
                    : result.Errors.Count > 0
                        ? FileTransferJobState.Failed
                        : FileTransferJobState.Completed;
                job.StatusText = job.State switch
                {
                    FileTransferJobState.Completed => $"Completed: {result.CompletedItems}, skipped: {result.SkippedItems}",
                    FileTransferJobState.Cancelled => "Cancelled",
                    _ => result.Errors.FirstOrDefault() ?? "Failed"
                };
                job.Completion.TrySetResult(result);
                if (_disposed)
                {
                    job.Cancellation.Dispose();
                }
                ActiveJob = null;
                OnStateChanged();
            }
        }
        finally
        {
            ActiveJob = null;
            _isProcessing = false;
            OnStateChanged();
        }
    }

    private static Task<ConflictResolution> ResolveConflictAsync(
        FileTransferJob job, FileConflict conflict, SynchronizationContext? context)
    {
        if (context is null) return job.ConflictResolver(conflict);
        var completion = new TaskCompletionSource<ConflictResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(async _ =>
        {
            try
            {
                job.Cancellation.Token.ThrowIfCancellationRequested();
                completion.TrySetResult(await job.ConflictResolver(conflict));
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, null);
        return completion.Task;
    }

    // Native copies can report every chunk. Limit dispatches before posting them to the UI,
    // so a fast disk cannot fill the message queue with progress updates.
    private sealed class ThrottledProgress(IProgress<FileOperationProgress> target) : IProgress<FileOperationProgress>
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _reported;

        public void Report(FileOperationProgress value)
        {
            // Completion is published separately by the queue; even tiny files must respect
            // the limit, otherwise a directory full of them can still swamp the dispatcher.
            if (_reported && _clock.ElapsedMilliseconds < 100) return;
            _reported = true;
            _clock.Restart();
            target.Report(value);
        }
    }

    private void TrimCompletedJobs()
    {
        while (Jobs.Count > 12)
        {
            var removable = Jobs.FirstOrDefault(job =>
                job.State is FileTransferJobState.Completed or
                    FileTransferJobState.Cancelled or
                    FileTransferJobState.Failed);
            if (removable is null)
            {
                return;
            }

            Jobs.Remove(removable);
            removable.Cancellation.Dispose();
        }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : duration.TotalMinutes >= 1
                ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
                : $"{Math.Max(1, duration.Seconds)}s";
    }
}
