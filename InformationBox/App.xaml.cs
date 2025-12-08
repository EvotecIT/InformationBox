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

namespace InformationBox;

/// <summary>
/// App bootstrapper.
/// </summary>
public partial class App : Application
{
    private static bool _autoThemeEnabled;
    private static MainViewModel? _viewModel;

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
        _autoThemeEnabled = string.Equals(userSettings.Theme, "Auto", StringComparison.OrdinalIgnoreCase) ||
                           (string.IsNullOrWhiteSpace(userSettings.Theme) &&
                            string.Equals(merged.Branding.Theme, "Auto", StringComparison.OrdinalIgnoreCase));

        var viewModel = new MainViewModel(merged, loaded.Source, userSettings);
        viewModel.UpdateTenant(tenant);
        _viewModel = viewModel;

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
        window.Show();

        if (merged.Layout.StartMinimized)
        {
            window.WindowState = WindowState.Minimized;
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
        }
        catch
        {
            Logger.Error("Password status retrieval failed");
            viewModel.UpdatePasswordStatus(new PasswordAgeResult(null, null, null));
        }
    }

    private static IntPtr GetWindowHandle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        return helper.Handle != IntPtr.Zero ? helper.Handle : helper.EnsureHandle();
    }

    private static async Task<IPasswordAgeProvider> ChoosePasswordProviderAsync(TenantContext tenant, AuthConfig auth, IntPtr parentWindow)
    {
        if (tenant.AzureAdJoined && !string.IsNullOrWhiteSpace(auth.ClientId))
        {
            var graph = await GraphClientFactory.TryCreateAsync(auth.ClientId, tenant.TenantId, parentWindow).ConfigureAwait(false);
            if (graph is not null)
            {
                return new GraphPasswordAgeProvider(graph);
            }
            Logger.Info("Graph client unavailable; falling back to LDAP");
        }

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
        if (!_autoThemeEnabled)
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
    /// Clean up event subscriptions on exit.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }
}
