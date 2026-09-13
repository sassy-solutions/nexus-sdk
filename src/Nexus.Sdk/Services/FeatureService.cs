// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Models;

namespace Nexus.Sdk.Services;

/// <summary>
/// Feature configuration service with IMemoryCache-backed caching.
/// Falls back to API when cache misses, returns defaults when API unavailable.
/// </summary>
public sealed class FeatureService : IFeatureService
{
    private readonly INexusClient _client;
    private readonly IMemoryCache _cache;
    private readonly NexusOptions _options;
    private readonly ILogger<FeatureService> _logger;

    public FeatureService(
        INexusClient client,
        IMemoryCache cache,
        IOptions<NexusOptions> options,
        ILogger<FeatureService> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FeatureConfig?> GetConfigAsync(string featureKey, CancellationToken ct = default)
    {
        var cacheKey = $"nexus:feature:{featureKey}";

        if (_cache.TryGetValue(cacheKey, out FeatureConfig? cached))
            return cached;

        try
        {
            var config = await _client.GetFeatureAsync(featureKey, ct);
            if (config is not null)
            {
                _cache.Set(cacheKey, config, TimeSpan.FromSeconds(_options.FeatureCacheTtlSeconds));
                return config;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch feature config for {Key}", featureKey);
        }

        // Fallback: return a default config based on DefaultFeatureEnabled
        if (_options.DefaultFeatureEnabled)
        {
            return new FeatureConfig(featureKey, true, null, null, null);
        }

        return null;
    }

    public void SetBulk(Dictionary<string, FeatureConfig> features)
    {
        var expiration = TimeSpan.FromSeconds(_options.FeatureCacheTtlSeconds);
        foreach (var (key, config) in features)
        {
            _cache.Set($"nexus:feature:{key}", config, expiration);
        }

        _logger.LogInformation("Pre-populated cache with {Count} feature configs", features.Count);
    }
}
