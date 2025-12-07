using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Represents a single navigable link in the UI.
/// </summary>
public sealed record LinkEntry
{
    /// <summary>
    /// Gets the user-facing text for the link button.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination URL opened when the link is clicked.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional icon identifier for the link.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
        = null;

    /// <summary>
    /// Gets the section that groups the link in the UI.
    /// </summary>
    [JsonPropertyName("section")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LinkSection Section { get; init; } = LinkSection.Support;

    /// <summary>
    /// Gets a value indicating whether the link should be displayed.
    /// </summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;

    /// <summary>
    /// Gets the order used when sorting links within a section.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; init; } = 0;
}
