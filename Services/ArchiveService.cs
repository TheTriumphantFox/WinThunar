using System.IO.Compression;

namespace WinThunar.Services;

public sealed class ArchiveService
{
    public async Task CreateAsync(
        IReadOnlyCollection<string> sourcePaths,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if (sourcePaths.Count == 0)
        {
            throw new ArgumentException("At least one item is required to create an archive.", nameof(sourcePaths));
        }

        var destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException($"'{Path.GetFileName(destination)}' already exists.");
        }

        var parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The archive destination needs a parent folder.");
        if (!Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException($"Folder not found: {parent}");
        }

        var temporaryPath = Path.Combine(parent, $".winthunar-archive-{Guid.NewGuid():N}.tmp");
        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create);
                foreach (var sourcePath in sourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddPath(archive, Path.GetFullPath(sourcePath), cancellationToken);
                }
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var archive = Path.GetFullPath(archivePath);
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationPath));
        if (!File.Exists(archive))
        {
            throw new FileNotFoundException("The archive no longer exists.", archive);
        }
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException($"'{Path.GetFileName(destination)}' already exists.");
        }

        var parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The extraction destination needs a parent folder.");
        var temporaryPath = Path.Combine(parent, $".winthunar-extract-{Guid.NewGuid():N}.tmp");
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZipFile.ExtractToDirectory(archive, temporaryPath, false);
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
            Directory.Move(temporaryPath, destination);
        }
        finally
        {
            TryDeleteDirectory(temporaryPath);
        }
    }

    private static void AddPath(
        ZipArchive archive,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var attributes = File.GetAttributes(sourcePath);
        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            archive.CreateEntryFromFile(sourcePath, Path.GetFileName(sourcePath), CompressionLevel.Optimal);
            return;
        }
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Archive creation stopped at the directory link or junction '{sourcePath}'.");
        }

        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath));
        var pending = new Stack<string>();
        pending.Push(sourcePath);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            var relativeDirectory = Path.GetRelativePath(sourcePath, directory);
            var archiveDirectory = relativeDirectory == "."
                ? rootName
                : $"{rootName}/{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}";
            var children = Directory.EnumerateFileSystemEntries(directory).ToArray();
            if (children.Length == 0)
            {
                archive.CreateEntry(archiveDirectory.TrimEnd('/') + "/");
            }

            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childAttributes = File.GetAttributes(child);
                if (childAttributes.HasFlag(FileAttributes.Directory))
                {
                    if (childAttributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        throw new IOException($"Archive creation stopped at the directory link or junction '{child}'.");
                    }

                    pending.Push(child);
                    continue;
                }

                var relative = Path.GetRelativePath(sourcePath, child).Replace(Path.DirectorySeparatorChar, '/');
                archive.CreateEntryFromFile(child, $"{rootName}/{relative}", CompressionLevel.Optimal);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
