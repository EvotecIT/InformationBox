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
    /// Gets the optional logo asset path or URL displayed in header.
    /// Supports: relative path, absolute path, URL, or pack URI.
    /// </summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; init; } = null;

    /// <summary>
    /// Gets the logo width in pixels. Use 0 for auto-sizing.
    /// </summary>
    [JsonPropertyName("logoWidth")]
    public int LogoWidth { get; init; } = 0;

    /// <summary>
    /// Gets the logo height in pixels. Default 32 for icon-style, use larger for full logos.
    /// </summary>
    [JsonPropertyName("logoHeight")]
    public int LogoHeight { get; init; } = 32;

    /// <summary>
    /// Gets the application icon path (used for window icon and taskbar).
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; } = null;

    /// <summary>
    /// Gets the company/vendor name shown in about or footer.
    /// </summary>
    [JsonPropertyName("companyName")]
    public string CompanyName { get; init; } = "Evotec";

    /// <summary>
    /// Gets the support email address used for log collection actions.
    /// </summary>
    [JsonPropertyName("supportEmail")]
    public string SupportEmail { get; init; } = "support@contoso.com";

    /// <summary>
    /// Gets the UI theme. Valid values: "Light", "Dark", "Classic", "Ocean", "Forest", "Sunset", "Auto".
    /// </summary>
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = "Light";
}
