using System.Diagnostics;

namespace InformationBox.Services;

/// <summary>
/// Opens URLs using the default handler.
/// </summary>
public static class UrlLauncher
{
    /// <summary>
    /// Launches the supplied URL using the default shell handler.
    /// </summary>
    /// <param name="url">Target URL or protocol.</param>
    public static void Open(string url)
    {
        try
        {
            var psi = new ProcessStartInfo(url)
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
}
