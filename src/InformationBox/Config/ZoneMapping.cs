using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Maps a DNS domain to a zone label for UI grouping.
/// </summary>
public sealed record ZoneMapping
{
    /// <summary>
    /// Gets the DNS suffix matched against the device domain.
    /// </summary>
    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Gets the friendly zone label shown in the UI.
    /// </summary>
    [JsonPropertyName("zone")]
    public string Zone { get; init; } = string.Empty;
}
