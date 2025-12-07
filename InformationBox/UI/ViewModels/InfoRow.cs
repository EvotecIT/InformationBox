namespace InformationBox.UI.ViewModels;

/// <summary>
/// Simple label/value pair for overview display.
/// </summary>
/// <param name="Label">Column label.</param>
/// <param name="Value">Associated value text.</param>
public sealed record InfoRow(string Label, string? Value);
