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
/// Manages an organization's platform storage resources: the catalog of provisionable
/// offerings, provisioning + listing per organization, health-status refresh, and binding a
/// resource to an application so its credentials are injected at deploy time.
/// </summary>
/// <remarks>
/// <para>
/// All routes wrapped here are gated by the <c>nexus:admin</c> policy and require a the platform identity provider
/// admin JWT — an <c>X-Api-Key</c> principal is rejected (403). Register with
/// <c>AddNexus(config, o =&gt; o.EnableManagement = true)</c> and set
/// <c>NexusOptions.BearerTokenProvider</c>.
/// </para>
/// <para>
/// <b>Provisioning creates REAL infrastructure that costs money.</b>
/// <see cref="ProvisionAsync"/> allocates a backing resource (database, bucket, …) the org is
/// billed for. Binding is metadata only: <see cref="BindAsync"/> takes effect at the NEXT deploy
/// of the (application, environment) — already-running pods keep their current environment until
/// redeployed. Deprovisioning and credential rotation are separate, guarded operations and are
/// intentionally NOT exposed on this service yet.
/// </para>
/// <para>The SDK never throws on API failure — every method returns a <see cref="NexusResult{T}"/>.</para>
/// </remarks>
public interface IResourceService
{
    /// <summary>
    /// Lists the catalog of provisionable resource offerings
    /// (maps <c>GET /api/v1/resources/offerings</c>). Admin-only.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The available offerings; empty list when none are configured.</returns>
    Task<NexusResult<IReadOnlyList<ResourceOffering>>> ListOfferingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists an organization's provisioned resources
    /// (maps <c>GET /api/v1/organizations/{orgId}/resources</c>). Admin-only.
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organization's resources; empty list when it has none.</returns>
    Task<NexusResult<IReadOnlyList<ResourceSummaryDto>>> ListAsync(
        string organizationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a single resource by id
    /// (maps <c>GET /api/v1/organizations/{orgId}/resources/{resourceId}</c>). Admin-only.
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="resourceId">The resource id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resource on success; NotFound failure if no such resource.</returns>
    Task<NexusResult<ResourceDto>> GetAsync(
        string organizationId,
        string resourceId,
        CancellationToken ct = default);

    /// <summary>
    /// Provisions a new resource for an organization
    /// (maps <c>POST /api/v1/organizations/{orgId}/resources</c>). Admin-only.
    /// <b>Creates real, billable infrastructure.</b>
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="request">The kind, org-unique name, optional project scope and provider options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The created resource (postgres provisions synchronously to <c>Ready</c>; other kinds may
    /// return in a <c>Provisioning</c> state — poll <see cref="RefreshStatusAsync"/>). A Conflict
    /// failure when the name is already taken in the org; a Validation failure for an unknown kind.
    /// </returns>
    Task<NexusResult<ResourceDto>> ProvisionAsync(
        string organizationId,
        ProvisionResourceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Refreshes a resource's health status from its backing provider
    /// (maps <c>POST /api/v1/organizations/{orgId}/resources/{resourceId}/refresh-status</c>),
    /// transitioning the resource between <c>Ready</c> and <c>Degraded</c> as needed. Admin-only.
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="resourceId">The resource id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resource with its refreshed status; NotFound failure if no such resource.</returns>
    Task<NexusResult<ResourceDto>> RefreshStatusAsync(
        string organizationId,
        string resourceId,
        CancellationToken ct = default);

    /// <summary>
    /// Binds a resource to an application, optionally scoped to one environment
    /// (maps <c>POST /api/v1/applications/{appId}/resources/{resourceId}/bindings</c>). Admin-only.
    /// The credentials reach the workload at the NEXT deploy of the (application, environment).
    /// </summary>
    /// <param name="applicationId">The application to bind to (UUID).</param>
    /// <param name="resourceId">The resource to bind (UUID).</param>
    /// <param name="organizationId">The owning organization id (UUID) — carried in the request body.</param>
    /// <param name="environmentId">Optional environment scope; <c>null</c> covers all environments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bound resource on success; NotFound / Conflict (already bound) failure otherwise.</returns>
    Task<NexusResult<ResourceDto>> BindAsync(
        string applicationId,
        string resourceId,
        string organizationId,
        Guid? environmentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a resource ↔ application binding
    /// (maps <c>DELETE /api/v1/applications/{appId}/resources/{resourceId}/bindings</c>). Admin-only.
    /// Pass the same <paramref name="environmentId"/> the binding was created with (<c>null</c> = the
    /// all-environments binding). Deployed pods keep their injected env vars until the next deploy;
    /// unbinding only stops future injection.
    /// </summary>
    /// <param name="applicationId">The application to unbind from (UUID).</param>
    /// <param name="resourceId">The resource to unbind (UUID).</param>
    /// <param name="organizationId">The owning organization id (UUID) — sent as a query parameter.</param>
    /// <param name="environmentId">The environment scope of the binding to remove; <c>null</c> = the all-environments binding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resource on success; NotFound failure if no such binding.</returns>
    Task<NexusResult<ResourceDto>> UnbindAsync(
        string applicationId,
        string resourceId,
        string organizationId,
        Guid? environmentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the resources bound to an application, across every environment scope
    /// (maps <c>GET /api/v1/applications/{appId}/resources?orgId=…</c>). Admin-only.
    /// </summary>
    /// <param name="applicationId">The application whose bound resources to list (UUID).</param>
    /// <param name="organizationId">The owning organization id (UUID) — sent as a query parameter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bindings on the application; empty list when it has none.</returns>
    Task<NexusResult<IReadOnlyList<ResourceBindingDto>>> ListApplicationResourcesAsync(
        string applicationId,
        string organizationId,
        CancellationToken ct = default);
}
