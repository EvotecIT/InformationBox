using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Controls which lightweight health checks are performed (no elevation required).
/// </summary>
public sealed record HealthOptions
{
    /// <summary>
    /// Ping timeout used for reachability/latency checks (0 disables ping checks).
    /// </summary>
    [JsonPropertyName("pingTimeoutMs")]
    public int PingTimeoutMs { get; init; } = 750;

    /// <summary>
    /// Cache duration in seconds to avoid repeated probing on rapid refreshes (0 disables caching).
    /// </summary>
    [JsonPropertyName("cacheSeconds")]
    public int CacheSeconds { get; init; } = 30;

    /// <summary>
    /// Optional ping targets shown under Health.
    /// </summary>
    [JsonPropertyName("pingTargets")]
    public IReadOnlyList<HealthEndpoint> PingTargets { get; init; } = new[]
    {
        new HealthEndpoint { Name = "Internet (8.8.8.8)", Target = "8.8.8.8" },
        new HealthEndpoint { Name = "Entra ID", Target = "login.microsoftonline.com" }
    };
}

/// <summary>
/// A single named health endpoint.
/// </summary>
public sealed record HealthEndpoint
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;
}
