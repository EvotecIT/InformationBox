using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace InformationBox.Services;

/// <summary>
/// Provides snapshot information about the active network connection.
/// </summary>
public static class NetworkInfoProvider
{
    /// <summary>
    /// Captures the current preferred network adapter, connection type, VPN status, and addressing info.
    /// </summary>
    /// <returns>A snapshot describing the current network connection.</returns>
    public static NetworkStatus GetCurrentStatus()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var active = interfaces
            .Where(IsCandidate)
            .OrderByDescending(Priority)
            .FirstOrDefault();

        if (active is null)
        {
            return new NetworkStatus("Offline", null, null, null, false);
        }

        var ip = active.GetIPProperties()
            .UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
            .Address
            .ToString();

        var connectionType = Describe(active.NetworkInterfaceType);
        var isVpn = active.NetworkInterfaceType is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel;
        var ssid = active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? TryGetWifiSsid() : null;

        return new NetworkStatus(connectionType, active.Name, ssid, ip, isVpn);
    }

    private static bool IsCandidate(NetworkInterface ni)
        => ni.OperationalStatus == OperationalStatus.Up
           && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
           && !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase);

    private static int Priority(NetworkInterface ni) => ni.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Wireless80211 => 3,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => 2,
        NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp => 1,
        _ => 0
    };

    private static string Describe(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet => "Ethernet",
        NetworkInterfaceType.GigabitEthernet => "Ethernet",
        NetworkInterfaceType.Tunnel => "VPN",
        NetworkInterfaceType.Ppp => "VPN",
        _ => type.ToString()
    };

    private static string? TryGetWifiSsid()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return null;
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);

            foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("SSID BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':');
                    if (idx > -1)
                    {
                        return line[(idx + 1)..].Trim();
                    }
                }
            }
        }
        catch
        {
            // ignore and fallback
        }

        return null;
    }
}

/// <summary>
/// Represents the current network connection snapshot used by the UI.
/// </summary>
/// <param name="ConnectionType">Friendly description of the medium (Wi-Fi, Ethernet, VPN, etc.).</param>
/// <param name="AdapterName">System-provided name of the adapter.</param>
/// <param name="Ssid">Wi-Fi SSID when connected wirelessly.</param>
/// <param name="Ipv4Address">Primary IPv4 address, if available.</param>
/// <param name="IsVpn">True when the adapter indicates a VPN connection.</param>
public sealed record NetworkStatus(
    string ConnectionType,
    string? AdapterName,
    string? Ssid,
    string? Ipv4Address,
    bool IsVpn);
