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
/// A provisionable resource offering: one <c>(kind, provider)</c> pair the platform can
/// provision. Mirrors the API's <c>ResourceOffering</c> exactly.
/// </summary>
/// <param name="Kind">The resource kind wire value, e.g. <c>postgres-database</c>.</param>
/// <param name="Provider">The backing provider, e.g. <c>managed-relational</c>. One provider per kind in v1.</param>
/// <param name="DisplayName">Human-readable name for catalogs.</param>
/// <param name="Description">Short description of what gets provisioned.</param>
/// <param name="QuotaFeatureCode">Plan feature-limit key resolving the per-org max, e.g. <c>resources.postgres-database.max</c>.</param>
public sealed record ResourceOffering(
    string Kind,
    string Provider,
    string DisplayName,
    string Description,
    string QuotaFeatureCode);

/// <summary>
/// Full read model of a provisioned platform resource. Mirrors the API's <c>ResourceDto</c>.
/// Carries credential KEY NAMES only (<see cref="CredentialKeys"/>) — never any credential value;
/// the actual secret material lives in the K8s secret named by <see cref="SecretName"/>.
/// </summary>
/// <param name="Id">The resource id.</param>
/// <param name="OrganizationId">The owning organization id.</param>
/// <param name="ProjectId">The owning project id, when scoped to one.</param>
/// <param name="Kind">The resource kind, e.g. <c>postgres-database</c>.</param>
/// <param name="Provider">The backing provider, e.g. <c>managed-relational</c>.</param>
/// <param name="Name">The tenant-chosen, org-unique resource name.</param>
/// <param name="Status">The lifecycle status, e.g. <c>Provisioning</c>, <c>Ready</c>, <c>Degraded</c>, <c>Failed</c>.</param>
/// <param name="ExternalId">The provider-side id of the backing resource, when provisioned.</param>
/// <param name="SecretName">The K8s secret holding the resource credentials, when minted.</param>
/// <param name="CredentialKeys">The credential key NAMES the secret exposes (never the values).</param>
/// <param name="Metadata">Provider-specific metadata (endpoint host, region, bucket, etc.).</param>
/// <param name="StatusMessage">Human-readable detail for the current status, when any.</param>
/// <param name="ProvisionedAt">When the backing resource became ready, when it has.</param>
/// <param name="CreatedBy">Who requested the resource.</param>
/// <param name="CreatedAt">When the resource record was created.</param>
/// <param name="UpdatedAt">When the resource record was last updated.</param>
/// <param name="Release">
/// What releasing this resource does to the object at the provider :
/// <c>Delete</c> (it is really deleted), <c>DeactivateOnly</c> (it survives, deactivated),
/// <c>OperatorOnly</c> (Nexus does not release it at all). A <c>Retired</c> status means the
/// second or the third happened and a human still has work to do.
/// </param>
/// <param name="RetiredAt">When Nexus released the resource while the provider object survived; null otherwise.</param>
public sealed record ResourceDto(
    Guid Id,
    string OrganizationId,
    Guid? ProjectId,
    string Kind,
    string Provider,
    string Name,
    string Status,
    string? ExternalId,
    string? SecretName,
    IReadOnlyList<string> CredentialKeys,
    IReadOnlyDictionary<string, string> Metadata,
    string? StatusMessage,
    DateTimeOffset? ProvisionedAt,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Release = null,
    DateTimeOffset? RetiredAt = null)
{
    /// <summary>
    /// Progress of the provisioning saga behind this resource, when one is on record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// the second leg of the triplet for a change that a client cannot ignore.
    /// <c>POST /resources</c> now returns while provisioning is still running, so the
    /// <c>Status</c> beside this is <c>Provisioning</c>, not <c>Ready</c>, and the
    /// credential key names are empty until the saga lands. A caller that used to treat the
    /// creation response as terminal must poll <c>GetResourceAsync</c> and read this.
    /// </para>
    /// <para>
    /// Null means nothing is on record — either the resource predates sagas, or its saga row
    /// is gone. It is not a synthetic "completed".
    /// </para>
    /// </remarks>
    public SagaProvisioningDto? Provisioning { get; init; }
}

/// <summary>
/// Progress of a provisioning saga. Mirrors the API's <c>SagaProvisioningDto</c>.
/// </summary>
/// <param name="Status">InProgress, Compensating, Completed, Failed or Compensated.</param>
/// <param name="Attempt">Which attempt this is. 1 unless something re-armed it.</param>
/// <param name="Steps">Every step, in declared order, with its status and error.</param>
/// <param name="UndoneEffects">
/// What a rollback could not undo. Non-empty means an operator has cleanup to do before a
/// retry is accepted.
/// </param>
public sealed record SagaProvisioningDto(
    string Status,
    int Attempt,
    IReadOnlyList<SagaProvisioningStepDto> Steps,
    IReadOnlyList<SagaUndoneEffectDto> UndoneEffects);

/// <summary>One step of a provisioning saga. Mirrors the API's <c>SagaProvisioningStepDto</c>.</summary>
/// <param name="Name">The step's declared name.</param>
/// <param name="Order">Its position in the saga, zero-based.</param>
/// <param name="Status">Pending, Completed, Failed or Compensated.</param>
/// <param name="Error">The failure message, when the step recorded one.</param>
public sealed record SagaProvisioningStepDto(
    string Name,
    int Order,
    string Status,
    string? Error);

/// <summary>
/// One external effect a saga's compensation left standing. Mirrors the API's
/// <c>UndoneEffect</c>. Carries names, never a credential.
/// </summary>
/// <param name="Kind">What kind of thing, e.g. <c>identity-project</c>.</param>
/// <param name="Identifier">The external id an operator needs to find it.</param>
/// <param name="Reason">Why the undo did not happen.</param>
/// <param name="BlocksRetry">Whether a replay over this effect is refused until it is cleaned up.</param>
public sealed record SagaUndoneEffectDto(
    string Kind,
    string Identifier,
    string Reason,
    bool BlocksRetry = true);

/// <summary>
/// Lightweight summary of a provisioned resource for list views. Mirrors the API's
/// <c>ResourceSummaryDto</c>.
/// </summary>
/// <param name="Id">The resource id.</param>
/// <param name="Kind">The resource kind, e.g. <c>postgres-database</c>.</param>
/// <param name="Provider">The backing provider, e.g. <c>managed-relational</c>.</param>
/// <param name="Name">The tenant-chosen, org-unique resource name.</param>
/// <param name="Status">The lifecycle status.</param>
/// <param name="CreatedAt">When the resource record was created.</param>
/// <param name="ProvisionedAt">When the backing resource became ready, when it has.</param>
public sealed record ResourceSummaryDto(
    Guid Id,
    string Kind,
    string Provider,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProvisionedAt);

/// <summary>
/// Read model of a resource ↔ application binding, joined with the resource's identity.
/// Mirrors the API's <c>ResourceBindingDto</c>. A <c>null</c> <see cref="EnvironmentId"/> covers
/// every environment of the application. Carries the secret NAME and credential key NAMES only —
/// never any credential value.
/// </summary>
/// <param name="Id">The binding id.</param>
/// <param name="ResourceId">The bound resource id.</param>
/// <param name="ResourceName">The bound resource's name.</param>
/// <param name="Kind">The bound resource's kind.</param>
/// <param name="Provider">The bound resource's provider.</param>
/// <param name="ResourceStatus">The bound resource's current status.</param>
/// <param name="ApplicationId">The application the resource is bound to.</param>
/// <param name="EnvironmentId">The environment scope; <c>null</c> = all environments.</param>
/// <param name="SecretName">The K8s secret injected at deploy, when the resource has one.</param>
/// <param name="CredentialKeys">The credential key NAMES the secret exposes (never the values).</param>
/// <param name="BoundBy">Who created the binding.</param>
/// <param name="BoundAt">When the binding was created.</param>
public sealed record ResourceBindingDto(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    string Kind,
    string Provider,
    string ResourceStatus,
    Guid ApplicationId,
    Guid? EnvironmentId,
    string? SecretName,
    IReadOnlyList<string> CredentialKeys,
    string? BoundBy,
    DateTimeOffset BoundAt);

/// <summary>
/// Input to provision a resource. Mirrors the API's <c>CreateResourceRequest</c> body.
/// Provisioning creates REAL backing infrastructure that costs money — the org is charged
/// for what is created here.
/// </summary>
/// <param name="Kind">The resource kind to provision, e.g. <c>postgres-database</c> (from <see cref="IResourceService.ListOfferingsAsync"/>).</param>
/// <param name="Name">The org-unique resource name (a stable identifier for later bind/refresh calls).</param>
/// <param name="ProjectId">Optional owning project id; <c>null</c> = org-scoped.</param>
/// <param name="Options">Optional provider-specific provisioning options (e.g. size / region hints).</param>
/// <param name="Provider">
/// Optional explicit provider for the kind . Omit it — the usual case — and the
/// organization's configured provider decides, then the catalog default. Naming one that does not
/// serve the kind is rejected rather than substituted.
/// </param>
public sealed record ProvisionResourceRequest(
    string Kind,
    string Name,
    Guid? ProjectId = null,
    IReadOnlyDictionary<string, string>? Options = null,
    string? Provider = null);
