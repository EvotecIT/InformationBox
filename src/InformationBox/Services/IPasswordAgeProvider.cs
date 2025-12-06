using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;

namespace InformationBox.Services;

/// <summary>
/// Provides password age information for the signed-in user.
/// </summary>
public interface IPasswordAgeProvider
{
    /// <summary>
    /// Retrieves password age information using the provided policy and tenant context.
    /// </summary>
    /// <param name="policy">Password policy used to calculate remaining time.</param>
    /// <param name="tenantContext">Tenant context describing the current device/user.</param>
    /// <param name="cancellationToken">Cancellation token for downstream IO.</param>
    Task<PasswordAgeResult> GetAsync(PasswordPolicy policy, TenantContext tenantContext, CancellationToken cancellationToken = default);
}
