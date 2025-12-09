using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;

namespace InformationBox.Services;

// ============================================================================
// LDAP PASSWORD AGE PROVIDER (On-Premises Active Directory)
// ============================================================================
//
// PURPOSE:
//   Retrieves password expiration status directly from on-premises Active Directory
//   using LDAP queries. Used when Microsoft Graph is not available.
//
// WHEN THIS PROVIDER IS USED:
//   - Domain-joined devices without Azure AD join
//   - When Graph authentication fails or is not configured
//   - Fallback for environments without cloud identity
//
// LDAP QUERY DETAILS:
//   Target: Current user's AD account
//   Filter: (sAMAccountName={Environment.UserName})
//   Attributes:
//     - pwdLastSet: FileTime value of when password was last changed
//     - userAccountControl: Bitmask with account flags including DONT_EXPIRE_PASSWORD
//
// PASSWORD DETECTION FLOW:
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  1. Connect to current domain                                   │
//   │     - Uses Domain.GetCurrentDomain() to find DC                 │
//   │     - Authenticates with current Windows credentials            │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  2. Search for current user by sAMAccountName                   │
//   │     - Uses DirectorySearcher with LDAP filter                   │
//   │     - Retrieves pwdLastSet and userAccountControl               │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  3. Check userAccountControl for DONT_EXPIRE_PASSWORD flag      │
//   │     - Bit 0x10000 (65536) = Password never expires              │
//   │     - If set: neverExpires = true, skip calculation             │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  4. Convert pwdLastSet (FileTime) to DateTime                   │
//   │     - FileTime is 100-nanosecond intervals since Jan 1, 1601    │
//   │     - Convert to DateTimeOffset for calculation                 │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  5. Calculate days remaining                                    │
//   │     - daysLeft = OnPremDays - daysSinceLastChange               │
//   │     - Always uses OnPremDays policy (this is on-prem)           │
//   └─────────────────────────────────────────────────────────────────┘
//
// REQUIREMENTS:
//   - Device must be domain-joined
//   - Domain controller must be reachable
//   - User must have permission to read their own AD attributes
//
// ============================================================================

/// <summary>
/// Retrieves password expiration status from on-premises Active Directory via LDAP.
/// </summary>
/// <remarks>
/// <para><b>When to use this provider:</b></para>
/// Use for domain-joined devices that are not Azure AD joined, or as a fallback
/// when Graph API is unavailable.
///
/// <para><b>Authentication:</b></para>
/// Uses the current Windows user's credentials (integrated authentication).
/// No additional credentials or configuration required.
///
/// <para><b>Entry point:</b></para>
/// <see cref="GetAsync"/> - Call this to get password expiration status.
/// </remarks>
/// <seealso cref="GraphPasswordAgeProvider"/>
public sealed class LdapPasswordAgeProvider : IPasswordAgeProvider
{
    /// <summary>
    /// Retrieves password expiration status from on-premises Active Directory without blocking the UI thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Algorithm:</b></para>
    /// <list type="number">
    ///   <item>Connect to the current domain using integrated Windows authentication</item>
    ///   <item>Search for the current user by sAMAccountName (escaped to avoid LDAP injection)</item>
    ///   <item>Read pwdLastSet (password change timestamp) and userAccountControl (flags)</item>
    ///   <item>Check UAC flag 0x10000 for "password never expires"</item>
    ///   <item>Calculate days remaining using OnPremDays policy</item>
    /// </list>
    /// <para><b>Safety:</b></para>
    /// Runs on a background thread with a 5s client timeout, escaped filters, and respects cancellation tokens to prevent UI hangs or LDAP injection.
    /// </remarks>
    /// <param name="policy">Password policy configuration.</param>
    /// <param name="tenantContext">Current tenant context (not used for LDAP).</param>
    /// <param name="cancellationToken">Cancellation token to abort the lookup.</param>
    /// <returns>Password age result with days remaining and never-expires flag.</returns>
    public async Task<PasswordAgeResult> GetAsync(
        PasswordPolicy policy,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // STEP 1: Connect to the current domain
                var domain = Domain.GetCurrentDomain();
                using var root = domain.GetDirectoryEntry();

                // STEP 2: Search for the current user
                using var searcher = new DirectorySearcher(root)
                {
                    Filter = $"(sAMAccountName={EscapeLdap(Environment.UserName)})",
                    ClientTimeout = ExecutionTimeouts.LdapClient
                };

                searcher.PropertiesToLoad.Add("pwdLastSet");
                searcher.PropertiesToLoad.Add("userAccountControl");

                var result = searcher.FindOne();
                if (result is null)
                {
                    Logger.Info($"LDAP: User {Environment.UserName} not found in AD");
                    return new PasswordAgeResult(null, policy.OnPremDays, null);
                }

                var pwdLastSetObj = result.Properties["pwdLastSet"]?[0];
                var uacObj = result.Properties["userAccountControl"]?[0];

                var neverExpires = false;
                if (uacObj is int uac)
                {
                    neverExpires = (uac & ActiveDirectoryConstants.DontExpirePassword) == ActiveDirectoryConstants.DontExpirePassword;
                }

                if (pwdLastSetObj is null)
                {
                    return new PasswordAgeResult(null, policy.OnPremDays, null, neverExpires);
                }

                // STEP 4: Convert pwdLastSet to DateTimeOffset
                // pwdLastSet is a FILETIME (100-nanosecond intervals since 1601-01-01 UTC).
                // DateTimeOffset.FromFileTime handles the conversion and preserves UTC semantics.
                var fileTime = (long)pwdLastSetObj;
                var lastChange = DateTimeOffset.FromFileTime(fileTime).ToUniversalTime();

                // STEP 5: Calculate remaining days (negative means expired)
                int? daysLeft = null;
                if (!neverExpires)
                {
                    var daysSince = (DateTimeOffset.UtcNow - lastChange).Days;
                    daysLeft = policy.OnPremDays - daysSince;
                }

                Logger.Info($"LDAP password age success: lastChange={lastChange:u} daysLeft={daysLeft} neverExpires={neverExpires}");
                return new PasswordAgeResult(lastChange, policy.OnPremDays, daysLeft, neverExpires);
            }
            catch (Exception ex)
            {
                Logger.Error("LDAP password age failed", ex);
                return new PasswordAgeResult(null, policy.OnPremDays, null);
            }
        }, cancellationToken);
    }

    private static string EscapeLdap(string value) =>
        value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
