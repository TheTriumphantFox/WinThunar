using System.Text.Json.Serialization;

namespace WinThunar.Models;

[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<PluginTargetKind>))]
public enum PluginTargetKind
{
    None = 0,
    Files = 1,
    Folders = 2,
    Mixed = Files | Folders
}

public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<PluginActionManifest> Actions { get; set; } = [];
}

public sealed class PluginActionManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public List<string> Arguments { get; set; } = [];
    public List<string> FilePatterns { get; set; } = ["*"];
    public PluginTargetKind Targets { get; set; } = PluginTargetKind.Mixed;
    public int MinimumSelection { get; set; } = 1;
    public int MaximumSelection { get; set; }
    public bool RequiresConfirmation { get; set; }
}

public sealed record AvailablePluginAction(
    PluginManifest Plugin,
    PluginActionManifest Action);

public sealed record PluginInvocation(
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public sealed record PluginSelectionItem(
    string Name,
    string FullPath,
    bool IsDirectory);
