using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;

namespace InformationBox.Services;

// ============================================================================
// HYBRID PASSWORD AGE PROVIDER (Graph + LDAP)
// ============================================================================
//
// PURPOSE:
//   Determines password expiration status using a hybrid approach that combines
//   Microsoft Graph API data with on-premises Active Directory LDAP queries.
//
// WHY HYBRID?
//   For organizations using Azure AD Connect (hybrid identity), password policies
//   are managed in on-premises AD, but Azure AD's passwordPolicies property often
//   doesn't reflect the on-prem "Password never expires" checkbox setting.
//
//   This provider solves that by:
//   1. First checking Azure AD (Graph) for passwordPolicies
//   2. For synced accounts, also checking LDAP for the UAC flag
//
// PASSWORD DETECTION FLOW:
//   ┌─────────────────────────────────────────────────────────────────┐
//   │                    GetAsync() called                            │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  1. Call Graph API /me endpoint                                 │
//   │     - Get lastPasswordChangeDateTime                            │
//   │     - Get onPremisesSyncEnabled (is this a synced account?)     │
//   │     - Get passwordPolicies (DisablePasswordExpiration?)         │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  2. Check if password never expires                             │
//   │     - First: Check Graph passwordPolicies                       │
//   │     - If synced account AND not found in Graph:                 │
//   │       → Query LDAP for userAccountControl UAC flag              │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  3. Calculate days remaining                                    │
//   │     - If neverExpires: daysLeft = null                          │
//   │     - If synced: use OnPremDays policy (e.g., 360 days)         │
//   │     - If cloud-only: use CloudDays policy (e.g., 180 days)      │
//   │     - daysLeft = policyDays - daysSinceLastChange               │
//   └─────────────────────────────────────────────────────────────────┘
//
// ACCOUNT TYPES SUPPORTED:
//   1. Cloud-only Azure AD accounts
//      - Uses Graph passwordPolicies for never-expires detection
//      - Uses CloudDays policy (default: 180 days)
//
//   2. Hybrid/Synced accounts (Azure AD Connect)
//      - Uses Graph for identity data + lastPasswordChangeDateTime
//      - Uses LDAP UAC flag for accurate never-expires detection
//      - Uses OnPremDays policy (default: 360 days)
//
//   3. Domain-joined only (no Azure AD)
//      - Falls back to LdapPasswordAgeProvider (separate class)
//
// LDAP UAC FLAG REFERENCE:
//   The userAccountControl attribute is a bitmask. Relevant flag:
//   - 0x10000 (65536) = DONT_EXPIRE_PASSWORD
//
//   Example UAC values:
//   - 0x200 (512) = Normal account
//   - 0x10200 (66048) = Normal account + Password never expires
//
// ============================================================================

/// <summary>
/// Password age provider that uses Microsoft Graph with LDAP fallback for hybrid accounts.
/// </summary>
/// <remarks>
/// <para><b>When to use this provider:</b></para>
/// Use for Azure AD joined or hybrid Azure AD joined devices where Graph API is available.
///
/// <para><b>Key feature - Hybrid detection:</b></para>
/// For synced accounts (<c>onPremisesSyncEnabled = true</c>), this provider performs
/// an additional LDAP query to check the on-premises AD userAccountControl flag,
/// because Azure AD's passwordPolicies often doesn't reflect the on-prem setting.
///
/// <para><b>Entry point:</b></para>
/// <see cref="GetAsync"/> - Call this to get password expiration status.
/// </remarks>
/// <seealso cref="LdapPasswordAgeProvider"/>
/// <seealso cref="GraphLiteClient"/>
public sealed class GraphPasswordAgeProvider : IPasswordAgeProvider
{
    private readonly GraphLiteClient _graphClient;

    /// <summary>
    /// Gets the most recent user identity retrieved from Graph.
    /// </summary>
    /// <remarks>
    /// This is populated after <see cref="GetAsync"/> is called.
    /// Used by the application to display user profile information.
    /// </remarks>
    public UserIdentity? LastIdentity { get; private set; }

    /// <summary>
    /// Initializes the provider with a configured Graph client.
    /// </summary>
    /// <param name="graphClient">
    /// Graph client with valid authentication. See <see cref="GraphLiteClient"/> for setup.
    /// </param>
    public GraphPasswordAgeProvider(GraphLiteClient graphClient)
    {
        _graphClient = graphClient;
    }

    /// <summary>
    /// Retrieves password expiration status using Graph API and optional LDAP fallback.
    /// </summary>
    /// <remarks>
    /// <para><b>Algorithm:</b></para>
    /// <list type="number">
    ///   <item>Query Microsoft Graph /me for user profile and password metadata</item>
    ///   <item>Extract lastPasswordChangeDateTime and onPremisesSyncEnabled</item>
    ///   <item>Check passwordPolicies for "DisablePasswordExpiration"</item>
    ///   <item>For synced accounts: also check LDAP UAC flag (0x10000)</item>
    ///   <item>Calculate days remaining based on policy (OnPremDays vs CloudDays)</item>
    /// </list>
    ///
    /// <para><b>Policy selection:</b></para>
    /// <list type="bullet">
    ///   <item>Synced accounts (onPremisesSyncEnabled=true): Use <see cref="PasswordPolicy.OnPremDays"/></item>
    ///   <item>Cloud-only accounts: Use <see cref="PasswordPolicy.CloudDays"/></item>
    /// </list>
    /// </remarks>
    /// <param name="policy">Password policy configuration with expiration days.</param>
    /// <param name="tenantContext">Current tenant context (for future extensibility).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Password age result with days remaining and never-expires flag.</returns>
    public async Task<PasswordAgeResult> GetAsync(
        PasswordPolicy policy,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // -----------------------------------------------------------------
            // STEP 1: Query Microsoft Graph for user profile
            // -----------------------------------------------------------------
            // This retrieves:
            //   - lastPasswordChangeDateTime: When password was last changed
            //   - onPremisesSyncEnabled: Whether account is synced from on-prem AD
            //   - passwordPolicies: Azure AD password policies (may include "DisablePasswordExpiration")
            // -----------------------------------------------------------------
            var me = await _graphClient.GetMeAsync(cancellationToken).ConfigureAwait(false);

            // Store identity for later use by the UI
            if (me is not null)
            {
                LastIdentity = UserIdentity.FromGraph(me);
            }

            var lastChange = me?.LastPasswordChangeDateTime;

            // -----------------------------------------------------------------
            // STEP 2: Determine which password policy to use
            // -----------------------------------------------------------------
            // Synced accounts follow on-premises policy (typically longer, e.g., 360 days)
            // Cloud-only accounts follow Azure AD policy (typically shorter, e.g., 180 days)
            // -----------------------------------------------------------------
            var policyDays = me?.OnPremisesSyncEnabled == true
                ? policy.OnPremDays
                : policy.CloudDays;

            // -----------------------------------------------------------------
            // STEP 3: Check for "password never expires" setting
            // -----------------------------------------------------------------
            // First, check Azure AD's passwordPolicies property.
            // This works for cloud-only accounts and some synced accounts.
            // -----------------------------------------------------------------
            var neverExpires = me?.PasswordNeverExpires ?? false;

            // -----------------------------------------------------------------
            // STEP 4: HYBRID FALLBACK - Check LDAP for synced accounts
            // -----------------------------------------------------------------
            // IMPORTANT: For synced accounts, Azure AD often does NOT reflect the
            // on-premises "Password never expires" checkbox in passwordPolicies.
            //
            // We perform an additional LDAP query to check the userAccountControl
            // attribute directly from the domain controller.
            //
            // This is the KEY HYBRID FEATURE that ensures accurate detection.
            // -----------------------------------------------------------------
            // For synced accounts, double-check on-prem flag via LDAP (async + cancellable).
            if (!neverExpires && me?.OnPremisesSyncEnabled == true)
            {
                neverExpires = await CheckLdapNeverExpiresAsync(cancellationToken).ConfigureAwait(false);
            }

            // -----------------------------------------------------------------
            // STEP 5: Calculate days remaining until password expires
            // -----------------------------------------------------------------
            // If password never expires, we don't calculate days (daysLeft = null)
            // Otherwise: daysLeft = policyDays - daysSinceLastChange
            //
            // Negative values indicate an expired password.
            // -----------------------------------------------------------------
            int? daysLeft = null;
            if (!neverExpires && lastChange is not null)
            {
                var daysSince = (DateTimeOffset.UtcNow - lastChange.Value).Days;
                daysLeft = policyDays - daysSince;
            }

            Logger.Info($"Graph password age: lastChange={lastChange:u} onPrem={me?.OnPremisesSyncEnabled} " +
                        $"neverExpires={neverExpires} graphPolicies={me?.PasswordPolicies} daysLeft={daysLeft}");

            return new PasswordAgeResult(lastChange, policyDays, daysLeft, neverExpires);
        }
        catch (Exception ex)
        {
            Logger.Error("Graph password age failed", ex);
            return new PasswordAgeResult(null, policy.CloudDays, null);
        }
    }

    // =========================================================================
    // LDAP HELPER - Check on-premises AD for "never expires" flag
    // =========================================================================

    /// <summary>
    /// Queries on-premises Active Directory for the "password never expires" UAC flag.
    /// </summary>
    /// <remarks>
    /// <para><b>How it works:</b></para>
    /// <list type="number">
    ///   <item>Connect to the current domain's directory</item>
    ///   <item>Search for the current user by sAMAccountName</item>
    ///   <item>Read the userAccountControl attribute</item>
    ///   <item>Check if bit 0x10000 (DONT_EXPIRE_PASSWORD) is set</item>
    /// </list>
    ///
    /// <para><b>When this is called:</b></para>
    /// Only for synced accounts when Azure AD's passwordPolicies doesn't indicate
    /// that password never expires. This ensures accurate detection for hybrid environments.
    ///
    /// <para><b>Failure handling:</b></para>
    /// Returns false (password does expire) if LDAP query fails. This is safe because:
    /// <list type="bullet">
    ///   <item>If device isn't domain-joined, LDAP will fail - expected</item>
    ///   <item>If DC is unreachable, we assume normal expiration - safe default</item>
    /// </list>
    /// </remarks>
    /// <returns>True if password never expires; false otherwise or on error.</returns>
    // Backgrounds the LDAP call so UI thread isn’t blocked; includes filter escaping and short timeout.
    private static Task<bool> CheckLdapNeverExpiresAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var domain = Domain.GetCurrentDomain();
                using var root = domain.GetDirectoryEntry();

                using var searcher = new DirectorySearcher(root)
                {
                    Filter = $"(sAMAccountName={EscapeLdap(Environment.UserName)})",
                    ClientTimeout = TimeSpan.FromSeconds(5)
                };

                searcher.PropertiesToLoad.Add("userAccountControl");

                var result = searcher.FindOne();

                if (result?.Properties["userAccountControl"]?[0] is int uac)
                {
                    const int DontExpire = 0x10000;

                    var neverExpires = (uac & DontExpire) == DontExpire;
                    Logger.Info($"LDAP UAC check: uac=0x{uac:X} neverExpires={neverExpires}");
                    return neverExpires;
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"LDAP UAC check failed (expected if not domain-joined): {ex.Message}");
            }

            return false;
        }, cancellationToken);
    }

    private static string EscapeLdap(string value) =>
        value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
