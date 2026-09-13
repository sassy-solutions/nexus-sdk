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
/// Manages an organization's provider-agnostic git connections (the N2 connect flow):
/// the signed-state install URL, the connection list/detail, local disconnect, and the
/// repositories a connection can see. The connect handshake is completed by
/// <see cref="CompleteGitHubSetupAsync"/>. All org-scoped routes are <c>nexus:admin</c>
/// gated server-side and tenant-pinned; a cross-tenant call surfaces as a failure
/// (403) without throwing.
/// </summary>
/// <remarks>
/// Connections never carry a durable secret — App installations mint ephemeral tokens on
/// demand. <see cref="ReconcileAsync"/> is a platform-admin operation (no org scope) that
/// mirrors the platform reconcile endpoint; it is included here so the management surface
/// covers the full git-connection lifecycle.
/// </remarks>
public interface IGitConnectionService
{
    /// <summary>Lists the organization's git connections (excludes disconnected by default).</summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="includeDisconnected">Whether to include disconnected connections.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connections on success; failure result otherwise.</returns>
    Task<NexusResult<IReadOnlyList<GitConnection>>> ListAsync(
        string organizationId,
        bool includeDisconnected = false,
        CancellationToken ct = default);

    /// <summary>Gets one git connection by id.</summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="connectionId">The connection id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection on success; NotFound failure if no such connection.</returns>
    Task<NexusResult<GitConnection>> GetAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the GitHub App install URL carrying a signed state token binding the install
    /// to this org + the calling admin. Send the browser here to install the App; GitHub then
    /// redirects to the setup page which calls <see cref="CompleteGitHubSetupAsync"/>.
    /// </summary>
    /// <param name="organizationId">The organization to bind the install to (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The install URL on success; failure otherwise.</returns>
    Task<NexusResult<GitInstallUrl>> GetInstallUrlAsync(
        string organizationId,
        CancellationToken ct = default);

    /// <summary>
    /// Completes the App install and activates the org's git connection. The org is derived
    /// server-side from the signed state on the <paramref name="input"/>, not a parameter here.
    /// </summary>
    /// <param name="input">The installation id, signed state, and optional setup-action / OAuth code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The activated connection on success; failure (e.g. 403 user mismatch, 409 already connected) otherwise.</returns>
    Task<NexusResult<GitConnection>> CompleteGitHubSetupAsync(
        CompleteGitHubSetupInput input,
        CancellationToken ct = default);

    /// <summary>Disconnects a git connection locally (never uninstalls the App on GitHub).</summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="connectionId">The connection id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success when disconnected; NotFound failure if no such connection.</returns>
    Task<NexusResult> DisconnectAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the repositories visible to the connection's credential. Requires the connection
    /// to be Active; the server mints an installation token on demand.
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID).</param>
    /// <param name="connectionId">The connection id (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The visible repositories on success; NotFound / Conflict (not active) failure otherwise.</returns>
    Task<NexusResult<IReadOnlyList<GitConnectionRepository>>> ListRepositoriesAsync(
        string organizationId,
        string connectionId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new GitHub organization under the platform's master enterprise for this Nexus
    /// organization (N4). The desired name is normalized server-side under the enterprise login
    /// prefix (e.g. <c>NXS-</c>). The created org still needs the platform App installed by a
    /// human — the result carries an install redirect URL pre-targeting it. Capability-gated:
    /// when the enterprise credential is absent the call fails with
    /// <c>GithubOrg.CreationUnavailable</c> (503). <c>nexus:admin</c> gated + tenant-pinned
    /// server-side.
    /// </summary>
    /// <param name="organizationId">The owning organization id (UUID); route-scoped and tenant-pinned.</param>
    /// <param name="input">The desired organization name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The created org (login, URL, install redirect URL) on success; failure otherwise
    /// (e.g. 409 <c>GithubOrg.LoginTaken</c> / <c>GitConnection.OrgAlreadyConnected</c>,
    /// 503 <c>GithubOrg.CreationUnavailable</c>).
    /// </returns>
    Task<NexusResult<CreatedGitNamespace>> CreateGithubOrganizationAsync(
        string organizationId,
        CreateGithubOrganizationInput input,
        CancellationToken ct = default);

    /// <summary>
    /// Reads the provider-agnostic git capability matrix (N4). Not org-scoped — any
    /// authenticated caller may read it; it carries no tenant data and drives UI affordances
    /// (show/hide the Connect and Create-organization actions). <c>connect</c> reflects the
    /// git server's declared App-install support; <c>createOrganization</c> reflects whether
    /// the enterprise creator is configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The per-provider capability flags on success; failure otherwise.</returns>
    Task<NexusResult<GitCapabilities>> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs an on-demand platform reconcile of the git-connection read model against the
    /// platform app's live installation directory. <paramref name="dryRun"/> = true (default)
    /// returns the full report with NO mutation. An apply run (<paramref name="dryRun"/> = false)
    /// REQUIRES an explicit <paramref name="connectionIds"/> list and applies each listed row's
    /// proposed action — never a sweep (destructive-ops house rule). Platform-admin only; not
    /// org-scoped.
    /// </summary>
    /// <param name="dryRun">When true, compute and return the report without mutating anything.</param>
    /// <param name="connectionIds">Required for an apply run (dryRun=false): the explicit connection ids to act on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reconcile report on success; failure otherwise.</returns>
    Task<NexusResult<GitReconcileReport>> ReconcileAsync(
        bool dryRun = true,
        IReadOnlyList<string>? connectionIds = null,
        CancellationToken ct = default);
}
