using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Authentication configuration for Graph delegated calls.
/// </summary>
public sealed record AuthConfig
{
    /// <summary>
    /// Gets the application (client) ID used to request Graph tokens.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;
}
