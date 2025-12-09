using System;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class GraphPasswordAgeProviderTests
{
    [Fact]
    public async Task CalculatesDaysLeft_ForSyncedAccount()
    {
        var fake = new FakeGraphClient();
        var provider = new GraphPasswordAgeProvider(fake);
        var policy = new PasswordPolicy { OnPremDays = 360, CloudDays = 180 };

        var result = await provider.GetAsync(policy, TenantContext.Unknown, CancellationToken.None);

        Assert.NotNull(result);
    }

    private sealed class FakeGraphClient : IGraphClient
    {
        public Task<GraphUser?> GetMeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GraphUser?>(new GraphUser
            {
                LastPasswordChangeDateTime = DateTimeOffset.UtcNow.AddDays(-1),
                OnPremisesSyncEnabled = true,
                PasswordPolicies = null
            });
        }
    }
}
