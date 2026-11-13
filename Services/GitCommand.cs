using System.Diagnostics;

namespace WinThunar.Services;

/// <summary>
/// Start settings for Git run on a folder the user is only browsing. A repository's own config can
/// name programs for Git to run (filters, an fsmonitor hook, a signature verifier, a transport), and
/// a downloaded or extracted folder can carry that config, so none of them may run.
/// </summary>
public static class GitCommand
{
    /// <summary>Lists filter settings with their scope; <c>-z</c> output is scope NUL key LF value NUL.</summary>
    public static readonly string[] ListFilterArguments = ["config", "-z", "--show-scope", "--get-regexp", @"^filter\."];

    public static ProcessStartInfo CreateStartInfo(
        string gitPath,
        string workingDirectory,
        IEnumerable<string> arguments,
        IReadOnlyCollection<string> repositoryFilters)
    {
        var start = new ProcessStartInfo(gitPath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        start.ArgumentList.Add("--no-optional-locks");
        // Command-line settings outrank every config file, including the repository's.
        AddSetting(start, "core.fsmonitor", "false");
        AddSetting(start, "protocol.allow", "never");
        AddSetting(start, "log.showSignature", "false");
        foreach (var filter in repositoryFilters)
        {
            // An empty command disables the filter; a required filter that does not run would fail the command.
            AddSetting(start, $"filter.{filter}.clean", string.Empty);
            AddSetting(start, $"filter.{filter}.smudge", string.Empty);
            AddSetting(start, $"filter.{filter}.process", string.Empty);
            AddSetting(start, $"filter.{filter}.required", "false");
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A caller's repository environment must not redirect this browser's Git commands.
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(key);
        }

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // A partial clone would otherwise fetch missing objects, which can run a configured SSH command.
        start.Environment["GIT_NO_LAZY_FETCH"] = "1";
        return start;
    }

    /// <summary>
    /// Names of filters with any setting from the repository's own config (local or worktree scope,
    /// including files it includes). Filters set only in system or global config, such as Git LFS,
    /// are the user's own and keep working. Returns null when a name cannot be overridden safely:
    /// <c>-c</c> splits at the first <c>=</c>, so such a filter could not be switched off.
    /// </summary>
    public static IReadOnlyList<string>? RepositoryFilterNames(string listFilterOutput)
    {
        var names = new List<string>();
        var records = listFilterOutput.Split('\0');
        for (var index = 0; index + 1 < records.Length; index += 2)
        {
            var scope = records[index];
            if (!scope.Equals("local", StringComparison.OrdinalIgnoreCase) &&
                !scope.Equals("worktree", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = records[index + 1];
            var newline = entry.IndexOf('\n');
            var key = newline < 0 ? entry : entry[..newline];
            const string prefix = "filter.";
            var lastDot = key.LastIndexOf('.');
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || lastDot <= prefix.Length)
            {
                continue;
            }

            var name = key[prefix.Length..lastDot];
            if (name.Contains('='))
            {
                return null;
            }

            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>Arguments for the Git plugin's actions, hardened against repository-configured programs.</summary>
    public static IReadOnlyList<string> PluginArguments(string operation, IEnumerable<string> relativePaths) => operation switch
    {
        "status" => ["status", "--short", "--ignore-submodules=all"],
        "diff" => ["diff", "--no-ext-diff", "--no-textconv", "--ignore-submodules=all", "--", .. relativePaths],
        "log" => ["log", "--no-show-signature", "--oneline", "-20", "--", .. relativePaths],
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown Git plugin action.")
    };

    private static void AddSetting(ProcessStartInfo start, string key, string value)
    {
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"{key}={value}");
    }
}
