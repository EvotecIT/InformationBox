using System;
using InformationBox.Services;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Represents password expiry state for display.
/// </summary>
/// <param name="LastChangedUtc">When the password was last changed.</param>
/// <param name="PolicyDays">Number of days allowed by policy.</param>
/// <param name="DaysLeft">Remaining days before expiry.</param>
/// <param name="IsValid">Indicates whether the data is meaningful.</param>
/// <param name="NeverExpires">Indicates the account is set to never expire.</param>
public sealed record PasswordStatusViewModel(
    DateTimeOffset? LastChangedUtc,
    int? PolicyDays,
    int? DaysLeft,
    bool IsValid,
    bool NeverExpires)
{
    /// <summary>
    /// Gets the estimated date when the next change is due.
    /// </summary>
    public DateTimeOffset? NextChangeUtc =>
        !NeverExpires && LastChangedUtc.HasValue && PolicyDays.HasValue
            ? LastChangedUtc.Value.AddDays(PolicyDays.Value)
            : null;

    /// <summary>
    /// Gets the percent of the policy window that has been used.
    /// </summary>
    public double? PercentUsed =>
        !NeverExpires && LastChangedUtc.HasValue && PolicyDays.HasValue && DaysLeft.HasValue && PolicyDays.Value > 0
            ? 100d - (DaysLeft.Value / (double)PolicyDays.Value * 100d)
            : null;

    /// <summary>
    /// Gets display-friendly text for remaining days.
    /// </summary>
    public string DaysLeftText
    {
        get
        {
            if (NeverExpires) return "Never";
            if (!DaysLeft.HasValue) return "N/A";
            return DaysLeft.Value switch
            {
                1 => "1 day",
                _ => $"{DaysLeft.Value} days"
            };
        }
    }

    /// <summary>
    /// Gets a friendly text summary for the health of the password.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (NeverExpires) return "Never expires";
            if (!DaysLeft.HasValue) return "Unavailable";

            // Include expiry date for context
            var expiryInfo = NextChangeUtc.HasValue
                ? $" · Expires {NextChangeUtc.Value.LocalDateTime:MMM d}"
                : "";

            return DaysLeft.Value switch
            {
                < 0 => $"Expired{expiryInfo}",
                0 => $"Expires today",
                1 => $"Expires tomorrow",
                <= 5 => $"Expiring soon{expiryInfo}",
                <= 14 => $"Expires {NextChangeUtc?.LocalDateTime:MMM d}",
                _ => NextChangeUtc.HasValue ? $"Expires {NextChangeUtc.Value.LocalDateTime:MMM d}" : "Healthy"
            };
        }
    }

    /// <summary>
    /// Creates a view model instance from the lower-level password age result.
    /// </summary>
    /// <param name="result">Password age calculation output.</param>
    /// <returns>A view model ready for binding.</returns>
    public static PasswordStatusViewModel From(PasswordAgeResult result) =>
        new(result.LastChangedUtc, result.PolicyDays, result.DaysLeft, result.DaysLeft.HasValue, result.NeverExpires);
}
