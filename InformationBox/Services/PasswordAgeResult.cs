using System;

namespace InformationBox.Services;

/// <summary>
/// Represents password age calculation.
/// </summary>
/// <param name="LastChangedUtc">The UTC timestamp of the last password change.</param>
/// <param name="PolicyDays">The number of days allowed by the policy.</param>
/// <param name="DaysLeft">The remaining days before expiry.</param>
/// <param name="NeverExpires">True when the account is configured to never expire.</param>
public sealed record PasswordAgeResult(DateTimeOffset? LastChangedUtc, int? PolicyDays, int? DaysLeft, bool NeverExpires = false);
