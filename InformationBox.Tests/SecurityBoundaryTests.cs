using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using InformationBox.Config;
using InformationBox.Config.Fixes;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class SecurityBoundaryTests
{
    [Fact]
    public async Task UserConfig_CannotSupplyElevatedActionsOrPolicy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"InformationBox-security-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "branding": { "productName": "User theme" },
                  "security": { "allowElevation": true },
                  "fixes": [
                    { "id": "flush-dns", "command": "Write-Output injected", "requiresAdmin": true },
                    { "name": "Custom elevated", "command": "Write-Output injected", "requiresAdmin": true }
                  ],
                  "tenantOverrides": {
                    "tenant-1": { "security": { "allowElevation": true } }
                  }
                }
                """);

            var loaded = await new ConfigLoader(new[] { path }).LoadAsync();
            var tenant = ConfigMerger.Merge(loaded.Config, "tenant-1");
            var flushDns = FixRegistry.BuildFixes(tenant.Fixes).Single(f => f.Id == "flush-dns");

            Assert.Equal("User theme", tenant.Branding.ProductName);
            Assert.False(tenant.Security.AllowElevation);
            Assert.Empty(tenant.Fixes);
            Assert.DoesNotContain("injected", flushDns.Command);
            Assert.True(flushDns.RequiresAdmin);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ProtectedMachineConfig_CannotEnableElevationFromJson()
    {
        var configured = new AppConfig
        {
            Security = new SecurityOptions { AllowElevation = true },
            TenantOverrides = new Dictionary<string, TenantOverride>
            {
                ["tenant-1"] = new() { Security = new SecurityOptions { AllowElevation = true } }
            }
        };

        var effective = ConfigLoader.RestrictExecutionConfig(configured, new AppConfig(), protectedMachineConfig: true);
        var tenant = ConfigMerger.Merge(effective, "tenant-1");

        Assert.False(effective.Security.AllowElevation);
        Assert.False(tenant.Security.AllowElevation);
    }

    [Theory]
    [InlineData("{\"tenantOverrides\":null}")]
    [InlineData("{\"tenantOverrides\":{\"tenant-1\":null}}")]
    public async Task UserConfig_NullTenantOverrides_DoNotPreventStartup(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"InformationBox-security-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, json);
            var loaded = await new ConfigLoader(new[] { path }).LoadAsync();
            Assert.False(loaded.Config.Security.AllowElevation);
            Assert.Empty(loaded.Config.TenantOverrides);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MachineConfigAcl_RejectsUserWritableFileOrDirectory()
    {
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var security = new FileSecurity();
        security.SetOwner(administrators);
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl,
            AccessControlType.Allow));

        Assert.True(ProtectedMachineConfig.HasPrivilegedOwnerAndWriters(security));

        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.WriteData,
            AccessControlType.Allow));
        Assert.False(ProtectedMachineConfig.HasPrivilegedOwnerAndWriters(security));
    }

    [Fact]
    public void ElevatedLaunch_UsesSystemExecutableAndNoUserTempOutput()
    {
        var startInfo = CommandRunner.CreateElevatedStartInfo("Write-Output hello");
        var encoded = startInfo.Arguments.Split(' ').Last();
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));

        Assert.Equal(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            startInfo.FileName);
        Assert.True(Path.IsPathFullyQualified(startInfo.FileName));
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);
        Assert.Equal(ProcessWindowStyle.Normal, startInfo.WindowStyle);
        Assert.Contains("Write-Output hello", script);
        Assert.DoesNotContain("Out-File", script);
        Assert.DoesNotContain("InfoBox_Output", script);
    }

    [Theory]
    [InlineData("sfc-scan")]
    [InlineData("dism-repair")]
    public void BuiltInRepairs_DoNotRequestNestedElevation(string id)
    {
        var action = FixRegistry.BuildFixes(Array.Empty<FixAction>()).Single(f => f.Id == id);

        Assert.True(action.RequiresAdmin);
        Assert.DoesNotContain("RunAs", action.Command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$env:SystemRoot\\System32\\", action.Command);
    }
}
