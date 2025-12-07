using System.Text.Json.Serialization;

namespace InformationBox.Config.Fixes;

/// <summary>
/// Predefined, documented categories for fix actions. Use <see cref="Custom"/> for tenant-specific buckets.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixCategory
{
    /// <summary>OneDrive repair/restart actions.</summary>
    OneDrive,
    /// <summary>Teams repair/reset actions.</summary>
    Teams,
    /// <summary>Browser-related fixes (cache clear, etc.).</summary>
    Browser,
    /// <summary>Core Windows maintenance (wsreset, settings reset).</summary>
    Windows,
    /// <summary>Support/diagnostics (log collection, etc.).</summary>
    Support,
    /// <summary>Custom category defined by tenant.</summary>
    Custom
}
