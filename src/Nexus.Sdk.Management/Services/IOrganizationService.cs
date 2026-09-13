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
/// Read/operate over Nexus organizations (tenants).
/// </summary>
/// <remarks>
/// <para>
/// All routes wrapped here are gated by the <c>nexus:admin</c> / <c>PlatformAdmin</c>
/// policies and require a administrator bearer token — an <c>X-Api-Key</c> principal is rejected (403).
/// Register with <c>AddNexus(config, o =&gt; o.EnableManagement = true)</c> and set
/// <c>NexusOptions.BearerTokenProvider</c>.
/// </para>
/// <para>The SDK never throws on API failure — every method returns a <see cref="NexusResult{T}"/>.</para>
/// </remarks>
public interface IOrganizationService
{
    /// <summary>
    /// Lists the organizations the current authenticated user belongs to
    /// (maps <c>GET /api/me/organizations</c>). Any authenticated JWT user can call this.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's organizations; empty list when the user has none.</returns>
    Task<NexusResult<IReadOnlyList<MyOrganizationDto>>> ListMineAsync(CancellationToken ct = default);

    // ListAsync, GetAsync and ToggleVipAsync are gone. They mapped
    // GET api/v1/organizations, GET api/v1/organizations/{id} and
    // PATCH api/v1/organizations/{id}/vip; the API mounts OrganizationsController on
    // api/organizations only, so all three returned 404 for every input. A caller could
    // not tell "this organization does not exist" from "this method never existed".
    // Restoring them means adding the routes server-side first, then a constant in
    // NexusRoutes — SdkContractTests refuses any route the snapshot does not serve.

    /// <summary>
    /// Convenience helper that resolves the single organization the current user belongs to
    /// (maps <c>GET /api/me/organizations</c> and returns the first entry).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's first organization; a NotFound failure when the user has none.</returns>
    Task<NexusResult<MyOrganizationDto>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists the organizations one organization owns — the shape of its sub-tree
    /// (maps <c>GET /api/v1/organizations/{id}/descendants</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns ids, names, statuses and depths, and nothing else. It is not a key to the
    /// descendants' data: reading a child organization still requires membership of that child.
    /// The root itself is never in its own results.
    /// </para>
    /// <para>
    /// Scoped like every other route on this controller — a caller who is not a member of the
    /// organization asked about (or a platform admin with an active operator session on it)
    /// gets a failure, not a filtered list.
    /// </para>
    /// </remarks>
    /// <param name="organizationId">The sub-tree root (UUID).</param>
    /// <param name="directOnly">Only immediate children rather than the whole sub-tree.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One page of the sub-tree; an empty page when the organization owns nothing.</returns>
    Task<NexusResult<OrganizationDescendantsPageDto>> ListDescendantsAsync(
        string organizationId,
        bool directOnly = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Reconciles the baseline the orchestrator resources for an organization's namespaces
    /// (maps <c>POST /api/v1/admin/organizations/{id}/reconcile-namespaces</c>). Idempotent.
    /// </summary>
    /// <remarks>
    /// Restricted to the <c>PlatformAdmin</c> policy — requires a platform-admin bearer token.
    /// </remarks>
    /// <param name="organizationId">The organization id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A report of namespaces touched, resources created vs already-present.</returns>
    Task<NexusResult<NamespaceReconciliationReportDto>> ReconcileOrganizationNamespacesAsync(
        string organizationId,
        CancellationToken ct = default);
}
