using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinThunar.Services;

public enum AppThemePreference
{
    System,
    Light,
    Dark
}

public sealed class AppearanceSettings
{
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
}

/// <summary>Shared appearance preferences stay separate from each window's session snapshot.</summary>
public sealed class AppearanceSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<AppThemePreference>(allowIntegerValues: false) }
    };
    private readonly string _settingsPath;

    public AppearanceSettingsStore(string? settingsPath = null) => _settingsPath = settingsPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinThunar", "appearance.json");

    public AppearanceSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(_settingsPath), SerializerOptions) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        { return new(); }
    }

    public void Save(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Theme)) throw new ArgumentOutOfRangeException(nameof(settings), "Unknown color theme.");
        var directory = Path.GetDirectoryName(_settingsPath) ?? throw new InvalidOperationException("Appearance settings need a parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporary, _settingsPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
