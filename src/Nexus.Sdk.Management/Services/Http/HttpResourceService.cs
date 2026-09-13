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

/// <summary>HTTP-backed implementation of <see cref="IResourceService"/>.</summary>
public sealed class HttpResourceService : IResourceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpResourceService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpResourceService"/> class.</summary>
    public HttpResourceService(HttpClient httpClient, ILogger<HttpResourceService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<ResourceOffering>>> ListOfferingsAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(NexusManagementRoutes.ResourceOfferings, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<ResourceOffering>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceOffering[]>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<ResourceOffering> offerings = payload ?? Array.Empty<ResourceOffering>();
            return NexusResult<IReadOnlyList<ResourceOffering>>.Success(offerings, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListOfferingsAsync (resources) failed");
            return NexusResult<IReadOnlyList<ResourceOffering>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<ResourceSummaryDto>>> ListAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<ResourceSummaryDto>>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient.GetAsync(ResourcesRoute(organizationId), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<ResourceSummaryDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceSummaryDto[]>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<ResourceSummaryDto> resources = payload ?? Array.Empty<ResourceSummaryDto>();
            return NexusResult<IReadOnlyList<ResourceSummaryDto>>.Success(resources, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync (resources) failed for org {OrganizationId}", organizationId);
            return NexusResult<IReadOnlyList<ResourceSummaryDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ResourceDto>> GetAsync(
        string organizationId,
        string resourceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<ResourceDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return NexusResult<ResourceDto>.Failure("resourceId is required.", (int)HttpStatusCode.BadRequest);
        }

        return await GetJson<ResourceDto>(
            NexusRoutes.Of(NexusManagementRoutes.OrganizationResource, organizationId, resourceId), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ResourceDto>> ProvisionAsync(
        string organizationId,
        ProvisionResourceRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<ResourceDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Kind))
        {
            return NexusResult<ResourceDto>.Failure("Kind is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return NexusResult<ResourceDto>.Failure("Name is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(ResourcesRoute(organizationId), request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ResourceDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ResourceDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ResourceDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ProvisionAsync (resources) failed for org {OrganizationId}", organizationId);
            return NexusResult<ResourceDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ResourceDto>> RefreshStatusAsync(
        string organizationId,
        string resourceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<ResourceDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return NexusResult<ResourceDto>.Failure("resourceId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsync(
                    NexusRoutes.Of(NexusManagementRoutes.OrganizationResourceRefreshStatus, organizationId, resourceId),
                    content: null,
                    ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ResourceDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ResourceDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ResourceDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RefreshStatusAsync (resources) failed for resource {ResourceId}", resourceId);
            return NexusResult<ResourceDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ResourceDto>> BindAsync(
        string applicationId,
        string resourceId,
        string organizationId,
        Guid? environmentId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<ResourceDto>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return NexusResult<ResourceDto>.Failure("resourceId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<ResourceDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(
                    BindingsRoute(applicationId, resourceId),
                    new BindResourceBody(organizationId, environmentId),
                    ManagementHttp.JsonOptions,
                    ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ResourceDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ResourceDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ResourceDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BindAsync (resources) failed for app {ApplicationId} resource {ResourceId}", applicationId, resourceId);
            return NexusResult<ResourceDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ResourceDto>> UnbindAsync(
        string applicationId,
        string resourceId,
        string organizationId,
        Guid? environmentId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<ResourceDto>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return NexusResult<ResourceDto>.Failure("resourceId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<ResourceDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        var route = NexusRoutes.Of(NexusManagementRoutes.ApplicationResourceBindings, applicationId, resourceId) + $"?orgId={Uri.EscapeDataString(organizationId)}";
        if (environmentId is { } envId)
        {
            route += $"&environmentId={envId}";
        }

        try
        {
            using var response = await _httpClient.DeleteAsync(route, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ResourceDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ResourceDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ResourceDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "UnbindAsync (resources) failed for app {ApplicationId} resource {ResourceId}", applicationId, resourceId);
            return NexusResult<ResourceDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<ResourceBindingDto>>> ListApplicationResourcesAsync(
        string applicationId,
        string organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<IReadOnlyList<ResourceBindingDto>>.Failure(
                "applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<ResourceBindingDto>>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        var route = NexusRoutes.Of(NexusManagementRoutes.ApplicationResources, applicationId) + $"?orgId={Uri.EscapeDataString(organizationId)}";

        try
        {
            using var response = await _httpClient.GetAsync(route, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp
                    .ToFailure<IReadOnlyList<ResourceBindingDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ResourceBindingDto[]>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<ResourceBindingDto> bindings = payload ?? Array.Empty<ResourceBindingDto>();
            return NexusResult<IReadOnlyList<ResourceBindingDto>>.Success(bindings, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListApplicationResourcesAsync (resources) failed for app {ApplicationId}", applicationId);
            return NexusResult<IReadOnlyList<ResourceBindingDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private static string ResourcesRoute(string organizationId) =>
        NexusRoutes.Of(NexusManagementRoutes.OrganizationResources, organizationId);

    private static string BindingsRoute(string applicationId, string resourceId) =>
        NexusRoutes.Of(NexusManagementRoutes.ApplicationResourceBindings, applicationId, resourceId);

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
            _logger.LogWarning(ex, "GET {Route} (resources) failed", route);
            return NexusResult<T>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    // Wire body mirroring the API's BindResourceRequest.
    private sealed record BindResourceBody(string OrganizationId, Guid? EnvironmentId);
}
