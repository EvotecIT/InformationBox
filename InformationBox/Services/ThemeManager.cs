using System;
using System.Collections.Generic;
using System.Windows;

namespace InformationBox.Services;

/// <summary>
/// Manages application theme loading and switching.
/// </summary>
public static class ThemeManager
{
    private const string ThemeResourceKey = "CurrentTheme";

    /// <summary>
    /// Gets the list of available theme names.
    /// </summary>
    public static IReadOnlyList<string> AvailableThemes { get; } = new[]
    {
        "Light",
        "Dark",
        "Classic",
        "Ocean",
        "Forest",
        "Sunset"
    };

    /// <summary>
    /// Gets the currently applied theme name.
    /// </summary>
    public static string CurrentTheme { get; private set; } = "Light";

    /// <summary>
    /// Applies the specified theme to the application.
    /// </summary>
    /// <param name="themeName">Theme name.</param>
    public static void ApplyTheme(string themeName)
    {
        var normalizedName = NormalizeThemeName(themeName);
        var themeUri = new Uri($"pack://application:,,,/Themes/{normalizedName}.xaml", UriKind.Absolute);

        ResourceDictionary themeDictionary;
        try
        {
            themeDictionary = new ResourceDictionary { Source = themeUri };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load theme '{normalizedName}': {ex.Message}. Falling back to Light.");
            themeDictionary = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Themes/Light.xaml", UriKind.Absolute)
            };
            normalizedName = "Light";
        }

        var appResources = Application.Current.Resources;

        // Remove any existing theme dictionary
        ResourceDictionary? existingTheme = null;
        foreach (var dict in appResources.MergedDictionaries)
        {
            if (dict.Contains(ThemeResourceKey) || dict.Source?.ToString().Contains("/Themes/") == true)
            {
                existingTheme = dict;
                break;
            }
        }

        if (existingTheme != null)
        {
            appResources.MergedDictionaries.Remove(existingTheme);
        }

        // Mark this dictionary as a theme dictionary for easy identification
        themeDictionary[ThemeResourceKey] = normalizedName;

        appResources.MergedDictionaries.Add(themeDictionary);
        CurrentTheme = normalizedName;

        Logger.Info($"Applied theme: {normalizedName}");
    }

    /// <summary>
    /// Normalizes the theme name to a valid value.
    /// </summary>
    private static string NormalizeThemeName(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return "Light";

        // Check if it's a known theme (case-insensitive)
        foreach (var theme in AvailableThemes)
        {
            if (theme.Equals(themeName, StringComparison.OrdinalIgnoreCase))
                return theme;
        }

        return "Light";
    }
}
