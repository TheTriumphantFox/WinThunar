using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace WinThunar.Services;

/// <summary>Explicit read-only Windows index queries; current filesystem metadata validates each candidate.</summary>
public sealed class IndexedFileSearchService
{
    public const int MaximumCandidates = 50000;
    private readonly Func<string, CancellationToken, IEnumerable<string>> _queryPaths;

    public IndexedFileSearchService() : this(QueryPaths) { }
    internal IndexedFileSearchService(Func<string, CancellationToken, IEnumerable<string>> queryPaths) => _queryPaths = queryPaths;

    public FileSearchSummary Search(IEnumerable<string> rootPaths, FileSearchQuery query, bool showHiddenFiles,
        Action<IReadOnlyList<FileSearchMatch>> reportBatch, CancellationToken cancellationToken = default,
        int maximumResults = FileSearchService.DefaultMaximumResults, bool includeSubfolders = true)
    {
        ArgumentNullException.ThrowIfNull(rootPaths);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reportBatch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        cancellationToken.ThrowIfCancellationRequested();
        var roots = rootPaths.Select(Path.GetFullPath).Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roots.Length == 0) return new(0, 0, false) { UsedWindowsIndex = true };
        var sql = BuildSql(roots, query, includeSubfolders);
        var batch = new List<FileSearchMatch>(40);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0, skipped = 0, candidates = 0;
        bool limit = false, candidateLimit = false;
        foreach (var candidate in _queryPaths(sql, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++candidates > MaximumCandidates) { candidateLimit = true; break; }
            try
            {
                if (!Path.IsPathFullyQualified(candidate)) { skipped++; continue; }
                var path = Path.GetFullPath(candidate);
                if (!seen.Add(path)) continue;
                if (!InScope(path, roots, includeSubfolders, showHiddenFiles, cancellationToken)) { skipped++; continue; }
                var attributes = File.GetAttributes(path);
                var directory = attributes.HasFlag(FileAttributes.Directory);
                var name = Path.GetFileName(path);
                if (!showHiddenFiles && Hidden(name, attributes)) continue;
                FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
                var match = new FileSearchMatch(name, path, directory, directory ? 0 : ((FileInfo)info).Length,
                    info.LastWriteTime, directory ? "" : info.Extension);
                if (!query.Matches(match) || (directory && query.ContentTerms.Count > 0)) continue;
                // Contents come from Windows' installed index filters. Do not reopen/hydrate the document.
                batch.Add(match);
                count++;
                if (batch.Count == 40) { reportBatch(batch.ToArray()); batch.Clear(); }
                if (count >= maximumResults) { limit = true; break; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { skipped++; }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (batch.Count > 0) reportBatch(batch.ToArray());
        return new(count, 0, limit) { UsedWindowsIndex = true, SkippedIndexEntries = skipped, IndexCandidateLimitReached = candidateLimit };
    }

    internal static string BuildSql(IReadOnlyList<string> roots, FileSearchQuery query, bool includeSubfolders)
    {
        var scope = includeSubfolders ? "SCOPE" : "DIRECTORY";
        var clauses = new List<string>
        {
            "(" + string.Join(" OR ", roots.Select(root => scope + "=" + Literal("file:" + root.Replace('\\', '/')))) + ")"
        };
        foreach (var name in query.NameTerms)
        {
            var pattern = new StringBuilder();
            var wildcard = name.IndexOfAny(['*', '?']) >= 0;
            if (!wildcard) pattern.Append('%');
            foreach (var character in name)
                pattern.Append(character switch { '*' => "%", '?' => "_", '%' => "[%]", '_' => "[_]", '[' => "[[]", ']' => "[]]", _ => character.ToString() });
            if (!wildcard) pattern.Append('%');
            clauses.Add("System.FileName LIKE " + Literal(pattern.ToString()));
        }
        foreach (var term in query.ContentTerms)
            clauses.Add("CONTAINS(System.Search.Contents," + Literal("\"" + term.Replace("\"", "\"\"") + "\"") + ")");
        return $"SELECT TOP {MaximumCandidates + 1} System.ItemPathDisplay FROM SYSTEMINDEX WHERE " + string.Join(" AND ", clauses);
    }

    private static string Literal(string value)
    {
        if (value.Any(char.IsControl)) throw new FormatException("Windows index queries cannot contain control characters.");
        return "'" + value.Replace("'", "''") + "'";
    }

    private static bool InScope(string path, string[] roots, bool recursive, bool showHidden, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(path);
        foreach (var root in roots)
        {
            if (parent is null) continue;
            var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!parent.Equals(root, StringComparison.OrdinalIgnoreCase) && (!recursive || !parent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;
            // A stale index entry must not expose descendants hidden by a folder or reached through a link.
            var ancestor = root;
            var allowed = true;
            var relative = Path.GetRelativePath(root, parent);
            foreach (var part in relative == "." ? [] : relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ancestor = Path.Combine(ancestor, part);
                var attributes = File.GetAttributes(ancestor);
                if (attributes.HasFlag(FileAttributes.ReparsePoint) || (!showHidden && Hidden(Path.GetFileName(ancestor), attributes))) { allowed = false; break; }
            }
            if (allowed) return true;
        }
        return false;
    }

    private static bool Hidden(string name, FileAttributes attributes) => name.StartsWith('.') ||
        (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

    private static IEnumerable<string> QueryPaths(string sql, CancellationToken cancellationToken)
    {
        object? connection = null, rows = null, fields = null, field = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection = CreateConnection();
            Set(connection!, "ConnectionTimeout", 5);
            Set(connection!, "CommandTimeout", 10);
            Set(connection!, "Mode", 1); // adModeRead
            Call(connection!, "Open", "Provider=Search.CollatorDSO.1;Extended Properties='Application=Windows';");
            cancellationToken.ThrowIfCancellationRequested();
            rows = Call(connection!, "Execute", sql);
            fields = Get(rows!, "Fields");
            field = Get(fields!, "Item", 0);
            while (!(bool)Get(rows!, "EOF")!)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Get(field!, "Value") is string path) yield return path;
                Call(rows!, "MoveNext");
            }
        }
        finally
        {
            Release(field);
            Release(fields);
            if (rows is not null) { TryClose(rows); Release(rows); }
            if (connection is not null) { TryClose(connection); Release(connection); }
        }
    }

    private static object? Get(object target, string member, params object[] args) => Invoke(target, member, BindingFlags.GetProperty, args);
    private static object CreateConnection()
    {
        try { return Activator.CreateInstance(Type.GetTypeFromProgID("ADODB.Connection", throwOnError: true)!)!; }
        catch (Exception ex) when (ex is COMException or TypeLoadException or MissingMethodException)
        { throw new IOException("Windows Search index is unavailable. Remove index:true to search the filesystem.", ex); }
    }
    private static void Set(object target, string member, object value) => Invoke(target, member, BindingFlags.SetProperty, [value]);
    private static object? Call(object target, string member, params object[] args) => Invoke(target, member, BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding, args);
    private static object? Invoke(object target, string member, BindingFlags flags, object[] args)
    {
        try { return target.GetType().InvokeMember(member, flags, null, target, args); }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
        { throw new IOException("Windows Search index is unavailable or could not complete this query. Remove index:true to search the filesystem. " + ex.GetBaseException().Message, ex); }
    }
    private static void TryClose(object target) { try { Call(target, "Close"); } catch (IOException) { } }
    private static void Release(object? target) { if (target is not null && Marshal.IsComObject(target)) Marshal.FinalReleaseComObject(target); }
}
