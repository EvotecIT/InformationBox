using System.Text.Json.Serialization;

namespace InformationBox.Config;

/// <summary>
/// UI layout behavior and defaults.
/// </summary>
public sealed record LayoutOptions
{
    /// <summary>
    /// Gets a value indicating whether the app should launch minimized.
    /// </summary>
    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; init; } = true;

    /// <summary>
    /// Gets the default window width in pixels.
    /// </summary>
    [JsonPropertyName("defaultWidth")]
    public int DefaultWidth { get; init; } = 820;

    /// <summary>
    /// Gets the default window height in pixels.
    /// </summary>
    [JsonPropertyName("defaultHeight")]
    public int DefaultHeight { get; init; } = 540;

    /// <summary>
    /// Gets the preferred monitor corner where the window should appear.
    /// </summary>
    [JsonPropertyName("preferredCorner")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PreferredCorner PreferredCorner { get; init; } = PreferredCorner.BottomRight;

    /// <summary>
    /// Horizontal anchor for initial placement.
    /// </summary>
    [JsonPropertyName("horizontalAnchor")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HorizontalAnchor HorizontalAnchor { get; init; } = HorizontalAnchor.Right;

    /// <summary>
    /// Vertical anchor for initial placement.
    /// </summary>
    [JsonPropertyName("verticalAnchor")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerticalAnchor VerticalAnchor { get; init; } = VerticalAnchor.Bottom;

    /// <summary>
    /// Optional pixel offset applied after anchoring on the X axis.
    /// </summary>
    [JsonPropertyName("offsetX")]
    public int OffsetX { get; init; } = 0;

    /// <summary>
    /// Optional pixel offset applied after anchoring on the Y axis.
    /// </summary>
    [JsonPropertyName("offsetY")]
    public int OffsetY { get; init; } = 0;

    /// <summary>
    /// Gets the behavior to select the display in multi-monitor scenarios.
    /// </summary>
    [JsonPropertyName("multiMonitor")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MultiMonitorBehavior MultiMonitor { get; init; } = MultiMonitorBehavior.Active;

    /// <summary>
    /// Gets a value indicating whether the window should remain in the tray only.
    /// </summary>
    [JsonPropertyName("trayOnly")]
    public bool TrayOnly { get; init; } = false;
}

/// <summary>
/// Preferred screen corner for initial placement.
/// </summary>
public enum PreferredCorner
{
    /// <summary>
    /// Positions the window in the top-left corner.
    /// </summary>
    TopLeft,
    /// <summary>
    /// Positions the window in the top-right corner.
    /// </summary>
    TopRight,
    /// <summary>
    /// Positions the window in the bottom-left corner.
    /// </summary>
    BottomLeft,
    /// <summary>
    /// Positions the window in the bottom-right corner.
    /// </summary>
    BottomRight
}

/// <summary>
/// Horizontal alignment target within the work area.
/// </summary>
public enum HorizontalAnchor
{
    /// <summary>
    /// Stick to the left edge.
    /// </summary>
    Left,
    /// <summary>
    /// Center horizontally.
    /// </summary>
    Center,
    /// <summary>
    /// Stick to the right edge.
    /// </summary>
    Right
}

/// <summary>
/// Vertical alignment target within the work area.
/// </summary>
public enum VerticalAnchor
{
    /// <summary>
    /// Stick to the top edge.
    /// </summary>
    Top,
    /// <summary>
    /// Center vertically.
    /// </summary>
    Center,
    /// <summary>
    /// Stick to the bottom edge.
    /// </summary>
    Bottom
}

/// <summary>
/// Determines which monitor should host the window when multiple are present.
/// </summary>
public enum MultiMonitorBehavior
{
    /// <summary>
    /// Always uses the primary display.
    /// </summary>
    Primary,
    /// <summary>
    /// Uses the display that currently has user focus.
    /// </summary>
    Active,
    /// <summary>
    /// Targets a specific display index.
    /// </summary>
    DisplayIndex
}
