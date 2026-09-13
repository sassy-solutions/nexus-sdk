// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Models;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Management.Services.Http;

/// <summary>HTTP-backed implementation of <see cref="IApplicationService"/>.</summary>
public sealed class HttpApplicationService : IApplicationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpApplicationService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpApplicationService"/> class.</summary>
    public HttpApplicationService(HttpClient httpClient, ILogger<HttpApplicationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<ApplicationDto>>> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? ownerOrganizationId = null,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        try
        {
            var url = NexusManagementRoutes.Applications + $"?page={page}&pageSize={pageSize}&includeArchived={includeArchived.ToString().ToLowerInvariant()}";
            if (!string.IsNullOrWhiteSpace(ownerOrganizationId))
            {
                url += $"&ownerOrganizationId={Uri.EscapeDataString(ownerOrganizationId)}";
            }

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<ApplicationDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListApplicationsResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<ApplicationDto> apps = payload?.Applications ?? Array.Empty<ApplicationDto>();
            return NexusResult<IReadOnlyList<ApplicationDto>>.Success(apps, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync failed");
            return NexusResult<IReadOnlyList<ApplicationDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ApplicationDetailDto>> GetAsync(string applicationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<ApplicationDetailDto>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(NexusRoutes.Of(NexusManagementRoutes.Application, applicationId), ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ApplicationDetailDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ApplicationDetailDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<ApplicationDetailDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<ApplicationDetailDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAsync({ApplicationId}) failed", applicationId);
            return NexusResult<ApplicationDetailDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<CreateApplicationResult>> CreateAsync(CreateApplicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(NexusManagementRoutes.Applications, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<CreateApplicationResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<CreateApplicationResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<CreateApplicationResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<CreateApplicationResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CreateAsync failed");
            return NexusResult<CreateApplicationResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> DeleteAsync(string applicationId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, NexusRoutes.Of(NexusManagementRoutes.Application, applicationId))
            {
                Content = JsonContent.Create(new ArchiveApplicationBody(reason), options: ManagementHttp.JsonOptions),
            };
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeleteAsync({ApplicationId}) failed", applicationId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> UpdateSettingsAsync(
        string organizationId,
        string applicationId,
        UpdateApplicationSettingsRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Org-scoped route (mirrors OrganizationApplicationsController) so the write
            // resolves under RLS for any authorized caller.
            var url =
                NexusRoutes.Of(NexusManagementRoutes.OrganizationApplicationSettings, organizationId, applicationId);

            using var response = await _httpClient
                .PutAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "UpdateSettingsAsync({ApplicationId}) failed", applicationId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<VersionDto>>> ListVersionsAsync(
        string applicationId,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<IReadOnlyList<VersionDto>>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationVersions, applicationId) + $"?skip={skip}&take={take}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<VersionDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ApplicationVersionsResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<VersionDto> versions = payload?.Versions ?? Array.Empty<VersionDto>();
            return NexusResult<IReadOnlyList<VersionDto>>.Success(versions, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListVersionsAsync({ApplicationId}) failed", applicationId);
            return NexusResult<IReadOnlyList<VersionDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<TagVersionResult>> TagVersionAsync(
        string applicationId,
        TagVersionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<TagVersionResult>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationVersions, applicationId);
            using var response = await _httpClient
                .PostAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<TagVersionResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<TagVersionResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<TagVersionResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<TagVersionResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TagVersionAsync({ApplicationId}) failed", applicationId);
            return NexusResult<TagVersionResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<VersionDeploymentResult>> DeployAsync(
        string applicationId,
        Guid environmentId,
        string tag,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(tag))
        {
            return NexusResult<VersionDeploymentResult>.Failure("applicationId and tag are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationVersionDeploy, applicationId, tag);
            using var response = await _httpClient
                .PostAsJsonAsync(url, new DeployVersionBody(environmentId), ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return await MapDeploymentResult(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeployAsync({ApplicationId}) failed", applicationId);
            return NexusResult<VersionDeploymentResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<VersionDecommissionResult>> DecommissionAsync(
        string applicationId,
        Guid environmentId,
        string tag,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(tag))
        {
            return NexusResult<VersionDecommissionResult>.Failure("applicationId and tag are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(
                NexusManagementRoutes.ApplicationVersionEnvironment,
                applicationId,
                tag,
                environmentId.ToString());
            using var response = await _httpClient.DeleteAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<VersionDecommissionResult>(response, ct).ConfigureAwait(false);
            }

            var result = await response.Content
                .ReadFromJsonAsync<VersionDecommissionResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return result is null
                ? NexusResult<VersionDecommissionResult>.Failure("Empty decommission response.", (int)HttpStatusCode.BadGateway)
                : NexusResult<VersionDecommissionResult>.Success(result, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DecommissionAsync({ApplicationId}) failed", applicationId);
            return NexusResult<VersionDecommissionResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<VersionDeploymentResult>> PromoteAsync(
        string applicationId,
        PromoteVersionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<VersionDeploymentResult>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationPromote, applicationId);
            using var response = await _httpClient
                .PostAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return await MapDeploymentResult(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PromoteAsync({ApplicationId}) failed", applicationId);
            return NexusResult<VersionDeploymentResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<ApplicationAuditLogDto>> GetAuditLogAsync(
        string applicationId,
        int skip = 0,
        int take = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<ApplicationAuditLogDto>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationAuditLog, applicationId) + $"?skip={skip}&take={take}";
            if (from.HasValue)
            {
                url += $"&from={Uri.EscapeDataString(from.Value.ToString("O", CultureInfo.InvariantCulture))}";
            }

            if (to.HasValue)
            {
                url += $"&to={Uri.EscapeDataString(to.Value.ToString("O", CultureInfo.InvariantCulture))}";
            }

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<ApplicationAuditLogDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<AuditLogResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return NexusResult<ApplicationAuditLogDto>.Failure("Empty response body.", (int)response.StatusCode);
            }

            var dto = new ApplicationAuditLogDto(
                payload.ApplicationId ?? applicationId,
                payload.Entries ?? Array.Empty<AuditEntryDto>(),
                payload.TotalCount);
            return NexusResult<ApplicationAuditLogDto>.Success(dto, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAuditLogAsync({ApplicationId}) failed", applicationId);
            return NexusResult<ApplicationAuditLogDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<CommitDto>>> ListRepositoryCommitsAsync(
        string applicationId,
        string? branch = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<IReadOnlyList<CommitDto>>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationRepositoryCommits, applicationId) + $"?limit={limit}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                url += $"&branch={Uri.EscapeDataString(branch)}";
            }

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<CommitDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ApplicationCommitsResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<CommitDto> commits = payload?.Commits ?? Array.Empty<CommitDto>();
            return NexusResult<IReadOnlyList<CommitDto>>.Success(commits, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListRepositoryCommitsAsync({ApplicationId}) failed", applicationId);
            return NexusResult<IReadOnlyList<CommitDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<BranchDto>>> ListRepositoryBranchesAsync(
        string applicationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<IReadOnlyList<BranchDto>>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationRepositoryBranches, applicationId);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<BranchDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ApplicationBranchesResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<BranchDto> branches = payload?.Branches ?? Array.Empty<BranchDto>();
            return NexusResult<IReadOnlyList<BranchDto>>.Success(branches, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListRepositoryBranchesAsync({ApplicationId}) failed", applicationId);
            return NexusResult<IReadOnlyList<BranchDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<BuildAndDeployFromCommitResult>> DeployFromCommitAsync(
        string applicationId,
        BuildAndDeployFromCommitRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<BuildAndDeployFromCommitResult>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationDeployFromCommit, applicationId);
            using var response = await _httpClient
                .PostAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<BuildAndDeployFromCommitResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<BuildAndDeployFromCommitResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<BuildAndDeployFromCommitResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<BuildAndDeployFromCommitResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeployFromCommitAsync({ApplicationId}) failed", applicationId);
            return NexusResult<BuildAndDeployFromCommitResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<BuildDeployStatusDto>> GetBuildDeployStatusAsync(
        string applicationId,
        string buildDeployId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return NexusResult<BuildDeployStatusDto>.Failure("applicationId is required.", (int)HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(buildDeployId))
        {
            return NexusResult<BuildDeployStatusDto>.Failure("buildDeployId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.ApplicationBuildDeploy, applicationId, buildDeployId);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<BuildDeployStatusDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<BuildDeployStatusDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<BuildDeployStatusDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<BuildDeployStatusDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetBuildDeployStatusAsync({ApplicationId}) failed", applicationId);
            return NexusResult<BuildDeployStatusDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private static async Task<NexusResult<VersionDeploymentResult>> MapDeploymentResult(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return await ManagementHttp.ToFailure<VersionDeploymentResult>(response, ct).ConfigureAwait(false);
        }

        var payload = await response.Content
            .ReadFromJsonAsync<VersionDeploymentResult>(ManagementHttp.JsonOptions, ct)
            .ConfigureAwait(false);

        return payload is null
            ? NexusResult<VersionDeploymentResult>.Failure("Empty response body.", (int)response.StatusCode)
            : NexusResult<VersionDeploymentResult>.Success(payload, (int)response.StatusCode);
    }

    private sealed record ListApplicationsResponse(ApplicationDto[]? Applications, int TotalCount, int Page, int PageSize, bool HasMore);

    private sealed record ApplicationVersionsResponse(string? ApplicationId, VersionDto[]? Versions);

    private sealed record AuditLogResponse(string? ApplicationId, AuditEntryDto[]? Entries, int TotalCount);

    private sealed record ApplicationCommitsResponse(string? ApplicationId, CommitDto[]? Commits);

    private sealed record ApplicationBranchesResponse(string? ApplicationId, BranchDto[]? Branches);

    private sealed record DeployVersionBody(Guid EnvironmentId);

    private sealed record ArchiveApplicationBody(string Reason);
}
