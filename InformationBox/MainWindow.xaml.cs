using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Interop;
using InformationBox.Services;
using InformationBox.UI.ViewModels;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace InformationBox;

/// <summary>
/// Main window for the Information Box application.
/// </summary>
public partial class MainWindow : Window
{
    private bool _forceClose;

    /// <summary>
    /// Initializes a new window instance and wires up the generated components.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
        SourceInitialized += OnSourceInitialized;

        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeApplied -= OnThemeApplied;
        DataContextChanged -= OnDataContextChanged;
        Closing -= OnWindowClosing;
        SourceInitialized -= OnSourceInitialized;

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => ApplyTitleBarTheme();

    /// <summary>
    /// Gets or sets whether the close button should minimize to tray instead of closing.
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (MinimizeToTrayOnClose)
        {
            WindowState = WindowState.Minimized;
            Hide();
        }
        else
        {
            Close();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // If minimize to tray is enabled and not force closing, hide instead of close
        if (MinimizeToTrayOnClose && !_forceClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
        }
    }

    /// <summary>
    /// Forces the window to close (bypasses minimize-to-tray).
    /// </summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel vm)
        {
            ApplyBranding(vm.PrimaryColor);
        }
    }

    private void ApplyBranding(string? primaryColor)
    {
        // Accent colors now come from theme files (Themes/*.xaml)
        // The primaryColor from config is no longer used for accent colors

        // Apply dense mode and max width if present on the view model
        if (DataContext is MainViewModel vm)
        {
            ApplyDensity(vm.Config.Layout.DenseMode);
            ApplyMaxWidth(vm.Config.Layout.MaxContentWidth);
        }
    }

    private void ApplyDensity(bool dense)
    {
        double scale = dense ? 0.9 : 1.0;
        RootGrid.Margin = new Thickness(10 * scale);
        MainTabs.Padding = new Thickness(0, 2 * scale, 0, 4 * scale);
        RootGrid.LayoutTransform = new ScaleTransform(scale, scale);
    }

    private void ApplyMaxWidth(int maxContentWidth)
    {
        if (maxContentWidth > 0)
        {
            RootGrid.MaxWidth = maxContentWidth;
            RootGrid.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            RootGrid.MaxWidth = double.PositiveInfinity;
            RootGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(ApplyTitleBarTheme);
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            System.Windows.Media.Color? captionColor = TryGetResourceColor("WindowBackgroundBrush")
                ?? TryGetResourceColor("CardBackgroundBrush");
            System.Windows.Media.Color? textColor = TryGetResourceColor("TextPrimaryBrush");
            System.Windows.Media.Color? borderColor = TryGetResourceColor("CardBorderBrush");

            if (captionColor is null || textColor is null)
            {
                return;
            }

            int captionColorRef = ToColorRef(captionColor.Value);
            int textColorRef = ToColorRef(textColor.Value);
            int borderColorRef = borderColor is null ? captionColorRef : ToColorRef(borderColor.Value);

            TrySetDwmColor(hwnd, DwmWindowAttributeCaptionColor, captionColorRef);
            TrySetDwmColor(hwnd, DwmWindowAttributeTextColor, textColorRef);
            TrySetDwmColor(hwnd, DwmWindowAttributeBorderColor, borderColorRef);

            bool isDark = IsDarkColor(captionColor.Value);
            TrySetImmersiveDarkMode(hwnd, isDark ? 1 : 0);
        }
        catch
        {
            // Best-effort (unsupported Windows versions / environments).
        }
    }

    private System.Windows.Media.Color? TryGetResourceColor(string key)
    {
        return TryFindResource(key) is SolidColorBrush brush ? brush.Color : null;
    }

    private static bool IsDarkColor(System.Windows.Media.Color color)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;
        double luminance = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        return luminance < 0.5;
    }

    private static int ToColorRef(System.Windows.Media.Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static void TrySetDwmColor(IntPtr hwnd, int attribute, int colorRef)
    {
        _ = DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(int));
    }

    private static void TrySetImmersiveDarkMode(IntPtr hwnd, int enabled)
    {
        if (DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    private const int DwmWindowAttributeUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DwmWindowAttributeCaptionColor = 35;
    private const int DwmWindowAttributeTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
