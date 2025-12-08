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
        // ═══════════════════════════════════════════════════════════════════════
        // USER-LEVEL ACTIONS (no admin required)
        // ═══════════════════════════════════════════════════════════════════════

        // OneDrive
        new()
        {
            Id = "restart-onedrive",
            Name = "Restart OneDrive",
            Description = "Close and start OneDrive to fix simple sync glitches.",
            Category = FixCategory.OneDrive,
            Command = "$exe = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\", \"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\") | Where-Object { Test-Path $_ } | Select-Object -First 1; if ($exe) { Stop-Process -Name OneDrive -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; Start-Process $exe; Write-Host 'OneDrive restarted.' }",
            ConfirmText = "OneDrive will be restarted. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "reset-onedrive",
            Name = "Reset OneDrive",
            Description = "Full OneDrive reset (re-syncs files, no data loss).",
            Category = FixCategory.OneDrive,
            Command = "$exe = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\", \"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\") | Where-Object { Test-Path $_ } | Select-Object -First 1; if ($exe) { Start-Process $exe -ArgumentList '/reset' -Wait; Start-Sleep -Seconds 5; Start-Process $exe; Write-Host 'OneDrive reset complete.' }",
            ConfirmText = "OneDrive will reset and re-sync. This may take a while. Continue?",
            Visible = true,
            Order = 2
        },

        // Teams
        new()
        {
            Id = "restart-teams",
            Name = "Restart Teams",
            Description = "Close and restart Microsoft Teams.",
            Category = FixCategory.Teams,
            Command = "Stop-Process -Name ms-teams -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; Start-Process 'shell:AppsFolder\\MSTeams_8wekyb3d8bbwe!MSTeams' -ErrorAction SilentlyContinue",
            ConfirmText = "Teams will be restarted. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "reset-teams-cache",
            Name = "Reset Teams cache",
            Description = "Clears Teams cache (safe, no data loss).",
            Category = FixCategory.Teams,
            Command = "Stop-Process -Name ms-teams -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; $paths = @(\"$env:LOCALAPPDATA\\Packages\\MSTeams_8wekyb3d8bbwe\\LocalCache\", \"$env:APPDATA\\Microsoft\\Teams\"); foreach ($p in $paths) { if (Test-Path $p) { Get-ChildItem $p -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } }",
            ConfirmText = "Teams will close and cache will be cleared. Continue?",
            Visible = true,
            Order = 2
        },

        // Browser
        new()
        {
            Id = "clear-edge-cache",
            Name = "Clear Edge cache",
            Description = "Opens Edge cache clear dialog.",
            Category = FixCategory.Browser,
            Command = "Start-Process msedge.exe 'edge://settings/clearBrowserData' -ErrorAction SilentlyContinue",
            ConfirmText = null,
            Visible = true,
            Order = 1
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
            Order = 2
        },
        new()
        {
            Id = "clear-firefox-cache",
            Name = "Clear Firefox cache",
            Description = "Opens Firefox cache clear dialog.",
            Category = FixCategory.Browser,
            Command = "Start-Process firefox.exe 'about:preferences#privacy' -ErrorAction SilentlyContinue",
            ConfirmText = null,
            Visible = true,
            Order = 3
        },

        // Office
        new()
        {
            Id = "restart-outlook",
            Name = "Restart Outlook",
            Description = "Close and restart Microsoft Outlook.",
            Category = FixCategory.Office,
            Command = "Stop-Process -Name outlook -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; Start-Process outlook.exe -ErrorAction SilentlyContinue",
            ConfirmText = "Outlook will be restarted. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "clear-outlook-cache",
            Name = "Clear Outlook cache",
            Description = "Clears Outlook autocomplete and temporary files.",
            Category = FixCategory.Office,
            Command = "Stop-Process -Name outlook -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; $cache = \"$env:LOCALAPPDATA\\Microsoft\\Outlook\\RoamCache\"; if (Test-Path $cache) { Remove-Item \"$cache\\*\" -Force -ErrorAction SilentlyContinue }; Start-Process outlook.exe -ErrorAction SilentlyContinue",
            ConfirmText = "Outlook will restart and cache will be cleared. Continue?",
            Visible = true,
            Order = 2
        },
        new()
        {
            Id = "repair-outlook-teams-addin",
            Name = "Repair Teams add-in",
            Description = "Re-registers the Teams meeting add-in for Outlook.",
            Category = FixCategory.Office,
            Command = "Stop-Process -Name outlook -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; $addinPath = \"$env:LOCALAPPDATA\\Microsoft\\TeamsMeetingAddin\\*\\Microsoft.Teams.AddinLoader.dll\"; $dll = Get-ChildItem -Path $addinPath -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if ($dll) { & regsvr32.exe /s $dll.FullName }; Start-Process outlook.exe -ErrorAction SilentlyContinue",
            ConfirmText = "Outlook will restart and the Teams add-in will be repaired. Continue?",
            Visible = true,
            Order = 3
        },
        new()
        {
            Id = "clear-office-cache",
            Name = "Clear Office cache",
            Description = "Clears Office document cache and temporary files.",
            Category = FixCategory.Office,
            Command = "$paths = @(\"$env:LOCALAPPDATA\\Microsoft\\Office\\16.0\\OfficeFileCache\", \"$env:LOCALAPPDATA\\Microsoft\\Office\\UnsavedFiles\"); foreach ($p in $paths) { if (Test-Path $p) { Remove-Item \"$p\\*\" -Recurse -Force -ErrorAction SilentlyContinue } }",
            ConfirmText = "Office cache will be cleared. Continue?",
            Visible = true,
            Order = 4
        },

        // Network (User-level)
        new()
        {
            Id = "release-renew-ip",
            Name = "Release/Renew IP",
            Description = "Releases and renews IP address from DHCP.",
            Category = FixCategory.Network,
            Command = "ipconfig /release; Start-Sleep -Seconds 2; ipconfig /renew",
            ConfirmText = "Network connection will briefly disconnect. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "reset-vpn-adapter",
            Name = "Reset VPN adapter",
            Description = "Disables/enables VPN adapters to clear stale tunnels.",
            Category = FixCategory.Network,
            Command = "Get-NetAdapter -Name '*vpn*','*VPN*','*Cisco*','*GlobalProtect*' -ErrorAction SilentlyContinue | Disable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue; Start-Sleep -Seconds 3; Get-NetAdapter -Name '*vpn*','*VPN*','*Cisco*','*GlobalProtect*' -ErrorAction SilentlyContinue | Enable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue",
            ConfirmText = "VPN adapters will be toggled. Continue?",
            Visible = true,
            Order = 2
        },
        new()
        {
            Id = "test-connectivity",
            Name = "Test connectivity",
            Description = "Tests network connectivity to common endpoints.",
            Category = FixCategory.Network,
            Command = @"Write-Host 'Testing network connectivity...' -ForegroundColor Cyan; Write-Host ''; $endpoints = @(@{Name='Google DNS';Target='8.8.8.8'}, @{Name='Microsoft';Target='microsoft.com'}, @{Name='Azure AD';Target='login.microsoftonline.com'}, @{Name='Office 365';Target='outlook.office365.com'}); foreach ($ep in $endpoints) { Write-Host ('Testing ' + $ep.Name + ' (' + $ep.Target + ')... ') -NoNewline; try { $ping = Test-Connection $ep.Target -Count 1 -ErrorAction Stop; Write-Host ('OK - ' + $ping.ResponseTime + 'ms') -ForegroundColor Green } catch { Write-Host 'FAILED' -ForegroundColor Red } }; Write-Host ''; Write-Host 'Connectivity test complete.' -ForegroundColor Cyan",
            ConfirmText = null,
            Visible = true,
            Order = 3
        },

        // Windows (User-level)
        new()
        {
            Id = "wsreset",
            Name = "Reset Microsoft Store",
            Description = "Runs wsreset to clear Store cache.",
            Category = FixCategory.Windows,
            Command = "Start-Process wsreset.exe",
            ConfirmText = "Microsoft Store cache will be reset. Continue?",
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "restart-explorer",
            Name = "Restart Explorer",
            Description = "Restarts Windows Explorer (taskbar, desktop).",
            Category = FixCategory.Windows,
            Command = "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; Start-Process explorer.exe",
            ConfirmText = "Explorer will restart (taskbar will disappear briefly). Continue?",
            Visible = true,
            Order = 2
        },
        new()
        {
            Id = "clear-temp-files",
            Name = "Clear temp files",
            Description = "Removes temporary files from user profile.",
            Category = FixCategory.Windows,
            Command = @"Write-Host 'Clearing temporary files...' -ForegroundColor Cyan; $paths = @($env:TEMP, ""$env:LOCALAPPDATA\Temp""); $freed = 0; $fileCount = 0; foreach ($p in $paths) { if (Test-Path $p) { Write-Host ""Cleaning: $p""; $files = Get-ChildItem $p -Recurse -ErrorAction SilentlyContinue; $freed += ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum; $fileCount += $files.Count; $files | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } }; Write-Host ''; Write-Host ""Cleanup complete!"" -ForegroundColor Green; Write-Host ""Files processed: $fileCount""; Write-Host ""Space freed: $([math]::Round($freed/1MB, 2)) MB""",
            ConfirmText = "Temporary files will be deleted. Continue?",
            Visible = true,
            Order = 3
        },

        // Support
        new()
        {
            Id = "collect-logs",
            Name = "Collect basic logs",
            Description = "Gather networking, device, and Azure AD status to a zip on Desktop.",
            Category = FixCategory.Support,
            Command = @"$outDir = Join-Path $env:TEMP 'InfoBoxLogs'; New-Item -ItemType Directory -Force -Path $outDir | Out-Null; ipconfig /all > (Join-Path $outDir 'ipconfig.txt'); dsregcmd /status > (Join-Path $outDir 'dsregcmd.txt') 2>&1; Get-NetIPConfiguration | Out-File (Join-Path $outDir 'netconfig.txt'); Get-NetAdapter | Out-File (Join-Path $outDir 'adapters.txt'); systeminfo > (Join-Path $outDir 'systeminfo.txt') 2>&1; Get-Date | Out-File (Join-Path $outDir 'stamp.txt'); $zip = Join-Path ([Environment]::GetFolderPath('Desktop')) ('InfoBoxLogs_' + $env:COMPUTERNAME + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.zip'); Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -Force; Set-Clipboard -Value $zip; Start-Process explorer.exe -ArgumentList '/select,', $zip",
            ConfirmText = null,
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "email-logs",
            Name = "Collect & email logs",
            Description = "Collects logs to Desktop, copies path, and opens email to support.",
            Category = FixCategory.Support,
            Command = @"$outDir = Join-Path $env:TEMP 'InfoBoxLogs'; New-Item -ItemType Directory -Force -Path $outDir | Out-Null; ipconfig /all > (Join-Path $outDir 'ipconfig.txt'); dsregcmd /status > (Join-Path $outDir 'dsregcmd.txt') 2>&1; Get-NetIPConfiguration | Out-File (Join-Path $outDir 'netconfig.txt'); Get-NetAdapter | Out-File (Join-Path $outDir 'adapters.txt'); systeminfo > (Join-Path $outDir 'systeminfo.txt') 2>&1; Get-Date | Out-File (Join-Path $outDir 'stamp.txt'); $zip = Join-Path ([Environment]::GetFolderPath('Desktop')) ('InfoBoxLogs_' + $env:COMPUTERNAME + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.zip'); Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -Force; Set-Clipboard -Value $zip; Start-Process explorer.exe -ArgumentList '/select,', $zip; Start-Process ('mailto:{{SUPPORT_EMAIL}}?subject=InfoBox%20logs%20for%20' + $env:COMPUTERNAME + '&body=Please%20attach%20the%20zip%20file%20shown%20in%20Explorer%20(path%20copied%20to%20clipboard).')",
            ConfirmText = "Collect logs and open email? You'll need to attach the file manually.",
            Visible = true,
            Order = 2
        },

        // ═══════════════════════════════════════════════════════════════════════
        // ADMIN-LEVEL ACTIONS (requires UAC elevation)
        // ═══════════════════════════════════════════════════════════════════════

        // Network (Admin)
        new()
        {
            Id = "flush-dns",
            Name = "Flush DNS cache",
            Description = "Clears the DNS resolver cache (requires admin).",
            Category = FixCategory.Network,
            Command = "Write-Host 'Flushing DNS cache...' -ForegroundColor Cyan; ipconfig /flushdns; Write-Host ''; Write-Host 'DNS cache flushed successfully.' -ForegroundColor Green",
            ConfirmText = "DNS cache will be flushed. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 10
        },
        new()
        {
            Id = "reset-winsock",
            Name = "Reset Winsock",
            Description = "Resets Winsock catalog (may require restart).",
            Category = FixCategory.Network,
            Command = "Write-Host 'Resetting Winsock catalog...' -ForegroundColor Cyan; netsh winsock reset; Write-Host ''; Write-Host 'Winsock reset complete.' -ForegroundColor Green; Write-Host 'Please restart your computer to apply changes.' -ForegroundColor Yellow",
            ConfirmText = "Winsock will be reset. A computer restart may be required. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 11
        },
        new()
        {
            Id = "reset-network-stack",
            Name = "Reset network stack",
            Description = "Full TCP/IP and Winsock reset (requires restart).",
            Category = FixCategory.Network,
            Command = "Write-Host 'Resetting network stack...' -ForegroundColor Cyan; Write-Host 'Step 1: Winsock reset'; netsh winsock reset; Write-Host 'Step 2: TCP/IP reset'; netsh int ip reset; Write-Host ''; Write-Host 'Network stack reset complete.' -ForegroundColor Green; Write-Host 'You MUST restart your computer to apply changes.' -ForegroundColor Yellow",
            ConfirmText = "Network stack will be reset. You MUST restart your computer after. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 12
        },

        // Printing (Admin)
        new()
        {
            Id = "restart-print-spooler",
            Name = "Restart Print Spooler",
            Description = "Restarts the Windows Print Spooler service.",
            Category = FixCategory.Printing,
            Command = "Write-Host 'Restarting Print Spooler service...' -ForegroundColor Cyan; Restart-Service -Name Spooler -Force; Write-Host ''; Write-Host 'Print Spooler restarted successfully.' -ForegroundColor Green",
            ConfirmText = "Print Spooler service will be restarted. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 1
        },
        new()
        {
            Id = "clear-print-queue",
            Name = "Clear print queue",
            Description = "Clears all stuck print jobs.",
            Category = FixCategory.Printing,
            Command = @"Write-Host 'Clearing print queue...' -ForegroundColor Cyan; Write-Host 'Stopping Print Spooler...'; Stop-Service -Name Spooler -Force; Start-Sleep -Seconds 2; Write-Host 'Removing queued jobs...'; Remove-Item -Path ""$env:SystemRoot\System32\spool\PRINTERS\*"" -Force -ErrorAction SilentlyContinue; Write-Host 'Starting Print Spooler...'; Start-Service -Name Spooler; Write-Host ''; Write-Host 'Print queue cleared successfully.' -ForegroundColor Green",
            ConfirmText = "All print jobs will be deleted. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 2
        },

        // Windows (Admin)
        new()
        {
            Id = "sfc-scan",
            Name = "System file check",
            Description = "Runs SFC /scannow to repair system files.",
            Category = FixCategory.Windows,
            Command = "Start-Process cmd.exe -ArgumentList '/k sfc /scannow' -Verb RunAs",
            ConfirmText = "This will scan and repair system files. It may take 10-30 minutes. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 10
        },
        new()
        {
            Id = "dism-repair",
            Name = "DISM repair",
            Description = "Repairs Windows image using DISM.",
            Category = FixCategory.Windows,
            Command = "Start-Process cmd.exe -ArgumentList '/k DISM /Online /Cleanup-Image /RestoreHealth' -Verb RunAs",
            ConfirmText = "This will repair the Windows image. It may take 15-45 minutes. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 11
        },
        new()
        {
            Id = "check-windows-update",
            Name = "Check Windows Update",
            Description = "Opens Windows Update settings to check for updates.",
            Category = FixCategory.Windows,
            Command = "Start-Process 'ms-settings:windowsupdate-action'",
            ConfirmText = null,
            RequiresAdmin = false,
            Visible = true,
            Order = 12
        },
        new()
        {
            Id = "gpupdate",
            Name = "Refresh Group Policy",
            Description = "Forces a Group Policy refresh.",
            Category = FixCategory.Windows,
            Command = "Write-Host 'Refreshing Group Policy...' -ForegroundColor Cyan; Write-Host ''; gpupdate /force; Write-Host ''; Write-Host 'Group Policy refresh complete.' -ForegroundColor Green",
            ConfirmText = "Group Policy will be refreshed. Continue?",
            RequiresAdmin = true,
            Visible = true,
            Order = 13
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
                    Category = ov.Category != FixCategory.Custom ? ov.Category : built.Category,
                    RequiresAdmin = ov.RequiresAdmin || built.RequiresAdmin
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
