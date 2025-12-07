using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace InformationBox.Services;

/// <summary>
/// Creates Graph clients using silent MSAL + WAM broker (no UI). Falls back to null if no cached token.
/// </summary>
public static class GraphClientFactory
{
    private static readonly string[] Scopes = { "User.Read" };

    /// <summary>
    /// Attempts to create a Graph client using cached Windows credentials, falling back to an interactive broker sign-in.
    /// </summary>
    /// <param name="clientId">The public client application ID registered in Entra.</param>
    /// <param name="tenantId">Optional tenant ID to target when requesting tokens.</param>
    /// <param name="parentWindow">Window handle used if an interactive broker prompt is required.</param>
    /// <returns>A configured Graph client when silent auth succeeds; otherwise, null.</returns>
    public static async Task<GraphServiceClient?> TryCreateAsync(string clientId, string? tenantId, IntPtr parentWindow)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Logger.Info("Graph client skipped: no clientId provided");
            return null;
        }

        try
        {
            var builder = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(string.IsNullOrWhiteSpace(tenantId)
                    ? "https://login.microsoftonline.com/common"
                    : $"https://login.microsoftonline.com/{tenantId}")
                .WithRedirectUri($"ms-appx-web://microsoft.aad.brokerplugin/{clientId}")
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows));

            var app = builder.Build();
            var msalCredential = new MsalSilentTokenCredential(app, Scopes, allowOperatingSystemAccount: true);
            if (await TryWarmupCredentialAsync(msalCredential, "brokered Windows SSO cache").ConfigureAwait(false))
            {
                Logger.Info("Graph client created via brokered Windows SSO cache");
                return new GraphServiceClient(msalCredential, Scopes);
            }

            if (await TryInteractiveBrokerAsync(app, parentWindow).ConfigureAwait(false))
            {
                Logger.Info("Graph client created via brokered interactive sign-in (token cached for future silent use)");
                return new GraphServiceClient(msalCredential, Scopes);
            }

            Logger.Info("Graph interactive fallback failed; falling back to LDAP");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("Graph client creation failed", ex);
            return null;
        }
    }

    private static async Task<bool> TryWarmupCredentialAsync(TokenCredential credential, string description)
    {
        try
        {
            await credential.GetTokenAsync(new TokenRequestContext(Scopes), default).ConfigureAwait(false);
            return true;
        }
        catch (CredentialUnavailableException ex)
        {
            Logger.Info($"Graph {description} unavailable: {ex.Message}");
            return false;
        }
        catch (AuthenticationFailedException ex)
        {
            Logger.Info($"Graph {description} failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> TryInteractiveBrokerAsync(IPublicClientApplication app, IntPtr parentWindow)
    {
        if (parentWindow == IntPtr.Zero)
        {
            Logger.Info("Graph interactive fallback skipped: parent window handle unavailable");
            return false;
        }

        try
        {
            await app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(parentWindow)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return true;
        }
        catch (MsalException ex)
        {
            Logger.Info($"Graph interactive fallback failed: {ex.Message}");
            return false;
        }
    }
}
