using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Branding and white-label options.
/// </summary>
public sealed record Branding
{
    /// <summary>
    /// Gets the product title displayed across the window.
    /// </summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = "Information Box";

    /// <summary>
    /// Gets the primary accent color (hex).
    /// </summary>
    [JsonPropertyName("primaryColor")]
    public string PrimaryColor { get; init; } = "#0050b3";

    /// <summary>
    /// Gets the secondary accent color (hex).
    /// </summary>
    [JsonPropertyName("secondaryColor")]
    public string SecondaryColor { get; init; } = "#e5f1ff";

    /// <summary>
    /// Gets the optional logo asset path or URL.
    /// </summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }
        = null;
}
