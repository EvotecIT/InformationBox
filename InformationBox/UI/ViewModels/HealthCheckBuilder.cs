using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using InformationBox.Config;
using InformationBox.Services;
using Microsoft.Win32;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Builds a lightweight, non-admin health summary for display in the UI.
/// </summary>
public static class HealthCheckBuilder
{
    public const string JoinStateLabel = "Join state";
    public const string ConnectionLabel = "Connection";
    public const string VpnLabel = "VPN";
    public const string UptimeLabel = "Uptime";
    public const string DiskSpaceLabel = "Disk space";
    public const string WindowsUpdateLabel = "Windows Update";
    public const string IntuneEnrolledLabel = "Intune enrolled";
    public const string IntuneLastSyncLabel = "Intune last sync";

    /// <summary>
    /// Builds placeholder rows while a background refresh is running.
    /// </summary>
    public static IReadOnlyList<InfoRow> BuildPlaceholder(TenantContext? tenant, HealthOptions? options = null)
    {
        const string checking = "Checking...";

        var rows = new List<InfoRow>
        {
            new(ConnectionLabel, checking),
            new(VpnLabel, checking),
            new(UptimeLabel, checking)
        };

        if (options is not null)
        {
            foreach (var target in options.PingTargets)
            {
                if (string.IsNullOrWhiteSpace(target.Target))
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(target.Name) ? target.Target : target.Name;
                rows.Add(new InfoRow(name, checking));
            }
        }

        rows.Add(new InfoRow(DiskSpaceLabel, checking));
        rows.Add(new InfoRow(WindowsUpdateLabel, checking));
        rows.Add(new InfoRow(IntuneEnrolledLabel, checking));
        rows.Add(new InfoRow(IntuneLastSyncLabel, checking));

        return WithJoinState(rows.AsReadOnly(), tenant);
    }

    /// <summary>
    /// Ensures the join state row is present and reflects the current tenant context.
    /// </summary>
    public static IReadOnlyList<InfoRow> WithJoinState(IReadOnlyList<InfoRow> rows, TenantContext? tenant)
    {
        var joinType = tenant?.JoinType.ToString() ?? "Unknown";
        var list = rows.ToList();

        var idx = list.FindIndex(r => string.Equals(r.Label, JoinStateLabel, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            list[idx] = list[idx] with { Value = joinType };
        }
        else
        {
            list.Insert(0, new InfoRow(JoinStateLabel, joinType));
        }

        return list.AsReadOnly();
    }

    /// <summary>
    /// Performs the configured health checks (best-effort, no elevation required).
    /// </summary>
    public static async Task<IReadOnlyList<InfoRow>> BuildAsync(
        NetworkStatus network,
        TenantContext? tenant,
        string systemDirectory,
        HealthOptions options,
        Func<string, int, CancellationToken, Task<long?>>? pingAsync,
        Func<string, DriveInfo>? driveInfoFactory,
        Func<string?>? readWindowsUpdateLastSuccessTime,
        CancellationToken cancellationToken)
    {
        // Explicit null checks keep this resilient even if upstream inputs change.
        var connectionType = string.IsNullOrWhiteSpace(network.ConnectionType) ? "Unknown" : network.ConnectionType;

        var rows = new List<InfoRow>
        {
            new(ConnectionLabel, connectionType),
            new(VpnLabel, network.IsVpn ? "Detected" : "No"),
            new(UptimeLabel, FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64)))
        };

        rows.AddRange(await BuildPingRowsAsync(options, pingAsync, cancellationToken).ConfigureAwait(false));
        rows.Add(BuildDiskRow(systemDirectory, driveInfoFactory));
        rows.Add(BuildWindowsUpdateRow(readWindowsUpdateLastSuccessTime));
        rows.AddRange(BuildIntuneRows());

        return WithJoinState(rows.AsReadOnly(), tenant);
    }

    private static async Task<IReadOnlyList<InfoRow>> BuildPingRowsAsync(
        HealthOptions options,
        Func<string, int, CancellationToken, Task<long?>>? pingAsync,
        CancellationToken cancellationToken)
    {
        if (options.PingTimeoutMs <= 0 || options.PingTargets.Count == 0)
        {
            return Array.Empty<InfoRow>();
        }

        pingAsync ??= DefaultPingAsync;

        var rows = new List<InfoRow>();
        var timeout = Math.Max(1, options.PingTimeoutMs);

        foreach (var target in options.PingTargets)
        {
            if (string.IsNullOrWhiteSpace(target.Target))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var name = string.IsNullOrWhiteSpace(target.Name) ? target.Target : target.Name;

            long? ms = null;
            try
            {
                ms = await pingAsync(target.Target, timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                ms = null;
            }

            rows.Add(new InfoRow(name, ms.HasValue ? $"OK ({ms.Value} ms)" : "Unreachable"));
        }

        return rows;
    }

    private static async Task<long?> DefaultPingAsync(string hostOrIp, int timeoutMs, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(hostOrIp, timeoutMs).WaitAsync(cancellationToken).ConfigureAwait(false);
        return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
    }

    private static InfoRow BuildDiskRow(string systemDirectory, Func<string, DriveInfo>? driveInfoFactory)
    {
        try
        {
            driveInfoFactory ??= drive => new DriveInfo(drive);
            var systemDrive = string.IsNullOrWhiteSpace(systemDirectory) ? "C:" : systemDirectory[..2];
            var di = driveInfoFactory(systemDrive);
            var pct = di.TotalSize > 0 ? (di.TotalFreeSpace * 100.0 / di.TotalSize) : 0;
            var state = pct < 10 ? "Low space" : "OK";
            return new InfoRow(DiskSpaceLabel, $"{state} ({pct:0}% free)");
        }
        catch
        {
            return new InfoRow(DiskSpaceLabel, "Unknown");
        }
    }

    private static InfoRow BuildWindowsUpdateRow(Func<string?>? readWindowsUpdateLastSuccessTime)
    {
        try
        {
            var last = readWindowsUpdateLastSuccessTime?.Invoke() ?? ReadWindowsUpdateLastSuccessTime();
            return new InfoRow(WindowsUpdateLabel, string.IsNullOrWhiteSpace(last) ? "Unknown" : last);
        }
        catch
        {
            return new InfoRow(WindowsUpdateLabel, "Unknown");
        }
    }

    private static string? ReadWindowsUpdateLastSuccessTime()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
        return key?.GetValue("LastSuccessTime") as string;
    }

    private static IEnumerable<InfoRow> BuildIntuneRows()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var enrollments = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments");

            if (enrollments is null)
            {
                return new[] { new InfoRow(IntuneEnrolledLabel, "Unknown") };
            }

            DateTimeOffset? latest = null;
            foreach (var name in enrollments.GetSubKeyNames())
            {
                using var sub = enrollments.OpenSubKey(name);
                if (sub is null)
                {
                    continue;
                }

                var lastSyncRaw = sub.GetValue("LastSyncTime") as string;
                if (TryParseRegistryTimestamp(lastSyncRaw, out var parsed))
                {
                    latest = latest is null || parsed > latest ? parsed : latest;
                }
            }

            var enrolled = enrollments.GetSubKeyNames().Length > 0;
            var syncValue = enrolled ? (latest?.ToLocalTime().ToString("g") ?? "Unknown") : "N/A";

            return new[]
            {
                new InfoRow(IntuneEnrolledLabel, enrolled ? "Yes" : "No"),
                new InfoRow(IntuneLastSyncLabel, syncValue)
            };
        }
        catch (UnauthorizedAccessException)
        {
            return new[] { new InfoRow(IntuneEnrolledLabel, "Unknown") };
        }
        catch
        {
            return new[] { new InfoRow(IntuneEnrolledLabel, "Unknown") };
        }
    }

    private static bool TryParseRegistryTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, out timestamp);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{(int)uptime.TotalMinutes}m";
    }
}
