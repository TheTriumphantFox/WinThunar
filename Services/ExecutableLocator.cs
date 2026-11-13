using System.Collections.Concurrent;

namespace WinThunar.Services;

/// <summary>
/// Finds helper programs by absolute path. Windows looks for a bare program name in the current
/// folder first, and shell execute also looks in the working folder, so a program planted in a
/// folder the user only browses must never be what runs.
/// </summary>
public static class ExecutableLocator
{
    private static readonly ConcurrentDictionary<string, string> Found = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] DefaultExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    public static string PowerShell { get; } =
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    public static string Explorer { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    /// <summary>Git for Windows, from PATH only; null when it is not installed.</summary>
    public static string? Git => FindOnPath("git.exe");

    /// <summary>Windows Terminal's app alias, from PATH only; null when it is not installed.</summary>
    public static string? WindowsTerminal => FindOnPath("wt.exe");

    /// <summary>
    /// Searches this process's PATH for an exact file name. A found path is remembered while it
    /// still exists; a miss is searched again next time, so installing Git later is noticed.
    /// </summary>
    public static string? FindOnPath(string fileName)
    {
        if (Found.TryGetValue(fileName, out var known) && File.Exists(known))
        {
            return known;
        }

        var path = Search([fileName], Environment.GetEnvironmentVariable("PATH"));
        if (path is null)
        {
            Found.TryRemove(fileName, out _);
            return null;
        }

        Found[fileName] = path;
        return path;
    }

    /// <summary>
    /// Returns the first candidate found in the fully qualified entries of <paramref name="pathVariable"/>,
    /// in order. Empty, relative and drive-relative entries are skipped because they would resolve
    /// against the current folder.
    /// </summary>
    public static string? Search(IReadOnlyList<string> candidates, string? pathVariable)
    {
        foreach (var rawEntry in (pathVariable ?? string.Empty).Split(Path.PathSeparator))
        {
            var entry = rawEntry.Trim().Trim('"');
            if (entry.Length == 0 || !Path.IsPathFullyQualified(entry))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                string full;
                try
                {
                    full = Path.Combine(entry, candidate);
                }
                catch (ArgumentException)
                {
                    break;
                }

                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a user plugin's command for shell execute with <paramref name="workingDirectory"/>
    /// as its working folder. A full path or a URI is returned unchanged. A bare name is looked up
    /// on PATH with PATHEXT; when it is not there it is returned unchanged for the shell's App
    /// Paths lookup, unless the working folder holds a matching program, which is refused.
    /// </summary>
    public static string ResolveShellCommand(string command, string workingDirectory, string? pathVariable = null, string? pathExtensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        command = command.Trim();
        if (Path.IsPathFullyQualified(command) ||
            (Uri.TryCreate(command, UriKind.Absolute, out var uri) && !uri.IsFile && uri.Scheme.Length > 1))
        {
            return command;
        }

        if (command.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 || Path.IsPathRooted(command))
        {
            throw new InvalidOperationException(
                $"The plugin command \"{command}\" is a relative path, which would start a program from the browsed folder. Use a full path or a program name on PATH.");
        }

        var extensions = (pathExtensions ?? Environment.GetEnvironmentVariable("PATHEXT"))?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(extension => extension.StartsWith('.'))
            .ToArray();
        if (extensions is null || extensions.Length == 0)
        {
            extensions = DefaultExtensions;
        }

        var candidates = Path.HasExtension(command)
            ? [command]
            : extensions.Select(extension => command + extension).ToArray();
        if (Search(candidates, pathVariable ?? Environment.GetEnvironmentVariable("PATH")) is { } found)
        {
            return found;
        }

        if (!string.IsNullOrEmpty(workingDirectory) &&
            candidates.Append(command).Any(candidate => File.Exists(Path.Combine(workingDirectory, candidate))))
        {
            throw new InvalidOperationException(
                $"The plugin command \"{command}\" is not on PATH, and the browsed folder holds a program with that name. WinThunar did not start it.");
        }

        return command;
    }
}
