using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace InformationBox.Services;

/// <summary>
/// Manages the system tray icon and its context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the TrayIconService.
    /// </summary>
    /// <param name="mainWindow">The main application window.</param>
    /// <param name="iconPath">Path to the icon file.</param>
    /// <param name="productName">Product name for tooltip.</param>
    public TrayIconService(Window mainWindow, string? iconPath, string productName)
    {
        _mainWindow = mainWindow;

        _notifyIcon = new NotifyIcon
        {
            Text = productName,
            Visible = true,
            Icon = LoadIcon(iconPath)
        };

        _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
        _notifyIcon.ContextMenuStrip = CreateContextMenu();

        // Handle window state changes
        _mainWindow.StateChanged += OnWindowStateChanged;
    }

    /// <summary>
    /// Gets or sets whether minimizing to tray is enabled.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Shows a balloon notification.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    /// <param name="icon">Icon type.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        if (_disposed) return;
        try
        {
            _notifyIcon.ShowBalloonTip(timeout, title, message, icon);
        }
        catch (ObjectDisposedException)
        {
            // Swallow if disposed between check and call
        }
    }

    private static Icon? LoadIcon(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            // Try to use application icon
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    return Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch
            {
                // Ignore
            }
            return SystemIcons.Application;
        }

        try
        {
            var fullPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.Combine(AppContext.BaseDirectory, iconPath);

            if (File.Exists(fullPath))
            {
                return new Icon(fullPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load tray icon: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show Information Box");
        showItem.Click += (_, _) => ShowWindow();
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(showItem);

        menu.Items.Add(new ToolStripSeparator());

        var refreshItem = new ToolStripMenuItem("Refresh");
        refreshItem.Click += (_, _) => OnRefreshClicked();
        menu.Items.Add(refreshItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => OnExitClicked();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnTrayIconDoubleClick(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    private void ShowWindow()
    {
        if (_disposed) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (MinimizeToTray && _mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.Hide();
        }
    }

    private void OnRefreshClicked()
    {
        if (_disposed) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Refresh command may be swapped during lifetime; re-check and guard null.
            if (_mainWindow.DataContext is UI.ViewModels.MainViewModel vm)
            {
                var refresh = vm.RefreshCommand;
                if (refresh != null && refresh.CanExecute(null))
                {
                    refresh.Execute(null);
                }
            }
        });
    }

    private void OnExitClicked()
    {
        if (_disposed) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Use ForceClose to bypass minimize-to-tray behavior
            if (_mainWindow is MainWindow mw)
            {
                mw.ForceClose();
            }
            Application.Current.Shutdown();
        });
    }

    /// <summary>
    /// Disposes the tray icon and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mainWindow.StateChanged -= OnWindowStateChanged;
        _notifyIcon.DoubleClick -= OnTrayIconDoubleClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
