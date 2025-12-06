using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Microsoft.Identity.Client;

namespace InformationBox.Services;

/// <summary>
/// TokenCredential that acquires tokens silently via MSAL public client (no UI).
/// </summary>
public sealed class MsalSilentTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly bool _allowOperatingSystemAccount;

    /// <summary>
    /// Initializes the credential with the MSAL public client and requested scopes.
    /// </summary>
    /// <param name="app">The public client used to look up cached accounts.</param>
    /// <param name="scopes">Scopes requested for Graph tokens.</param>
    /// <param name="allowOperatingSystemAccount">When true, falls back to the OS account if no MSAL cache entry exists.</param>
    public MsalSilentTokenCredential(IPublicClientApplication app, string[] scopes, bool allowOperatingSystemAccount = false)
    {
        _app = app;
        _scopes = scopes;
        _allowOperatingSystemAccount = allowOperatingSystemAccount;
    }

    /// <inheritdoc />
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var account = await ResolveAccountAsync().ConfigureAwait(false);
        if (account is null)
        {
            throw new Azure.Identity.AuthenticationFailedException("No cached account available for silent auth");
        }

        try
        {
            var result = await _app.AcquireTokenSilent(_scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        catch (MsalUiRequiredException ex)
        {
            throw new Azure.Identity.AuthenticationFailedException("Silent token unavailable", ex);
        }
        catch (MsalException ex)
        {
            throw new Azure.Identity.AuthenticationFailedException("Silent token acquisition failed", ex);
        }
    }

    private async Task<IAccount?> ResolveAccountAsync()
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (account is not null)
        {
            return account;
        }

        if (_allowOperatingSystemAccount)
        {
            try
            {
                return PublicClientApplication.OperatingSystemAccount;
            }
            catch (MsalClientException)
            {
                // WAM not available or machine not AAD joined
            }
        }

        return null;
    }
}
