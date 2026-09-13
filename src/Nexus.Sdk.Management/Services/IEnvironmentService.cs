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
/// Read/create environments (mirrors <c>EnvironmentsController</c>, route prefix
/// <c>api/v1/environments</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth note:</b> <c>EnvironmentsController</c> carries no <c>[Authorize]</c> attribute, so
/// these routes are reachable by any authenticated principal that satisfies the global
/// fallback policy — including an <c>X-Api-Key</c> principal. They therefore work with either
/// an API key or a bearer token. (The platform listing here is organization-scoped via the
/// required <c>organizationId</c>; the under-protection of these routes is a known platform
/// inconsistency, flagged in the PR body.)
/// </para>
/// <para>The SDK never throws on API failure — every method returns a <see cref="NexusResult{T}"/>.</para>
/// </remarks>
public interface IEnvironmentService
{
    /// <summary>
    /// Lists environments for an organization (maps <c>GET /api/v1/environments?organizationId=</c>).
    /// </summary>
    /// <param name="organizationId">The owning organization id (required by the API).</param>
    /// <param name="provisionedOnly">When true, returns only environments whose namespace is provisioned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of environments.</returns>
    Task<NexusResult<IReadOnlyList<EnvironmentDto>>> ListAsync(
        string organizationId,
        bool provisionedOnly = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a single environment (maps <c>GET /api/v1/environments/{id}?organizationId=</c>).
    /// </summary>
    /// <param name="environmentId">The environment id.</param>
    /// <param name="organizationId">The owning organization id (required by the API).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The environment detail; a NotFound failure when it does not exist.</returns>
    Task<NexusResult<EnvironmentDetailDto>> GetAsync(
        string environmentId,
        string organizationId,
        CancellationToken ct = default);

    /// <summary>Creates an environment with K8s provisioning (maps <c>POST /api/v1/environments</c>).</summary>
    /// <param name="request">Create-environment request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created environment's identity.</returns>
    Task<NexusResult<CreateEnvironmentResult>> CreateAsync(CreateEnvironmentRequest request, CancellationToken ct = default);
}
