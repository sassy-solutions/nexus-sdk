// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Management.Models;

/// <summary>
/// A provider-agnostic git connection owned by an organization. Mirrors the API's
/// <c>GitConnectionResponse</c> exactly. Never contains a durable secret — App
/// installations mint ephemeral tokens on demand from <see cref="InstallationRef"/>.
/// </summary>
/// <param name="Id">The Nexus-side connection id.</param>
/// <param name="OrganizationId">The owning Nexus organization id.</param>
/// <param name="Provider">The provider discriminator (e.g. <c>github</c>).</param>
/// <param name="AuthMode">The auth mode (e.g. <c>app-installation</c>).</param>
/// <param name="AccountLogin">The account login (org slug or username) the connection targets.</param>
/// <param name="AccountType">Whether the account is an <c>organization</c> or a <c>user</c>.</param>
/// <param name="InstallationRef">The provider-side installation/credential reference, when any.</param>
/// <param name="Status">The connection status (<c>pending|active|suspended|disconnected</c>).</param>
/// <param name="CreatedBy">Who initiated the connection.</param>
/// <param name="CreatedAt">When the connection was initiated.</param>
/// <param name="UpdatedBy">Who last updated the connection, when any.</param>
/// <param name="UpdatedAt">When the connection was last updated, when any.</param>
public sealed record GitConnection(
    Guid Id,
    Guid OrganizationId,
    string Provider,
    string AuthMode,
    string AccountLogin,
    string AccountType,
    string? InstallationRef,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

/// <summary>A repository visible to a connection's credential. Mirrors <c>ConnectionRepositoryResponse</c>.</summary>
/// <param name="FullName">The <c>owner/name</c> full repository name.</param>
/// <param name="HtmlUrl">The repository's web URL.</param>
/// <param name="DefaultBranch">The repository's default branch.</param>
/// <param name="Private">Whether the repository is private.</param>
public sealed record GitConnectionRepository(
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool Private);

/// <summary>The GitHub App install URL carrying a signed state token. Mirrors <c>InstallUrlResponse</c>.</summary>
/// <param name="Url">The fully-qualified GitHub App install URL.</param>
public sealed record GitInstallUrl(string Url);

/// <summary>
/// Input for creating a new GitHub organization under the platform's master enterprise
/// for a Nexus organization (N4). Mirrors the API's <c>CreateGitHubOrganizationRequestBody</c>.
/// The desired <see cref="Name"/> is normalized server-side under the enterprise login
/// prefix (e.g. <c>NXS-</c>) into the final GitHub login.
/// </summary>
/// <param name="Name">The desired organization name; normalized to a prefixed GitHub login.</param>
public sealed record CreateGithubOrganizationInput(string Name);

/// <summary>
/// A GitHub organization created under the platform's master enterprise. Mirrors the API's
/// <c>CreateGitHubOrganizationResponse</c>. The org still needs the platform App installed by
/// a human — send the browser to <see cref="InstallRedirectUrl"/> to complete the connect flow.
/// </summary>
/// <param name="Login">The created org login (enterprise prefix applied, as GitHub returned it).</param>
/// <param name="HtmlUrl">The org's github.com URL.</param>
/// <param name="InstallRedirectUrl">The App install URL pre-targeting the new org — a human must visit it.</param>
public sealed record CreatedGitNamespace(
    string Login,
    string HtmlUrl,
    string InstallRedirectUrl);

/// <summary>
/// The git capability matrix surfaced on <c>GET /api/v1/git/capabilities</c> (N4). Mirrors the
/// API's <c>GitCapabilitiesDto</c>. Providers is a list so future providers (gitlab, …) slot in
/// without a contract break; consumers key on <see cref="GitProviderCapability.Provider"/>.
/// </summary>
/// <param name="Providers">Per-provider capability flags.</param>
public sealed record GitCapabilities(IReadOnlyList<GitProviderCapability> Providers);

/// <summary>
/// Capability flags for a single git provider. Mirrors the API's <c>GitProviderCapabilityDto</c>.
/// Drives UI affordances: hide/disable the unsupported action and show
/// <see cref="CreateOrganizationDisabledReason"/> when set.
/// </summary>
/// <param name="Provider">The provider discriminator (e.g. <c>github</c>).</param>
/// <param name="Connect">Whether the App-install connect flow is available (provider configured).</param>
/// <param name="CreateOrganization">Whether creating a new org under the master enterprise is available.</param>
/// <param name="CreateOrganizationDisabledReason">
/// Why org creation is unavailable, or <see langword="null"/> when
/// <paramref name="CreateOrganization"/> is true.
/// </param>
public sealed record GitProviderCapability(
    string Provider,
    bool Connect,
    bool CreateOrganization,
    string? CreateOrganizationDisabledReason);

/// <summary>
/// Input for completing a GitHub App install and activating the org's connection.
/// Mirrors the API's <c>CompleteSetupRequest</c> body. The organization is derived
/// server-side from the signed <see cref="State"/>, not from a parameter here.
/// </summary>
/// <param name="InstallationId">GitHub's numeric installation id (as text).</param>
/// <param name="State">The signed state token GitHub echoed back.</param>
/// <param name="SetupAction">GitHub's <c>setup_action</c> (informational).</param>
/// <param name="Code">The OAuth code GitHub appends when user-authorization-during-install is on.</param>
public sealed record CompleteGitHubSetupInput(
    string InstallationId,
    string State,
    string? SetupAction = null,
    string? Code = null);

/// <summary>
/// The proposed reconcile action for one connection row. Mirrors the API's
/// <c>GitReconcileAction</c> — the member ORDER must stay identical: the API serializes
/// enums as their numeric value (no <c>JsonStringEnumConverter</c> is configured), so the
/// wire format is the integer index.
/// </summary>
public enum GitReconcileAction
{
    /// <summary>No change needed — the row already matches the installation directory.</summary>
    None,

    /// <summary>A Pending connection whose installation exists — activate it.</summary>
    ActivatePending,

    /// <summary>An Active connection whose account login drifted from the authoritative one — refresh it.</summary>
    RefreshAccountLogin,

    /// <summary>An Active connection whose installation is now suspended — suspend it.</summary>
    Suspend,

    /// <summary>A Suspended connection whose installation is live again — reactivate it.</summary>
    Unsuspend,

    /// <summary>A connection whose installation vanished from the directory — disconnect it (2-strike / explicit only).</summary>
    Disconnect,
}

/// <summary>One connection row's reconcile diff. Mirrors <c>GitReconcileItem</c>.</summary>
/// <param name="ConnectionId">The read-model row id.</param>
/// <param name="OrganizationId">The owning organization.</param>
/// <param name="InstallationRef">The provider installation reference the row carries.</param>
/// <param name="CurrentAccountLogin">The account login currently in the read model.</param>
/// <param name="CurrentStatus">The connection's current status.</param>
/// <param name="ProposedAction">The action reconciliation proposes for this row.</param>
/// <param name="AuthoritativeAccountLogin">The account login reported by the live installation, when present.</param>
/// <param name="InstallationSuspended">Whether the live installation is currently suspended (null when vanished).</param>
/// <param name="Applied">Whether the proposed action was applied in this pass.</param>
/// <param name="ApplyError">The error message when an attempted apply failed.</param>
public sealed record GitReconcileItem(
    Guid ConnectionId,
    Guid OrganizationId,
    string? InstallationRef,
    string CurrentAccountLogin,
    string CurrentStatus,
    GitReconcileAction ProposedAction,
    string? AuthoritativeAccountLogin,
    bool? InstallationSuspended,
    bool Applied,
    string? ApplyError);

/// <summary>An installation present on the provider that no live connection row references. Mirrors <c>GitReconcileOrphan</c>.</summary>
/// <param name="InstallationRef">The provider installation reference.</param>
/// <param name="AccountLogin">The account login the installation lives on.</param>
/// <param name="Suspended">Whether the installation is suspended.</param>
public sealed record GitReconcileOrphan(
    string InstallationRef,
    string AccountLogin,
    bool Suspended);

/// <summary>The full result of a reconcile pass. Mirrors <c>GitReconcileReport</c>.</summary>
/// <param name="DryRun">Whether this pass mutated anything.</param>
/// <param name="AutoApply">Whether this was the automatic hosted run (always false from the manual endpoint).</param>
/// <param name="Items">Per-connection diff + proposed action + apply outcome.</param>
/// <param name="Orphans">Installations live on the provider with no matching live connection row (informational).</param>
/// <param name="AppliedCount">Number of items whose action was actually applied.</param>
public sealed record GitReconcileReport(
    bool DryRun,
    bool AutoApply,
    IReadOnlyList<GitReconcileItem> Items,
    IReadOnlyList<GitReconcileOrphan> Orphans,
    int AppliedCount);
