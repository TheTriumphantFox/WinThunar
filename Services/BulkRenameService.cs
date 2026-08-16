using System.Globalization;

namespace WinThunar.Services;

public enum BulkRenameMode
{
    SearchAndReplace,
    Prefix,
    Suffix,
    Numbering,
    Uppercase,
    Lowercase,
    TitleCase
}

public sealed record BulkRenameOptions(
    BulkRenameMode Mode,
    string FirstValue,
    string SecondValue,
    bool PreserveExtension = true);

public sealed record BulkRenamePlanItem(
    string SourcePath,
    string DestinationPath,
    string OriginalName,
    string NewName);

public sealed class BulkRenameService
{
    public IReadOnlyList<BulkRenamePlanItem> BuildPlan(
        IReadOnlyList<string> sourcePaths,
        BulkRenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(options);
        if (sourcePaths.Count < 2)
        {
            throw new ArgumentException("Bulk rename requires at least two items.", nameof(sourcePaths));
        }

        var plan = new List<BulkRenamePlanItem>(sourcePaths.Count);
        var sourceSet = sourcePaths.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < sourcePaths.Count; index++)
        {
            var sourcePath = NormalizePath(sourcePaths[index]);
            var originalName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath));
            var extension = options.PreserveExtension && File.Exists(sourcePath)
                ? Path.GetExtension(originalName)
                : string.Empty;
            var stem = extension.Length > 0 ? originalName[..^extension.Length] : originalName;
            var renamedStem = RenameStem(stem, index, options);
            var newName = renamedStem + extension;
            var validationError = FileOperationService.ValidateLeafName(newName);
            if (validationError is not null)
            {
                throw new InvalidOperationException($"{newName}: {validationError}");
            }

            var parent = Path.GetDirectoryName(sourcePath)
                ?? throw new InvalidOperationException($"Could not determine the parent folder for {sourcePath}.");
            var destinationPath = Path.Combine(parent, newName);
            if (!destinations.Add(destinationPath))
            {
                throw new InvalidOperationException($"More than one item would be renamed to '{newName}'.");
            }

            if (!sourceSet.Contains(destinationPath) && PathExists(destinationPath))
            {
                throw new IOException($"An item named '{newName}' already exists.");
            }

            plan.Add(new BulkRenamePlanItem(sourcePath, destinationPath, originalName, newName));
        }

        return plan;
    }

    public Task ApplyAsync(IReadOnlyList<BulkRenamePlanItem> plan) => Task.Run(() => Apply(plan));

    public IReadOnlyList<BulkRenamePlanItem> Reverse(IReadOnlyList<BulkRenamePlanItem> plan) =>
        plan.Select(item => new BulkRenamePlanItem(
            item.DestinationPath,
            item.SourcePath,
            item.NewName,
            item.OriginalName)).ToArray();

    private static string RenameStem(string stem, int index, BulkRenameOptions options) => options.Mode switch
    {
        BulkRenameMode.SearchAndReplace => stem.Replace(
            options.FirstValue,
            options.SecondValue,
            StringComparison.CurrentCultureIgnoreCase),
        BulkRenameMode.Prefix => options.FirstValue + stem,
        BulkRenameMode.Suffix => stem + options.FirstValue,
        BulkRenameMode.Numbering => $"{options.FirstValue}{FormatNumber(index, options.SecondValue)}",
        BulkRenameMode.Uppercase => stem.ToUpper(CultureInfo.CurrentCulture),
        BulkRenameMode.Lowercase => stem.ToLower(CultureInfo.CurrentCulture),
        BulkRenameMode.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(stem.ToLower(CultureInfo.CurrentCulture)),
        _ => stem
    };

    private static string FormatNumber(int index, string startText)
    {
        var start = int.TryParse(startText, out var parsed) ? parsed : 1;
        var width = Math.Max(2, (start + index).ToString(CultureInfo.InvariantCulture).Length);
        return (start + index).ToString($"D{width}", CultureInfo.InvariantCulture);
    }

    private static void Apply(IReadOnlyList<BulkRenamePlanItem> plan)
    {
        var changed = plan.Where(item => !PathsExactlyEqual(item.SourcePath, item.DestinationPath)).ToArray();
        var currentPaths = changed.ToDictionary(item => item, item => item.SourcePath);

        try
        {
            foreach (var item in changed)
            {
                if (!PathExists(item.SourcePath))
                {
                    throw new FileNotFoundException("The item to rename no longer exists.", item.SourcePath);
                }

                var parent = Path.GetDirectoryName(item.SourcePath)!;
                var temporaryPath = Path.Combine(parent, $".winthunar-rename-{Guid.NewGuid():N}.tmp");
                Move(item.SourcePath, temporaryPath);
                currentPaths[item] = temporaryPath;
            }

            foreach (var item in changed)
            {
                Move(currentPaths[item], item.DestinationPath);
                currentPaths[item] = item.DestinationPath;
            }
        }
        catch
        {
            foreach (var item in changed.Reverse())
            {
                var currentPath = currentPaths[item];
                if (PathExists(currentPath) && !PathExists(item.SourcePath))
                {
                    try
                    {
                        Move(currentPath, item.SourcePath);
                    }
                    catch
                    {
                        // Preserve the original exception; any surviving temporary path remains recoverable.
                    }
                }
            }

            throw;
        }
    }

    private static void Move(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool PathsExactlyEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.Ordinal);
}
