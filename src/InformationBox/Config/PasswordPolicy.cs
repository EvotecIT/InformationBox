using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Controls password expiry thresholds.
/// </summary>
public sealed record PasswordPolicy
{
    /// <summary>
    /// Gets the maximum allowed password age for on-premises accounts.
    /// </summary>
    [JsonPropertyName("onPremDays")]
    public int OnPremDays { get; init; } = 360;

    /// <summary>
    /// Gets the maximum allowed password age for cloud-only accounts.
    /// </summary>
    [JsonPropertyName("cloudDays")]
    public int CloudDays { get; init; } = 180;
}
