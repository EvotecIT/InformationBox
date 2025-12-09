using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace InformationBox.Services;

// ============================================================================
// MICROSOFT GRAPH API CLIENT
// ============================================================================
//
// PURPOSE:
//   Lightweight, AOT-compatible client for querying Microsoft Graph API.
//   Retrieves user profile and password information for the signed-in user.
//
// AUTHENTICATION FLOW:
//   1. This client receives a TokenCredential from Azure.Identity
//   2. TokenCredential handles the actual authentication (WAM, device code, etc.)
//   3. We request tokens with the configured scopes (typically User.Read)
//   4. Tokens are automatically cached and refreshed by Azure.Identity
//
// SECURITY CONSIDERATIONS:
//   - Uses delegated permissions (user context), not application permissions
//   - Only requests User.Read scope - minimal privilege principle
//   - No secrets stored in code - uses Windows Account Manager (WAM) or device auth
//   - Tokens are short-lived and automatically refreshed
//
// GRAPH API ENDPOINT:
//   GET https://graph.microsoft.com/v1.0/me
//   Returns the signed-in user's profile with selected fields only ($select)
//
// REQUIRED AZURE AD APP REGISTRATION:
//   1. Register app in Azure Portal > App registrations
//   2. Set "Mobile and desktop applications" redirect URI to:
//      https://login.microsoftonline.com/common/oauth2/nativeclient
//   3. Enable "Allow public client flows" = Yes
//   4. Add API permission: Microsoft Graph > User.Read (delegated)
//   5. Copy Application (client) ID to config.json
//
// ============================================================================

/// <summary>
/// Minimal, AOT-friendly Graph client for querying the /me endpoint.
///
/// <para><b>Why a custom client instead of Microsoft.Graph SDK?</b></para>
/// <list type="bullet">
///   <item>AOT/trimming compatible - no reflection-based serialization</item>
///   <item>Minimal dependencies - only Azure.Identity and System.Text.Json</item>
///   <item>Fast startup - no SDK initialization overhead</item>
///   <item>Precise control over which fields are requested</item>
/// </list>
///
/// <para><b>Authentication:</b></para>
/// The client uses Azure.Identity's TokenCredential abstraction, which supports:
/// <list type="bullet">
///   <item>Windows Account Manager (WAM) - SSO with Windows Hello, seamless for AAD-joined devices</item>
///   <item>Interactive browser authentication - fallback for non-joined devices</item>
///   <item>Device code flow - for scenarios where browser isn't available</item>
/// </list>
/// </summary>
/// <remarks>
/// Entry point: <see cref="GetMeAsync"/> - Call this to retrieve user profile.
/// Token acquisition is handled automatically by the injected <see cref="TokenCredential"/>.
/// </remarks>
public class GraphLiteClient : IGraphClient
{
    // -------------------------------------------------------------------------
    // GRAPH API CONFIGURATION
    // -------------------------------------------------------------------------
    //
    // The $select parameter limits the response to only the fields we need.
    // This reduces payload size and ensures we don't request more data than necessary.
    //
    // Fields requested:
    //   - displayName, userPrincipalName, mail: User identity
    //   - proxyAddresses: Email aliases (for display)
    //   - businessPhones, mobilePhone: Contact info
    //   - jobTitle, department, officeLocation: Organizational info
    //   - lastPasswordChangeDateTime: For password expiry calculation
    //   - onPremisesSyncEnabled: Indicates hybrid/synced account (affects password policy)
    //   - passwordPolicies: Contains "DisablePasswordExpiration" if password never expires
    // -------------------------------------------------------------------------
    private static readonly Uri MeUri = new(
        "https://graph.microsoft.com/v1.0/me?$select=" +
        "displayName,userPrincipalName,mail,proxyAddresses," +
        "businessPhones,mobilePhone,jobTitle,department,officeLocation," +
        "lastPasswordChangeDateTime,onPremisesSyncEnabled,passwordPolicies");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TokenCredential _credential;
    private readonly string[] _scopes;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes the Graph client with authentication credentials.
    /// </summary>
    /// <param name="credential">
    /// Azure.Identity credential for token acquisition. Typically one of:
    /// <list type="bullet">
    ///   <item><c>InteractiveBrowserCredential</c> with WAM broker for AAD-joined devices</item>
    ///   <item><c>DeviceCodeCredential</c> for headless scenarios</item>
    /// </list>
    /// </param>
    /// <param name="scopes">
    /// OAuth scopes to request. For this app: <c>["User.Read"]</c>
    /// </param>
    /// <param name="httpClient">Optional HTTP client for testing/customization.</param>
    public GraphLiteClient(TokenCredential credential, string[] scopes, HttpClient? httpClient = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Retrieves the signed-in user's profile from Microsoft Graph.
    /// </summary>
    /// <remarks>
    /// <para><b>Authentication flow:</b></para>
    /// <list type="number">
    ///   <item>Request access token from Azure AD via TokenCredential</item>
    ///   <item>Token is cached - subsequent calls use cached token until expiry</item>
    ///   <item>Attach token as Bearer authorization header</item>
    ///   <item>Call Graph API and deserialize response</item>
    /// </list>
    ///
    /// <para><b>Error handling:</b></para>
    /// Returns null on failure (auth error, network error, etc.) rather than throwing.
    /// Errors are logged for troubleshooting.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User profile or null if the call fails.</returns>
    public async Task<GraphUser?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Acquire access token
        // -----------------------------
        // TokenCredential.GetTokenAsync handles:
        //   - Token cache lookup (returns cached token if valid)
        //   - Token refresh (if cached token is expired but refresh token is valid)
        //   - Interactive auth (if no valid tokens, prompts user)
        //
        // For WAM-based auth on AAD-joined devices, this is typically silent (SSO).
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(_scopes),
            cancellationToken).ConfigureAwait(false);

        // Step 2: Build and send HTTP request
        // ------------------------------------
        using var req = new HttpRequestMessage(HttpMethod.Get, MeUri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);

        // Step 3: Handle response
        // -----------------------
        if (!resp.IsSuccessStatusCode)
        {
            // Log detailed error for troubleshooting
            // Common errors:
            //   - 401 Unauthorized: Token expired or invalid
            //   - 403 Forbidden: Missing permissions (check app registration)
            //   - 404 Not Found: User doesn't exist (shouldn't happen for /me)
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Logger.Error($"Graph /me failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
            return null;
        }

        // Step 4: Deserialize response
        // ----------------------------
        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<GraphUser>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}

// ============================================================================
// GRAPH USER MODEL
// ============================================================================
//
// Maps to Microsoft Graph User resource (subset of fields).
// Documentation: https://learn.microsoft.com/en-us/graph/api/resources/user
//
// ============================================================================

/// <summary>
/// Represents user profile data from Microsoft Graph /me endpoint.
/// </summary>
/// <remarks>
/// This is a minimal projection - only fields needed by the application.
/// Each property maps to a Microsoft Graph User resource property.
/// </remarks>
public sealed class GraphUser
{
    /// <summary>Display name (e.g., "John Doe").</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>User principal name - the sign-in identifier (e.g., "john@contoso.com").</summary>
    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; init; }

    /// <summary>Primary SMTP email address.</summary>
    [JsonPropertyName("mail")]
    public string? Mail { get; init; }

    /// <summary>All email addresses including aliases (SMTP:, smtp:, SIP:, etc.).</summary>
    [JsonPropertyName("proxyAddresses")]
    public string[]? ProxyAddresses { get; init; }

    /// <summary>Business phone numbers from directory.</summary>
    [JsonPropertyName("businessPhones")]
    public string[]? BusinessPhones { get; init; }

    /// <summary>Mobile phone number.</summary>
    [JsonPropertyName("mobilePhone")]
    public string? MobilePhone { get; init; }

    /// <summary>Job title (e.g., "Software Engineer").</summary>
    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; init; }

    /// <summary>Department name.</summary>
    [JsonPropertyName("department")]
    public string? Department { get; init; }

    /// <summary>Office location/building.</summary>
    [JsonPropertyName("officeLocation")]
    public string? OfficeLocation { get; init; }

    /// <summary>
    /// Timestamp of when the user's password was last changed.
    /// Used to calculate days remaining until password expires.
    /// </summary>
    /// <remarks>
    /// For synced accounts, this reflects the on-premises AD password change time.
    /// </remarks>
    [JsonPropertyName("lastPasswordChangeDateTime")]
    public DateTimeOffset? LastPasswordChangeDateTime { get; init; }

    /// <summary>
    /// Indicates whether the account is synced from on-premises Active Directory.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>true</c> = Hybrid account (synced from AD via Azure AD Connect)</item>
    ///   <item><c>false</c> or <c>null</c> = Cloud-only account</item>
    /// </list>
    /// This affects password policy: synced accounts use on-prem policy days.
    /// </remarks>
    [JsonPropertyName("onPremisesSyncEnabled")]
    public bool? OnPremisesSyncEnabled { get; init; }

    /// <summary>
    /// Password policies applied to this user account.
    /// </summary>
    /// <remarks>
    /// Possible values (space-separated string):
    /// <list type="bullet">
    ///   <item><c>DisablePasswordExpiration</c> - Password never expires</item>
    ///   <item><c>DisableStrongPassword</c> - Weak passwords allowed</item>
    /// </list>
    ///
    /// <b>Important:</b> For synced accounts, this may not reflect the on-premises
    /// AD "Password never expires" checkbox. The app performs an additional LDAP
    /// check for synced accounts to get the accurate value.
    /// </remarks>
    [JsonPropertyName("passwordPolicies")]
    public string? PasswordPolicies { get; init; }

    /// <summary>
    /// Returns true if the <see cref="PasswordPolicies"/> contains "DisablePasswordExpiration".
    /// </summary>
    /// <remarks>
    /// This checks the Azure AD policy. For synced accounts, also check LDAP UAC flag.
    /// See <see cref="GraphPasswordAgeProvider.CheckLdapNeverExpiresAsync(System.Threading.CancellationToken)"/> for hybrid detection.
    /// </remarks>
    public bool PasswordNeverExpires =>
        PasswordPolicies?.Contains("DisablePasswordExpiration", StringComparison.OrdinalIgnoreCase) == true;
}
