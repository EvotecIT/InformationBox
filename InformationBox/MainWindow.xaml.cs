using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
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
    }

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
}
