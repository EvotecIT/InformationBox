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

    private string _settingsPath = SettingsPath;

    /// <summary>
    /// Gets or sets the user's preferred theme.
    /// </summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Gets or sets the OTP vault file path.
    /// </summary>
    [JsonPropertyName("otpVaultPath")]
    public string? OtpVaultPath { get; set; }

    /// <summary>
    /// Gets or sets whether the OTP tab uses the single-code compact view.
    /// </summary>
    [JsonPropertyName("otpCompactMode")]
    public bool OtpCompactMode { get; set; } = true;

    /// <summary>
    /// Loads user settings from disk, or returns defaults if not found.
    /// </summary>
    /// <param name="settingsPath">Optional settings profile path; defaults to this Windows user’s settings.</param>
    public static UserSettings Load(string? settingsPath = null)
    {
        string path = settingsPath is null ? SettingsPath : Path.GetFullPath(settingsPath);
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null)
                {
                    settings._settingsPath = path;
                    Logger.Info($"User settings loaded from {path}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load user settings: {ex.Message}");
        }

        return new UserSettings { _settingsPath = path };
    }

    /// <summary>
    /// Saves the current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            Logger.Info($"User settings saved to {_settingsPath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save user settings: {ex.Message}");
        }
    }
}
