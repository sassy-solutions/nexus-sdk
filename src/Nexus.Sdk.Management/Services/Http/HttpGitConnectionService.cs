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
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Models;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Management.Services.Http;

/// <summary>HTTP-backed implementation of <see cref="IGitConnectionService"/>.</summary>
public sealed class HttpGitConnectionService : IGitConnectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpGitConnectionService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpGitConnectionService"/> class.</summary>
    public HttpGitConnectionService(HttpClient httpClient, ILogger<HttpGitConnectionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<GitConnection>>> ListAsync(
        string organizationId,
        bool includeDisconnected = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<GitConnection>>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        var route = ConnectionsRoute(organizationId);
        if (includeDisconnected)
        {
            route += "?includeDisconnected=true";
        }

        try
        {
            using var response = await _httpClient.GetAsync(route, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<GitConnection>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListConnectionsEnvelope>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<GitConnection> connections = payload?.Connections ?? Array.Empty<GitConnection>();
            return NexusResult<IReadOnlyList<GitConnection>>.Success(connections, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync (git-connections) failed for org {OrganizationId}", organizationId);
            return NexusResult<IReadOnlyList<GitConnection>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<GitConnection>> GetAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<GitConnection>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return NexusResult<GitConnection>.Failure("connectionId is required.", (int)HttpStatusCode.BadRequest);
        }

        return await GetJson<GitConnection>(
            NexusRoutes.Of(NexusManagementRoutes.OrganizationGitConnection, organizationId, connectionId), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NexusResult<GitInstallUrl>> GetInstallUrlAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<GitInstallUrl>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsync(NexusRoutes.Of(NexusManagementRoutes.OrganizationGitConnectionsGitHubInstallUrl, organizationId), content: null, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<GitInstallUrl>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitInstallUrl>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<GitInstallUrl>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<GitInstallUrl>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetInstallUrlAsync (git-connections) failed for org {OrganizationId}", organizationId);
            return NexusResult<GitInstallUrl>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<GitConnection>> CompleteGitHubSetupAsync(
        CompleteGitHubSetupInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.InstallationId))
        {
            return NexusResult<GitConnection>.Failure("installationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(input.State))
        {
            return NexusResult<GitConnection>.Failure("state is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(NexusManagementRoutes.GitGitHubSetupComplete, input, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<GitConnection>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitConnection>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<GitConnection>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<GitConnection>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CompleteGitHubSetupAsync (git-connections) failed");
            return NexusResult<GitConnection>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> DisconnectAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return NexusResult.Failure("connectionId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .DeleteAsync(NexusRoutes.Of(NexusManagementRoutes.OrganizationGitConnection, organizationId, connectionId), ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
            }

            return NexusResult.Success((int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DisconnectAsync (git-connections) failed for connection {ConnectionId}", connectionId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<GitConnectionRepository>>> ListRepositoriesAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<GitConnectionRepository>>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return NexusResult<IReadOnlyList<GitConnectionRepository>>.Failure(
                "connectionId is required.", (int)HttpStatusCode.BadRequest);
        }

        var route = NexusRoutes.Of(NexusManagementRoutes.OrganizationGitConnectionRepositories, organizationId, connectionId);

        try
        {
            using var response = await _httpClient.GetAsync(route, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp
                    .ToFailure<IReadOnlyList<GitConnectionRepository>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListRepositoriesEnvelope>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<GitConnectionRepository> repositories =
                payload?.Repositories ?? Array.Empty<GitConnectionRepository>();
            return NexusResult<IReadOnlyList<GitConnectionRepository>>.Success(repositories, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListRepositoriesAsync (git-connections) failed for connection {ConnectionId}", connectionId);
            return NexusResult<IReadOnlyList<GitConnectionRepository>>.Failure(
                ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<CreatedGitNamespace>> CreateGithubOrganizationAsync(
        string organizationId,
        CreateGithubOrganizationInput input,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<CreatedGitNamespace>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return NexusResult<CreatedGitNamespace>.Failure(
                "name is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(GithubOrganizationsRoute(organizationId), input, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<CreatedGitNamespace>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<CreatedGitNamespace>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<CreatedGitNamespace>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<CreatedGitNamespace>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CreateGithubOrganizationAsync (git-connections) failed for org {OrganizationId}", organizationId);
            return NexusResult<CreatedGitNamespace>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public Task<NexusResult<GitCapabilities>> GetCapabilitiesAsync(CancellationToken ct = default) =>
        GetJson<GitCapabilities>(NexusManagementRoutes.GitCapabilities, ct);

    /// <inheritdoc/>
    public async Task<NexusResult<GitReconcileReport>> ReconcileAsync(
        bool dryRun = true,
        IReadOnlyList<string>? connectionIds = null,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(
                    NexusManagementRoutes.PlatformGitReconcile,
                    new ReconcileRequestBody(dryRun, connectionIds),
                    ManagementHttp.JsonOptions,
                    ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<GitReconcileReport>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitReconcileReport>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<GitReconcileReport>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<GitReconcileReport>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ReconcileAsync (git-connections) failed (dryRun={DryRun})", dryRun);
            return NexusResult<GitReconcileReport>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private static string ConnectionsRoute(string organizationId) =>
        NexusRoutes.Of(NexusManagementRoutes.OrganizationGitConnections, organizationId);

    private static string GithubOrganizationsRoute(string organizationId) =>
        NexusRoutes.Of(NexusManagementRoutes.OrganizationGitHubOrganizations, organizationId);

    private async Task<NexusResult<T>> GetJson<T>(string route, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(route, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<T>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<T>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<T>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<T>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GET {Route} (git-connections) failed", route);
            return NexusResult<T>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed record ListConnectionsEnvelope(IReadOnlyList<GitConnection>? Connections);

    private sealed record ListRepositoriesEnvelope(IReadOnlyList<GitConnectionRepository>? Repositories);

    private sealed record ReconcileRequestBody(bool DryRun, IReadOnlyList<string>? ConnectionIds);
}
