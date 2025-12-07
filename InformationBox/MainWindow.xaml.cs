using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using InformationBox.UI.ViewModels;

namespace InformationBox;

/// <summary>
/// Main window for the Information Box application.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new window instance and wires up the generated components.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
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
        var fallback = (Color)ColorConverter.ConvertFromString("#0050b3");
        Color baseColor;
        try
        {
            baseColor = (Color)ColorConverter.ConvertFromString(primaryColor ?? "#0050b3");
        }
        catch
        {
            baseColor = fallback;
        }

        Resources["AccentBrush"] = new SolidColorBrush(baseColor);
        Resources["AccentBrushDark"] = new SolidColorBrush(AdjustBrightness(baseColor, -0.18));
        Resources["AccentBrushLight"] = new SolidColorBrush(AdjustBrightness(baseColor, 0.32));
        Resources["AccentForegroundBrush"] = new SolidColorBrush(ChooseForeground(baseColor));

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

    private static Color AdjustBrightness(Color color, double delta)
    {
        byte Clamp(double v) => (byte)Math.Max(0, Math.Min(255, v));
        return Color.FromArgb(
            color.A,
            Clamp(color.R * (1 + delta)),
            Clamp(color.G * (1 + delta)),
            Clamp(color.B * (1 + delta)));
    }

    private static Color ChooseForeground(Color color)
    {
        // Relative luminance for contrast; pick white when dark, black when light.
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        return luminance < 0.6 ? Colors.White : Colors.Black;
    }
}
