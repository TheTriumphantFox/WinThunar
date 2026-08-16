using CommunityToolkit.Mvvm.ComponentModel;
using WinThunar.Services;

namespace WinThunar.Models;

public enum FileTransferJobState
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed
}

public partial class FileTransferJob : ObservableObject
{
    internal FileTransferJob(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        FileTransferMode mode,
        Func<FileConflict, Task<ConflictResolution>> conflictResolver)
    {
        SourcePaths = sourcePaths;
        DestinationDirectory = destinationDirectory;
        Mode = mode;
        ConflictResolver = conflictResolver;
        Title = $"{(mode == FileTransferMode.Move ? "Move" : "Copy")} {sourcePaths.Count} item{(sourcePaths.Count == 1 ? string.Empty : "s")}";
        StatusText = "Queued";
    }

    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlyList<string> SourcePaths { get; }
    public string DestinationDirectory { get; }
    public FileTransferMode Mode { get; }
    public string Title { get; }
    public CancellationTokenSource Cancellation { get; } = new();

    internal Func<FileConflict, Task<ConflictResolution>> ConflictResolver { get; }
    internal TaskCompletionSource<FileOperationResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    public partial FileTransferJobState State { get; set; } = FileTransferJobState.Queued;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    public FileOperationResult? Result { get; internal set; }
}
