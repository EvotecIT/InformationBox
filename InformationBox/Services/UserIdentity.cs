using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graph.Models;

namespace InformationBox.Services;

/// <summary>
/// Represents the signed-in user's identity details surfaced in the UI.
/// </summary>
/// <param name="DisplayName">Primary display name.</param>
/// <param name="UserPrincipalName">User principal name.</param>
/// <param name="PrimaryEmail">Preferred email address.</param>
/// <param name="Aliases">Alternate addresses or proxy aliases.</param>
/// <param name="JobTitle">Job title text.</param>
/// <param name="Department">Department text.</param>
/// <param name="OfficeLocation">Office location string.</param>
/// <param name="MobilePhone">Mobile phone number.</param>
/// <param name="BusinessPhones">Business phone numbers.</param>
/// <param name="IsGraphBacked">True when populated from Microsoft Graph.</param>
public sealed record UserIdentity(
    string DisplayName,
    string? UserPrincipalName,
    string? PrimaryEmail,
    IReadOnlyList<string> Aliases,
    string? JobTitle,
    string? Department,
    string? OfficeLocation,
    string? MobilePhone,
    IReadOnlyList<string> BusinessPhones,
    bool IsGraphBacked)
{
    /// <summary>
    /// Creates a local identity snapshot using environment variables.
    /// </summary>
    public static UserIdentity FromEnvironment()
    {
        var displayName = Environment.UserName;
        var upn = TryGetUpn();
        var emailGuess = upn?.Contains('@') == true ? upn : null;
        return new UserIdentity(
            displayName,
            upn,
            emailGuess,
            Array.Empty<string>(),
            JobTitle: null,
            Department: null,
            OfficeLocation: null,
            MobilePhone: null,
            Array.Empty<string>(),
            IsGraphBacked: false);
    }

    /// <summary>
    /// Creates an identity snapshot from the Microsoft Graph /me payload.
    /// </summary>
    /// <param name="me">Graph user resource.</param>
    public static UserIdentity FromGraph(User me)
    {
        var aliasesSource = me.ProxyAddresses ?? new List<string>();
        var aliases = aliasesSource
            .Select(ParseProxy)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Where(a => a.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var phonesSource = me.BusinessPhones ?? new List<string>();
        var businessPhones = phonesSource
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToArray();

        return new UserIdentity(
            me.DisplayName ?? me.UserPrincipalName ?? Environment.UserName,
            me.UserPrincipalName,
            me.Mail ?? aliases.FirstOrDefault(),
            aliases,
            me.JobTitle,
            me.Department,
            me.OfficeLocation,
            me.MobilePhone,
            businessPhones,
            IsGraphBacked: true);
    }

    private static string? ParseProxy(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var separator = raw.IndexOf(':');
        var value = separator >= 0 ? raw[(separator + 1)..] : raw;
        if (value.StartsWith("/o=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return value;
    }

    private static string? TryGetUpn()
    {
        var dnsDomain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
        if (!string.IsNullOrWhiteSpace(dnsDomain))
        {
            return $"{Environment.UserName}@{dnsDomain}".ToLowerInvariant();
        }

        return null;
    }
}
