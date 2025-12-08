using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.UI.ViewModels;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace InformationBox;

// ============================================================================
// APPLICATION ENTRY POINT - STARTUP AND INITIALIZATION
// ============================================================================
//
// PURPOSE:
//   Main application bootstrapper that initializes configuration, authentication,
//   and the UI. This is the primary orchestrator for the application startup sequence.
//
// STARTUP FLOW:
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  1. OnStartup() - Main entry point                             │
//   │     - Subscribe to Windows theme change events                  │
//   │     - Load configuration (config.json from multiple locations)  │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  2. Detect device join state (TenantInfoProvider)              │
//   │     - Native API: NetGetAadJoinInformation / DsregGetJoinInfo  │
//   │     - Fallback: dsregcmd.exe /status parsing                   │
//   │     - Fallback: Registry keys                                  │
//   │     - Fallback: AD Domain detection                            │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  3. Merge configuration with tenant overrides                  │
//   │     - Base config + tenantOverrides[tenantId] merged           │
//   │     - User settings loaded (%LOCALAPPDATA%\settings.json)      │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  4. Apply theme and create main window                         │
//   │     - Theme priority: User setting > Config > Auto-detect      │
//   │     - Window placement based on layout config                  │
//   │     - System tray integration if enabled                       │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  5. Initialize password provider (async)                       │
//   │     - AAD joined + ClientId configured → GraphPasswordProvider │
//   │     - Domain joined only → LdapPasswordAgeProvider             │
//   │     - No join → No password detection                          │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  6. Fetch password status and update UI                        │
//   │     - Cache loaded first for instant display                   │
//   │     - Live data fetched in background                          │
//   │     - Cache updated on successful fetch                        │
//   └─────────────────────────────────────────────────────────────────┘
//
// AUTHENTICATION PROVIDER SELECTION:
//   The ChoosePasswordProviderAsync() method selects the appropriate provider:
//
//   ┌──────────────────────────────────┬────────────────────────────────────┐
//   │ Condition                        │ Provider Selected                  │
//   ├──────────────────────────────────┼────────────────────────────────────┤
//   │ AAD joined + ClientId configured │ GraphPasswordAgeProvider           │
//   │                                  │ (tries Graph, then LDAP fallback)  │
//   ├──────────────────────────────────┼────────────────────────────────────┤
//   │ AAD joined, no ClientId          │ LdapPasswordAgeProvider            │
//   ├──────────────────────────────────┼────────────────────────────────────┤
//   │ Domain joined only               │ LdapPasswordAgeProvider            │
//   ├──────────────────────────────────┼────────────────────────────────────┤
//   │ Not joined                       │ LdapPasswordAgeProvider (will fail)│
//   └──────────────────────────────────┴────────────────────────────────────┘
//
// GRAPH AUTHENTICATION FLOW:
//   When Graph is used, authentication happens via GraphClientFactory:
//
//   1. Create InteractiveBrowserCredential with Windows Account Manager (WAM)
//   2. For AAD-joined devices: Silent SSO via WAM (no prompt)
//   3. For other devices: Browser popup for interactive sign-in
//   4. Token cached by Azure.Identity for subsequent calls
//
// CONFIGURATION LOAD ORDER:
//   1. --config <path> (command line, future)
//   2. C:\ProgramData\InformationBox\config.json (machine-wide)
//   3. %APPDATA%\InformationBox\config.json (user-specific)
//   4. Assets/config.default.json (embedded defaults)
//
// USER SETTINGS:
//   Stored in %LOCALAPPDATA%\InformationBox\settings.json
//   Contains user preferences like theme selection.
//
// CACHING:
//   Password status is cached to provide instant display on startup.
//   Cache is refreshed when live data is successfully fetched.
//
// ============================================================================

/// <summary>
/// Application entry point and bootstrapper.
/// </summary>
/// <remarks>
/// <para><b>Entry points:</b></para>
/// <list type="bullet">
///   <item><see cref="OnStartup"/> - Main initialization sequence</item>
///   <item><see cref="ChoosePasswordProviderAsync"/> - Auth provider selection</item>
/// </list>
///
/// <para><b>Theme management:</b></para>
/// <list type="bullet">
///   <item><see cref="ResolveTheme"/> - Determines which theme to apply</item>
///   <item><see cref="OnUserPreferenceChanged"/> - Handles Windows theme changes</item>
///   <item><see cref="EnableAutoTheme"/>/<see cref="DisableAutoTheme"/> - Toggle auto-switch</item>
/// </list>
/// </remarks>
public partial class App : Application
{
    // Guard access to shared static state that can be touched from multiple threads (theme changes, tray).
    private static readonly object StateLock = new();
    private static bool _autoThemeEnabled;
    private static MainViewModel? _viewModel;
    private static TrayIconService? _trayIcon;

    /// <summary>
    /// Handles application startup by loading configuration, tenant state, and initializing the window.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Subscribe to Windows theme changes
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        var loader = new ConfigLoader(ConfigLoader.DefaultCandidatePaths());
        var loaded = await loader.LoadAsync();
        Logger.Info($"Config loaded from {loaded.Source}");

        TenantContext tenant;
        try
        {
            tenant = TenantInfoProvider.GetTenantContext();
            Logger.Info($"Tenant context: Id={tenant.TenantId ?? "<null>"} Name={tenant.TenantName ?? "<null>"} Join={tenant.JoinType}");
        }
        catch
        {
            Logger.Error("Tenant detection threw unexpectedly");
            tenant = TenantContext.Unknown;
        }

        var merged = ConfigMerger.Merge(loaded.Config, tenant.TenantId);

        // Load user settings (theme preference, etc.)
        var userSettings = UserSettings.Load();

        // Apply theme: prefer user setting, then config, then auto-detect from Windows
        var themeToApply = ResolveTheme(userSettings.Theme, merged.Branding.Theme);
        ThemeManager.ApplyTheme(themeToApply);
        Logger.Info($"Theme applied: {ThemeManager.CurrentTheme} (requested={themeToApply}, user={userSettings.Theme}, config={merged.Branding.Theme}, autoMode={ThemeManager.IsAutoMode})");

        // Enable auto-switch if user selected "Auto" or (no user preference and config is Auto)
        lock (StateLock)
        {
            _autoThemeEnabled = string.Equals(userSettings.Theme, "Auto", StringComparison.OrdinalIgnoreCase) ||
                               (string.IsNullOrWhiteSpace(userSettings.Theme) &&
                                string.Equals(merged.Branding.Theme, "Auto", StringComparison.OrdinalIgnoreCase));
        }

        var viewModel = new MainViewModel(merged, loaded.Source, userSettings);
        viewModel.UpdateTenant(tenant);
        _viewModel = viewModel;

        // Load cached data first (provides instant display while live data loads)
        var cacheLoaded = await viewModel.LoadFromCacheAsync();
        if (cacheLoaded)
        {
            Logger.Info("Cached data loaded for instant display");
        }

        var window = new MainWindow
        {
            DataContext = viewModel,
            Title = merged.Branding.ProductName,
            Width = merged.Layout.DefaultWidth,
            Height = merged.Layout.DefaultHeight
        };

        // Apply window icon from config
        ApplyWindowIcon(window, merged.Branding.Icon);

        ApplyLayout(window, merged.Layout);

        // Initialize system tray icon and minimize-to-tray behavior
        var enableTray = merged.Layout.TrayOnly;
        window.MinimizeToTrayOnClose = enableTray;

        var trayIcon = new TrayIconService(window, merged.Branding.Icon, merged.Branding.ProductName)
        {
            MinimizeToTray = enableTray
        };
        lock (StateLock)
        {
            _trayIcon = trayIcon;
        }

        window.Show();

        if (merged.Layout.StartMinimized)
        {
            window.WindowState = WindowState.Minimized;
            if (merged.Layout.TrayOnly)
            {
                window.Hide();
            }
        }

        var windowHandle = GetWindowHandle(window);

        try
        {
            var passwordProvider = await ChoosePasswordProviderAsync(tenant, merged.Auth, windowHandle);
            var pwdStatus = await passwordProvider.GetAsync(merged.PasswordPolicy, tenant).ConfigureAwait(false);
            viewModel.UpdatePasswordStatus(pwdStatus);
            if (passwordProvider is GraphPasswordAgeProvider graphProvider && graphProvider.LastIdentity is not null)
            {
                viewModel.UpdateIdentity(graphProvider.LastIdentity);
            }
            Logger.Info($"Password status: daysLeft={pwdStatus.DaysLeft} policyDays={pwdStatus.PolicyDays}");

            // Save to cache after successful live data fetch
            await viewModel.SaveToCacheAsync();
        }
        catch
        {
            Logger.Error("Password status retrieval failed");

            // Only show "unavailable" if no cache was loaded - preserve cached data for offline scenarios
            if (!cacheLoaded)
            {
                viewModel.UpdatePasswordStatus(new PasswordAgeResult(null, null, null));
                Logger.Info("No cache available, showing unavailable status");
            }
            else
            {
                Logger.Info("Live fetch failed but cached data preserved for offline display");
            }
        }
    }

    private static IntPtr GetWindowHandle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        return helper.Handle != IntPtr.Zero ? helper.Handle : helper.EnsureHandle();
    }

    /// <summary>
    /// Selects the appropriate password provider based on device join state and configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Provider selection logic:</b></para>
    /// <list type="number">
    ///   <item>If device is Azure AD joined AND ClientId is configured in auth section:
    ///     <list type="bullet">
    ///       <item>Try to create Graph client with WAM (Windows Account Manager) authentication</item>
    ///       <item>For AAD-joined devices: Silent SSO (no user interaction needed)</item>
    ///       <item>For other devices: Interactive browser sign-in</item>
    ///       <item>If Graph client creation succeeds: Use GraphPasswordAgeProvider</item>
    ///     </list>
    ///   </item>
    ///   <item>If Graph is unavailable or not configured: Use LdapPasswordAgeProvider</item>
    /// </list>
    ///
    /// <para><b>GraphPasswordAgeProvider features:</b></para>
    /// <list type="bullet">
    ///   <item>Queries Microsoft Graph /me endpoint for user profile</item>
    ///   <item>Gets lastPasswordChangeDateTime and passwordPolicies</item>
    ///   <item>For synced accounts: Falls back to LDAP to check on-prem "never expires" flag</item>
    ///   <item>Uses CloudDays policy for cloud-only accounts</item>
    ///   <item>Uses OnPremDays policy for synced (hybrid) accounts</item>
    /// </list>
    ///
    /// <para><b>LdapPasswordAgeProvider features:</b></para>
    /// <list type="bullet">
    ///   <item>Queries on-premises Active Directory via LDAP</item>
    ///   <item>Gets pwdLastSet and userAccountControl attributes</item>
    ///   <item>Checks UAC flag 0x10000 for "password never expires"</item>
    ///   <item>Always uses OnPremDays policy</item>
    /// </list>
    /// </remarks>
    /// <param name="tenant">Current tenant context from TenantInfoProvider.</param>
    /// <param name="auth">Authentication configuration containing ClientId.</param>
    /// <param name="parentWindow">Window handle for WAM authentication dialogs.</param>
    /// <returns>The selected password provider instance.</returns>
    private static async Task<IPasswordAgeProvider> ChoosePasswordProviderAsync(TenantContext tenant, AuthConfig auth, IntPtr parentWindow)
    {
        // -----------------------------------------------------------------
        // STEP 1: Check if Graph authentication is possible
        // -----------------------------------------------------------------
        // Requirements for Graph:
        //   - Device must be Azure AD joined (tenant.AzureAdJoined = true)
        //   - ClientId must be configured in config.json auth section
        //
        // Without both conditions, we skip Graph and use LDAP directly.
        // -----------------------------------------------------------------
        if (tenant.AzureAdJoined && !string.IsNullOrWhiteSpace(auth.ClientId))
        {
            // -----------------------------------------------------------------
            // STEP 2: Create Graph client with WAM authentication
            // -----------------------------------------------------------------
            // GraphClientFactory.TryCreateAsync:
            //   - Creates InteractiveBrowserCredential with WAM broker
            //   - For AAD-joined devices: Uses Windows Account Manager for SSO
            //   - For other devices: Opens browser for interactive sign-in
            //   - parentWindow ensures auth dialogs appear on correct window
            // -----------------------------------------------------------------
            var graph = await GraphClientFactory.TryCreateAsync(auth.ClientId, tenant.TenantId, parentWindow).ConfigureAwait(false);
            if (graph is not null)
            {
                // GraphPasswordAgeProvider does hybrid detection:
                // Graph API first, then LDAP fallback for synced accounts
                return new GraphPasswordAgeProvider(graph);
            }
            Logger.Info("Graph client unavailable; falling back to LDAP");
        }

        // -----------------------------------------------------------------
        // STEP 3: Fall back to LDAP provider
        // -----------------------------------------------------------------
        // Used when:
        //   - Device is not Azure AD joined (domain-joined only)
        //   - ClientId is not configured
        //   - Graph client creation failed
        //
        // LdapPasswordAgeProvider queries on-premises AD directly.
        // -----------------------------------------------------------------
        return new LdapPasswordAgeProvider();
    }

    private static void ApplyLayout(Window window, LayoutOptions layout)
    {
        var workArea = SystemParameters.WorkArea;

        // Prefer explicit anchors; fall back to legacy corner values only when anchors are default.
        var horizontal = layout.HorizontalAnchor;
        var vertical = layout.VerticalAnchor;

        var anchorsCustomized = layout.HorizontalAnchor != HorizontalAnchor.Right || layout.VerticalAnchor != VerticalAnchor.Bottom;

        if (!anchorsCustomized)
        {
            horizontal = layout.PreferredCorner is PreferredCorner.TopRight or PreferredCorner.BottomRight
                ? HorizontalAnchor.Right
                : HorizontalAnchor.Left;
            vertical = layout.PreferredCorner is PreferredCorner.BottomLeft or PreferredCorner.BottomRight
                ? VerticalAnchor.Bottom
                : VerticalAnchor.Top;
        }

        double left = horizontal switch
        {
            HorizontalAnchor.Center => workArea.Left + (workArea.Width - window.Width) / 2 + layout.OffsetX,
            HorizontalAnchor.Right => workArea.Right - window.Width - layout.OffsetX,
            _ => workArea.Left + layout.OffsetX
        };

        double top = vertical switch
        {
            VerticalAnchor.Center => workArea.Top + (workArea.Height - window.Height) / 2 + layout.OffsetY,
            VerticalAnchor.Bottom => workArea.Bottom - window.Height - layout.OffsetY,
            _ => workArea.Top + layout.OffsetY
        };

        window.Left = left;
        window.Top = top;
    }

    /// <summary>
    /// Applies a custom window icon from the specified path.
    /// </summary>
    private static void ApplyWindowIcon(Window window, string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return;

        try
        {
            var fullPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.Combine(AppContext.BaseDirectory, iconPath);

            if (File.Exists(fullPath))
            {
                var iconUri = new Uri(fullPath, UriKind.Absolute);
                window.Icon = new BitmapImage(iconUri);
                Logger.Info($"Window icon applied: {fullPath}");
            }
            else
            {
                Logger.Info($"Window icon not found: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to apply window icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves which theme to apply based on user preference and config.
    /// Returns "Auto" if auto-detection should be used, otherwise a specific theme name.
    /// </summary>
    private static string ResolveTheme(string? userTheme, string configTheme)
    {
        // 1. User preference takes priority
        if (!string.IsNullOrWhiteSpace(userTheme))
            return userTheme;

        // 2. If config specifies "Auto", return "Auto" (ThemeManager will detect)
        if (string.Equals(configTheme, "Auto", StringComparison.OrdinalIgnoreCase))
            return "Auto";

        // 3. Use config theme
        if (!string.IsNullOrWhiteSpace(configTheme))
            return configTheme;

        // 4. Fall back to auto-detection
        return "Auto";
    }

    /// <summary>
    /// Handles Windows user preference changes (including theme).
    /// </summary>
    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Only react to General category (includes theme changes)
        if (e.Category != UserPreferenceCategory.General)
            return;

        // Only auto-switch if enabled
        bool auto;
        lock (StateLock)
        {
            auto = _autoThemeEnabled;
        }

        if (!auto)
            return;

        var newTheme = ThemeManager.GetWindowsTheme();
        if (!string.Equals(ThemeManager.CurrentTheme, newTheme, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info($"Windows theme changed, switching to: {newTheme}");
            Current.Dispatcher.Invoke(() =>
            {
                // Apply the theme while keeping Auto mode
                ThemeManager.ApplyTheme("Auto");
            });
        }
    }

    /// <summary>
    /// Disables auto theme switching (called when user manually selects a specific theme).
    /// </summary>
    public static void DisableAutoTheme()
    {
        _autoThemeEnabled = false;
    }

    /// <summary>
    /// Enables auto theme switching (called when user selects "Auto").
    /// </summary>
    public static void EnableAutoTheme()
    {
        _autoThemeEnabled = true;
    }

    /// <summary>
    /// Gets the tray icon service for showing notifications.
    /// </summary>
    public static TrayIconService? TrayIcon
    {
        get
        {
            lock (StateLock)
            {
                return _trayIcon;
            }
        }
    }

    /// <summary>
    /// Clean up event subscriptions on exit.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
