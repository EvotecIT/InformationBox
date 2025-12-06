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
    string ConfigSource);
