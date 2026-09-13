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
/// CRUD over projects within an organization (mirrors <c>ProjectsController</c>,
/// route prefix <c>api/organizations/{orgId}/projects</c>).
/// </summary>
/// <remarks>
/// All routes are gated by the <c>nexus:admin</c> policy and require a administrator bearer token
/// (configure <c>NexusOptions.BearerTokenProvider</c>). The SDK never throws on API failure.
/// </remarks>
public interface IProjectService
{
    /// <summary>Lists projects in an organization (maps <c>GET .../projects</c>).</summary>
    /// <param name="organizationId">The owning organization id.</param>
    /// <param name="page">1-based page index.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="includeArchived">Include archived projects.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of project summaries.</returns>
    Task<NexusResult<IReadOnlyList<ProjectDto>>> ListAsync(
        string organizationId,
        int page = 1,
        int pageSize = 20,
        bool includeArchived = false,
        CancellationToken ct = default);

    /// <summary>Gets a single project (maps <c>GET .../projects/{id}</c>).</summary>
    /// <param name="organizationId">The owning organization id.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The project detail; a NotFound failure when it does not exist.</returns>
    Task<NexusResult<ProjectDetailDto>> GetAsync(string organizationId, string projectId, CancellationToken ct = default);

    /// <summary>Creates a project (maps <c>POST .../projects</c>).</summary>
    /// <param name="organizationId">The owning organization id.</param>
    /// <param name="request">Create-project request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created project's identity.</returns>
    Task<NexusResult<CreateProjectResult>> CreateAsync(
        string organizationId,
        CreateProjectRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Archives a project (maps <c>DELETE .../projects/{id}</c>; the API soft-deletes by
    /// archiving). A reason is recorded in the audit trail.
    /// </summary>
    /// <param name="organizationId">The owning organization id.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="reason">Reason recorded on the archive event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on archive; a failure result otherwise.</returns>
    Task<NexusResult> DeleteAsync(
        string organizationId,
        string projectId,
        string reason,
        CancellationToken ct = default);
}
