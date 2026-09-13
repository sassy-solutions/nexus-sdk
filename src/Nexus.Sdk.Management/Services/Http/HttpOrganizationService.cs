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

/// <summary>HTTP-backed implementation of <see cref="IOrganizationService"/>.</summary>
public sealed class HttpOrganizationService : IOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpOrganizationService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpOrganizationService"/> class.</summary>
    public HttpOrganizationService(HttpClient httpClient, ILogger<HttpOrganizationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<MyOrganizationDto>>> ListMineAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(NexusManagementRoutes.MyOrganizations, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<MyOrganizationDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListMyOrganizationsResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<MyOrganizationDto> orgs = payload?.Organizations ?? Array.Empty<MyOrganizationDto>();
            return NexusResult<IReadOnlyList<MyOrganizationDto>>.Success(orgs, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListMineAsync failed");
            return NexusResult<IReadOnlyList<MyOrganizationDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<MyOrganizationDto>> GetCurrentAsync(CancellationToken ct = default)
    {
        var mine = await ListMineAsync(ct).ConfigureAwait(false);
        if (!mine.IsSuccess)
        {
            return NexusResult<MyOrganizationDto>.Failure(mine.Error ?? "Failed to resolve current organization.", mine.StatusCode);
        }

        var first = mine.Value is { Count: > 0 } list ? list[0] : null;
        return first is null
            ? NexusResult<MyOrganizationDto>.Failure("The current user belongs to no organization.", (int)HttpStatusCode.NotFound)
            : NexusResult<MyOrganizationDto>.Success(first, mine.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<NexusResult<OrganizationDescendantsPageDto>> ListDescendantsAsync(
        string organizationId,
        bool directOnly = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<OrganizationDescendantsPageDto>.Failure(
                "organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationDescendants, organizationId)
                      + $"?directOnly={directOnly.ToString().ToLowerInvariant()}&page={page}&pageSize={pageSize}";

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<OrganizationDescendantsPageDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<OrganizationDescendantsPageDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            // An empty sub-tree is a 200 carrying an empty array, so a null payload means an
            // empty body — not "no descendants". The two must not be collapsed: the first is a
            // broken response, the second is the answer for every leaf organization.
            if (payload is null)
            {
                return NexusResult<OrganizationDescendantsPageDto>.Failure(
                    "Empty response body.", (int)response.StatusCode);
            }

            // A body with no `descendants` member deserialises the list as null despite the
            // non-nullable declaration; a caller enumerating the page must never see that.
            var normalized = payload.Descendants is null
                ? payload with { Descendants = Array.Empty<OrganizationDescendantDto>() }
                : payload;

            return NexusResult<OrganizationDescendantsPageDto>.Success(normalized, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListDescendantsAsync({OrganizationId}) failed", organizationId);
            return NexusResult<OrganizationDescendantsPageDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<NamespaceReconciliationReportDto>> ReconcileOrganizationNamespacesAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<NamespaceReconciliationReportDto>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PostAsync(NexusRoutes.Of(NexusManagementRoutes.AdminOrganizationReconcileNamespaces, organizationId), content: null, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<NamespaceReconciliationReportDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NamespaceReconciliationReportDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<NamespaceReconciliationReportDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<NamespaceReconciliationReportDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ReconcileOrganizationNamespacesAsync({OrganizationId}) failed", organizationId);
            return NexusResult<NamespaceReconciliationReportDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    // Wire envelope mirroring the controller list response.
    private sealed record ListMyOrganizationsResponse(MyOrganizationDto[]? Organizations);
}
