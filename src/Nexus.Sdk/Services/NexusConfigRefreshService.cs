// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Services;

/// <summary>
/// The background service that keeps <see cref="NexusConfigSnapshotStore"/> fresh. It performs a
/// blocking initial load at startup (so the snapshot exists before the first request), then polls
/// <c>GET api/v2/sdk/config</c> every <see cref="NexusOptions.ConfigRefreshSeconds"/> with a conditional
/// <c>If-None-Match</c> (a no-change poll is a cheap 304, no swap). It is the ONLY writer of the store,
/// publishes each swap to the pre-host <c>IConfiguration</c> providers, and publishes the ambient
/// <see cref="NexusConfig"/> accessor.
/// </summary>
public sealed class NexusConfigRefreshService : BackgroundService
{
    /// <summary>The named <see cref="HttpClient"/> (API key + context handlers + resilience) used to poll.</summary>
    public const string HttpClientName = "NexusConfig";

    private static readonly TimeSpan InitialLoadBudget = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NexusConfigSnapshotStore _store;
    private readonly INexusConfig _config;
    private readonly NexusOptions _options;
    private readonly ILogger<NexusConfigRefreshService> _logger;

    private bool _lastPollFailed;
    private bool _apiKeyWarningLogged;

    public NexusConfigRefreshService(
        IHttpClientFactory httpClientFactory,
        NexusConfigSnapshotStore store,
        INexusConfig config,
        IOptions<NexusOptions> options,
        ILogger<NexusConfigRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Publish the ambient accessor early so NexusConfig.Config(...) works even if the initial load
        // times out (it will then read the empty snapshot + process environment).
        NexusConfig.SetAccessor(_config);

        // Initial load with a bounded budget so a slow/unreachable Nexus cannot hang host startup.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(InitialLoadBudget);
        try
        {
            await LoadOnceAsync(budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Nexus initial config load exceeded its {Budget}s budget; continuing with the current snapshot.", InitialLoadBudget.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Fail-soft: the initial load never stops the host by itself. Only the required-key gate
            // below (fail-closed, opt-in) does.
            LogPollFailure(ex);
        }

        // Fail-closed: required keys unmet after the first load stop the host (same intent as
        // ValidateOnStart). Nexus being down at boot is exactly the case this guards.
        EnforceRequiredKeys();

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _options.ConfigRefreshSeconds));
        using var timer = new PeriodicTimer(period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await LoadOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogPollFailure(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
    }

    /// <summary>
    /// Fetches the manifest once (conditional on the current ETag) and swaps the store on change. A 304
    /// leaves the snapshot untouched. Throws only <see cref="OperationCanceledException"/> (budget /
    /// shutdown); other failures are logged and swallowed (fail-soft — the required-key gate is the
    /// fail-closed path).
    /// </summary>
    private async Task LoadOnceAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            if (!_apiKeyWarningLogged)
            {
                _apiKeyWarningLogged = true;
                _logger.LogWarning("Nexus API key not configured; the config snapshot stays empty (process environment variables still resolve).");
            }

            return;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, NexusRoutes.SdkConfig);
        var currentEtag = _store.Current.ETag;
        if (!string.IsNullOrEmpty(currentEtag))
        {
            // Send verbatim what the server gave us; TryAddWithoutValidation avoids ETag parse quirks.
            request.Headers.TryAddWithoutValidation("If-None-Match", currentEtag);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPollFailure(ex);
            return;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _lastPollFailed = false;
                return; // Unchanged — keep the current snapshot.
            }

            if (!response.IsSuccessStatusCode)
            {
                LogPollFailure(new HttpRequestException($"GET api/v2/sdk/config returned {(int)response.StatusCode}."));
                return;
            }

            var manifest = await response.Content
                .ReadFromJsonAsync<NexusConfigManifestDto>(cancellationToken)
                .ConfigureAwait(false);
            if (manifest is null)
            {
                LogPollFailure(new InvalidOperationException("GET api/v2/sdk/config returned an unparseable body."));
                return;
            }

            SwapFromManifest(manifest, response);
            _lastPollFailed = false;
        }
    }

    private void SwapFromManifest(NexusConfigManifestDto manifest, HttpResponseMessage response)
    {
        var entries = new Dictionary<string, ConfigEntrySnapshot>(StringComparer.Ordinal);
        foreach (var e in manifest.Entries ?? [])
        {
            entries[e.Key] = new ConfigEntrySnapshot(e.Key, e.DeliveryMode, e.IsSecret, e.Value, e.ValueVersion, e.Source);
        }

        var etag = manifest.ETag ?? response.Headers.ETag?.ToString();
        var snapshot = new ConfigSnapshot(entries, etag, manifest.ResolvedAt == default ? DateTimeOffset.UtcNow : manifest.ResolvedAt);

        _store.Swap(snapshot);
        NexusConfigProviderBridge.Publish(snapshot);

        _logger.LogInformation("Nexus config snapshot refreshed: {Count} entries (etag {ETag}).", entries.Count, etag);
    }

    private void EnforceRequiredKeys()
    {
        if (_options.RequiredKeys.Length == 0)
        {
            return;
        }

        var missing = _options.RequiredKeys.Where(key => _config.Get(key) is null).ToArray();
        if (missing.Length > 0)
        {
            throw new NexusConfigUnavailableException(missing);
        }
    }

    private void LogPollFailure(Exception ex)
    {
        if (_lastPollFailed)
        {
            _logger.LogDebug(ex, "Nexus config poll still failing.");
            return;
        }

        _lastPollFailed = true;
        _logger.LogWarning(ex, "Nexus config poll failed; keeping the current snapshot until the next successful refresh.");
    }
}
