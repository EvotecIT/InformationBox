using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Services;
using InformationBox.UI.ViewModels;
using Xunit;

namespace InformationBox.Tests;

public class HealthCheckBuilderTests
{
    [Fact]
    public void BuildPlaceholder_NullTenant_SetsJoinStateUnknown()
    {
        var rows = HealthCheckBuilder.BuildPlaceholder(null);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("X")]
    public void GetSystemDriveRootOrDefault_FallsBackToC_WhenInvalid(string? systemDirectory)
    {
        Assert.Equal("C:\\", HealthCheckBuilder.GetSystemDriveRootOrDefault(systemDirectory));
    }

    [Fact]
    public void GetSystemDriveRootOrDefault_ReturnsRoot_WhenDriveLetterPath()
    {
        Assert.Equal("X:\\", HealthCheckBuilder.GetSystemDriveRootOrDefault(@"X:\Windows\System32"));
    }

    [Fact]
    public void BuildDiskSpaceRow_ReturnsUnknown_WhenDriveFactoryThrows()
    {
        var row = HealthCheckBuilder.BuildDiskSpaceRow(@"X:\Windows", _ => throw new UnauthorizedAccessException());
        Assert.Equal(HealthCheckBuilder.DiskSpaceLabel, row.Label);
        Assert.Equal("Unknown", row.Value);
    }

    [Fact]
    public void BuildWindowsUpdateRow_ReturnsUnknown_WhenReaderThrows()
    {
        var row = HealthCheckBuilder.BuildWindowsUpdateRow(() => throw new UnauthorizedAccessException());
        Assert.Equal(HealthCheckBuilder.WindowsUpdateLabel, row.Label);
        Assert.Equal("Unknown", row.Value);
    }

    [Fact]
    public async Task BuildInternetRowAsync_ReturnsOffline_WhenNetworkOffline()
    {
        var network = new NetworkStatus("Offline", null, null, null, false);
        var row = await HealthCheckBuilder.BuildInternetRowAsync(network);
        Assert.Equal("Offline", row.Value);
    }

    [Fact]
    public async Task BuildInternetRowAsync_ReturnsNoResponse_WhenPingFails()
    {
        var network = new NetworkStatus("Wi-Fi", "Adapter", "ssid", "192.168.1.2", false);

        var row = await HealthCheckBuilder.BuildInternetRowAsync(
            network,
            pingAsync: (_, _, _) => Task.FromResult(new HealthCheckBuilder.PingResult(false, null)),
            cancellationToken: CancellationToken.None);

        Assert.Equal("No response", row.Value);
    }

    [Fact]
    public async Task BuildInternetRowAsync_FormatsRoundTrip_WhenPingSuccessful()
    {
        var network = new NetworkStatus("Wi-Fi", "Adapter", "ssid", "192.168.1.2", false);

        var row = await HealthCheckBuilder.BuildInternetRowAsync(
            network,
            pingAsync: (_, _, _) => Task.FromResult(new HealthCheckBuilder.PingResult(true, 42)),
            cancellationToken: CancellationToken.None);

        Assert.Equal("OK (42 ms)", row.Value);
    }

    [Fact]
    public async Task BuildInternetRowAsync_ReturnsUnreachable_WhenPingThrows()
    {
        var network = new NetworkStatus("Wi-Fi", "Adapter", "ssid", "192.168.1.2", false);

        var row = await HealthCheckBuilder.BuildInternetRowAsync(
            network,
            pingAsync: (_, _, _) => throw new InvalidOperationException("boom"),
            cancellationToken: CancellationToken.None);

        Assert.Equal("Unreachable", row.Value);
    }

    [Fact]
    public async Task BuildAsync_ReturnsExpectedRows()
    {
        var network = new NetworkStatus("Wi-Fi", "Adapter", "ssid", "192.168.1.2", false);
        var tenant = new TenantContext(null, null, null, TenantJoinType.AzureAdJoined, true, false, false);

        var rows = await HealthCheckBuilder.BuildAsync(
            network,
            tenant,
            @"X:\Windows",
            pingAsync: (_, _, _) => Task.FromResult(new HealthCheckBuilder.PingResult(true, 5)),
            driveInfoFactory: _ => throw new Exception("disk"),
            readWindowsUpdateLastSuccessTime: () => "2025-01-01",
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            new[]
            {
                HealthCheckBuilder.InternetLabel,
                HealthCheckBuilder.DiskSpaceLabel,
                HealthCheckBuilder.JoinStateLabel,
                HealthCheckBuilder.WindowsUpdateLabel
            },
            rows.Select(r => r.Label).ToArray());

        Assert.Equal("OK (5 ms)", rows[0].Value);
        Assert.Equal("Unknown", rows[1].Value);
        Assert.Equal("AzureAdJoined", rows[2].Value);
        Assert.Equal("2025-01-01", rows[3].Value);
    }
}

