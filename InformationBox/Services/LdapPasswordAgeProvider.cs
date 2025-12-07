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
            searcher.PropertiesToLoad.Add("userAccountControl");
            var result = searcher.FindOne();
            if (result is null)
            {
                return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null));
            }

            var pwdLastSetObj = result.Properties["pwdLastSet"]?[0];
            var uacObj = result.Properties["userAccountControl"]?[0];

            var neverExpires = false;
            if (uacObj is int uac)
            {
                const int DontExpire = 0x10000;
                neverExpires = (uac & DontExpire) == DontExpire;
            }

            if (pwdLastSetObj is null)
            {
                return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null, neverExpires));
            }

            var fileTime = (long)pwdLastSetObj;
            var lastChange = DateTimeOffset.FromFileTime(fileTime).ToUniversalTime();
            int? daysLeft = null;
            if (!neverExpires)
            {
                var daysSince = (DateTimeOffset.UtcNow - lastChange).Days;
                daysLeft = policy.OnPremDays - daysSince;
            }
            Logger.Info($"LDAP password age success: lastChange={lastChange:u} daysLeft={daysLeft} neverExpires={neverExpires}");
            return Task.FromResult(new PasswordAgeResult(lastChange, policy.OnPremDays, daysLeft, neverExpires));
        }
        catch (Exception ex)
        {
            Logger.Error("LDAP password age failed", ex);
            return Task.FromResult(new PasswordAgeResult(null, policy.OnPremDays, null));
        }
    }
}
