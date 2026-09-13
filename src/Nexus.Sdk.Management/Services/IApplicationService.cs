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
/// CRUD and version lifecycle over applications (mirrors <c>ApplicationsController</c>,
/// route prefix <c>api/v1/applications</c>).
/// </summary>
/// <remarks>
/// <para>
/// The controller is gated by <c>nexus:admin</c>, so all methods here require a the platform identity provider
/// admin JWT (configure <c>NexusOptions.BearerTokenProvider</c>) — with two nuances:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="TagVersionAsync"/> (<c>POST .../versions</c>) is additionally
/// reachable by an <c>X-Api-Key</c> carrying the <c>applications:tag_version</c> scope (the
/// CI <c>CanTagVersion</c> policy) — this is the GitHub-Actions tag path.</description></item>
/// <item><description><see cref="DeployAsync"/> / promote depend on the deploy path which on the
/// server also enforces the <c>deployments:production</c> permission for the admin JWT.</description></item>
/// </list>
/// <para>The SDK never throws on API failure — every method returns a <see cref="NexusResult{T}"/>.</para>
/// </remarks>
public interface IApplicationService
{
    /// <summary>Lists applications (maps <c>GET /api/v1/applications</c>).</summary>
    /// <param name="page">1-based page index.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ownerOrganizationId">Optional owner-organization filter.</param>
    /// <param name="includeArchived">Include archived applications.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of application summaries.</returns>
    Task<NexusResult<IReadOnlyList<ApplicationDto>>> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? ownerOrganizationId = null,
        bool includeArchived = false,
        CancellationToken ct = default);

    /// <summary>Gets a single application (maps <c>GET /api/v1/applications/{id}</c>).</summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The application detail; a NotFound failure when it does not exist.</returns>
    Task<NexusResult<ApplicationDetailDto>> GetAsync(string applicationId, CancellationToken ct = default);

    /// <summary>Creates an application (maps <c>POST /api/v1/applications</c>).</summary>
    /// <param name="request">Create-application request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created application's identity.</returns>
    Task<NexusResult<CreateApplicationResult>> CreateAsync(CreateApplicationRequest request, CancellationToken ct = default);

    /// <summary>Archives an application (maps <c>DELETE /api/v1/applications/{id}</c>).</summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="reason">Reason recorded on the archive event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on archive; a failure result otherwise.</returns>
    Task<NexusResult> DeleteAsync(string applicationId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Lists an application's tagged versions, newest-first
    /// (maps <c>GET /api/v1/applications/{id}/versions</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="skip">Number of versions to skip.</param>
    /// <param name="take">Maximum number of versions to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page of versions.</returns>
    Task<NexusResult<IReadOnlyList<VersionDto>>> ListVersionsAsync(
        string applicationId,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Tags a new version of an application (maps <c>POST /api/v1/applications/{id}/versions</c>).
    /// Reachable by an admin JWT or an API key with the <c>applications:tag_version</c> scope.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="request">Tag-version request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tagged version's identity.</returns>
    Task<NexusResult<TagVersionResult>> TagVersionAsync(
        string applicationId,
        TagVersionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deploys a tagged version to an environment
    /// (maps <c>POST /api/v1/applications/{id}/versions/{tag}/deploy</c> with body <c>{ environmentId }</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="environmentId">Target environment id.</param>
    /// <param name="tag">The version tag to deploy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deployment result (URL, the deployment controller app name, version, environment).</returns>
    Task<NexusResult<VersionDeploymentResult>> DeployAsync(
        string applicationId,
        Guid environmentId,
        string tag,
        CancellationToken ct = default);

    /// <summary>
    /// Decommissions a specific deployed version from an environment. Independent of deploying —
    /// coexisting versions keep serving; only this (version, environment) deployment is torn down
    /// (maps <c>DELETE /api/v1/applications/{id}/versions/{tag}/environments/{envId}</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="environmentId">The environment the version is deployed to.</param>
    /// <param name="tag">The version tag to decommission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decommission result (version, environment, the deployment controller app name).</returns>
    Task<NexusResult<VersionDecommissionResult>> DecommissionAsync(
        string applicationId,
        Guid environmentId,
        string tag,
        CancellationToken ct = default);

    /// <summary>
    /// Promotes the version currently deployed in one environment to a target environment
    /// (maps <c>POST /api/v1/applications/{id}/promote</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="request">Promote-version request (from/to environment + version tag).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deployment result for the target environment.</returns>
    Task<NexusResult<VersionDeploymentResult>> PromoteAsync(
        string applicationId,
        PromoteVersionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an application's editable, post-deploy settings — description, app-level
    /// environment variables, replicas, resource limits, and scaling
    /// (maps <c>PUT /api/v1/organizations/{orgId}/applications/{id}/settings</c>).
    /// </summary>
    /// <remarks>
    /// Org-scoped so it resolves under row-level security for any authorized caller. Replicas
    /// and resource limits are hot-applied to the running deployment; environment-variable
    /// changes take effect on the next deploy. Immutable fields (name, subdomain, template,
    /// repository) cannot be changed here.
    /// </remarks>
    /// <param name="organizationId">The owning organization id (route scope).</param>
    /// <param name="applicationId">The application id.</param>
    /// <param name="request">The new settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success on update; a failure result otherwise.</returns>
    Task<NexusResult> UpdateSettingsAsync(
        string organizationId,
        string applicationId,
        UpdateApplicationSettingsRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the application's audit log, newest-first and paginated
    /// (maps <c>GET /api/v1/applications/{id}/audit-log</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="skip">Number of entries to skip.</param>
    /// <param name="take">Maximum number of entries to return.</param>
    /// <param name="from">Optional inclusive lower time bound.</param>
    /// <param name="to">Optional inclusive upper time bound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The paged audit log.</returns>
    Task<NexusResult<ApplicationAuditLogDto>> GetAuditLogAsync(
        string applicationId,
        int skip = 0,
        int take = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists recent git commits for an application's linked repository so a caller can pick a
    /// commit to deploy (maps <c>GET /api/v1/applications/{id}/repository/commits</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="branch">
    /// Optional branch to list commits from. When <see langword="null"/>, the repository's
    /// default branch is used.
    /// </param>
    /// <param name="limit">Maximum number of commits to return (clamped server-side to 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newest-first list of commits.</returns>
    Task<NexusResult<IReadOnlyList<CommitDto>>> ListRepositoryCommitsAsync(
        string applicationId,
        string? branch = null,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the git branches for an application's linked repository
    /// (maps <c>GET /api/v1/applications/{id}/repository/branches</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of branches.</returns>
    Task<NexusResult<IReadOnlyList<BranchDto>>> ListRepositoryBranchesAsync(
        string applicationId,
        CancellationToken ct = default);

    /// <summary>
    /// Starts an on-demand build-and-deploy from a commit/branch
    /// (maps <c>POST /api/v1/applications/{id}/deploy-from-commit</c>). Returns immediately
    /// with a status resource — poll <see cref="GetBuildDeployStatusAsync"/> for progress.
    /// Requires the <c>deployments:production</c> permission.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="request">The commit/branch + environment to build and deploy.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NexusResult<BuildAndDeployFromCommitResult>> DeployFromCommitAsync(
        string applicationId,
        BuildAndDeployFromCommitRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the live status of an on-demand build-and-deploy operation
    /// (maps <c>GET /api/v1/applications/{id}/build-deploys/{buildDeployId}</c>). Poll until
    /// the status is terminal (<c>Deployed</c> or <c>Failed</c>).
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="buildDeployId">The build-deploy operation id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<NexusResult<BuildDeployStatusDto>> GetBuildDeployStatusAsync(
        string applicationId,
        string buildDeployId,
        CancellationToken ct = default);
}
