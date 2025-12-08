using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformationBox.Services;

/// <summary>
/// Manages local caching of application data for offline access.
/// </summary>
public static class CacheService
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InformationBox",
        "cache.json");

    // Prevent serving stale data forever; cache entries older than this are discarded.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Loads cached data from disk asynchronously, or returns null if not available.
    /// </summary>
    public static async Task<CachedData?> LoadAsync()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var json = await File.ReadAllTextAsync(CachePath).ConfigureAwait(false);
                var cache = JsonSerializer.Deserialize<CachedData>(json, JsonOptions);
                if (cache != null)
                {
                    if (cache.LastUpdated.ToUniversalTime() < DateTime.UtcNow - CacheTtl)
                    {
                        Logger.Info($"Cache expired (last updated: {cache.LastUpdated:u}); deleting.");
                        File.Delete(CachePath);
                        return null;
                    }

                    Logger.Info($"Cache loaded from {CachePath} (last updated: {cache.LastUpdated:u})");
                    return cache;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load cache: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Saves data to the cache file asynchronously.
    /// </summary>
    public static async Task SaveAsync(CachedData data)
    {
        try
        {
            var directory = Path.GetDirectoryName(CachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            data.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(CachePath, json).ConfigureAwait(false);
            Logger.Info($"Cache saved to {CachePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the cache file asynchronously.
    /// </summary>
    public static async Task ClearAsync()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                await Task.Run(() => File.Delete(CachePath)).ConfigureAwait(false);
                Logger.Info("Cache cleared");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to clear cache: {ex.Message}");
        }
    }

    // Backward-compatible synchronous wrappers.
    public static CachedData? Load() => LoadAsync().GetAwaiter().GetResult();
    public static void Save(CachedData data) => SaveAsync(data).GetAwaiter().GetResult();
    public static void Clear() => ClearAsync().GetAwaiter().GetResult();
}

/// <summary>
/// Container for all cached application data.
/// </summary>
public sealed class CachedData
{
    /// <summary>
    /// When this cache was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Cached password status information.
    /// </summary>
    [JsonPropertyName("passwordStatus")]
    public CachedPasswordStatus? PasswordStatus { get; set; }

    /// <summary>
    /// Cached user identity information.
    /// </summary>
    [JsonPropertyName("identity")]
    public CachedIdentity? Identity { get; set; }

    /// <summary>
    /// Cached tenant information.
    /// </summary>
    [JsonPropertyName("tenant")]
    public CachedTenant? Tenant { get; set; }

    /// <summary>
    /// Cached network information.
    /// </summary>
    [JsonPropertyName("network")]
    public CachedNetwork? Network { get; set; }
}

/// <summary>
/// Cached password status.
/// </summary>
public sealed class CachedPasswordStatus
{
    [JsonPropertyName("daysLeft")]
    public int? DaysLeft { get; set; }

    [JsonPropertyName("policyDays")]
    public int? PolicyDays { get; set; }

    [JsonPropertyName("lastChangedUtc")]
    public DateTime? LastChangedUtc { get; set; }

    [JsonPropertyName("neverExpires")]
    public bool NeverExpires { get; set; }
}

/// <summary>
/// Cached user identity.
/// </summary>
public sealed class CachedIdentity
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("upn")]
    public string? Upn { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// Cached tenant information.
/// </summary>
public sealed class CachedTenant
{
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("tenantName")]
    public string? TenantName { get; set; }

    [JsonPropertyName("domainName")]
    public string? DomainName { get; set; }

    [JsonPropertyName("joinType")]
    public string? JoinType { get; set; }

    [JsonPropertyName("azureAdJoined")]
    public bool AzureAdJoined { get; set; }
}

/// <summary>
/// Cached network information.
/// </summary>
public sealed class CachedNetwork
{
    [JsonPropertyName("computerName")]
    public string? ComputerName { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("macAddress")]
    public string? MacAddress { get; set; }

    [JsonPropertyName("adapterName")]
    public string? AdapterName { get; set; }

    [JsonPropertyName("gateway")]
    public string? Gateway { get; set; }

    [JsonPropertyName("dnsServers")]
    public string? DnsServers { get; set; }
}
