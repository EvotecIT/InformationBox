using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;

namespace InformationBox.Services;

/// <summary>
/// Retrieves password age for on-prem domain users via LDAP when Graph is not available.
/// </summary>
public sealed class LdapPasswordAgeProvider : IPasswordAgeProvider
{
    /// <inheritdoc />
    public Task<PasswordAgeResult> GetAsync(PasswordPolicy policy, TenantContext tenantContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var domain = Domain.GetCurrentDomain();
            using var root = domain.GetDirectoryEntry();
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(sAMAccountName={Environment.UserName})"
            };
            searcher.PropertiesToLoad.Add("pwdLastSet");
            var result = searcher.FindOne();
            if (result is null)
            {
                return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null));
            }

            var pwdLastSetObj = result.Properties["pwdLastSet"]?[0];
            if (pwdLastSetObj is null)
            {
                return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null));
            }

            var fileTime = (long)pwdLastSetObj;
            var lastChange = DateTimeOffset.FromFileTime(fileTime).ToUniversalTime();
            var daysSince = (DateTimeOffset.UtcNow - lastChange).Days;
            var daysLeft = policy.OnPremDays - daysSince;
            Logger.Info($"LDAP password age success: lastChange={lastChange:u} daysLeft={daysLeft}");
            return Task.FromResult(new PasswordAgeResult(lastChange, policy.OnPremDays, daysLeft));
        }
        catch (Exception ex)
        {
            Logger.Error("LDAP password age failed", ex);
            return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null));
        }
    }
}
