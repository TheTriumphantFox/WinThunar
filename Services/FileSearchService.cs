namespace WinThunar.Services;

public sealed record FileSearchMatch(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime Modified,
    string Extension);

public sealed record FileSearchSummary(
    int ResultCount,
    int SkippedDirectories,
    bool LimitReached);

public sealed class FileSearchService
{
    public const int DefaultMaximumResults = 5000;
    private const int BatchSize = 40;

    public FileSearchSummary Search(
        string rootPath,
        string query,
        bool showHiddenFiles,
        Action<IReadOnlyList<FileSearchMatch>> reportBatch,
        CancellationToken cancellationToken = default,
        int maximumResults = DefaultMaximumResults,
        bool includeSubfolders = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(reportBatch);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");
        }

        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        var normalizedQuery = query.Trim();
        var pendingDirectories = new Stack<string>();
        var batch = new List<FileSearchMatch>(BatchSize);
        var resultCount = 0;
        var skippedDirectories = 0;
        var limitReached = false;
        pendingDirectories.Push(Path.GetFullPath(rootPath));

        while (pendingDirectories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(directory)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skippedDirectories++;
                continue;
            }

            for (var index = children.Length - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childPath = children[index];
                try
                {
                    var attributes = File.GetAttributes(childPath);
                    var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(childPath));
                    if (!showHiddenFiles && IsHidden(name, attributes))
                    {
                        continue;
                    }

                    if (includeSubfolders && isDirectory && !attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        pendingDirectories.Push(childPath);
                    }

                    if (!name.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }

                    var info = isDirectory
                        ? (FileSystemInfo)new DirectoryInfo(childPath)
                        : new FileInfo(childPath);
                    batch.Add(new FileSearchMatch(
                        name,
                        info.FullName,
                        isDirectory,
                        isDirectory ? 0 : ((FileInfo)info).Length,
                        info.LastWriteTime,
                        isDirectory ? string.Empty : info.Extension));
                    resultCount++;

                    if (batch.Count == BatchSize)
                    {
                        reportBatch(batch.ToArray());
                        batch.Clear();
                    }

                    if (resultCount >= maximumResults)
                    {
                        limitReached = true;
                        pendingDirectories.Clear();
                        break;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
                {
                    if (Directory.Exists(childPath))
                    {
                        skippedDirectories++;
                    }
                }
            }
        }

        if (batch.Count > 0)
        {
            reportBatch(batch.ToArray());
        }

        return new FileSearchSummary(resultCount, skippedDirectories, limitReached);
    }

    private static bool IsHidden(string name, FileAttributes attributes) =>
        name.StartsWith('.') ||
        attributes.HasFlag(FileAttributes.Hidden) ||
        attributes.HasFlag(FileAttributes.System);
}
