using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InformationBox.Services;

namespace InformationBox.Config;

/// <summary>
/// User-specific settings that persist across sessions.
/// Stored separately from the main config to allow user customization.
/// </summary>
public sealed class UserSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InformationBox",
        "settings.json");

    /// <summary>
    /// Gets or sets the user's preferred theme.
    /// </summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Loads user settings from disk, or returns defaults if not found.
    /// </summary>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null)
                {
                    Logger.Info($"User settings loaded from {SettingsPath}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load user settings: {ex.Message}");
        }

        return new UserSettings();
    }

    /// <summary>
    /// Saves the current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            Logger.Info($"User settings saved to {SettingsPath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save user settings: {ex.Message}");
        }
    }
}
