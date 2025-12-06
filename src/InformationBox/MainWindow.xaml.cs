using System.Windows;

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
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
