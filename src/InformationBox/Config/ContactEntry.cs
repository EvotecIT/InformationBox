using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Represents a contact method for the service desk.
/// </summary>
public sealed record ContactEntry
{
    /// <summary>
    /// Gets the display label for the contact.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the contact email address, if any.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }
        = null;

    /// <summary>
    /// Gets the contact phone number, if any.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }
        = null;
}
