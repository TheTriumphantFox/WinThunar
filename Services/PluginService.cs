using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinThunar.Models;
using SearchPattern = System.IO.Enumeration.FileSystemName;

namespace WinThunar.Services;

public sealed class PluginService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _bundledDirectory;
    private readonly string _userDirectory;

    public PluginService(string? bundledDirectory = null, string? userDirectory = null)
    {
        _bundledDirectory = bundledDirectory ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
        _userDirectory = userDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinThunar",
            "Plugins");
    }

    public IReadOnlyList<PluginManifest> Plugins { get; private set; } = [];
    public IReadOnlyList<string> Diagnostics { get; private set; } = [];
    public string UserDirectory => _userDirectory;

    public void Reload()
    {
        var plugins = new Dictionary<string, PluginManifest>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        LoadDirectory(_bundledDirectory, plugins, diagnostics);
        LoadDirectory(_userDirectory, plugins, diagnostics);
        Plugins = plugins.Values.OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<AvailablePluginAction> GetApplicableActions(
        IReadOnlyCollection<PluginSelectionItem> selection)
    {
        if (selection.Count == 0)
        {
            return [];
        }

        var result = new List<AvailablePluginAction>();
        foreach (var plugin in Plugins.Where(plugin => plugin.Enabled))
        {
            foreach (var action in plugin.Actions)
            {
                if (IsApplicable(action, selection))
                {
                    result.Add(new AvailablePluginAction(plugin, action));
                }
            }
        }

        return result;
    }

    public PluginInvocation BuildInvocation(
        PluginActionManifest action,
        IReadOnlyCollection<string> selectedPaths,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(action.Command) || action.Command.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Built-in actions do not create external process invocations.");
        }

        var paths = selectedPaths.Select(Path.GetFullPath).ToArray();
        if (paths.Length == 0)
        {
            throw new ArgumentException("At least one selected path is required.", nameof(selectedPaths));
        }

        var first = paths[0];
        var arguments = new List<string>();
        foreach (var template in action.Arguments)
        {
            if (string.Equals(template, "{selected}", StringComparison.OrdinalIgnoreCase))
            {
                arguments.AddRange(paths);
                continue;
            }

            arguments.Add(template
                .Replace("{file}", first, StringComparison.OrdinalIgnoreCase)
                .Replace("{name}", Path.GetFileName(first), StringComparison.OrdinalIgnoreCase)
                .Replace("{directory}", workingDirectory, StringComparison.OrdinalIgnoreCase));
        }

        return new PluginInvocation(action.Command, arguments, workingDirectory);
    }

    public void Execute(PluginInvocation invocation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.Command,
            WorkingDirectory = invocation.WorkingDirectory,
            UseShellExecute = true
        };
        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
    }

    public void SaveUserPlugin(PluginManifest plugin)
    {
        ValidatePlugin(plugin);
        Directory.CreateDirectory(_userDirectory);
        var path = Path.Combine(_userDirectory, $"{plugin.Id}.json");
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(plugin, SerializerOptions));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        Reload();
    }

    private static bool IsApplicable(PluginActionManifest action, IReadOnlyCollection<PluginSelectionItem> selection)
    {
        if (selection.Count < Math.Max(1, action.MinimumSelection) ||
            (action.MaximumSelection > 0 && selection.Count > action.MaximumSelection))
        {
            return false;
        }

        var hasFiles = selection.Any(entry => !entry.IsDirectory);
        var hasFolders = selection.Any(entry => entry.IsDirectory);
        if ((hasFiles && !action.Targets.HasFlag(PluginTargetKind.Files)) ||
            (hasFolders && !action.Targets.HasFlag(PluginTargetKind.Folders)))
        {
            return false;
        }

        var patterns = action.FilePatterns.Count == 0 ? ["*"] : action.FilePatterns;
        return selection.All(entry => entry.IsDirectory || patterns.Any(pattern =>
            SearchPattern.MatchesSimpleExpression(pattern, entry.Name, ignoreCase: true)));
    }

    private static void LoadDirectory(
        string directory,
        IDictionary<string, PluginManifest> plugins,
        ICollection<string> diagnostics)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var plugin = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path), SerializerOptions)
                    ?? throw new InvalidDataException("The manifest was empty.");
                ValidatePlugin(plugin);
                plugins[plugin.Id] = plugin;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                diagnostics.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }

    private static void ValidatePlugin(PluginManifest plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrWhiteSpace(plugin.Id) || plugin.Id.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException("Plugin IDs may contain only letters, digits, periods, hyphens, and underscores.");
        }
        if (string.IsNullOrWhiteSpace(plugin.Name))
        {
            throw new InvalidDataException("The plugin name is required.");
        }
        if (plugin.Actions is null)
        {
            throw new InvalidDataException("The plugin actions list cannot be null.");
        }
        if (plugin.Actions.Any(action => action is null ||
                                         action.Arguments is null ||
                                         action.FilePatterns is null ||
                                         action.Arguments.Any(argument => argument is null) ||
                                         action.FilePatterns.Any(pattern => string.IsNullOrWhiteSpace(pattern)) ||
                                         string.IsNullOrWhiteSpace(action.Id) ||
                                         string.IsNullOrWhiteSpace(action.Name) ||
                                         string.IsNullOrWhiteSpace(action.Command)))
        {
            throw new InvalidDataException("Every plugin action requires non-null argument and pattern lists plus an ID, name, and command.");
        }
        if (plugin.Actions.GroupBy(action => action.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Plugin action IDs must be unique within a plugin.");
        }
    }
}
