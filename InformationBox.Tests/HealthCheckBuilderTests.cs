using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.UI.ViewModels;
using Xunit;

namespace InformationBox.Tests;

public class HealthCheckBuilderTests
{
    [Fact]
    public void BuildPlaceholder_NullTenant_SetsJoinStateUnknown()
    {
        var options = new HealthOptions { PingTargets = Array.Empty<HealthEndpoint>() };

        var rows = HealthCheckBuilder.BuildPlaceholder(null, options);

        var join = rows.Single(r => r.Label == HealthCheckBuilder.JoinStateLabel).Value;
        Assert.Equal("Unknown", join);
    }

    [Fact]
    public void WithJoinState_UpdatesJoinStateRow()
    {
        var rows = new[] { new InfoRow(HealthCheckBuilder.JoinStateLabel, "Old") };
        var tenant = new TenantContext(null, null, null, TenantJoinType.AzureAdJoined, true, false, false);

        var updated = HealthCheckBuilder.WithJoinState(rows, tenant);

        Assert.Equal("AzureAdJoined", updated.Single(r => r.Label == HealthCheckBuilder.JoinStateLabel).Value);
    }

    [Fact]
    public async Task BuildAsync_ReturnsExpectedRows()
    {
        var network = new NetworkStatus("Wi-Fi", "Adapter", "ssid", "192.168.1.2", false);
        var tenant = new TenantContext(null, null, null, TenantJoinType.AzureAdJoined, true, false, false);
        var options = new HealthOptions
        {
            PingTimeoutMs = 50,
            PingTargets = new[]
            {
                new HealthEndpoint { Name = "Internet", Target = "8.8.8.8" }
            }
        };

        var rows = await HealthCheckBuilder.BuildAsync(
            network,
            tenant,
            @"X:\Windows",
            options,
            pingAsync: (_, _, _) => Task.FromResult<long?>(5),
            driveInfoFactory: _ => throw new Exception("disk"),
            readWindowsUpdateLastSuccessTime: () => "2025-01-01",
            cancellationToken: CancellationToken.None);

        Assert.Equal(HealthCheckBuilder.JoinStateLabel, rows[0].Label);
        Assert.Equal("AzureAdJoined", rows[0].Value);

        Assert.Contains(rows, r => r.Label == HealthCheckBuilder.ConnectionLabel && r.Value == "Wi-Fi");
        Assert.Contains(rows, r => r.Label == HealthCheckBuilder.VpnLabel && r.Value == "No");
        Assert.Contains(rows, r => r.Label == "Internet" && r.Value == "OK (5 ms)");
        Assert.Contains(rows, r => r.Label == HealthCheckBuilder.DiskSpaceLabel && r.Value == "Unknown");
        Assert.Contains(rows, r => r.Label == HealthCheckBuilder.WindowsUpdateLabel && r.Value == "2025-01-01");
        Assert.Contains(rows, r => r.Label == HealthCheckBuilder.IntuneEnrolledLabel);
    }
}

