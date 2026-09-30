using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Controls potentially sensitive execution behaviors.
/// </summary>
public sealed record SecurityOptions
{
    /// <summary>
    /// Gets the embedded build policy indicating whether the app may trigger UAC prompts for fix actions.
    /// Runtime configuration cannot change this value.
    /// When false, actions marked as <c>requiresAdmin</c> are executed without elevation (and may fail if not already elevated).
    /// </summary>
    [JsonPropertyName("allowElevation")]
    public bool AllowElevation { get; init; } = false;
}

