using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using InformationBox.Services;
using System.Diagnostics;
using System.Linq;

namespace InformationBox.Services;

/// <summary>
/// Retrieves tenant/join information using dsreg.dll (no process spawn).
/// </summary>
public static class TenantInfoProvider
{
    /// <summary>
    /// Discovers the current tenant context by querying native APIs, dsregcmd output, and registry fallbacks.
    /// </summary>
    /// <returns>A populated tenant context describing device join state.</returns>
    public static TenantContext GetTenantContext()
    {
        if (!OperatingSystem.IsWindows())
        {
            return TenantContext.Unknown;
        }

        var nativeContext = TryNativeJoinInfo();
        if (nativeContext is not null)
        {
            return nativeContext;
        }

        // Try parsing dsregcmd /status
        var cmdContext = TryDsregCmd();
        if (cmdContext is not null)
        {
            return cmdContext;
        }

        // Registry-based detection for AAD/workplace
        var regContext = TryRegistryTenant();
        if (regContext is not null)
        {
            Logger.Info($"Registry tenant found: {regContext.TenantId} {regContext.TenantName}");
            return regContext;
        }

        // Detect AD domain join
        try
        {
            var domain = System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain();
            Logger.Info($"Domain join detected: {domain?.Name}");
            return new TenantContext(
                TenantId: null,
                TenantName: domain?.Name,
                DomainName: domain?.Name,
                JoinType: TenantJoinType.DomainJoined,
                AzureAdJoined: false,
                WorkplaceJoined: false,
                DomainJoined: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Domain detection failed", ex);
        }

        Logger.Info("Tenant detection exhausted all options; returning Unknown");
        return TenantContext.Unknown;
    }

    private static string? PtrToString(IntPtr ptr) => ptr == IntPtr.Zero ? null : Marshal.PtrToStringUni(ptr);

    private static TenantContext? TryNativeJoinInfo()
        => TryNetApiJoinInfo() ?? TryDsregDllJoinInfo();

    private static TenantContext? TryNetApiJoinInfo()
    {
        IntPtr infoPtr = IntPtr.Zero;
        try
        {
            var hr = NetGetAadJoinInformation(null, out infoPtr);
            if (hr == 0 && infoPtr != IntPtr.Zero)
            {
                var info = Marshal.PtrToStructure<DSREG_JOIN_INFO>(infoPtr);
                return CreateTenantContext(info, "NetGetAadJoinInformation");
            }
            Logger.Info($"NetGetAadJoinInformation returned hr={hr} ptr={(infoPtr == IntPtr.Zero ? "null" : "non-null")}");
        }
        catch (EntryPointNotFoundException ex)
        {
            Logger.Info($"NetGetAadJoinInformation entry point not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error("NetGetAadJoinInformation failed", ex);
        }
        finally
        {
            if (infoPtr != IntPtr.Zero)
            {
                NetFreeAadJoinInformation(infoPtr);
            }
        }

        return null;
    }

    private static TenantContext? TryDsregDllJoinInfo()
    {
        IntPtr infoPtr = IntPtr.Zero;
        try
        {
            var hr = DsregGetJoinInfo(IntPtr.Zero, out infoPtr);
            if (hr == 0 && infoPtr != IntPtr.Zero)
            {
                var info = Marshal.PtrToStructure<DSREG_JOIN_INFO>(infoPtr);
                return CreateTenantContext(info, "dsreg");
            }
            Logger.Info($"dsreg returned hr={hr} ptr={(infoPtr == IntPtr.Zero ? "null" : "non-null")}");
        }
        catch (EntryPointNotFoundException ex)
        {
            Logger.Info($"dsreg entry point not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error("dsreg invocation failed", ex);
        }
        finally
        {
            if (infoPtr != IntPtr.Zero)
            {
                DsregFreeJoinInfo(infoPtr);
            }
        }

        return null;
    }

    private static TenantContext CreateTenantContext(DSREG_JOIN_INFO info, string source)
    {
        var tenantId = PtrToString(info.pszTenantId);
        var tenantName = PtrToString(info.pszTenantDisplayName);
        var domain = PtrToString(info.pszIdpDomain);

        var joinType = info.joinType switch
        {
            DSREG_JOIN_TYPE.DSREG_DEVICE_JOIN => TenantJoinType.AzureAdJoined,
            DSREG_JOIN_TYPE.DSREG_WORKPLACE_JOIN => TenantJoinType.WorkplaceJoined,
            _ => TenantJoinType.Unknown
        };

        Logger.Info($"{source} join info: JoinType={info.joinType} TenantId={tenantId ?? "<null>"} TenantName={tenantName ?? "<null>"}");
        return new TenantContext(
            tenantId,
            tenantName,
            domain,
            joinType,
            AzureAdJoined: joinType == TenantJoinType.AzureAdJoined,
            WorkplaceJoined: joinType == TenantJoinType.WorkplaceJoined,
            DomainJoined: false);
    }

    private static TenantContext? TryDsregCmd()
    {
        try
        {
            var psi = new ProcessStartInfo("dsregcmd.exe", "/status")
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
            proc.WaitForExit(2000);

            string? Get(string key)
            {
                var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase));
                if (line == null) return null;
                var parts = line.Split(':');
                return parts.Length > 1 ? parts[1].Trim() : null;
            }

            var aad = Get("AzureAdJoined");
            var tenantId = Get("TenantId");
            var tenantName = Get("TenantName");
            var domain = Get("DomainName");

            if (string.Equals(aad, "YES", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info($"dsregcmd parsed: TenantId={tenantId} TenantName={tenantName} Domain={domain}");
                return new TenantContext(
                    tenantId,
                    tenantName,
                    domain,
                    TenantJoinType.AzureAdJoined,
                    AzureAdJoined: true,
                    WorkplaceJoined: false,
                    DomainJoined: false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("dsregcmd parse failed", ex);
        }
        return null;
    }

    private static TenantContext? TryRegistryTenant()
    {
        const string keyPath = "SOFTWARE\\Microsoft\\AzureAD\\TenantInformation";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null)
            {
                return null;
            }

            foreach (var subName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subName);
                if (sub == null)
                {
                    continue;
                }

                var tenantId = sub.GetValue("TenantId") as string ?? subName;
                var tenantName = sub.GetValue("Name") as string ?? sub.GetValue("TenantDisplayName") as string;
                var domain = sub.GetValue("Domain") as string;

                return new TenantContext(
                    tenantId,
                    tenantName,
                    domain,
                    TenantJoinType.AzureAdJoined,
                    AzureAdJoined: true,
                    WorkplaceJoined: false,
                    DomainJoined: false);
            }
        }
        catch
        {
            // ignore and fallback
        }

        return null;
    }

    [DllImport("dsreg.dll", CharSet = CharSet.Unicode)]
    private static extern int DsregGetJoinInfo(IntPtr reserved, out IntPtr ppJoinInfo);

    [DllImport("dsreg.dll")]
    private static extern void DsregFreeJoinInfo(IntPtr pJoinInfo);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetGetAadJoinInformation(string? pcszTenantId, out IntPtr ppJoinInfo);

    [DllImport("netapi32.dll")]
    private static extern void NetFreeAadJoinInformation(IntPtr pJoinInfo);

    private enum DSREG_JOIN_TYPE
    {
        DSREG_UNKNOWN_JOIN = 0,
        DSREG_DEVICE_JOIN = 1,
        DSREG_WORKPLACE_JOIN = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DSREG_USER_INFO
    {
        public IntPtr pszUserEmail;
        public IntPtr pszUserKeyId;
        public IntPtr pszUserKeyName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DSREG_JOIN_INFO
    {
        public DSREG_JOIN_TYPE joinType;
        public IntPtr pJoinCertificate;
        public IntPtr pszDeviceId;
        public IntPtr pszIdpDomain;
        public IntPtr pszTenantId;
        public IntPtr pszJoinUserEmail;
        public IntPtr pszTenantDisplayName;
        public IntPtr pszMdmEnrollmentUrl;
        public IntPtr pszMdmTermsOfUseUrl;
        public IntPtr pszMdmComplianceUrl;
        public IntPtr pszUserSettingSyncUrl;
        public IntPtr pUserInfo;
    }
}
