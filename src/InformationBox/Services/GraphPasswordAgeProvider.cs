using System;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace InformationBox.Services;

/// <summary>
/// Retrieves password age via Microsoft Graph /me.
/// </summary>
public sealed class GraphPasswordAgeProvider : IPasswordAgeProvider
{
    private readonly GraphServiceClient _graphClient;

    /// <summary>
    /// Gets the most recent identity payload returned by Graph, if any.
    /// </summary>
    public UserIdentity? LastIdentity { get; private set; }

    /// <summary>
    /// Initializes the provider with the Graph client to call /me.
    /// </summary>
    /// <param name="graphClient">Configured Graph client.</param>
    public GraphPasswordAgeProvider(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    /// <inheritdoc />
    public async Task<PasswordAgeResult> GetAsync(PasswordPolicy policy, TenantContext tenantContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var me = await _graphClient.Me.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = new[]
                {
                    "displayName",
                    "userPrincipalName",
                    "mail",
                    "proxyAddresses",
                    "businessPhones",
                    "mobilePhone",
                    "jobTitle",
                    "department",
                    "officeLocation",
                    "lastPasswordChangeDateTime",
                    "onPremisesSyncEnabled"
                };
            }, cancellationToken).ConfigureAwait(false);

            if (me is not null)
            {
                LastIdentity = UserIdentity.FromGraph(me);
            }

            var lastChange = me?.LastPasswordChangeDateTime;
            var policyDays = me?.OnPremisesSyncEnabled == true ? policy.OnPremDays : policy.CloudDays;
            int? daysLeft = null;
            if (lastChange is not null)
            {
                var daysSince = (DateTimeOffset.UtcNow - lastChange.Value).Days;
                daysLeft = policyDays - daysSince;
            }

            Logger.Info($"Graph password age success: lastChange={lastChange:u} onPrem={me?.OnPremisesSyncEnabled} daysLeft={daysLeft}");
            return new PasswordAgeResult(lastChange, policyDays, daysLeft);
        }
        catch (Exception ex)
        {
            Logger.Error("Graph password age failed", ex);
            return new PasswordAgeResult(null, policy.CloudDays, null);
        }
    }
}
