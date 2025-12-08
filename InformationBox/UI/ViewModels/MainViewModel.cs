using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.Config.Fixes;
using InformationBox.UI.Commands;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Primary view model backing the main window.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _selectedTheme = "Light";
    private readonly UserSettings _userSettings;
    private bool _isRefreshing;
    private NetworkStatus _networkStatus;
    private DateTime? _lastUpdated;
    private bool _isUsingCachedData;
    private TenantContext? _currentTenant;

    // Troubleshoot tab state
    private FixAction? _selectedFix;
    private bool _isFixRunning;
    private string _fixOutput = string.Empty;
    private bool _fixSuccess;
    private CancellationTokenSource? _fixCancellation;
    private string _fixSearchText = string.Empty;

    /// <summary>
    /// Initializes the view model using the provided configuration and source metadata.
    /// </summary>
    /// <param name="config">Effective configuration for the current tenant.</param>
    /// <param name="source">Identifier describing where the config originated.</param>
    /// <param name="userSettings">User settings for persistence.</param>
    public MainViewModel(AppConfig config, string source, UserSettings userSettings)
    {
        Config = config;
        ConfigSource = source;
        _userSettings = userSettings;
        // Use user's saved preference, or "Auto" if in auto mode, or the current applied theme
        _selectedTheme = !string.IsNullOrWhiteSpace(userSettings.Theme)
            ? userSettings.Theme
            : (ThemeManager.IsAutoMode ? "Auto" : ThemeManager.CurrentTheme);
        ProductName = config.Branding.ProductName;
        CurrentZone = ResolveZone(config);
        Links = new ReadOnlyCollection<LinkEntry>(config.Links
            .Where(l => l.Visible)
            .OrderBy(l => l.Order)
            .ToArray());
        LocalSites = new ReadOnlyCollection<LocalSite>(config.LocalSites
            .Where(s => s.Visible && (string.IsNullOrWhiteSpace(s.Zone) || s.Zone.Equals(CurrentZone, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(s => s.Order)
            .ToArray());
        Contacts = new ReadOnlyCollection<ContactEntry>(config.Contacts.ToArray());
        Fixes = new ReadOnlyCollection<FixAction>(FixRegistry.BuildFixes(config.Fixes).ToList());
        PrimaryLink = Links.FirstOrDefault();
        _networkStatus = NetworkInfoProvider.GetCurrentStatus();
        InfoCard = new InfoCardViewModel(Environment.MachineName, null, null, TenantJoinType.Unknown, source);
        PasswordStatus = new PasswordStatusViewModel(null, null, null, false, false);
        LinkCommand = new RelayCommand<string>(OpenUrl);
        LocalSiteCommand = new RelayCommand<string>(OpenUrl);
        CopyTextCommand = new RelayCommand<string>(CopyToClipboard);
        OpenSettingsCommand = new RelayCommand<string>(OpenUrl);
        RunFixCommand = new RelayCommand<FixAction>(RunFix);
        RefreshCommand = new RelayCommand(Refresh, () => !IsRefreshing);

        // Troubleshoot commands
        RunSelectedFixCommand = new AsyncRelayCommand(RunSelectedFixAsync, () => SelectedFix != null && !IsFixRunning);
        CancelFixCommand = new RelayCommand(CancelFix, () => IsFixRunning);
        ToggleFixCommand = new RelayCommand(ToggleFix, () => SelectedFix != null || IsFixRunning);
        ClearOutputCommand = new RelayCommand(ClearOutput);
        SelectFixCommand = new RelayCommand<FixAction>(SelectFix);

        // Build grouped fixes for the troubleshoot tab
        FixesByCategory = BuildFixesByCategory();
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
    /// Gets the resolved zone (from domain→zone mappings).
    /// </summary>
    public string CurrentZone { get; }

    /// <summary>
    /// True when a zone mapping was found.
    /// </summary>
    public bool HasZone => !string.Equals(CurrentZone, "Unknown", StringComparison.OrdinalIgnoreCase);

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
    /// Refreshes network and system information.
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// Gets whether a refresh operation is in progress.
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (_isRefreshing != value)
            {
                _isRefreshing = value;
                OnPropertyChanged();
            }
        }
    }

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
    /// Gets the current network status.
    /// </summary>
    public NetworkStatus NetworkStatus
    {
        get => _networkStatus;
        private set
        {
            _networkStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrimaryIpv4));
        }
    }

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
    /// Gets the timestamp when data was last refreshed.
    /// </summary>
    public DateTime? LastUpdated
    {
        get => _lastUpdated;
        private set
        {
            _lastUpdated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastUpdatedText));
        }
    }

    /// <summary>
    /// Gets a human-readable string for when data was last updated.
    /// </summary>
    public string LastUpdatedText
    {
        get
        {
            if (_lastUpdated == null) return string.Empty;
            var prefix = _isUsingCachedData ? "Cached: " : "Updated: ";
            return prefix + _lastUpdated.Value.ToString("g");
        }
    }

    /// <summary>
    /// Gets whether the current data is from cache (offline mode).
    /// </summary>
    public bool IsUsingCachedData
    {
        get => _isUsingCachedData;
        private set
        {
            _isUsingCachedData = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastUpdatedText));
        }
    }

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
    /// Shows when both identity and password data are missing (likely offline).
    /// </summary>
    public bool ShowOfflineBanner => ShowIdentityUnavailable && ShowPasswordUnavailable;

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
    /// Gets a value indicating whether the troubleshoot tab should be rendered.
    /// </summary>
    public bool ShowTroubleshootTab => Fixes.Count > 0;

    /// <summary>
    /// Gets or sets the search text for filtering troubleshoot actions.
    /// </summary>
    public string FixSearchText
    {
        get => _fixSearchText;
        set
        {
            if (_fixSearchText != value)
            {
                _fixSearchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredFixesByCategory));
            }
        }
    }

    /// <summary>
    /// Gets the fixes organized by category for the troubleshoot tab.
    /// </summary>
    public IReadOnlyList<FixCategoryGroup> FixesByCategory { get; }

    /// <summary>
    /// Gets the filtered fixes based on search text.
    /// </summary>
    public IReadOnlyList<FixCategoryGroup> FilteredFixesByCategory
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_fixSearchText))
                return FixesByCategory;

            return FixesByCategory
                .Select(g => new FixCategoryGroup(
                    g.Name,
                    g.Actions.Where(a =>
                        (!string.IsNullOrWhiteSpace(a.Name) && a.Name.Contains(_fixSearchText, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(a.Description) && a.Description.Contains(_fixSearchText, StringComparison.OrdinalIgnoreCase)))
                    .ToArray()))
                .Where(g => g.Actions.Count > 0)
                .ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the currently selected fix action.
    /// </summary>
    public FixAction? SelectedFix
    {
        get => _selectedFix;
        set
        {
            if (_selectedFix != value)
            {
                _selectedFix = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedFix));
                OnPropertyChanged(nameof(SelectedFixAdminText));
                OnPropertyChanged(nameof(ShowFixAdminIcon));
            }
        }
    }

    /// <summary>
    /// Gets whether a fix is currently selected.
    /// </summary>
    public bool HasSelectedFix => _selectedFix != null;

    /// <summary>
    /// Gets text indicating if the selected fix requires admin.
    /// </summary>
    public string SelectedFixAdminText => _selectedFix?.RequiresAdmin == true ? "Requires administrator" : "";

    /// <summary>
    /// Gets whether a fix is currently running.
    /// </summary>
    public bool IsFixRunning
    {
        get => _isFixRunning;
        private set
        {
            if (_isFixRunning != value)
            {
                _isFixRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRunFix));
                OnPropertyChanged(nameof(FixButtonText));
                OnPropertyChanged(nameof(ShowFixAdminIcon));
            }
        }
    }

    /// <summary>
    /// Gets whether a fix can be run (selected and not running).
    /// </summary>
    public bool CanRunFix => _selectedFix != null && !_isFixRunning;

    /// <summary>
    /// Gets the output from the last/current fix execution.
    /// </summary>
    public string FixOutput
    {
        get => _fixOutput;
        private set
        {
            _fixOutput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFixOutput));
        }
    }

    /// <summary>
    /// Gets whether there is any fix output to display.
    /// </summary>
    public bool HasFixOutput => !string.IsNullOrWhiteSpace(_fixOutput);

    /// <summary>
    /// Gets whether the last fix execution was successful.
    /// </summary>
    public bool FixSuccess
    {
        get => _fixSuccess;
        private set
        {
            _fixSuccess = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Command to run the currently selected fix.
    /// </summary>
    public ICommand RunSelectedFixCommand { get; }

    /// <summary>
    /// Command to cancel a running fix.
    /// </summary>
    public ICommand CancelFixCommand { get; }

    /// <summary>
    /// Command to toggle between run and cancel (single button).
    /// </summary>
    public ICommand ToggleFixCommand { get; }

    /// <summary>
    /// Gets the text for the toggle fix button based on current state.
    /// </summary>
    public string FixButtonText => IsFixRunning ? "Cancel" : "Run";

    /// <summary>
    /// Gets whether to show the admin shield icon (only when not running and action requires admin).
    /// </summary>
    public bool ShowFixAdminIcon => !IsFixRunning && (_selectedFix?.RequiresAdmin ?? false);

    /// <summary>
    /// Command to clear the output panel.
    /// </summary>
    public ICommand ClearOutputCommand { get; }

    /// <summary>
    /// Command to select a fix action.
    /// </summary>
    public ICommand SelectFixCommand { get; }

    /// <summary>
    /// Gets the client ID from config for convenience bindings.
    /// </summary>
    public string ClientId => Config.Auth.ClientId;

    /// <summary>
    /// Gets the list of available themes.
    /// </summary>
    public IReadOnlyList<string> AvailableThemes => ThemeManager.AvailableThemes;

    /// <summary>
    /// Gets or sets the currently selected theme.
    /// </summary>
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme != value && value != null)
            {
                _selectedTheme = value;
                ThemeManager.ApplyTheme(value);
                _userSettings.Theme = value;
                _userSettings.Save();

                // Enable/disable auto-switching based on selection
                if (string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    App.EnableAutoTheme();
                }
                else
                {
                    App.DisableAutoTheme();
                }

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Updates the theme from system preference without saving (for auto-switch).
    /// </summary>
    public void UpdateThemeFromSystem(string theme)
    {
        if (_selectedTheme != theme)
        {
            _selectedTheme = theme;
            OnPropertyChanged(nameof(SelectedTheme));
        }
    }

    /// <summary>
    /// Updates the tenant context, refreshing overview and network rows.
    /// </summary>
    /// <param name="context">Tenant information detected at runtime.</param>
    public void UpdateTenant(TenantContext context)
    {
        _currentTenant = context;
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
    /// Gets the logo path or URL configured in branding.
    /// </summary>
    public string? LogoSource => Config.Branding.Logo;

    /// <summary>
    /// Gets whether a logo is configured.
    /// </summary>
    public bool HasLogo => !string.IsNullOrWhiteSpace(Config.Branding.Logo);

    /// <summary>
    /// Gets the logo width (0 = auto).
    /// </summary>
    public double LogoWidth => Config.Branding.LogoWidth > 0 ? Config.Branding.LogoWidth : double.NaN;

    /// <summary>
    /// Gets the logo height.
    /// </summary>
    public double LogoHeight => Config.Branding.LogoHeight > 0 ? Config.Branding.LogoHeight : 32;

    /// <summary>
    /// Gets the company name for branding.
    /// </summary>
    public string CompanyName => Config.Branding.CompanyName;

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
        OnPropertyChanged(nameof(ShowOfflineBanner));
    }

    private void Refresh()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;

        try
        {
            // Refresh network status
            NetworkStatus = NetworkInfoProvider.GetCurrentStatus();
            NetworkRows = BuildNetwork(NetworkStatus);
            StatusNetworkRows = BuildStatusNetworkRows(NetworkStatus);

            OnPropertyChanged(nameof(NetworkRows));
            OnPropertyChanged(nameof(StatusNetworkRows));

            // Update timestamp and save to cache
            MarkAsLiveData();
            _ = SaveToCacheAsync();

            Logger.Info("Data refreshed successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Refresh failed: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void RunFix(FixAction? action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.Command))
        {
            return;
        }

        try
        {
            // Build confirmation message (include admin warning if needed)
            var confirmMessage = action.ConfirmText;
            if (action.RequiresAdmin && !string.IsNullOrWhiteSpace(confirmMessage))
            {
                confirmMessage = $"⚠️ This action requires administrator privileges.\n\n{confirmMessage}";
            }
            else if (action.RequiresAdmin)
            {
                confirmMessage = "⚠️ This action requires administrator privileges. Continue?";
            }

            if (!string.IsNullOrWhiteSpace(confirmMessage))
            {
                var icon = action.RequiresAdmin ? MessageBoxImage.Warning : MessageBoxImage.Question;
                var result = MessageBox.Show(confirmMessage, action.Name, MessageBoxButton.OKCancel, icon);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }

            // Replace placeholders with config values
            var command = ReplacePlaceholders(action.Command);

            if (action.RequiresAdmin)
            {
                // Run with UAC elevation using encoded command to prevent injection
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe")
                {
                    Arguments = BuildEncodedArguments(command),
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try
                {
                    System.Diagnostics.Process.Start(psi);
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // User cancelled UAC prompt - silently ignore
                    Logger.Info($"Fix action '{action.Name}' cancelled by user (UAC declined)");
                }
            }
            else
            {
                // Run without elevation (hidden window) using encoded command to avoid shell parsing issues
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", BuildEncodedArguments($"try {{ {command} }} catch {{ }}", hidden: true))
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
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

    private static string ResolveZone(AppConfig config)
    {
        var domain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
        if (string.IsNullOrWhiteSpace(domain))
        {
            return "Unknown";
        }

        var match = config.Zones.FirstOrDefault(z => domain.Equals(z.Domain, StringComparison.OrdinalIgnoreCase));
        return match?.Zone ?? "Unknown";
    }

    private IReadOnlyList<FixCategoryGroup> BuildFixesByCategory()
    {
        return Fixes
            .GroupBy(f => f.Category)
            .Select(g => new FixCategoryGroup(GetCategoryDisplayName(g.Key), g.ToArray()))
            .ToArray();
    }

    private static string GetCategoryDisplayName(FixCategory category) => category switch
    {
        FixCategory.OneDrive => "OneDrive",
        FixCategory.Teams => "Microsoft Teams",
        FixCategory.Browser => "Browser",
        FixCategory.Office => "Microsoft Office",
        FixCategory.Network => "Network",
        FixCategory.Printing => "Printing",
        FixCategory.Windows => "Windows",
        FixCategory.Support => "Support & Logs",
        FixCategory.Custom => "Other",
        _ => category.ToString()
    };

    private void SelectFix(FixAction? fix)
    {
        SelectedFix = fix;
    }

    private async Task RunSelectedFixAsync()
    {
        if (_selectedFix == null || _isFixRunning)
            return;

        var action = _selectedFix;

        // Show confirmation if needed
        if (!string.IsNullOrWhiteSpace(action.ConfirmText))
        {
            var confirmMessage = action.RequiresAdmin
                ? $"This action requires administrator privileges.\n\n{action.ConfirmText}"
                : action.ConfirmText;

            var icon = action.RequiresAdmin ? MessageBoxImage.Warning : MessageBoxImage.Question;
            var result = MessageBox.Show(confirmMessage, action.Name, MessageBoxButton.OKCancel, icon);
            if (result != MessageBoxResult.OK)
            {
                return;
            }
        }

        IsFixRunning = true;
        FixOutput = $"Running: {action.Name}...\n\n";
        FixSuccess = false;

        _fixCancellation = new CancellationTokenSource();

        try
        {
            // Replace placeholders
            var command = ReplacePlaceholders(action.Command);

            CommandResult result;

            if (action.RequiresAdmin)
            {
                FixOutput += "[Elevated] Requesting administrator privileges...\n";
                result = await CommandRunner.RunAsAdminAsync(command).ConfigureAwait(false);
            }
            else
            {
                result = await CommandRunner.RunAsync(
                    command,
                    line => Application.Current.Dispatcher.Invoke(() =>
                    {
                        FixOutput += line + "\n";
                    }),
                    _fixCancellation.Token).ConfigureAwait(false);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                FixSuccess = result.Success;

                var output = new StringBuilder();
                output.AppendLine($"\n{'─'.ToString().PadRight(50, '─')}");
                output.AppendLine($"Status: {(result.Success ? "Success" : "Failed")}");
                output.AppendLine($"Exit code: {result.ExitCode}");
                output.AppendLine($"Duration: {result.Duration.TotalSeconds:F1}s");

                if (!string.IsNullOrWhiteSpace(result.Output) && !_fixOutput.Contains(result.Output))
                {
                    output.AppendLine($"\nOutput:\n{result.Output}");
                }

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    output.AppendLine($"\nError:\n{result.Error}");
                }

                FixOutput += output.ToString();
            });

            Logger.Info($"Fix '{action.Name}' completed: success={result.Success}, exitCode={result.ExitCode}");
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FixOutput += $"\n\nException: {ex.Message}";
                FixSuccess = false;
            });
            Logger.Error($"Fix '{action.Name}' failed", ex);
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsFixRunning = false;
            });
            _fixCancellation?.Dispose();
            _fixCancellation = null;
        }
    }

    private void CancelFix()
    {
        if (_fixCancellation != null && !_fixCancellation.IsCancellationRequested)
        {
            _fixCancellation.Cancel();
            FixOutput += "\n\nCancelling...";
        }
    }

    private void ToggleFix()
    {
        if (IsFixRunning)
        {
            CancelFix();
        }
        else if (SelectedFix != null)
        {
            // Fire and forget with fault logging to avoid unobserved exceptions
            SafeFireAndForget(RunSelectedFixAsync());
        }
    }

    private void ClearOutput()
    {
        FixOutput = string.Empty;
        FixSuccess = false;
    }

    // Wraps PowerShell commands in -EncodedCommand and normalizes env vars to avoid injection and env tampering.
    private static string BuildEncodedArguments(string script, bool hidden = false)
    {
        var normalized = AddSafeEnvPreamble(script);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(normalized));
        var window = hidden ? "-WindowStyle Hidden " : string.Empty;
        return $"-NoLogo -NoProfile {window}-ExecutionPolicy Bypass -EncodedCommand {encoded}";
    }

    // Captures background task faults to log instead of crashing on unobserved exceptions.
    private static void SafeFireAndForget(Task task)
    {
        task.ContinueWith(
            t => Logger.Error("Background fix execution failed", t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Replaces templated placeholders with PowerShell-safe single-quoted literals.
    /// </summary>
    private string ReplacePlaceholders(string command)
    {
        string Sq(string value)
        {
            var safe = (value ?? string.Empty).Replace("'", "''");
            return $"'{safe}'";
        }

        return command
            .Replace("{{SUPPORT_EMAIL}}", Sq(Config.Branding.SupportEmail))
            .Replace("{{COMPANY_NAME}}", Sq(Config.Branding.CompanyName))
            .Replace("{{PRODUCT_NAME}}", Sq(Config.Branding.ProductName));
    }

    private static string AddSafeEnvPreamble(string script)
    {
        static string Sq(string value) => value.Replace("'", "''");

        var localAppData = Sq(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var appData = Sq(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        var temp = Sq(System.IO.Path.GetTempPath());
        var systemRoot = Sq(Environment.GetFolderPath(Environment.SpecialFolder.Windows) ??
                           Environment.GetEnvironmentVariable("SystemRoot") ??
                           "C:\\Windows");

        return $"$env:LOCALAPPDATA='{localAppData}';$env:APPDATA='{appData}';$env:TEMP='{temp}';$env:SystemRoot='{systemRoot}';{script}";
    }

    /// <summary>
    /// Loads cached data and applies it to the view model.
    /// Returns true if cache was loaded successfully.
    /// </summary>
    public async Task<bool> LoadFromCacheAsync()
    {
        var cache = await CacheService.LoadAsync();
        if (cache == null)
        {
            return false;
        }

        IsUsingCachedData = true;
        LastUpdated = cache.LastUpdated;

        // Apply cached password status
        if (cache.PasswordStatus != null)
        {
            DateTimeOffset? lastChanged = cache.PasswordStatus.LastChangedUtc.HasValue
                ? new DateTimeOffset(cache.PasswordStatus.LastChangedUtc.Value)
                : null;
            var status = new PasswordAgeResult(
                lastChanged,
                cache.PasswordStatus.PolicyDays,
                cache.PasswordStatus.DaysLeft,
                cache.PasswordStatus.NeverExpires);
            UpdatePasswordStatus(status);
        }

        // Apply cached identity
        if (cache.Identity != null)
        {
            var identity = new UserIdentity(
                cache.Identity.DisplayName ?? "Unknown",
                cache.Identity.Upn,
                cache.Identity.Email,
                Array.Empty<string>(),
                null, null, null, null,
                Array.Empty<string>(),
                IsGraphBacked: false);
            UpdateIdentity(identity);
        }

        // Apply cached tenant
        if (cache.Tenant != null)
        {
            var joinType = cache.Tenant.JoinType switch
            {
                "AzureAdJoined" => TenantJoinType.AzureAdJoined,
                "HybridAzureAdJoined" => TenantJoinType.HybridAzureAdJoined,
                "DomainJoined" => TenantJoinType.DomainJoined,
                "WorkplaceJoined" => TenantJoinType.WorkplaceJoined,
                "Workgroup" => TenantJoinType.Workgroup,
                _ => TenantJoinType.Unknown
            };
            var tenant = new TenantContext(
                cache.Tenant.TenantId,
                cache.Tenant.TenantName,
                cache.Tenant.DomainName,
                joinType,
                cache.Tenant.AzureAdJoined,
                joinType == TenantJoinType.WorkplaceJoined,
                joinType == TenantJoinType.DomainJoined || joinType == TenantJoinType.HybridAzureAdJoined);
            UpdateTenant(tenant);
        }

        // Apply cached network
        if (cache.Network != null)
        {
            // Network is refreshed live, but we can show cached as fallback
            Logger.Info("Cache loaded with network data from last session");
        }

        Logger.Info($"Cache applied (from {cache.LastUpdated:g})");
        return true;
    }

    /// <summary>
    /// Saves current state to cache for offline access.
    /// </summary>
    public async Task SaveToCacheAsync()
    {
        var cache = new CachedData
        {
            LastUpdated = DateTime.UtcNow
        };

        // Cache password status
        if (PasswordStatus.IsValid || PasswordStatus.NeverExpires)
        {
            cache.PasswordStatus = new CachedPasswordStatus
            {
                DaysLeft = PasswordStatus.DaysLeft,
                PolicyDays = PasswordStatus.PolicyDays,
                LastChangedUtc = PasswordStatus.LastChangedUtc?.UtcDateTime,
                NeverExpires = PasswordStatus.NeverExpires
            };
        }

        // Cache identity from current rows
        if (IdentityRows.Count > 0)
        {
            cache.Identity = new CachedIdentity
            {
                DisplayName = IdentityRows.FirstOrDefault(r => r.Label == "Display name")?.Value,
                Upn = IdentityRows.FirstOrDefault(r => r.Label == "UPN")?.Value,
                Email = IdentityRows.FirstOrDefault(r => r.Label == "Email")?.Value
            };
        }

        // Cache tenant
        if (_currentTenant != null)
        {
            cache.Tenant = new CachedTenant
            {
                TenantId = _currentTenant.TenantId,
                TenantName = _currentTenant.TenantName,
                DomainName = _currentTenant.DomainName,
                JoinType = _currentTenant.JoinType.ToString(),
                AzureAdJoined = _currentTenant.AzureAdJoined
            };
        }

        // Cache network
        cache.Network = new CachedNetwork
        {
            ComputerName = Environment.MachineName,
            IpAddress = NetworkStatus.Ipv4Address,
            AdapterName = NetworkStatus.AdapterName
        };

        await CacheService.SaveAsync(cache);
        LastUpdated = cache.LastUpdated;
        IsUsingCachedData = false;
    }

    /// <summary>
    /// Marks data as live (not cached) and updates the timestamp.
    /// </summary>
    public void MarkAsLiveData()
    {
        IsUsingCachedData = false;
        LastUpdated = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
