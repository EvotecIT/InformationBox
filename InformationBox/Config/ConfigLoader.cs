using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InformationBox.Config;

/// <summary>
/// Loads configuration from embedded defaults and optional override files.
/// </summary>
public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IReadOnlyList<string> _candidatePaths;

    /// <summary>
    /// Initializes a new loader with the ordered list of override paths to probe.
    /// </summary>
    /// <param name="candidatePaths">Files to check after loading embedded defaults.</param>
    public ConfigLoader(IEnumerable<string> candidatePaths)
    {
        _candidatePaths = candidatePaths.ToArray();
    }

    /// <summary>
    /// Attempts to load configuration, applying the first readable override file if present.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel disk or JSON IO.</param>
    /// <returns>The effective configuration along with the source it was loaded from.</returns>
    public async Task<ConfigResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var embedded = await ReadEmbeddedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var path in _candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                await using var stream = File.OpenRead(path);
                var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (config is not null)
                {
                    return new ConfigResult(config, path);
                }
            }
            catch (Exception ex) when (IsJsonIssue(ex))
            {
                // Ignore malformed override and fall back to next candidate.
            }
        }

        return new ConfigResult(embedded, "embedded-default");
    }

    private static bool IsJsonIssue(Exception ex) =>
        ex is JsonException or NotSupportedException or ArgumentException;

    private static async Task<AppConfig> ReadEmbeddedAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "InformationBox.Assets.config.default.json";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException("Embedded config not found", resourceName);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return config ?? new AppConfig();
    }

    /// <summary>
    /// Returns the default config override search order, optionally prefixed with an explicit path.
    /// </summary>
    /// <param name="explicitPath">A user-specified config file to consider first.</param>
    public static IEnumerable<string> DefaultCandidatePaths(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath!;
        }

        var programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InformationBox", "config.json");
        yield return programData;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InformationBox", "config.json");
        yield return appData;
    }
}

/// <summary>
/// Result of loading configuration.
/// </summary>
/// <param name="Config">The parsed application configuration.</param>
/// <param name="Source">Where the configuration was loaded from (path or identifier).</param>
public sealed record ConfigResult(AppConfig Config, string Source);
