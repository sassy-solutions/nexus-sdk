// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Nexus.Sdk.Management.Models;

// ----------------------------------------------------------------------
// SDK-local request/response records mirroring the Nexus.Api management
// controller DTOs (Organizations, Projects, Applications, Environments,
// Roles). These are intentionally decoupled from Nexus.Core / Nexus.Api
// types so the SDK package carries no server-side dependency and the wire
// contract stays explicit and reviewable in one place.
// ----------------------------------------------------------------------

#region Organizations

// OrganizationDto, OrganizationDetailDto and OrganizationVipStatusDto
// left with the three methods that returned them. They mirrored GET api/v1/organizations,
// GET api/v1/organizations/{id} and PATCH api/v1/organizations/{id}/vip, none of which the
// API serves: OrganizationsController is mounted on api/organizations only. A DTO whose
// only producer is an unserved route is a shape nothing can ever fill.

/// <summary>One organization the current user belongs to (mirrors <c>MyOrganizationResponse</c>).</summary>
public sealed record MyOrganizationDto(
    Guid OrganizationId,
    string Name,
    string OrganizationStatus,
    Guid MyUserId,
    string MyStatus,
    string[] Roles);

/// <summary>
/// one organization under another (mirrors <c>OrganizationDescendantResponse</c>).
/// </summary>
/// <remarks>
/// Placement and identity only. Holding this row is not access to that organization's data:
/// reading a descendant still requires membership of it, exactly as it did before the
/// hierarchy existed.
/// </remarks>
/// <param name="Depth">Levels below the organization asked about — a direct child is 1.</param>
public sealed record OrganizationDescendantDto(
    string Id,
    string Name,
    string? DisplayName,
    string Status,
    string? ParentOrganizationId,
    int Depth);

/// <summary>
/// one page of an organization's sub-tree (mirrors
/// <c>ListDescendantOrganizationsResponse</c>). The page metadata is carried rather than
/// dropped: a governance tree is the one list in this SDK whose size the caller cannot guess,
/// and <paramref name="HasMore"/> is the only honest way to know a page was not the last.
/// </summary>
public sealed record OrganizationDescendantsPageDto(
    IReadOnlyList<OrganizationDescendantDto> Descendants,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasMore);

/// <summary>
/// Per-namespace reconciliation outcome (mirrors <c>NamespaceReconciliationResult</c>
/// on the tenant-namespace-reconciler surface).
/// </summary>
/// <param name="Outcome">
/// One of <c>Reconciled</c>, <c>Skipped</c> (namespace absent on the cluster), or
/// <c>Failed</c>. A skipped namespace is neither a success nor a failure.
/// </param>
/// <param name="NotRequired">
/// Logical resource types this namespace does not need: nothing was written and nothing
/// is missing. A third basket beside <paramref name="Created"/> and
/// <paramref name="AlreadyPresent"/> . Nothing links this DTO to the record
/// it mirrors at compile time, so it is kept in step by hand — see the type it mirrors.
/// </param>
public sealed record NamespaceReconciliationResultDto(
    string Namespace,
    string Kind,
    bool Succeeded,
    IReadOnlyList<string> Created,
    IReadOnlyList<string> AlreadyPresent,
    string? ErrorMessage,
    string Outcome = "Reconciled",
    IReadOnlyList<string>? NotRequired = null);

/// <summary>
/// Aggregate report for reconciling one organization's namespaces
/// (mirrors <c>NamespaceReconciliationReport</c>).
/// </summary>
/// <param name="SkippedCount">
/// Number of namespaces skipped because they do not exist on the cluster — an
/// expected, benign condition distinct from <paramref name="FailedCount"/>.
/// </param>
/// <param name="NotRequiredCount">
/// Number of resources that were not required across all namespaces: nothing was
/// written and nothing is missing . Not a fault — a namespace whose
/// resources are all not-required is still legitimately reconciled and still counts
/// in <paramref name="SucceededCount"/>.
/// </param>
public sealed record NamespaceReconciliationReportDto(
    Guid OrganizationId,
    IReadOnlyList<NamespaceReconciliationResultDto> Namespaces,
    DateTimeOffset ReconciledAt,
    int SucceededCount,
    int FailedCount,
    int CreatedCount,
    int AlreadyPresentCount,
    int SkippedCount = 0,
    int NotRequiredCount = 0);

#endregion

#region Projects

/// <summary>Summary view of a project (mirrors <c>ProjectSummaryResponse</c>).</summary>
public sealed record ProjectDto(
    string Id,
    string Name,
    string? DisplayName,
    string Status,
    int OidcAppCount,
    DateTimeOffset CreatedAt);

/// <summary>Full project detail (mirrors <c>ProjectDetailResponse</c>).</summary>
public sealed record ProjectDetailDto(
    string Id,
    string OrganizationId,
    string Name,
    string? DisplayName,
    string? Description,
    string Status,
    [property: JsonPropertyName("zitadelProjectId")] string? IdentityProjectId,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ProvisionedAt);

/// <summary>Create-project request body (mirrors <c>CreateProjectRequest</c>).</summary>
public sealed record CreateProjectRequest(
    string Name,
    string? DisplayName = null,
    string? Description = null);

/// <summary>Create-project response (mirrors <c>CreateProjectResponse</c>).</summary>
public sealed record CreateProjectResult(
    string Id,
    string Name,
    string? DisplayName,
    DateTimeOffset CreatedAt);

#endregion

#region Applications

/// <summary>Summary view of an application (mirrors <c>ApplicationResponse</c>).</summary>
public sealed record ApplicationDto(
    string Id,
    string OwnerOrganizationId,
    string Name,
    string Subdomain,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>Full application detail (mirrors <c>ApplicationDetailResponse</c>).</summary>
public sealed record ApplicationDetailDto(
    string Id,
    string OwnerOrganizationId,
    string Name,
    string Subdomain,
    string? Description,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    ApplicationSettingsDto? Settings = null);

/// <summary>
/// An application's editable settings (mirrors <c>ApplicationSettingsResponse</c>):
/// replicas + resource limits are hot-applied; env vars apply on the next deploy.
/// </summary>
public sealed record ApplicationSettingsDto(
    int Replicas,
    string? CpuRequest,
    string? CpuLimit,
    string? MemoryRequest,
    string? MemoryLimit,
    bool AutoscalingEnabled,
    int MinReplicas,
    int MaxReplicas,
    int TargetCpuUtilizationPercent,
    IReadOnlyDictionary<string, string> EnvironmentVariables);

/// <summary>
/// Initial application settings supplied at create time (mirrors
/// <c>CreateApplicationSettingsRequest</c>).
/// </summary>
public sealed record CreateApplicationSettings(
    int Replicas,
    string CpuRequest,
    string CpuLimit,
    string MemoryRequest,
    string MemoryLimit,
    bool AutoscalingEnabled,
    int MinReplicas,
    int MaxReplicas,
    int TargetCpuUtilizationPercent,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);

/// <summary>Create-application request body (mirrors <c>CreateApplicationRequest</c>).</summary>
public sealed record CreateApplicationRequest(
    string OwnerOrganizationId,
    string Name,
    string Subdomain,
    string? Description = null,
    CreateApplicationSettings? Settings = null);

/// <summary>
/// Update-application-settings request body (mirrors <c>UpdateApplicationSettingsRequest</c>).
/// Carries only the editable fields; <c>ExpectedName</c> / <c>ExpectedSubdomain</c> are
/// optional immutability assertions.
/// </summary>
public sealed record UpdateApplicationSettingsRequest(
    string? Description,
    int Replicas,
    string CpuRequest,
    string CpuLimit,
    string MemoryRequest,
    string MemoryLimit,
    bool AutoscalingEnabled,
    int MinReplicas,
    int MaxReplicas,
    int TargetCpuUtilizationPercent,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    string? ExpectedName = null,
    string? ExpectedSubdomain = null);

/// <summary>Create-application response (mirrors <c>CreateApplicationResponse</c>).</summary>
public sealed record CreateApplicationResult(
    string Id,
    string Name,
    string Subdomain,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>A single tagged version of an application (mirrors <c>VersionResponse</c>).</summary>
public sealed record VersionDto(
    string Tag,
    string CommitSha,
    string? Description,
    string TaggedBy,
    DateTimeOffset TaggedAt);

/// <summary>Tag-version request body (mirrors <c>TagVersionRequest</c>).</summary>
public sealed record TagVersionRequest(
    string VersionTag,
    string? CommitSha = null,
    string? Description = null);

/// <summary>Tag-version result (mirrors <c>TagVersionResult</c>).</summary>
public sealed record TagVersionResult(
    string VersionTag,
    string CommitSha);

/// <summary>
/// Result of a versioned deployment or promotion (mirrors <c>VersionDeploymentResult</c>).
/// </summary>
public sealed record VersionDeploymentResult(
    string Url,
    string ArgoAppName,
    string VersionTag,
    string EnvironmentName);

/// <summary>
/// Result of decommissioning a specific deployed version from an environment
/// (mirrors <c>VersionDecommissionResult</c>). Independent of deploy — only the named
/// (version, environment) deployment is torn down; coexisting versions keep serving.
/// </summary>
public sealed record VersionDecommissionResult(
    string VersionTag,
    string EnvironmentName,
    string ArgoAppName);

/// <summary>
/// Promote-version request body (mirrors <c>PromoteVersionRequest</c> on the
/// versions-promote-audit surface).
/// </summary>
public sealed record PromoteVersionRequest(
    Guid FromEnvironmentId,
    Guid ToEnvironmentId,
    string Version);

/// <summary>A single audit-log entry (mirrors <c>AuditEntryResponse</c>).</summary>
public sealed record AuditEntryDto(
    string EventType,
    string Actor,
    DateTimeOffset OccurredAt,
    string? Version,
    string Summary);

/// <summary>An application's paged audit log (mirrors <c>ApplicationAuditLogResponse</c>).</summary>
public sealed record ApplicationAuditLogDto(
    string ApplicationId,
    IReadOnlyList<AuditEntryDto> Entries,
    int TotalCount);

/// <summary>
/// A single git commit on an application's linked repository (mirrors <c>CommitResponse</c>),
/// surfaced so a caller can pick a commit to deploy.
/// </summary>
public sealed record CommitDto(
    string Sha,
    string Message,
    string? AuthorName,
    DateTimeOffset? AuthoredAt,
    string HtmlUrl);

/// <summary>A single git branch on an application's linked repository (mirrors <c>BranchResponse</c>).</summary>
public sealed record BranchDto(
    string Name,
    string Sha,
    bool Protected);

/// <summary>
/// Request to start an on-demand build-and-deploy from a commit/branch (mirrors
/// <c>BuildAndDeployFromCommitRequest</c>). Provide exactly one of
/// <see cref="CommitSha"/> or <see cref="Branch"/>; <see cref="VersionTag"/> is optional.
/// </summary>
public sealed record BuildAndDeployFromCommitRequest(
    Guid EnvironmentId,
    string? CommitSha = null,
    string? Branch = null,
    string? VersionTag = null);

/// <summary>Result of starting a build-and-deploy (mirrors <c>BuildAndDeployFromCommitResult</c>).</summary>
public sealed record BuildAndDeployFromCommitResult(
    string BuildDeployId,
    string ApplicationId,
    Guid EnvironmentId,
    string VersionTag,
    string? CommitSha,
    string? Branch,
    string Status,
    DateTimeOffset RequestedAt);

/// <summary>Live status of a build-and-deploy operation (mirrors <c>BuildDeployStatusResponse</c>).</summary>
public sealed record BuildDeployStatusDto(
    string BuildDeployId,
    string ApplicationId,
    Guid EnvironmentId,
    string? EnvironmentName,
    string VersionTag,
    string? CommitSha,
    string? Branch,
    string Status,
    string? FailureReason,
    string? Url,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt);

#endregion

#region Environments

/// <summary>Summary view of an environment (mirrors <c>EnvironmentListItem</c>).</summary>
public sealed record EnvironmentDto(
    Guid Id,
    string Name,
    string Description,
    string Slug,
    string Region,
    string Status,
    string? NamespaceName,
    bool IsDefault,
    DateTimeOffset? ProvisionedAt,
    DateTimeOffset CreatedAt);

/// <summary>Full environment detail (mirrors <c>GetEnvironmentResponse</c>).</summary>
public sealed record EnvironmentDetailDto(
    Guid EnvironmentId,
    string Name,
    string Description,
    string Slug,
    string? TemplateId,
    string Region,
    string OrganizationId,
    string Status,
    string? NamespaceName,
    bool IsDefault,
    DateTimeOffset? ProvisionedAt);

/// <summary>Create-environment request body (mirrors <c>CreateEnvironmentRequest</c>).</summary>
public sealed record CreateEnvironmentRequest(
    string Name,
    string Description,
    string Slug,
    string Region,
    string OrganizationId,
    string CreatedBy,
    string? TemplateId = null,
    string? UserId = null,
    bool IsDefault = false);

/// <summary>Create-environment response (mirrors <c>CreateEnvironmentResponse</c>).</summary>
public sealed record CreateEnvironmentResult(Guid EnvironmentId);

#endregion

#region Roles

/// <summary>Summary view of a role (mirrors <c>RoleResponse</c>).</summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount);

/// <summary>Full role detail (mirrors <c>RoleDetailResponse</c>).</summary>
public sealed record RoleDetailDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    Dictionary<string, string[]> Permissions,
    bool IsSystem,
    int UserCount,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

/// <summary>Create-role request body (mirrors <c>CreateRoleRequest</c>).</summary>
public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    Dictionary<string, string[]> Permissions);

/// <summary>Update-role request body (mirrors <c>UpdateRoleRequest</c>).</summary>
public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    Dictionary<string, string[]> Permissions);

/// <summary>
/// An ABAC-lite role attribute triple (mirrors <c>RoleAttributeDto</c>).
/// <paramref name="ConditionOperator"/> is the enum name (e.g. <c>Equals</c>).
/// </summary>
public sealed record RoleAttributeDto(
    Guid Id,
    Guid RoleId,
    string Resource,
    string Action,
    string? ConditionLeft,
    string? ConditionOperator,
    string? ConditionRight,
    string? ConditionDisplay,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// Structured condition payload for an attribute (mirrors <c>AttributeConditionPayload</c>).
/// <paramref name="Operator"/> is the <c>ConditionOperator</c> enum name
/// (one of <c>Equals</c>, <c>NotEquals</c>, <c>In</c>, <c>LessThan</c>, <c>GreaterThan</c>).
/// </summary>
public sealed record AttributeConditionPayload(
    string Left,
    string Operator,
    string Right);

/// <summary>
/// Add-attribute request body (mirrors <c>AddRoleAttributeRequest</c>). Provide either a
/// structured <paramref name="Condition"/> or a <paramref name="RawExpression"/> (parsed
/// server-side), or neither for an unconditional grant.
/// </summary>
public sealed record AddRoleAttributeRequest(
    string Resource,
    string Action,
    AttributeConditionPayload? Condition = null,
    string? RawExpression = null);

/// <summary>Add-attribute response (mirrors <c>AddRoleAttributeResponse</c>).</summary>
public sealed record AddRoleAttributeResult(Guid AttributeId);

/// <summary>
/// One scoped role assignment in a role-assignment replace request
/// (mirrors <c>RoleAssignmentRequest</c>).
/// </summary>
/// <param name="RoleId">Role identifier.</param>
/// <param name="ScopeType">"Organization", "Project", or "Environment".</param>
/// <param name="ScopeId">Scoped resource id; <see langword="null"/> for Organization scope.</param>
public sealed record RoleAssignmentRequest(
    Guid RoleId,
    string ScopeType,
    Guid? ScopeId = null);

#endregion
