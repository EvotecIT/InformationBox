using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.Config.Fixes;
using InformationBox.UI.Commands;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Primary view model backing the main window.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Initializes the view model using the provided configuration and source metadata.
    /// </summary>
    /// <param name="config">Effective configuration for the current tenant.</param>
    /// <param name="source">Identifier describing where the config originated.</param>
    public MainViewModel(AppConfig config, string source)
    {
        Config = config;
        ConfigSource = source;
        ProductName = config.Branding.ProductName;
        Links = new ReadOnlyCollection<LinkEntry>(config.Links
            .Where(l => l.Visible)
            .OrderBy(l => l.Order)
            .ToArray());
        LocalSites = new ReadOnlyCollection<LocalSite>(config.LocalSites
            .Where(s => s.Visible)
            .OrderBy(s => s.Order)
            .ToArray());
        Contacts = new ReadOnlyCollection<ContactEntry>(config.Contacts.ToArray());
        Fixes = new ReadOnlyCollection<FixAction>(FixRegistry.BuildFixes(config.Fixes).ToList());
        PrimaryLink = Links.FirstOrDefault();
        NetworkStatus = NetworkInfoProvider.GetCurrentStatus();
        InfoCard = new InfoCardViewModel(Environment.MachineName, null, null, TenantJoinType.Unknown, source);
        PasswordStatus = new PasswordStatusViewModel(null, null, null, false, false);
        LinkCommand = new RelayCommand<string>(OpenUrl);
        LocalSiteCommand = new RelayCommand<string>(OpenUrl);
        CopyTextCommand = new RelayCommand<string>(CopyToClipboard);
        OpenSettingsCommand = new RelayCommand<string>(OpenUrl);
        RunFixCommand = new RelayCommand<FixAction>(RunFix);
        PrimaryColor = config.Branding.PrimaryColor;
        OverviewRows = new ReadOnlyCollection<InfoRow>(Array.Empty<InfoRow>());
        IdentityRows = new ReadOnlyCollection<InfoRow>(Array.Empty<InfoRow>());
        NetworkRows = new ReadOnlyCollection<InfoRow>(Array.Empty<InfoRow>());
        StatusDeviceRows = new ReadOnlyCollection<InfoRow>(Array.Empty<InfoRow>());
        StatusNetworkRows = new ReadOnlyCollection<InfoRow>(Array.Empty<InfoRow>());
        UpdateIdentity(UserIdentity.FromEnvironment());
        PrimaryUpn = IdentityRows.FirstOrDefault(r => r.Label == "UPN")?.Value ?? Environment.UserName;
    }

    /// <summary>
    /// Gets the active configuration used by the UI.
    /// </summary>
    public AppConfig Config { get; }

    /// <summary>
    /// Gets the identifier representing which file or fallback provided the config.
    /// </summary>
    public string ConfigSource { get; }

    /// <summary>
    /// Gets the product name displayed in the window header.
    /// </summary>
    public string ProductName { get; }

    /// <summary>
    /// Gets the ordered set of quick links displayed in the Links section.
    /// </summary>
    public IReadOnlyList<LinkEntry> Links { get; }

    /// <summary>
    /// Gets the filtered list of local site resources.
    /// </summary>
    public IReadOnlyList<LocalSite> LocalSites { get; }

    /// <summary>
    /// Gets the contacts rendered in the contacts panel.
    /// </summary>
    public IReadOnlyList<ContactEntry> Contacts { get; }

    /// <summary>
    /// Gets the fix actions exposed in the Fix tab.
    /// </summary>
    public IReadOnlyList<FixAction> Fixes { get; }

    /// <summary>
    /// Gets the highlighted link surfaced in the header.
    /// </summary>
    public LinkEntry? PrimaryLink { get; }

    /// <summary>
    /// Gets a value indicating whether a primary action link is available.
    /// </summary>
    public bool HasPrimaryLink => PrimaryLink is not null;

    /// <summary>
    /// Gets the command invoked when a main link is clicked.
    /// </summary>
    public ICommand LinkCommand { get; }

    /// <summary>
    /// Gets the command invoked when a local site link is clicked.
    /// </summary>
    public ICommand LocalSiteCommand { get; }

    /// <summary>
    /// Copies text to clipboard.
    /// </summary>
    public ICommand CopyTextCommand { get; }

    /// <summary>
    /// Opens OS settings or URLs.
    /// </summary>
    public ICommand OpenSettingsCommand { get; }

    /// <summary>
    /// Runs a configured fix action.
    /// </summary>
    public ICommand RunFixCommand { get; }

    /// <summary>
    /// Gets the header card model summarizing the tenant/device.
    /// </summary>
    public InfoCardViewModel InfoCard { get; private set; }

    /// <summary>
    /// Gets the password status summary.
    /// </summary>
    public PasswordStatusViewModel PasswordStatus { get; private set; }

    /// <summary>
    /// Gets the condensed device rows shown on the Status tab.
    /// </summary>
    public IReadOnlyList<InfoRow> StatusDeviceRows { get; private set; }

    /// <summary>
    /// Gets the condensed network rows shown on the Status tab.
    /// </summary>
    public IReadOnlyList<InfoRow> StatusNetworkRows { get; private set; }

    /// <summary>
    /// Gets the rows shown in the device overview card.
    /// </summary>
    public IReadOnlyList<InfoRow> OverviewRows { get; private set; }

    /// <summary>
    /// Gets the identity detail rows.
    /// </summary>
    public IReadOnlyList<InfoRow> IdentityRows { get; private set; }

    /// <summary>
    /// Gets the network detail rows.
    /// </summary>
    public IReadOnlyList<InfoRow> NetworkRows { get; private set; }

    /// <summary>
    /// Gets the captured network status at load time.
    /// </summary>
    public NetworkStatus NetworkStatus { get; }

    /// <summary>
    /// Primary UPN/email used for copy actions.
    /// </summary>
    public string? PrimaryUpn { get; private set; }

    /// <summary>
    /// Primary IPv4 used for copy actions.
    /// </summary>
    public string PrimaryIpv4 => NetworkStatus.Ipv4Address ?? "Unknown";

    /// <summary>
    /// Gets a value indicating whether identity data came from Microsoft Graph.
    /// </summary>
    public bool IdentityFromGraph { get; private set; }

    /// <summary>
    /// Gets the label describing the identity source.
    /// </summary>
    public string IdentitySourceLabel => IdentityFromGraph ? "Account (Online)" : "Account (Local)";

    /// <summary>
    /// Gets the caption expanding on the identity source.
    /// </summary>
    public string IdentitySourceCaption => IdentityFromGraph ? "Synced from Microsoft Graph" : "Read from Windows session";

    /// <summary>
    /// Gets a value indicating whether the password health card should be displayed.
    /// </summary>
    public bool ShowPasswordHealth => Config.FeatureFlags.ShowHealth;

    /// <summary>
    /// Shows a banner when password data is unavailable.
    /// </summary>
    public bool ShowPasswordUnavailable => !(PasswordStatus.IsValid || PasswordStatus.NeverExpires);

    /// <summary>
    /// Shows a banner when identity rows are empty.
    /// </summary>
    public bool ShowIdentityUnavailable => IdentityRows.Count == 0;

    /// <summary>
    /// Gets a value indicating whether any links are available.
    /// </summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the Local Sites card should be shown.
    /// </summary>
    public bool HasLocalSites => Config.FeatureFlags.ShowLocalSites && LocalSites.Count > 0;

    /// <summary>
    /// Gets a value indicating whether contacts should be displayed.
    /// </summary>
    public bool HasContacts => Config.FeatureFlags.ShowContacts && Contacts.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the support tab should be rendered.
    /// </summary>
    public bool ShowSupportTab => HasLinks || HasLocalSites || HasContacts;

    /// <summary>
    /// Gets a value indicating whether the fix tab should be rendered.
    /// </summary>
    public bool ShowFixTab => Fixes.Count > 0;

    /// <summary>
    /// Gets the client ID from config for convenience bindings.
    /// </summary>
    public string ClientId => Config.Auth.ClientId;

    /// <summary>
    /// Updates the tenant context, refreshing overview and network rows.
    /// </summary>
    /// <param name="context">Tenant information detected at runtime.</param>
    public void UpdateTenant(TenantContext context)
    {
        InfoCard = new InfoCardViewModel(Environment.MachineName, context.TenantId, context.TenantName, context.JoinType, ConfigSource);
        OverviewRows = BuildOverview(context);
        NetworkRows = BuildNetwork(NetworkStatus);
        StatusDeviceRows = BuildStatusDeviceRows(context);
        StatusNetworkRows = BuildStatusNetworkRows(NetworkStatus);
        OnPropertyChanged(nameof(InfoCard));
        OnPropertyChanged(nameof(OverviewRows));
        OnPropertyChanged(nameof(NetworkRows));
        OnPropertyChanged(nameof(StatusDeviceRows));
        OnPropertyChanged(nameof(StatusNetworkRows));
        OnPropertyChanged(nameof(PrimaryIpv4));
    }

    /// <summary>
    /// Applies the latest password age data to the UI.
    /// </summary>
    /// <param name="status">Password age calculation result.</param>
    public void UpdatePasswordStatus(PasswordAgeResult status)
    {
        PasswordStatus = PasswordStatusViewModel.From(status);
        OnPropertyChanged(nameof(PasswordStatus));
        OnPropertyChanged(nameof(ShowPasswordUnavailable));
    }

    /// <summary>
    /// Gets the primary brand color applied to accent UI.
    /// </summary>
    public string PrimaryColor { get; }

    /// <summary>
    /// Updates the identity rows from either Graph or environment data.
    /// </summary>
    /// <param name="identity">Identity payload to surface.</param>
    public void UpdateIdentity(UserIdentity identity)
    {
        IdentityFromGraph = identity.IsGraphBacked;
        IdentityRows = BuildIdentity(identity);
        PrimaryUpn = identity.UserPrincipalName ?? identity.PrimaryEmail ?? Environment.UserName;
        OnPropertyChanged(nameof(IdentityFromGraph));
        OnPropertyChanged(nameof(IdentitySourceLabel));
        OnPropertyChanged(nameof(IdentitySourceCaption));
        OnPropertyChanged(nameof(IdentityRows));
        OnPropertyChanged(nameof(PrimaryUpn));
        OnPropertyChanged(nameof(ShowIdentityUnavailable));
    }

    private void RunFix(FixAction? action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.Command))
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(action.ConfirmText))
            {
                var result = MessageBox.Show(action.ConfirmText, action.Name, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }

            var cmd = $"try {{ {action.Command.Replace("\"", "\\\"")} }} catch {{ }}";
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoLogo -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{cmd}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Error($"Fix action '{action.Name}' failed to launch", ex);
            MessageBox.Show("Unable to launch this action. Please contact support.", "Action failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        UrlLauncher.Open(url);
    }

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            Logger.Error("Clipboard copy failed", ex);
        }
    }

    private static ReadOnlyCollection<InfoRow> BuildOverview(TenantContext ctx)
    {
        var rows = new List<InfoRow>
        {
            new("Machine", Environment.MachineName),
            new("User", Environment.UserName),
            new("Domain", Environment.UserDomainName),
            new("Join", DescribeJoinType(ctx.JoinType)),
            new("Tenant", ctx.TenantName ?? "Unknown"),
            new("TenantId", ctx.TenantId ?? "Unknown")
        };

        return new ReadOnlyCollection<InfoRow>(rows);
    }

    private static ReadOnlyCollection<InfoRow> BuildStatusDeviceRows(TenantContext ctx)
    {
        var rows = new List<InfoRow>
        {
            new("Machine", Environment.MachineName),
            new("User", Environment.UserName),
            new("Domain", Environment.UserDomainName)
        };

        if (!string.IsNullOrWhiteSpace(ctx.TenantName))
        {
            rows.Add(new("Tenant", ctx.TenantName));
        }

        return new ReadOnlyCollection<InfoRow>(rows);
    }

    private static ReadOnlyCollection<InfoRow> BuildStatusNetworkRows(NetworkStatus network)
    {
        var rows = new List<InfoRow>
        {
            new("Connection", network.ConnectionType),
            new("SSID", string.IsNullOrWhiteSpace(network.Ssid) ? "N/A" : network.Ssid),
            new("IPv4", network.Ipv4Address ?? "Unknown")
        };

        return new ReadOnlyCollection<InfoRow>(rows);
    }

    private static ReadOnlyCollection<InfoRow> BuildNetwork(NetworkStatus network)
    {
        var rows = new List<InfoRow>
        {
            new("Connection", network.ConnectionType),
            new("Adapter", network.AdapterName ?? "Unknown"),
            new("SSID", string.IsNullOrWhiteSpace(network.Ssid) ? "N/A" : network.Ssid),
            new("IPv4", network.Ipv4Address ?? "Unknown"),
            new("VPN", network.IsVpn ? "Active" : "No")
        };

        return new ReadOnlyCollection<InfoRow>(rows);
    }

    private static ReadOnlyCollection<InfoRow> BuildIdentity(UserIdentity identity)
    {
        var aliasText = identity.Aliases.Count switch
        {
            0 => "None",
            <= 4 => string.Join(Environment.NewLine, identity.Aliases),
            _ => string.Join(Environment.NewLine, identity.Aliases.Take(4)) + Environment.NewLine + $"(+{identity.Aliases.Count - 4} more)"
        };

        var phoneLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(identity.MobilePhone))
        {
            phoneLines.Add(identity.MobilePhone!);
        }
        if (identity.BusinessPhones.Count > 0)
        {
            phoneLines.AddRange(identity.BusinessPhones);
        }
        var phoneText = phoneLines.Count == 0 ? "None" : string.Join(Environment.NewLine, phoneLines);

        var rows = new List<InfoRow>
        {
            new("Display name", identity.DisplayName),
            new("UPN", identity.UserPrincipalName ?? "Unknown"),
            new("Email", identity.PrimaryEmail ?? "Unknown"),
            new("Job title", identity.JobTitle ?? "Not set"),
            new("Department", identity.Department ?? "Not set"),
            new("Office", identity.OfficeLocation ?? "Not set"),
            new("Phone", phoneText),
            new("Aliases", aliasText)
        };

        return new ReadOnlyCollection<InfoRow>(rows);
    }

    private static string DescribeJoinType(TenantJoinType joinType) => joinType switch
    {
        TenantJoinType.AzureAdJoined => "Azure AD joined",
        TenantJoinType.HybridAzureAdJoined => "Hybrid Azure AD joined",
        TenantJoinType.DomainJoined => "Domain joined",
        TenantJoinType.WorkplaceJoined => "Workplace joined",
        TenantJoinType.Workgroup => "Workgroup",
        _ => "Unknown"
    };


    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
