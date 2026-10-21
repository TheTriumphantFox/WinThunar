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
    bool LimitReached)
{
    public int SkippedContentFiles { get; init; }
    public bool UsedWindowsIndex { get; init; }
    public bool IndexCandidateLimitReached { get; init; }
    public int SkippedIndexEntries { get; init; }
}

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

        return SearchRoots([rootPath], query, showHiddenFiles, reportBatch, cancellationToken, maximumResults, includeSubfolders);
    }

    public FileSearchSummary SearchRoots(
        IEnumerable<string> rootPaths, string query, bool showHiddenFiles,
        Action<IReadOnlyList<FileSearchMatch>> reportBatch, CancellationToken cancellationToken = default,
        int maximumResults = DefaultMaximumResults, bool includeSubfolders = true)
    {
        ArgumentNullException.ThrowIfNull(rootPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(reportBatch);

        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        var parsedQuery = FileSearchQuery.Parse(query);
        if (parsedQuery.UseWindowsIndex)
            return new IndexedFileSearchService().Search(rootPaths, parsedQuery, showHiddenFiles, reportBatch, cancellationToken, maximumResults, includeSubfolders);
        var pendingDirectories = new Stack<string>();
        var batch = new List<FileSearchMatch>(BatchSize);
        var resultCount = 0;
        var skippedDirectories = 0;
        var skippedContentFiles = 0;
        var limitReached = false;
        foreach (var root in rootPaths.Reverse()) pendingDirectories.Push(Path.GetFullPath(root));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (pendingDirectories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(Path.TrimEndingDirectorySeparator(directory))) continue;
            IEnumerator<string> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skippedDirectories++;
                continue;
            }

            using (children)
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string childPath;
                try
                {
                    if (!children.MoveNext()) break;
                    childPath = children.Current;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    skippedDirectories++;
                    break;
                }
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

                    var info = isDirectory
                        ? (FileSystemInfo)new DirectoryInfo(childPath)
                        : new FileInfo(childPath);
                    var match = new FileSearchMatch(
                        name,
                        info.FullName,
                        isDirectory,
                        isDirectory ? 0 : ((FileInfo)info).Length,
                        info.LastWriteTime,
                        isDirectory ? string.Empty : info.Extension);
                    if (!parsedQuery.Matches(match)) continue;
                    if (parsedQuery.ContentTerms.Count > 0)
                    {
                        if (isDirectory) continue;
                        if (!TextFileContent.Contains(childPath, parsedQuery.ContentTerms, cancellationToken, out var skipped))
                        {
                            if (skipped) skippedContentFiles++;
                            continue;
                        }
                    }
                    batch.Add(match);
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

        return new FileSearchSummary(resultCount, skippedDirectories, limitReached) { SkippedContentFiles = skippedContentFiles };
    }

    private static bool IsHidden(string name, FileAttributes attributes) =>
        name.StartsWith('.') ||
        attributes.HasFlag(FileAttributes.Hidden) ||
        attributes.HasFlag(FileAttributes.System);
}
