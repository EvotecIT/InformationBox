using System.Text.Json.Serialization;

namespace InformationBox.Config.Fixes;

/// <summary>
/// Represents a user-invokable fix action (script/command). Can override a built-in when <see cref="Id"/> matches.
/// </summary>
public sealed record FixAction
{
    /// <summary>
    /// Optional stable identifier for built-in actions (enables overrides).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "Unnamed action";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;

    [JsonPropertyName("confirm")]
    public string? ConfirmText { get; init; }

    [JsonPropertyName("category")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FixCategory Category { get; init; } = FixCategory.Custom;

    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;

    [JsonPropertyName("order")]
    public int Order { get; init; } = 0;

    /// <summary>
    /// Indicates whether this action requires administrator privileges (UAC elevation).
    /// </summary>
    [JsonPropertyName("requiresAdmin")]
    public bool RequiresAdmin { get; init; } = false;
}
