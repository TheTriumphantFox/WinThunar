using System.Globalization;
using System.IO.Enumeration;
using System.Text;

namespace WinThunar.Services;

/// <summary>A filesystem-only query: filters inspect metadata and never open file contents.</summary>
public sealed class FileSearchQuery
{
    private readonly List<Func<FileSearchMatch, bool>> _filters = [];

    public static FileSearchQuery Parse(string query, DateTime? today = null)
    {
        var parsed = new FileSearchQuery();
        foreach (var token in Tokenize(query))
        {
            var colon = token.IndexOf(':');
            var key = colon < 0 ? "" : token[..colon].ToLowerInvariant();
            var value = colon < 0 ? token : token[(colon + 1)..];
            switch (key)
            {
                case "name":
                    if (value.Length == 0) throw new FormatException("Use name:report or name:\"annual report\".");
                    parsed.AddName(value);
                    break;
                case "ext":
                case "type":
                    if (value.Length == 0) throw new FormatException("Use type:pdf, type:file, or type:folder.");
                    var extension = "." + value.TrimStart('*', '.');
                    parsed._filters.Add(value.ToLowerInvariant() switch
                    {
                        "folder" or "directory" => item => item.IsDirectory,
                        "file" => item => !item.IsDirectory,
                        _ => item => !item.IsDirectory && item.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)
                    });
                    break;
                case "size":
                    parsed._filters.Add(SizeFilter(value));
                    break;
                case "modified":
                case "date":
                    parsed._filters.Add(DateFilter(value, (today ?? DateTime.Today).Date));
                    break;
                default:
                    parsed.AddName(token);
                    break;
            }
        }
        return parsed;
    }

    public bool Matches(FileSearchMatch item) => _filters.All(filter => filter(item));

    private void AddName(string value) => _filters.Add(value.IndexOfAny(['*', '?']) >= 0
        ? item => FileSystemName.MatchesSimpleExpression(value, item.Name, ignoreCase: true)
        : item => item.Name.Contains(value, StringComparison.CurrentCultureIgnoreCase));

    private static IEnumerable<string> Tokenize(string query)
    {
        var token = new StringBuilder();
        var quoted = false;
        foreach (var character in query)
        {
            if (character == '"') quoted = !quoted;
            else if (char.IsWhiteSpace(character) && !quoted)
            {
                if (token.Length == 0) continue;
                yield return token.ToString();
                token.Clear();
            }
            else token.Append(character);
        }
        if (quoted) throw new FormatException("Close the quotation mark in the search query.");
        if (token.Length > 0) yield return token.ToString();
    }

    private static Func<FileSearchMatch, bool> SizeFilter(string value)
    {
        var (comparison, number) = SplitComparison(value);
        var range = number.Split("..", StringSplitOptions.None);
        if (range.Length == 2 && comparison == "=")
        {
            var minimum = Bytes(range[0]);
            var maximum = Bytes(range[1]);
            if (minimum > maximum) throw new FormatException("The minimum size must be less than the maximum size.");
            return item => !item.IsDirectory && item.Size >= minimum && item.Size <= maximum;
        }
        var bytes = Bytes(number);
        return item => !item.IsDirectory && Compare(item.Size.CompareTo(bytes), comparison);
    }

    private static long Bytes(string value)
    {
        var units = new[] { "TB", "GB", "MB", "KB", "B" };
        var unit = units.FirstOrDefault(unit => value.EndsWith(unit, StringComparison.OrdinalIgnoreCase));
        var digits = unit is null ? value : value[..^unit.Length];
        var multiplier = unit switch { "TB" => 1L << 40, "GB" => 1L << 30, "MB" => 1L << 20, "KB" => 1L << 10, _ => 1L };
        try
        {
            if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number < 0)
                throw new FormatException();
            return checked((long)(number * multiplier));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        { throw new FormatException("Use size:>10MB, size:1KB..5MB, or a size in bytes."); }
    }

    private static Func<FileSearchMatch, bool> DateFilter(string value, DateTime today)
    {
        if (value.Equals("today", StringComparison.OrdinalIgnoreCase)) return item => item.Modified.Date == today;
        if (value.Equals("yesterday", StringComparison.OrdinalIgnoreCase)) return item => item.Modified.Date == today.AddDays(-1);
        if (value.Equals("last7days", StringComparison.OrdinalIgnoreCase))
            return item => item.Modified.Date >= today.AddDays(-6) && item.Modified.Date <= today;
        var (comparison, dates) = SplitComparison(value);
        var range = dates.Split("..", StringSplitOptions.None);
        var first = Date(range[0]);
        if (range.Length == 2 && comparison == "=")
        {
            var last = Date(range[1]);
            if (first > last) throw new FormatException("The start date must be before the end date.");
            return item => item.Modified.Date >= first && item.Modified.Date <= last;
        }
        if (range.Length != 1) throw new FormatException("Use modified:2026-10-01..2026-10-21.");
        return item => Compare(item.Modified.Date.CompareTo(first), comparison);
    }

    private static DateTime Date(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) ? date : throw new FormatException("Use modified:today, modified:last7days, or an ISO date such as modified:>=2026-10-01.");

    private static (string Comparison, string Value) SplitComparison(string value)
    {
        foreach (var comparison in new[] { ">=", "<=", ">", "<", "=" })
            if (value.StartsWith(comparison, StringComparison.Ordinal)) return (comparison, value[comparison.Length..]);
        return ("=", value);
    }

    private static bool Compare(int result, string comparison) => comparison switch
    { ">" => result > 0, ">=" => result >= 0, "<" => result < 0, "<=" => result <= 0, _ => result == 0 };
}
