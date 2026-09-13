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

/// <summary>HTTP-backed implementation of <see cref="IRoleService"/>.</summary>
public sealed class HttpRoleService : IRoleService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpRoleService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpRoleService"/> class.</summary>
    public HttpRoleService(HttpClient httpClient, ILogger<HttpRoleService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<RoleDto>>> ListAsync(
        string organizationId,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<RoleDto>>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRoles, organizationId) + $"?includeDeleted={includeDeleted.ToString().ToLowerInvariant()}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<RoleDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListRolesResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<RoleDto> roles = payload?.Roles ?? Array.Empty<RoleDto>();
            return NexusResult<IReadOnlyList<RoleDto>>.Success(roles, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync({OrganizationId}) failed", organizationId);
            return NexusResult<IReadOnlyList<RoleDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<RoleDetailDto>> GetAsync(string organizationId, string roleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId))
        {
            return NexusResult<RoleDetailDto>.Failure("organizationId and roleId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRole, organizationId, roleId);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<RoleDetailDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<RoleDetailDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<RoleDetailDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<RoleDetailDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAsync({RoleId}) failed", roleId);
            return NexusResult<RoleDetailDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<Guid>> CreateAsync(string organizationId, CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<Guid>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(RolesRoute(organizationId), request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<Guid>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<CreateRoleResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<Guid>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<Guid>.Success(payload.Id, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CreateAsync({OrganizationId}) failed", organizationId);
            return NexusResult<Guid>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> UpdateAsync(string organizationId, string roleId, UpdateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId))
        {
            return NexusResult.Failure("organizationId and roleId are required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRole, organizationId, roleId);
            using var response = await _httpClient
                .PutAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "UpdateAsync({RoleId}) failed", roleId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> DeleteAsync(string organizationId, string roleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId))
        {
            return NexusResult.Failure("organizationId and roleId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRole, organizationId, roleId);
            using var response = await _httpClient.DeleteAsync(url, ct).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeleteAsync({RoleId}) failed", roleId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<RoleAttributeDto>>> ListAttributesAsync(
        string organizationId,
        string roleId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId))
        {
            return NexusResult<IReadOnlyList<RoleAttributeDto>>.Failure("organizationId and roleId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRoleAttributes, organizationId, roleId);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<RoleAttributeDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ListRoleAttributesResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<RoleAttributeDto> attributes = payload?.Attributes ?? Array.Empty<RoleAttributeDto>();
            return NexusResult<IReadOnlyList<RoleAttributeDto>>.Success(attributes, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAttributesAsync({RoleId}) failed", roleId);
            return NexusResult<IReadOnlyList<RoleAttributeDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<AddRoleAttributeResult>> AddAttributeAsync(
        string organizationId,
        string roleId,
        AddRoleAttributeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId))
        {
            return NexusResult<AddRoleAttributeResult>.Failure("organizationId and roleId are required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRoleAttributes, organizationId, roleId);
            using var response = await _httpClient
                .PostAsJsonAsync(url, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<AddRoleAttributeResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<AddRoleAttributeResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<AddRoleAttributeResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<AddRoleAttributeResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AddAttributeAsync({RoleId}) failed", roleId);
            return NexusResult<AddRoleAttributeResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> RemoveAttributeAsync(
        string organizationId,
        string roleId,
        string attributeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(attributeId))
        {
            return NexusResult.Failure("organizationId, roleId and attributeId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationRoleAttribute, organizationId, roleId, attributeId);
            using var response = await _httpClient.DeleteAsync(url, ct).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RemoveAttributeAsync({RoleId}/{AttributeId}) failed", roleId, attributeId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> AssignToUserAsync(
        string organizationId,
        string userId,
        IReadOnlyList<RoleAssignmentRequest> assignments,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(userId))
        {
            return NexusResult.Failure("organizationId and userId are required.", (int)HttpStatusCode.BadRequest);
        }

        ArgumentNullException.ThrowIfNull(assignments);

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.OrganizationPlatformUserRoleAssignments, organizationId, userId);
            var body = new UpdateUserRoleAssignmentsBody(assignments.ToArray());
            using var response = await _httpClient
                .PutAsJsonAsync(url, body, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? NexusResult.Success((int)response.StatusCode)
                : await ManagementHttp.ToFailure(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AssignToUserAsync({UserId}) failed", userId);
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private static string RolesRoute(string organizationId) =>
        NexusRoutes.Of(NexusManagementRoutes.OrganizationRoles, organizationId);

    private sealed record ListRolesResponse(RoleDto[]? Roles);

    private sealed record ListRoleAttributesResponse(RoleAttributeDto[]? Attributes);

    private sealed record CreateRoleResponse(Guid Id);

    private sealed record UpdateUserRoleAssignmentsBody(RoleAssignmentRequest[] Assignments);
}
