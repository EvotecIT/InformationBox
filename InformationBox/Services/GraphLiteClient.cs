using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace InformationBox.Services;

/// <summary>
/// Minimal, AOT-friendly Graph client for the limited /me payload we need.
/// </summary>
public sealed class GraphLiteClient
{
    private static readonly Uri MeUri = new("https://graph.microsoft.com/v1.0/me?$select=displayName,userPrincipalName,mail,proxyAddresses,businessPhones,mobilePhone,jobTitle,department,officeLocation,lastPasswordChangeDateTime,onPremisesSyncEnabled");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TokenCredential _credential;
    private readonly string[] _scopes;
    private readonly HttpClient _httpClient;

    public GraphLiteClient(TokenCredential credential, string[] scopes, HttpClient? httpClient = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Calls GET /me and returns the typed payload.
    /// </summary>
    public async Task<GraphUser?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken).ConfigureAwait(false);
        using var req = new HttpRequestMessage(HttpMethod.Get, MeUri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Logger.Error($"Graph /me failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
            return null;
        }

        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<GraphUser>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Minimal projection of Microsoft Graph user needed by the app.
/// </summary>
public sealed class GraphUser
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; init; }

    [JsonPropertyName("mail")]
    public string? Mail { get; init; }

    [JsonPropertyName("proxyAddresses")]
    public string[]? ProxyAddresses { get; init; }

    [JsonPropertyName("businessPhones")]
    public string[]? BusinessPhones { get; init; }

    [JsonPropertyName("mobilePhone")]
    public string? MobilePhone { get; init; }

    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; init; }

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("officeLocation")]
    public string? OfficeLocation { get; init; }

    [JsonPropertyName("lastPasswordChangeDateTime")]
    public DateTimeOffset? LastPasswordChangeDateTime { get; init; }

    [JsonPropertyName("onPremisesSyncEnabled")]
    public bool? OnPremisesSyncEnabled { get; init; }
}
