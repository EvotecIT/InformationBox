using System;
using System.Collections.Generic;
using System.Linq;

namespace InformationBox.Config.Fixes;

/// <summary>
/// Provides the built-in fix catalog and merges it with tenant overrides.
/// </summary>
public static class FixRegistry
{
    private static readonly FixAction[] BuiltInFixes =
    {
        new()
        {
            Id = "restart-onedrive",
            Name = "Restart OneDrive",
            Description = "Close and start OneDrive to fix simple sync glitches.",
            Category = FixCategory.OneDrive,
            Command = "$exe = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\", \"C:\\\\Program Files\\\\Microsoft OneDrive\\\\OneDrive.exe\") | Where-Object { Test-Path $_ } | Select-Object -First 1; if ($exe) { Stop-Process -Name OneDrive -ErrorAction SilentlyContinue; & $exe }",
            ConfirmText = "OneDrive will be restarted. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "reset-teams-cache",
            Name = "Reset Teams cache",
            Description = "Clears Teams cache (safe, no data loss).",
            Category = FixCategory.Teams,
            Command = "Stop-Process -Name ms-teams -ErrorAction SilentlyContinue; $cache = Join-Path $env:APPDATA 'Microsoft\\Teams\\IndexedDB'; if (Test-Path $cache) { Remove-Item $cache -Recurse -Force -ErrorAction SilentlyContinue }",
            ConfirmText = "Teams will close and cache will be cleared. Continue?",
            Visible = true,
            Order = 2
        },
        new()
        {
            Id = "clear-edge-cache",
            Name = "Clear Edge cache",
            Description = "Clears Edge browser cache (user data kept).",
            Category = FixCategory.Browser,
            Command = "Start-Process msedge.exe 'msedge://settings/clearBrowserData' -ErrorAction SilentlyContinue",
            ConfirmText = null,
            Visible = true,
            Order = 3
        },
        new()
        {
            Id = "clear-chrome-cache",
            Name = "Clear Chrome cache",
            Description = "Opens Chrome cache clear dialog.",
            Category = FixCategory.Browser,
            Command = "Start-Process chrome.exe 'chrome://settings/clearBrowserData' -ErrorAction SilentlyContinue",
            ConfirmText = null,
            Visible = true,
            Order = 4
        },
        new()
        {
            Id = "wsreset",
            Name = "Reset Microsoft Store",
            Description = "Runs wsreset to clear Store cache.",
            Category = FixCategory.Windows,
            Command = "Start-Process wsreset.exe",
            ConfirmText = "Microsoft Store cache will be reset. Continue?",
            Visible = true,
            Order = 5
        },
        new()
        {
            Id = "collect-logs",
            Name = "Collect basic logs",
            Description = "Gather networking and dsreg status to a zip on your Desktop.",
            Category = FixCategory.Support,
            Command = "$outDir = Join-Path $env:TEMP 'InfoBoxLogs'; New-Item -ItemType Directory -Force -Path $outDir | Out-Null; ipconfig /all > (Join-Path $outDir 'ipconfig.txt'); dsregcmd /status > (Join-Path $outDir 'dsregcmd.txt') 2>&1; Get-Date | Out-File (Join-Path $outDir 'stamp.txt'); $zip = Join-Path ([Environment]::GetFolderPath('Desktop')) ('InfoBoxLogs_' + $env:COMPUTERNAME + '.zip'); Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -Force; Start-Process explorer.exe (Split-Path $zip); Write-Output \"Logs saved to $zip\"",
            ConfirmText = null,
            Visible = true,
            Order = 6
        },
        new()
        {
            Id = "email-logs",
            Name = "Collect & email logs",
            Description = "Collects logs to Desktop and opens email to support with attachment.",
            Category = FixCategory.Support,
            Command = "$outDir = Join-Path $env:TEMP 'InfoBoxLogs'; New-Item -ItemType Directory -Force -Path $outDir | Out-Null; ipconfig /all > (Join-Path $outDir 'ipconfig.txt'); dsregcmd /status > (Join-Path $outDir 'dsregcmd.txt') 2>&1; Get-Date | Out-File (Join-Path $outDir 'stamp.txt'); $zip = Join-Path ([Environment]::GetFolderPath('Desktop')) ('InfoBoxLogs_' + $env:COMPUTERNAME + '.zip'); Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -Force; $mailto = 'mailto:support@contoso.com?subject=InfoBox%20logs%20for%20'+$env:COMPUTERNAME+'&body=Logs%20are%20attached.'; Start-Process explorer.exe (Split-Path $zip); Start-Process $mailto; Start-Process powershell.exe \"-NoLogo -NoProfile -Command Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('Attach the zip from your Desktop to the email if it did not auto-attach.','InfoBox logs');\"",
            ConfirmText = "Collect logs and open an email draft to support?",
            Visible = true,
            Order = 7
        },
        new()
        {
            Id = "reset-vpn-adapter",
            Name = "Reset VPN adapter",
            Description = "Disables/enables VPN adapters to clear stale tunnels.",
            Category = FixCategory.Windows,
            Command = "Get-NetAdapter -Name '*vpn*','*VPN*' -ErrorAction SilentlyContinue | Disable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; Get-NetAdapter -Name '*vpn*','*VPN*' -ErrorAction SilentlyContinue | Enable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue",
            ConfirmText = "VPN adapters will be toggled. Continue?",
            Visible = true,
            Order = 8
        },
        new()
        {
            Id = "repair-outlook-teams-addin",
            Name = "Repair Outlook Teams add-in",
            Description = "Re-registers the Teams meeting add-in for Outlook.",
            Category = FixCategory.Windows,
            Command = "Stop-Process -Name outlook -ErrorAction SilentlyContinue; $addinPath = \"$env:LOCALAPPDATA\\Microsoft\\TeamsMeetingAddin\\*\\Microsoft.Teams.AddinLoader.dll\"; $dll = Get-ChildItem -Path $addinPath -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if ($dll) { & regsvr32.exe /s $dll.FullName }; Start-Process outlook.exe",
            ConfirmText = "Outlook will restart and the Teams add-in will be repaired. Continue?",
            Visible = true,
            Order = 9
        }
    };

    public static IReadOnlyList<FixAction> BuildFixes(IReadOnlyList<FixAction> configured)
    {
        var overrides = configured
            .Where(f => !string.IsNullOrWhiteSpace(f.Id))
            .ToDictionary(f => f.Id!, StringComparer.OrdinalIgnoreCase);

        var merged = new List<FixAction>();

        foreach (var built in BuiltInFixes)
        {
            if (overrides.TryGetValue(built.Id!, out var ov))
            {
                var effective = built with
                {
                    Name = string.IsNullOrWhiteSpace(ov.Name) ? built.Name : ov.Name,
                    Description = string.IsNullOrWhiteSpace(ov.Description) ? built.Description : ov.Description,
                    Command = string.IsNullOrWhiteSpace(ov.Command) ? built.Command : ov.Command,
                    ConfirmText = ov.ConfirmText ?? built.ConfirmText,
                    Visible = ov.Visible,
                    Order = ov.Order != 0 ? ov.Order : built.Order,
                    Category = ov.Category != FixCategory.Custom ? ov.Category : built.Category
                };
                if (effective.Visible && !string.IsNullOrWhiteSpace(effective.Command))
                {
                    merged.Add(effective);
                }
            }
            else if (built.Visible && !string.IsNullOrWhiteSpace(built.Command))
            {
                merged.Add(built);
            }
        }

        var custom = configured
            .Where(f => string.IsNullOrWhiteSpace(f.Id) && f.Visible && !string.IsNullOrWhiteSpace(f.Command))
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Order)
            .ThenBy(f => f.Name);
        merged.AddRange(custom);

        return merged
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Order)
            .ThenBy(f => f.Name)
            .ToArray();
    }
}
