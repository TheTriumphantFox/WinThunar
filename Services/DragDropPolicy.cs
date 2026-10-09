namespace WinThunar.Services;

[Flags]
public enum DropModifierKeys
{
    None = 0,
    Control = 1,
    Shift = 2
}

public enum FileDropTargetKind
{
    None,
    Directory,
    Trash,
    Executable
}

public enum FileDropOperation
{
    None,
    Copy,
    Move,
    Link,
    Trash,
    Execute
}

public sealed record FileDropTarget(FileDropTargetKind Kind, string? Path);

public sealed record FileDropDecision(FileDropOperation Operation, string? RejectionReason = null)
{
    public bool IsAllowed => Operation != FileDropOperation.None;
}

public static class DragDropPolicy
{
    public static FileDropDecision Evaluate(
        IReadOnlyCollection<string> sourcePaths,
        FileDropTarget target,
        DropModifierKeys modifiers,
        bool searchActive,
        bool sourceIsRecycleBin)
    {
        if (sourcePaths.Count == 0)
        {
            return Reject("The drag does not contain filesystem items.");
        }

        if (searchActive)
        {
            return Reject("Finish or close the search before dropping files here.");
        }

        if (target.Kind == FileDropTargetKind.Trash)
        {
            return sourceIsRecycleBin
                ? Reject("These items are already in Trash.")
                : new FileDropDecision(FileDropOperation.Trash);
        }

        if (target.Kind == FileDropTargetKind.Executable)
        {
            return sourceIsRecycleBin
                ? Reject("Restore Trash items before opening them with an application.")
                : new FileDropDecision(FileDropOperation.Execute);
        }

        if (target.Kind != FileDropTargetKind.Directory ||
            string.IsNullOrWhiteSpace(target.Path) ||
            !Directory.Exists(target.Path))
        {
            return Reject("That location cannot accept files.");
        }

        var destination = Path.GetFullPath(target.Path);
        foreach (var sourcePath in sourcePaths)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                return Reject("One or more dragged items no longer exist.");
            }

            if (FileOperationService.PathsReferToSameItem(sourcePath, destination))
            {
                return Reject("An item cannot be dropped onto itself.");
            }

            if (Directory.Exists(sourcePath) &&
                FileOperationService.IsSameOrDescendant(destination, sourcePath))
            {
                return Reject("A folder cannot be dropped into itself.");
            }
        }

        var needsDirectoryCreation = sourcePaths.Any(Directory.Exists);
        if (!FileOperationService.CanWriteToDirectory(destination, needsDirectoryCreation))
        {
            return Reject("You do not have permission to add items to that folder.");
        }

        if (sourceIsRecycleBin)
        {
            return new FileDropDecision(FileDropOperation.Move);
        }

        if ((modifiers & (DropModifierKeys.Control | DropModifierKeys.Shift)) ==
            (DropModifierKeys.Control | DropModifierKeys.Shift))
        {
            return new FileDropDecision(FileDropOperation.Link);
        }

        if ((modifiers & DropModifierKeys.Control) != 0)
        {
            return new FileDropDecision(FileDropOperation.Copy);
        }

        if ((modifiers & DropModifierKeys.Shift) != 0)
        {
            return new FileDropDecision(FileDropOperation.Move);
        }

        return new FileDropDecision(
            FileOperationService.ArePathsOnSameVolume(sourcePaths, destination)
                ? FileDropOperation.Move
                : FileDropOperation.Copy);
    }

    private static FileDropDecision Reject(string reason) =>
        new(FileDropOperation.None, reason);
}
