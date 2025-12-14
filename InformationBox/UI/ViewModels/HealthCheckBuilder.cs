using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Services;
using Microsoft.Win32;

namespace InformationBox.UI.ViewModels;

internal static class HealthCheckBuilder
{
    internal const string InternetLabel = "Internet";
    internal const string DiskSpaceLabel = "Disk space";
    internal const string JoinStateLabel = "Join state";
    internal const string WindowsUpdateLabel = "Windows Update";

    internal const int DefaultPingTimeoutMs = 750;

    internal static IReadOnlyList<InfoRow> BuildPlaceholder(TenantContext? tenant)
    {
        var join = tenant?.JoinType.ToString() ?? "Unknown";
        return new List<InfoRow>
        {
            new(InternetLabel, "Checking..."),
            new(DiskSpaceLabel, "Checking..."),
            new(JoinStateLabel, join),
            new(WindowsUpdateLabel, "Checking...")
        }.AsReadOnly();
    }

    internal static IReadOnlyList<InfoRow> WithJoinState(IReadOnlyList<InfoRow> rows, TenantContext? tenant)
    {
        var join = tenant?.JoinType.ToString() ?? "Unknown";
        var updated = rows
            .Select(r => r.Label == JoinStateLabel ? r with { Value = join } : r)
            .ToList();

        if (updated.All(r => r.Label != JoinStateLabel))
        {
            updated.Add(new InfoRow(JoinStateLabel, join));
        }

        return updated.AsReadOnly();
    }

    internal static string GetSystemDriveRootOrDefault(string? systemDirectory)
    {
        if (!string.IsNullOrWhiteSpace(systemDirectory))
        {
            try
            {
                var root = Path.GetPathRoot(systemDirectory);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    return root;
                }
            }
            catch
            {
                // fall through
            }

            if (systemDirectory.Length >= 2 && systemDirectory[1] == ':')
            {
                return systemDirectory[..2] + "\\";
            }
        }

        return "C:\\";
    }

    internal static InfoRow BuildDiskSpaceRow(string? systemDirectory, Func<string, DriveInfo>? driveInfoFactory = null)
    {
        driveInfoFactory ??= static driveRoot => new DriveInfo(driveRoot);

        try
        {
            var driveRoot = GetSystemDriveRootOrDefault(systemDirectory);
            var drive = driveInfoFactory(driveRoot);
            var pct = drive.TotalSize > 0 ? (drive.TotalFreeSpace * 100.0 / drive.TotalSize) : 0;
            var state = pct < 10 ? "Low space" : "OK";
            return new InfoRow(DiskSpaceLabel, $"{state} ({pct:0}% free)");
        }
        catch
        {
            return new InfoRow(DiskSpaceLabel, "Unknown");
        }
    }

    internal static InfoRow BuildWindowsUpdateRow(Func<string?>? readLastSuccessTime = null)
    {
        readLastSuccessTime ??= ReadWindowsUpdateLastSuccessTime;

        try
        {
            var last = readLastSuccessTime();
            return new InfoRow(WindowsUpdateLabel, string.IsNullOrWhiteSpace(last) ? "Unknown" : last);
        }
        catch (Exception ex)
        {
            Logger.Info($"Windows Update registry read failed: {ex.GetType().Name}: {ex.Message}");
            return new InfoRow(WindowsUpdateLabel, "Unknown");
        }
    }

    internal readonly record struct PingResult(bool IsSuccess, long? RoundTripTimeMs);

    internal static async Task<InfoRow> BuildInternetRowAsync(
        NetworkStatus network,
        Func<string, int, CancellationToken, Task<PingResult>>? pingAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(network.ConnectionType, "Offline", StringComparison.OrdinalIgnoreCase))
        {
            return new InfoRow(InternetLabel, "Offline");
        }

        pingAsync ??= DefaultPingAsync;

        try
        {
            var reply = await pingAsync("8.8.8.8", DefaultPingTimeoutMs, cancellationToken).ConfigureAwait(false);
            if (!reply.IsSuccess)
            {
                return new InfoRow(InternetLabel, "No response");
            }

            var rtt = reply.RoundTripTimeMs.HasValue ? $"{reply.RoundTripTimeMs.Value} ms" : "OK";
            return new InfoRow(InternetLabel, $"OK ({rtt})");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new InfoRow(InternetLabel, "Unreachable");
        }
    }

    internal static async Task<IReadOnlyList<InfoRow>> BuildAsync(
        NetworkStatus network,
        TenantContext? tenant,
        string? systemDirectory,
        Func<string, int, CancellationToken, Task<PingResult>>? pingAsync,
        Func<string, DriveInfo>? driveInfoFactory,
        Func<string?>? readWindowsUpdateLastSuccessTime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var internetTask = BuildInternetRowAsync(network, pingAsync, cancellationToken);

        // Run remaining checks immediately; they are quick but still best-effort.
        var disk = BuildDiskSpaceRow(systemDirectory, driveInfoFactory);
        var join = new InfoRow(JoinStateLabel, tenant?.JoinType.ToString() ?? "Unknown");
        var windowsUpdate = BuildWindowsUpdateRow(readWindowsUpdateLastSuccessTime);

        var internet = await internetTask.ConfigureAwait(false);

        return new List<InfoRow>
        {
            internet,
            disk,
            join,
            windowsUpdate
        }.AsReadOnly();
    }

    private static async Task<PingResult> DefaultPingAsync(string address, int timeoutMs, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(address, timeoutMs).WaitAsync(cancellationToken).ConfigureAwait(false);

        var isSuccess = reply?.Status == IPStatus.Success;
        return new PingResult(isSuccess, reply?.RoundtripTime);
    }

    private static string? ReadWindowsUpdateLastSuccessTime()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
        return key?.GetValue("LastSuccessTime") as string;
    }
}
