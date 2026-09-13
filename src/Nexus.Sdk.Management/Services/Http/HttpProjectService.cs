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

/// <summary>HTTP-backed implementation of <see cref="IProjectService"/>.</summary>
public sealed class HttpProjectService : IProjectService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProjectService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpProjectService"/> class.</summary>
    public HttpProjectService(HttpClient httpClient, ILogger<HttpProjectService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<ProjectDto>>> ListAsync(
        string organizationId,
        int page = 1,
        int pageSize = 20,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<ProjectDto>>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationProjects, organizationId) +
                      $"?page={page}&pageSize={pageSize}&includeArchived={includeArchived.ToString().ToLowerInvariant()}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<ProjectDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListProjectsResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<ProjectDto> projects = payload?.Projects ?? Array.Empty<ProjectDto>();
            return NexusResult<IReadOnlyList<ProjectDto>>.Success(projects, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync({OrganizationId}) failed", organizationId);
            return NexusResult<IReadOnlyList<ProjectDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ProjectDetailDto>> GetAsync(string organizationId, string projectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(projectId))
        {
            return NexusResult<ProjectDetailDto>.Failure("organizationId and projectId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationProject, organizationId, projectId);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ProjectDetailDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ProjectDetailDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ProjectDetailDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ProjectDetailDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAsync({ProjectId}) failed", projectId);
            return NexusResult<ProjectDetailDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<CreateProjectResult>> CreateAsync(
        string organizationId,
        CreateProjectRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<CreateProjectResult>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationProjects, organizationId);
            using var response = await _httpClient
                .PostAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<CreateProjectResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<CreateProjectResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<CreateProjectResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<CreateProjectResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CreateAsync({OrganizationId}) failed", organizationId);
            return NexusResult<CreateProjectResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> DeleteAsync(
        string organizationId,
        string projectId,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(projectId))
        {
            return NexusResult.Failure("organizationId and projectId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationProject, organizationId, projectId);
            using var request = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = JsonContent.Create(new ArchiveProjectBody(reason), options: ManagementHttp.JsonOptions),
            };
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeleteAsync({ProjectId}) failed", projectId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed record ListProjectsResponse(ProjectDto[]? Projects, int TotalCount, int Page, int PageSize, bool HasMore);

    private sealed record ArchiveProjectBody(string Reason);
}
