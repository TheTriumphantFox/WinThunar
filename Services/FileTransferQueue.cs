using System.Collections.ObjectModel;
using WinThunar.Models;

namespace WinThunar.Services;

public sealed class FileTransferQueue
{
    private readonly FileOperationService _fileOperations;
    private readonly Queue<FileTransferJob> _pending = new();
    private bool _isProcessing;

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

                var progress = new Progress<FileOperationProgress>(current =>
                {
                    var total = Math.Max(1, current.TotalItems);
                    job.ProgressPercent = Math.Clamp(current.CompletedItems * 100d / total, 0, 100);
                    job.StatusText = $"{current.ItemName} ({current.CompletedItems} of {current.TotalItems})";
                    OnStateChanged();
                });

                FileOperationResult result;
                try
                {
                    result = await _fileOperations.TransferAsync(
                        job.SourcePaths,
                        job.DestinationDirectory,
                        job.Mode,
                        job.ConflictResolver,
                        progress,
                        job.Cancellation.Token);
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
}
