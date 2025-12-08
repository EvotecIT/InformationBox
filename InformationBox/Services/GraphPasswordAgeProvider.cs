using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;

namespace InformationBox.Services;

/// <summary>
/// Retrieves password age via Microsoft Graph /me.
/// For synced accounts, also checks LDAP for the "never expires" UAC flag.
/// </summary>
public sealed class GraphPasswordAgeProvider : IPasswordAgeProvider
{
    private readonly GraphLiteClient _graphClient;

    /// <summary>
    /// Gets the most recent identity payload returned by Graph, if any.
    /// </summary>
    public UserIdentity? LastIdentity { get; private set; }

    /// <summary>
    /// Initializes the provider with the Graph client to call /me.
    /// </summary>
    /// <param name="graphClient">Configured Graph client.</param>
    public GraphPasswordAgeProvider(GraphLiteClient graphClient)
    {
        _graphClient = graphClient;
    }

    /// <inheritdoc />
    public async Task<PasswordAgeResult> GetAsync(PasswordPolicy policy, TenantContext tenantContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var me = await _graphClient.GetMeAsync(cancellationToken).ConfigureAwait(false);

            if (me is not null)
            {
                LastIdentity = UserIdentity.FromGraph(me);
            }

            var lastChange = me?.LastPasswordChangeDateTime;
            var policyDays = me?.OnPremisesSyncEnabled == true ? policy.OnPremDays : policy.CloudDays;

            // Check for never expires: first from Graph, then from LDAP for synced accounts
            var neverExpires = me?.PasswordNeverExpires ?? false;

            // For synced accounts, Azure AD often doesn't have the passwordPolicies set correctly
            // So we also check LDAP for the UAC flag
            if (!neverExpires && me?.OnPremisesSyncEnabled == true)
            {
                neverExpires = CheckLdapNeverExpires();
            }

            int? daysLeft = null;
            if (!neverExpires && lastChange is not null)
            {
                var daysSince = (DateTimeOffset.UtcNow - lastChange.Value).Days;
                daysLeft = policyDays - daysSince;
            }

            Logger.Info($"Graph password age: lastChange={lastChange:u} onPrem={me?.OnPremisesSyncEnabled} neverExpires={neverExpires} graphPolicies={me?.PasswordPolicies} daysLeft={daysLeft}");
            return new PasswordAgeResult(lastChange, policyDays, daysLeft, neverExpires);
        }
        catch (Exception ex)
        {
            Logger.Error("Graph password age failed", ex);
            return new PasswordAgeResult(null, policy.CloudDays, null);
        }
    }

    /// <summary>
    /// Checks LDAP for the "password never expires" UAC flag.
    /// </summary>
    private static bool CheckLdapNeverExpires()
    {
        try
        {
            var domain = Domain.GetCurrentDomain();
            using var root = domain.GetDirectoryEntry();
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(sAMAccountName={Environment.UserName})"
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
    }
}
