using System.Text.Json.Serialization;

namespace InformationBox.Config.Fixes;

/// <summary>
/// Predefined, documented categories for fix actions. Use <see cref="Custom"/> for tenant-specific buckets.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixCategory
{
    OneDrive,
    Teams,
    Browser,
    Windows,
    Support,
    Custom
}
