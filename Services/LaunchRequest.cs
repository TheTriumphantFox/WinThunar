using System.Text;

namespace WinThunar.Services;

/// <summary>
/// A folder-open request from the command line or a redirected activation. Accepts plain paths,
/// file URIs, Explorer-style switches (<c>/select,</c>, <c>/root,</c>, <c>/n</c>, <c>/e</c>), and
/// <c>--select</c>.
/// </summary>
public sealed record LaunchRequest(string? FolderPath, string? SelectPath)
{
    public const string RecycleBinPath = "shell:RecycleBinFolder";
    public const string ComputerPath = "shell:MyComputerFolder";

    private static readonly string[] ComputerAliases =
    [
        ComputerPath,
        "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
        "shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"
    ];

    private static readonly string[] RecycleBinAliases =
    [
        RecycleBinPath,
        "::{645FF040-5081-101B-9F08-08002B30309D}",
        "shell:::{645FF040-5081-101B-9F08-08002B30309D}"
    ];

    public static LaunchRequest Empty { get; } = new(null, null);

    public bool IsEmpty => FolderPath is null;

    public static LaunchRequest Parse(IEnumerable<string> arguments)
    {
        string? target = null;
        string? select = null;
        var tokens = arguments.ToList();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index].Trim();
            if (token.Length == 0)
            {
                continue;
            }

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var separator = token.IndexOf('=');
                var name = (separator < 0 ? token[2..] : token[2..separator]).ToLowerInvariant();
                var value = separator < 0 ? null : token[(separator + 1)..];
                if (name is "select" or "root" && value is null && index + 1 < tokens.Count)
                {
                    value = tokens[++index];
                }

                if (name == "select")
                {
                    select = value ?? select;
                }
                else if (name == "root")
                {
                    target = value ?? target;
                }

                continue;
            }

            if (token.StartsWith('/') && !token.StartsWith("//", StringComparison.Ordinal))
            {
                // Explorer chains switches with commas: /e,/select,C:\path, or /n,C:\path.
                var remainder = token;
                while (remainder.StartsWith('/'))
                {
                    var comma = remainder.IndexOf(',');
                    var name = (comma < 0 ? remainder[1..] : remainder[1..comma]).Trim().ToLowerInvariant();
                    remainder = comma < 0 ? string.Empty : remainder[(comma + 1)..].TrimStart();
                    if (name is "select" or "root")
                    {
                        var value = remainder;
                        if (value.Length == 0 && index + 1 < tokens.Count)
                        {
                            value = tokens[++index];
                        }

                        if (name == "select")
                        {
                            select = value;
                        }
                        else
                        {
                            target = value;
                        }

                        remainder = string.Empty;
                    }
                }

                if (remainder.Length > 0)
                {
                    target = remainder;
                }

                continue;
            }

            target = token;
        }

        return Resolve(target, select);
    }

    /// <summary>
    /// Parses an activation argument string, which for unpackaged apps can include the executable.
    /// </summary>
    public static LaunchRequest ParseCommandLine(string? commandLine, string? executablePath = null)
    {
        var tokens = SplitCommandLine(commandLine ?? string.Empty).ToList();
        if (tokens.Count > 0 && IsExecutable(tokens[0], executablePath))
        {
            tokens.RemoveAt(0);
        }

        return Parse(tokens);
    }

    /// <summary>Splits a command line the way CommandLineToArgvW does.</summary>
    public static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '\\')
            {
                var start = index;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    index++;
                }

                var slashes = index - start;
                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    current.Append('\\', slashes / 2);
                    if (slashes % 2 == 1)
                    {
                        current.Append('"');
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else
                {
                    current.Append('\\', slashes);
                    index--;
                }

                hasToken = true;
                continue;
            }

            if (character == '"')
            {
                if (inQuotes && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                hasToken = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(character))
            {
                if (hasToken)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            current.Append(character);
            hasToken = true;
        }

        if (hasToken)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static bool IsExecutable(string token, string? executablePath)
    {
        if (!token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        executablePath ??= Environment.ProcessPath;
        return executablePath is not null &&
            (string.Equals(token, executablePath, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Path.GetFileName(token), Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase));
    }

    private static LaunchRequest Resolve(string? target, string? select)
    {
        var selectPath = NormalizePath(select);
        if (selectPath is not null && (File.Exists(selectPath) || Directory.Exists(selectPath)))
        {
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(selectPath));
            if (parent is not null)
            {
                return new LaunchRequest(parent, selectPath);
            }

            // A drive root has no parent to select it in; open it instead.
            return new LaunchRequest(selectPath, null);
        }

        var targetPath = NormalizePath(target ?? select);
        if (targetPath is null)
        {
            return Empty;
        }

        if (RecycleBinAliases.Any(alias => string.Equals(alias, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            return new LaunchRequest(RecycleBinPath, null);
        }

        if (ComputerAliases.Any(alias => string.Equals(alias, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            return new LaunchRequest(ComputerPath, null);
        }

        if (Directory.Exists(targetPath))
        {
            return new LaunchRequest(targetPath, null);
        }

        // Opening a file path shows it selected in its folder, like Explorer.
        if (File.Exists(targetPath) && Path.GetDirectoryName(targetPath) is { } folder)
        {
            return new LaunchRequest(folder, targetPath);
        }

        return Empty;
    }

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().Trim('"');
        // A shell command of "%V" for a drive root arrives as C:" because \" escapes the quote.
        if (value.Length == 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        {
            value += Path.DirectorySeparatorChar;
        }
        if (value.StartsWith("::", StringComparison.Ordinal) ||
            value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            value = uri.LocalPath;
        }

        value = Environment.ExpandEnvironmentVariables(value);
        try
        {
            var full = Path.GetFullPath(value);
            // Keep "C:\" intact but drop trailing separators elsewhere.
            return Path.GetPathRoot(full) == full ? full : Path.TrimEndingDirectorySeparator(full);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
