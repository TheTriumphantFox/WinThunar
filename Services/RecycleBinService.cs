using System.Security.Principal;
using WinThunar.Models;

namespace WinThunar.Services;

public static class RecycleBinService
{
    public const string VirtualPath = "shell:RecycleBinFolder";

    public static IReadOnlyList<FileSystemEntry> ReadEntries(
        BrowserSortColumn sortColumn,
        bool sortDescending,
        bool foldersFirst,
        CancellationToken cancellationToken)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            return [];
        }

        var entries = new List<FileSystemEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recycleDirectory = Path.Combine(drive.Name, "$Recycle.Bin", sid);
            if (!Directory.Exists(recycleDirectory))
            {
                continue;
            }

            IEnumerable<string> metadataFiles;
            try
            {
                metadataFiles = Directory.EnumerateFiles(recycleDirectory, "$I*").ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var metadataPath in metadataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var metadata = ReadMetadata(metadataPath);
                    if (metadata is null)
                    {
                        continue;
                    }

                    var recycledPath = Path.Combine(
                        recycleDirectory,
                        "$R" + Path.GetFileName(metadataPath)[2..]);
                    var isDirectory = Directory.Exists(recycledPath);
                    if (!isDirectory && !File.Exists(recycledPath))
                    {
                        continue;
                    }

                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(metadata.OriginalPath));
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = metadata.OriginalPath;
                    }

                    entries.Add(new FileSystemEntry(
                        name,
                        recycledPath,
                        isDirectory,
                        isDirectory ? string.Empty : FormatSize(metadata.OriginalSize),
                        isDirectory ? "Folder" : FileType(Path.GetExtension(metadata.OriginalPath)),
                        metadata.DeletedAt.ToString("g"),
                        isDirectory ? "\uE8B7" : "\uE8A5",
                        isDirectory ? 0 : metadata.OriginalSize,
                        metadata.DeletedAt));
                }
                catch
                {
                    // Corrupt, inaccessible, and concurrently removed recycle records are skipped.
                }
            }
        }

        return SortEntries(entries, sortColumn, sortDescending, foldersFirst).ToList();
    }

    private static RecycleMetadata? ReadMetadata(string metadataPath)
    {
        using var stream = new FileStream(
            metadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new BinaryReader(stream, System.Text.Encoding.Unicode);
        if (stream.Length < 24)
        {
            return null;
        }

        var version = reader.ReadInt64();
        var originalSize = reader.ReadInt64();
        var deletionFileTime = reader.ReadInt64();
        string originalPath;
        if (version >= 2)
        {
            if (stream.Length < 28)
            {
                return null;
            }

            var characterCount = reader.ReadInt32();
            if (characterCount <= 0 || characterCount > 32768 ||
                stream.Position + (characterCount * sizeof(char)) > stream.Length)
            {
                return null;
            }

            originalPath = new string(reader.ReadChars(characterCount)).TrimEnd('\0');
        }
        else
        {
            var availableCharacters = (int)Math.Min(260, (stream.Length - stream.Position) / sizeof(char));
            originalPath = new string(reader.ReadChars(availableCharacters)).TrimEnd('\0');
        }

        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return null;
        }

        DateTime deletedAt;
        try
        {
            deletedAt = DateTime.FromFileTime(deletionFileTime);
        }
        catch
        {
            deletedAt = DateTime.MinValue;
        }

        return new RecycleMetadata(originalPath, Math.Max(0, originalSize), deletedAt);
    }

    private static IOrderedEnumerable<FileSystemEntry> SortEntries(
        IEnumerable<FileSystemEntry> entries,
        BrowserSortColumn sortColumn,
        bool sortDescending,
        bool foldersFirst)
    {
        Func<FileSystemEntry, object> keySelector = sortColumn switch
        {
            BrowserSortColumn.Size => entry => entry.ByteSize,
            BrowserSortColumn.Type => entry => entry.Type,
            BrowserSortColumn.Modified => entry => entry.ModifiedTime,
            _ => entry => entry.Name
        };

        IOrderedEnumerable<FileSystemEntry> ordered;
        if (foldersFirst)
        {
            ordered = entries.OrderByDescending(entry => entry.IsDirectory);
            ordered = sortDescending
                ? ordered.ThenByDescending(keySelector, ObjectKeyComparer.Instance)
                : ordered.ThenBy(keySelector, ObjectKeyComparer.Instance);
        }
        else
        {
            ordered = sortDescending
                ? entries.OrderByDescending(keySelector, ObjectKeyComparer.Instance)
                : entries.OrderBy(keySelector, ObjectKeyComparer.Instance);
        }

        return ordered.ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private static string FileType(string extension) =>
        string.IsNullOrWhiteSpace(extension) ? "File" : $"{extension.TrimStart('.').ToUpperInvariant()} file";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }

    private sealed class ObjectKeyComparer : IComparer<object>
    {
        public static ObjectKeyComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (x is string left && y is string right)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
            }

            return Comparer<object>.Default.Compare(x, y);
        }
    }

    private sealed record RecycleMetadata(string OriginalPath, long OriginalSize, DateTime DeletedAt);
}
