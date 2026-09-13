// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Models;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Client;

/// <summary>
/// Typed HttpClient implementation for the Nexus tenant API.
/// </summary>
public sealed class NexusClient : INexusClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NexusClient> _logger;

    public NexusClient(HttpClient httpClient, ILogger<NexusClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FeatureConfig?> GetFeatureAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(NexusRoutes.Of(NexusRoutes.SdkFeature, key), ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get feature {Key}: {Status}", key, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<FeatureConfig>(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get feature {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool?> CheckFeatureAsync(string key, NexusSubject subject, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.Tier))
            query.Add($"tier={Uri.EscapeDataString(subject.Tier)}");
        if (!string.IsNullOrWhiteSpace(subject.EndUserId))
            query.Add($"endUserId={Uri.EscapeDataString(subject.EndUserId)}");
        if (subject.Attributes is { Count: > 0 })
        {
            foreach (var kvp in subject.Attributes)
            {
                // ASP.NET binds a Dictionary<string,string> from attributes[name]=value.
                query.Add(
                    $"attributes[{Uri.EscapeDataString(kvp.Key)}]={Uri.EscapeDataString(kvp.Value)}");
            }
        }

        if (!string.IsNullOrWhiteSpace(subject.Environment))
            query.Add($"environment={Uri.EscapeDataString(subject.Environment)}");
        if (!string.IsNullOrWhiteSpace(subject.Version))
            query.Add($"version={Uri.EscapeDataString(subject.Version)}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

        try
        {
            var response = await _httpClient.GetAsync(
                NexusRoutes.Of(NexusRoutes.FeatureFlagCheck, key) + qs, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Per-subject feature check for {Key} returned {Status}", key, (int)response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<FeatureCheckResponse>(ct);
            return result?.IsEnabled;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Per-subject feature check failed for {Key}", key);
            return null;
        }
    }

    private sealed record FeatureCheckResponse(string Key, bool IsEnabled, string ResolvedBy);

    public async Task<NexusResult> TrackAsync(string eventName, Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        try
        {
            var request = new { Event = eventName, Metadata = metadata };
            var response = await _httpClient.PostAsJsonAsync(NexusRoutes.SdkTrack, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Nexus track failed: {Status} {Error}", (int)response.StatusCode, error);
                return NexusResult.Failure(error, (int)response.StatusCode);
            }

            return NexusResult.Success((int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to track event {Event}", eventName);
            return NexusResult.Failure(ex.Message, 0);
        }
    }

    public async Task<NexusResult> BillAsync(string featureKey, int units = 1, CancellationToken ct = default)
    {
        try
        {
            var request = new { FeatureKey = featureKey, Units = units };
            var response = await _httpClient.PostAsJsonAsync(NexusRoutes.SdkBill, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Nexus bill failed: {Status} {Error}", (int)response.StatusCode, error);
                return NexusResult.Failure(error, (int)response.StatusCode);
            }

            return NexusResult.Success((int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to bill feature {Key}", featureKey);
            return NexusResult.Failure(ex.Message, 0);
        }
    }

    /// <inheritdoc />
    public async Task<NexusPermissionDecision> CheckPermissionAsync(string permission, CancellationToken ct = default)
    {
        try
        {
            var request = new { Permission = permission };
            var response = await _httpClient.PostAsJsonAsync(NexusRoutes.SdkPermissionsCheck, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // 401/403 are answers: the platform looked at this principal and refused it.
                // Everything else — 5xx, 404, a gateway page — means the question was never
                // put to the authorization logic, and reporting that as "denied" is the lie
                // removes.
                var status = (int)response.StatusCode;
                if (status is 401 or 403)
                {
                    return NexusPermissionDecision.Denied;
                }

                _logger.LogWarning(
                    "Permission check for {Permission} could not be answered: HTTP {Status}",
                    permission,
                    status);
                return NexusPermissionDecision.Indeterminate;
            }

            var result = await response.Content.ReadFromJsonAsync<PermissionCheckResponse>(ct);
            if (result is null)
            {
                // 2xx with a body we cannot read is not a verdict either.
                _logger.LogWarning(
                    "Permission check for {Permission} returned an unreadable body", permission);
                return NexusPermissionDecision.Indeterminate;
            }

            return result.Allowed ? NexusPermissionDecision.Allowed : NexusPermissionDecision.Denied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to check permission {Permission}", permission);
            return NexusPermissionDecision.Indeterminate;
        }
    }

    public async Task<SdkRegistrationResponse?> RegisterAsync(SdkRegistrationRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(NexusRoutes.SdkRegister, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("SDK registration failed: {Status} {Error}", (int)response.StatusCode, error);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SdkRegistrationResponse>(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to register with Nexus");
            return null;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(NexusRoutes.Health, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed record PermissionCheckResponse(bool Allowed);
}
