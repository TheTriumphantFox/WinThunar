using System.Text.Json;

namespace WinThunar.Services;

public sealed class UpdateSettings
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    public bool CheckAutomatically { get; set; } = true;
    public DateTimeOffset? LastCheck { get; set; }

    /// <summary>
    /// A check time in the future means the clock moved backwards; treat it as due rather than
    /// waiting out the skew.
    /// </summary>
    public bool IsCheckDue(DateTimeOffset now) =>
        CheckAutomatically &&
        (LastCheck is not { } last || last > now || now - last >= CheckInterval);
}

/// <summary>
/// Update preferences live apart from session.json because every window rewrites the session from
/// its own snapshot, which would roll back a check time recorded by another window.
/// </summary>
public sealed class UpdateSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public UpdateSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinThunar",
            "updates.json");
    }

    public UpdateSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new UpdateSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(_settingsPath), SerializerOptions)
                ?? new UpdateSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UpdateSettings();
        }
    }

    public void Save(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The update settings file needs a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
