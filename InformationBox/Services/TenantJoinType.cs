namespace InformationBox.Services;

/// <summary>
/// Describes how the device is registered to the organization.
/// </summary>
public enum TenantJoinType
{
    /// <summary>
    /// The join state could not be determined.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Device is Azure AD joined.
    /// </summary>
    AzureAdJoined = 1,
    /// <summary>
    /// Device is hybrid Azure AD joined (domain + cloud).
    /// </summary>
    HybridAzureAdJoined = 2,
    /// <summary>
    /// Device is not joined to any directory (workgroup).
    /// </summary>
    Workgroup = 3,
    /// <summary>
    /// Device is joined to an on-premises Active Directory domain.
    /// </summary>
    DomainJoined = 4,
    /// <summary>
    /// Device is workplace joined (Azure AD registered).
    /// </summary>
    WorkplaceJoined = 5
}
