using System;
using System.Diagnostics;

namespace InformationBox.Services;

/// <summary>
/// Opens URLs using the default handler.
/// </summary>
public static class UrlLauncher
{
    /// <summary>
    /// Opens a web URL or one of the application's fixed Windows Settings destinations.
    /// </summary>
    /// <param name="url">Target web URL or built-in Settings destination.</param>
    public static void Open(string url)
    {
        if (!TryGetAllowedDestination(url, out var destination))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(destination)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Swallow: we don't want to crash UI on bad links.
        }
    }

    internal static bool IsAllowedDestination(string? url) => TryGetAllowedDestination(url, out _);

    private static bool TryGetAllowedDestination(string? url, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url is "ms-settings:network-status" or "ms-settings:network-vpn")
        {
            destination = url;
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        destination = uri.AbsoluteUri;
        return true;
    }
}
