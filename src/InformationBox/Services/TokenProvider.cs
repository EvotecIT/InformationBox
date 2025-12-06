using System;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;

namespace InformationBox.Services;

/// <summary>
/// Simple bearer token provider for Graph.
/// </summary>
public sealed class TokenProvider : IAccessTokenProvider
{
    private readonly Func<string> _tokenFactory;

    /// <summary>
    /// Initializes the provider with a delegate that supplies tokens.
    /// </summary>
    /// <param name="tokenFactory">Function invoked when Kiota requests a token.</param>
    public TokenProvider(Func<string> tokenFactory)
    {
        _tokenFactory = tokenFactory;
    }

    /// <inheritdoc />
    public Task<string> GetAuthorizationTokenAsync(Uri uri, System.Collections.Generic.Dictionary<string, object>? additionalAuthenticationContext = null, System.Threading.CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokenFactory());
    }

    /// <inheritdoc />
    public AllowedHostsValidator AllowedHostsValidator { get; } = new();
}
