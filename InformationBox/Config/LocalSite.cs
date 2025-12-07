using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Represents a local site link tied to a zone.
/// </summary>
public sealed record LocalSite
{
    /// <summary>
    /// Gets the user-facing description of the local site.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the address opened when the entry is selected.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets the zone identifier this site belongs to.
    /// </summary>
    [JsonPropertyName("zone")]
    public string Zone { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the link is shown.
    /// </summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;

    /// <summary>
    /// Gets the ordering value used when sorting sites.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; init; } = 0;
}
