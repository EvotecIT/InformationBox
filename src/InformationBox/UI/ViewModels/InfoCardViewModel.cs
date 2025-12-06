using InformationBox.Services;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Simple DTO for header info.
/// </summary>
/// <param name="DeviceName">Device hostname.</param>
/// <param name="TenantId">Tenant identifier, if known.</param>
/// <param name="TenantName">Tenant display name.</param>
/// <param name="JoinType">Device join classification.</param>
/// <param name="ConfigSource">Active configuration source identifier.</param>
public sealed record InfoCardViewModel(
    string DeviceName,
    string? TenantId,
    string? TenantName,
    TenantJoinType JoinType,
    string ConfigSource)
{
    /// <summary>
    /// Name shown in the header – prefers tenant name, falls back to device name.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(TenantName) ? DeviceName : TenantName!;

    /// <summary>
    /// Human-friendly join text for the pill badge.
    /// </summary>
    public string JoinTypeLabel => JoinType switch
    {
        TenantJoinType.AzureAdJoined => "Azure AD joined",
        TenantJoinType.HybridAzureAdJoined => "Hybrid joined",
        TenantJoinType.DomainJoined => "Domain joined",
        TenantJoinType.WorkplaceJoined => "Workplace joined",
        TenantJoinType.Workgroup => "Workgroup",
        _ => "Join unknown"
    };

    /// <summary>
    /// Whether the join badge should be rendered.
    /// </summary>
    public bool ShowJoinType => JoinType != TenantJoinType.Unknown;

    /// <summary>
    /// Subtle background tint for the join badge.
    /// </summary>
    public string JoinBadgeBackground => JoinType switch
    {
        TenantJoinType.AzureAdJoined => "#E7F1FF",
        TenantJoinType.HybridAzureAdJoined => "#EAF7ED",
        TenantJoinType.DomainJoined => "#F1E9FF",
        TenantJoinType.WorkplaceJoined => "#E8F6FF",
        TenantJoinType.Workgroup => "#FFF4E6",
        _ => "#F4F4F4"
    };

    /// <summary>
    /// Border color matching the badge background tone.
    /// </summary>
    public string JoinBadgeBorder => JoinType switch
    {
        TenantJoinType.AzureAdJoined => "#4A74C6",
        TenantJoinType.HybridAzureAdJoined => "#2E8B57",
        TenantJoinType.DomainJoined => "#6C4AC6",
        TenantJoinType.WorkplaceJoined => "#1A7FB2",
        TenantJoinType.Workgroup => "#C27D28",
        _ => "#B0B0B0"
    };

    /// <summary>
    /// Text color for the join badge.
    /// </summary>
    public string JoinBadgeForeground => JoinType switch
    {
        TenantJoinType.AzureAdJoined => "#27448B",
        TenantJoinType.HybridAzureAdJoined => "#24693F",
        TenantJoinType.DomainJoined => "#4F2C9E",
        TenantJoinType.WorkplaceJoined => "#0F5D82",
        TenantJoinType.Workgroup => "#9C5E16",
        _ => "#4A4A4A"
    };
}
