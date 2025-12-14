using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// Root configuration for the Information Box application.
/// </summary>
public sealed record AppConfig
{
    /// <summary>
    /// Gets the schema version of the persisted configuration.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the feature flag switches that control optional UI areas.
    /// </summary>
    [JsonPropertyName("featureFlags")]
    public FeatureFlags FeatureFlags { get; init; } = new();

    /// <summary>
    /// Gets the navigation links shown inside the app.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<LinkEntry> Links { get; init; } = Array.Empty<LinkEntry>();

    /// <summary>
    /// Gets the password expiration policy thresholds.
    /// </summary>
    [JsonPropertyName("passwordPolicy")]
    public PasswordPolicy PasswordPolicy { get; init; } = new();

    /// <summary>
    /// Gets the domain-to-zone mappings for local site grouping.
    /// </summary>
    [JsonPropertyName("zones")]
    public IReadOnlyList<ZoneMapping> Zones { get; init; } = Array.Empty<ZoneMapping>();

    /// <summary>
    /// Gets the environment-specific site shortcuts.
    /// </summary>
    [JsonPropertyName("localSites")]
    public IReadOnlyList<LocalSite> LocalSites { get; init; } = Array.Empty<LocalSite>();

    /// <summary>
    /// Gets the contact directory entries surfaced in the UI.
    /// </summary>
    [JsonPropertyName("contacts")]
    public IReadOnlyList<ContactEntry> Contacts { get; init; } = Array.Empty<ContactEntry>();

    /// <summary>
    /// Gets the user-facing fix actions (scripts/commands) exposed in the Fix tab.
    /// </summary>
    [JsonPropertyName("fixes")]
    public IReadOnlyList<Fixes.FixAction> Fixes { get; init; } = Array.Empty<Fixes.FixAction>();

    /// <summary>
    /// Gets the branding customization settings.
    /// </summary>
    [JsonPropertyName("branding")]
    public Branding Branding { get; init; } = new();

    /// <summary>
    /// Gets the layout options applied to the main window.
    /// </summary>
    [JsonPropertyName("layout")]
    public LayoutOptions Layout { get; init; } = new();

    /// <summary>
    /// Gets the per-tenant overrides keyed by tenant identifier.
    /// </summary>
    [JsonPropertyName("tenantOverrides")]
    public IReadOnlyDictionary<string, TenantOverride> TenantOverrides { get; init; }
        = new Dictionary<string, TenantOverride>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the authentication configuration for Graph calls.
    /// </summary>
    [JsonPropertyName("auth")]
    public AuthConfig Auth { get; init; } = new();

    /// <summary>
    /// Gets configuration for non-admin health checks (reachability, update age, MDM, etc.).
    /// </summary>
    [JsonPropertyName("health")]
    public HealthOptions Health { get; init; } = new();

    /// <summary>
    /// Gets configuration controlling execution behaviors (e.g. whether the app may prompt for UAC elevation).
    /// </summary>
    [JsonPropertyName("security")]
    public SecurityOptions Security { get; init; } = new();
}
