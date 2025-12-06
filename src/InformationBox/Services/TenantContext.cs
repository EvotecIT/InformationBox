using System;

namespace InformationBox.Services;

/// <summary>
/// Device/tenant context derived from Windows join state.
/// </summary>
/// <param name="TenantId">The Azure AD tenant identifier when known.</param>
/// <param name="TenantName">Friendly tenant or domain name.</param>
/// <param name="DomainName">Active Directory domain name, if available.</param>
/// <param name="JoinType">Join classification for the device.</param>
/// <param name="AzureAdJoined">True when device is Azure AD joined.</param>
/// <param name="WorkplaceJoined">True when device is Azure AD registered.</param>
/// <param name="DomainJoined">True when device is domain joined.</param>
public sealed record TenantContext(
    string? TenantId,
    string? TenantName,
    string? DomainName,
    TenantJoinType JoinType,
    bool AzureAdJoined,
    bool WorkplaceJoined,
    bool DomainJoined)
{
    /// <summary>
    /// Gets a value indicating whether a tenant ID is available.
    /// </summary>
    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);

    /// <summary>
    /// Gets a singleton context that represents an unknown tenant state.
    /// </summary>
    public static TenantContext Unknown { get; } = new(null, null, null, TenantJoinType.Unknown, false, false, false);
}
