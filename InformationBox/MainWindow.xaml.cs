using System;
using System.Windows;
using System.Windows.Media;
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
