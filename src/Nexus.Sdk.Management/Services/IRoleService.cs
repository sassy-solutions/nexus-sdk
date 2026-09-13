// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Models;

namespace Nexus.Sdk.Management.Services;

/// <summary>
/// CRUD over RBAC roles plus ABAC-lite attribute management and user assignment
/// (mirrors <c>RolesController</c> at <c>api/v1/organizations/{organizationId}/roles</c> and
/// the role-assignments route on <c>PlatformUsersController</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every role route is guarded by a <c>roles_*</c> permission policy (and additionally by an
/// org-scope check that reads the JWT-derived PlatformUser context); the assignment route is
/// guarded by <c>users_update</c>. None of these are satisfiable by an <c>X-Api-Key</c>
/// principal — a administrator bearer token with the right permissions is required
/// (configure <c>NexusOptions.BearerTokenProvider</c>).
/// </para>
/// <para>The SDK never throws on API failure — every method returns a <see cref="NexusResult{T}"/>.</para>
/// </remarks>
public interface IRoleService
{
    /// <summary>Lists roles in an organization (maps <c>GET .../roles</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="includeDeleted">Include soft-deleted roles.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of roles.</returns>
    Task<NexusResult<IReadOnlyList<RoleDto>>> ListAsync(
        string organizationId,
        bool includeDeleted = false,
        CancellationToken ct = default);

    /// <summary>Gets a single role (maps <c>GET .../roles/{roleId}</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The role detail; a NotFound failure when it does not exist.</returns>
    Task<NexusResult<RoleDetailDto>> GetAsync(string organizationId, string roleId, CancellationToken ct = default);

    /// <summary>Creates a role (maps <c>POST .../roles</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="request">Create-role request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created role id.</returns>
    Task<NexusResult<Guid>> CreateAsync(string organizationId, CreateRoleRequest request, CancellationToken ct = default);

    /// <summary>Updates a role (maps <c>PUT .../roles/{roleId}</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="request">Update-role request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on update; a failure result otherwise.</returns>
    Task<NexusResult> UpdateAsync(string organizationId, string roleId, UpdateRoleRequest request, CancellationToken ct = default);

    /// <summary>Deletes a role (maps <c>DELETE .../roles/{roleId}</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on delete; a failure result otherwise.</returns>
    Task<NexusResult> DeleteAsync(string organizationId, string roleId, CancellationToken ct = default);

    /// <summary>Lists a role's ABAC-lite attribute triples (maps <c>GET .../roles/{roleId}/attributes</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The role's attributes.</returns>
    Task<NexusResult<IReadOnlyList<RoleAttributeDto>>> ListAttributesAsync(
        string organizationId,
        string roleId,
        CancellationToken ct = default);

    /// <summary>Adds an ABAC-lite attribute to a role (maps <c>POST .../roles/{roleId}/attributes</c>).</summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="request">Add-attribute request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new attribute's id.</returns>
    Task<NexusResult<AddRoleAttributeResult>> AddAttributeAsync(
        string organizationId,
        string roleId,
        AddRoleAttributeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes an attribute from a role (maps <c>DELETE .../roles/{roleId}/attributes/{attributeId}</c>).
    /// </summary>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="attributeId">The attribute id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on removal; a failure result otherwise.</returns>
    Task<NexusResult> RemoveAttributeAsync(
        string organizationId,
        string roleId,
        string attributeId,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces a platform user's scoped role assignments (maps
    /// <c>PUT api/v1/organizations/{organizationId}/platform-users/{userId}/role-assignments</c>).
    /// </summary>
    /// <remarks>This is a full replace — the supplied set becomes the user's complete assignment set.</remarks>
    /// <param name="organizationId">The organization id.</param>
    /// <param name="userId">The platform user id.</param>
    /// <param name="assignments">The complete set of scoped role assignments to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on update; a failure result otherwise.</returns>
    Task<NexusResult> AssignToUserAsync(
        string organizationId,
        string userId,
        IReadOnlyList<RoleAssignmentRequest> assignments,
        CancellationToken ct = default);
}
