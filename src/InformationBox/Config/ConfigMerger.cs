using System;

namespace InformationBox.Config;

/// <summary>
/// Applies tenant-specific overrides onto the base configuration.
/// </summary>
public static class ConfigMerger
{
    /// <summary>
    /// Creates a merged configuration by applying tenant overrides on top of the base config.
    /// </summary>
    /// <param name="baseConfig">The base configuration loaded from disk or embedded defaults.</param>
    /// <param name="tenantId">The current tenant identifier, if known.</param>
    /// <returns>The effective configuration for the tenant.</returns>
    public static AppConfig Merge(AppConfig baseConfig, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return baseConfig;
        }

        if (!baseConfig.TenantOverrides.TryGetValue(tenantId, out var overlay) || overlay is null)
        {
            return baseConfig;
        }

        return baseConfig with
        {
            Links = overlay.Links ?? baseConfig.Links,
            PasswordPolicy = overlay.PasswordPolicy ?? baseConfig.PasswordPolicy,
            FeatureFlags = overlay.FeatureFlags ?? baseConfig.FeatureFlags,
            Branding = overlay.Branding ?? baseConfig.Branding,
            LocalSites = overlay.LocalSites ?? baseConfig.LocalSites,
            Contacts = overlay.Contacts ?? baseConfig.Contacts,
            Layout = overlay.Layout ?? baseConfig.Layout
        };
    }
}
