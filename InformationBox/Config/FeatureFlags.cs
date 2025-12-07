using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Toggle individual feature areas in the UI.
/// </summary>
public sealed record FeatureFlags
{
    /// <summary>
    /// Gets a value indicating whether the Local Sites card is displayed.
    /// </summary>
    [JsonPropertyName("showLocalSites")]
    public bool ShowLocalSites { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether contextual help links are shown.
    /// </summary>
    [JsonPropertyName("showHelp")]
    public bool ShowHelp { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the contacts panel is visible.
    /// </summary>
    [JsonPropertyName("showContacts")]
    public bool ShowContacts { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether password health data is rendered.
    /// </summary>
    [JsonPropertyName("showHealth")]
    public bool ShowHealth { get; init; } = false;
}
