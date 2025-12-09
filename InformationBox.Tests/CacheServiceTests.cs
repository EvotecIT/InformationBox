using System;
using System.IO;
using System.Threading.Tasks;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class CacheServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsData_WithCustomPath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ibx_cache_{Guid.NewGuid():N}.json");
        using var scope = CacheService.UseCustomCachePath(tempPath);

        var data = new CachedData
        {
            LastUpdated = DateTime.UtcNow,
            Identity = new CachedIdentity { DisplayName = "Test User", Email = "test@example.com" }
        };

        await CacheService.SaveAsync(data);
        var loaded = await CacheService.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Test User", loaded!.Identity?.DisplayName);
    }

    [Fact]
    public async Task ExpiredCache_IsDeleted_WithCustomPath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ibx_cache_{Guid.NewGuid():N}.json");
        using var scope = CacheService.UseCustomCachePath(tempPath);

        var data = new CachedData
        {
            LastUpdated = DateTime.UtcNow,
            Identity = new CachedIdentity { DisplayName = "Old User" }
        };

        await CacheService.SaveAsync(data);

        // Rewrite file with an expired timestamp to simulate stale cache
        var expired = new CachedData
        {
            LastUpdated = DateTime.UtcNow.AddDays(-8),
            Identity = data.Identity
        };
        var json = System.Text.Json.JsonSerializer.Serialize(expired, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(tempPath, json);

        var loaded = await CacheService.LoadAsync();

        Assert.Null(loaded);
        Assert.False(File.Exists(tempPath));
    }
}
