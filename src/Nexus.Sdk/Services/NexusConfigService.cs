// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Services;

/// <summary>
/// The <see cref="INexusConfig"/> implementation. Reads from the in-memory
/// <see cref="NexusConfigSnapshotStore"/> (kept fresh by <see cref="NexusConfigRefreshService"/>) fused
/// with the process environment — env FIRST by default. No per-call HTTP, no <c>IMemoryCache</c>, and
/// crucially no sync-over-async: the synchronous <see cref="Get"/> path is pure in-memory lookups.
/// </summary>
public sealed class NexusConfigService : INexusConfig
{
    private readonly NexusConfigSnapshotStore _store;
    private readonly NexusOptions _options;
    private readonly ILogger<NexusConfigService> _logger;

    // MountedSecret keys that resolved to null off-cluster: warn once per key, not per read (log spam).
    private readonly ConcurrentDictionary<string, byte> _warnedMountedSecretKeys = new(StringComparer.Ordinal);

    // The manifest delivery-mode string for a the orchestrator-Secret-delivered entry (its value is never on
    // the SDK channel). Kept as a literal so the client SDK stays free of a Nexus.Core dependency.
    private const string MountedSecretDeliveryMode = "K8sSecret";

    public NexusConfigService(
        NexusConfigSnapshotStore store,
        IOptions<NexusOptions> options,
        ILogger<NexusConfigService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        // 1. Process environment wins by default (docker-compose / kubectl set env / EnvVar+MountedSecret
        //    delivery all surface here) — opt out with NexusOptions.DisableEnvironmentOverride.
        if (!_options.DisableEnvironmentOverride)
        {
            var fromEnv = ResolveFromEnvironment(key);
            if (fromEnv is not null)
            {
                return fromEnv;
            }
        }

        // 2. Remote snapshot.
        var entry = _store.Get(key);
        if (entry is null)
        {
            return null;
        }

        if (entry.Value is not null)
        {
            return entry.Value;
        }

        // A MountedSecret entry never carries its value on the SDK channel: the manifest value is null by
        // design and the real value arrives via the workload env (handled by step 1). If we reach here the
        // process is running off-cluster (or the key is not mounted) — surface a one-time, explicit hint
        // rather than a silent null.
        if (string.Equals(entry.DeliveryMode, MountedSecretDeliveryMode, StringComparison.Ordinal)
            && _warnedMountedSecretKeys.TryAdd(key, 0))
        {
            _logger.LogWarning(
                "Config key '{Key}' is delivered via a platform-mounted secret; its value is not exposed over the SDK channel. Run in-platform (so it is mounted into the process environment) or switch its delivery mode to RuntimePull.",
                key);
        }

        return null;
    }

    /// <inheritdoc />
    public string GetRequired(string key) =>
        Get(key) ?? throw new NexusConfigKeyMissingException(key);

    /// <inheritdoc />
    public IChangeToken Watch() => _store.GetReloadToken();

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Get(key));

    /// <inheritdoc />
    public Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Snapshot values first (MountedSecret entries have a null value and are skipped).
        foreach (var (key, entry) in _store.GetAll())
        {
            if (entry.Value is not null)
            {
                result[key] = entry.Value;
            }
        }

        // Process-environment overrides layered on top when they win.
        if (!_options.DisableEnvironmentOverride)
        {
            foreach (var key in result.Keys.ToArray())
            {
                var fromEnv = ResolveFromEnvironment(key);
                if (fromEnv is not null)
                {
                    result[key] = fromEnv;
                }
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = Get(key);
        if (value is null)
        {
            return Task.FromResult<T?>(null);
        }

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(value));
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to deserialize config key {Key} as {Type}", key, typeof(T).Name);
            return Task.FromResult<T?>(null);
        }
    }

    /// <summary>
    /// Reads a process environment variable for <paramref name="key"/>, trying the exact key first then
    /// the <c>:</c>→<c>__</c> form (so <c>ConnectionStrings:Default</c> also matches the
    /// <c>ConnectionStrings__Default</c> pod env var).
    /// </summary>
    private static string? ResolveFromEnvironment(string key)
    {
        var exact = Environment.GetEnvironmentVariable(key);
        if (exact is not null)
        {
            return exact;
        }

        if (key.Contains(':', StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(key.Replace(":", "__", StringComparison.Ordinal));
        }

        return null;
    }
}
