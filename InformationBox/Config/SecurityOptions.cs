using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Controls potentially sensitive execution behaviors.
/// </summary>
public sealed record SecurityOptions
{
    /// <summary>
    /// Gets a value indicating whether the app is allowed to trigger UAC elevation prompts for fix actions.
    /// When false, actions marked as <c>requiresAdmin</c> are executed without elevation (and may fail if not already elevated).
    /// </summary>
    [JsonPropertyName("allowElevation")]
    public bool AllowElevation { get; init; } = false;
}

