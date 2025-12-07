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
}
