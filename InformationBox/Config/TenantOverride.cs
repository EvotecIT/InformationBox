using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Per-tenant override of base configuration settings.
/// </summary>
public sealed record TenantOverride
{
    /// <summary>
    /// Gets optional tenant-specific link overrides.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<LinkEntry>? Links { get; init; }
        = null;

    /// <summary>
    /// Gets a tenant-specific password policy override.
    /// </summary>
    [JsonPropertyName("passwordPolicy")]
    public PasswordPolicy? PasswordPolicy { get; init; }
        = null;

    /// <summary>
    /// Gets the feature flags to override for the tenant.
    /// </summary>
    [JsonPropertyName("featureFlags")]
    public FeatureFlags? FeatureFlags { get; init; }
        = null;

    /// <summary>
    /// Gets branding overrides applied for the tenant.
    /// </summary>
    [JsonPropertyName("branding")]
    public Branding? Branding { get; init; }
        = null;

    /// <summary>
    /// Gets tenant-specific local site definitions.
    /// </summary>
    [JsonPropertyName("localSites")]
    public IReadOnlyList<LocalSite>? LocalSites { get; init; }
        = null;

    /// <summary>
    /// Gets tenant-specific contact entries.
    /// </summary>
    [JsonPropertyName("contacts")]
    public IReadOnlyList<ContactEntry>? Contacts { get; init; }
        = null;

    /// <summary>
    /// Gets layout overrides that apply to the tenant.
    /// </summary>
    [JsonPropertyName("layout")]
    public LayoutOptions? Layout { get; init; }
        = null;

    /// <summary>
    /// Gets health check overrides that apply to the tenant.
    /// </summary>
    [JsonPropertyName("health")]
    public HealthOptions? Health { get; init; }
        = null;

    /// <summary>
    /// Gets security-related overrides that apply to the tenant.
    /// </summary>
    [JsonPropertyName("security")]
    public SecurityOptions? Security { get; init; }
        = null;
}
