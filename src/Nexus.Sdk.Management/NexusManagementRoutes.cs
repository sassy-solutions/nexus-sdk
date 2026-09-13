// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Management;

/// <summary>
/// the governance half of the route registry: every path the administration
/// surface reaches, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// These constants left <c>Nexus.Sdk.Client.NexusRoutes</c> when the SDK split in two. The cut
/// is not cosmetic: <c>SassySolutions.Nexus.Sdk</c> is what a tenant application takes to be
/// <em>governed by</em> Nexus, and it must not even <b>name</b> a route it is not allowed to
/// call. The client package's own contract test now asserts that its registry and this one do
/// not intersect.
/// </para>
/// <para>
/// Placeholder filling still goes through <see cref="Client.NexusRoutes.Of"/>: one
/// implementation, one escaping rule, two registries.
/// </para>
/// </remarks>
public static class NexusManagementRoutes
{
    // ---------------------------------------------------------------------
    // Organizations
    // ---------------------------------------------------------------------

    /// <summary>The organizations the calling principal belongs to.</summary>
    public const string MyOrganizations = "api/v1/me/organizations";

    /// <summary>Reconciles the baseline the orchestrator resources of an organization's namespaces.</summary>
    public const string AdminOrganizationReconcileNamespaces =
        "api/v1/admin/organizations/{orgId}/reconcile-namespaces";

    /// <summary>
    /// The organizations one organization owns — the shape of its sub-tree .
    /// Spelled with <c>{id}</c> because that is the parameter the served template carries:
    /// the action binds <c>[FromRoute] string id</c>, which is not one of the names
    /// <c>ApiVersionRoutePrefixConvention</c> canonicalises onto <c>{orgId}</c>.
    /// </summary>
    public const string OrganizationDescendants = "api/v1/organizations/{id}/descendants";

    // ---------------------------------------------------------------------
    // Projects
    // ---------------------------------------------------------------------

    /// <summary>Projects of an organization.</summary>
    public const string OrganizationProjects = "api/v1/organizations/{orgId}/projects";

    /// <summary>One project of an organization.</summary>
    public const string OrganizationProject = "api/v1/organizations/{orgId}/projects/{id}";

    // ---------------------------------------------------------------------
    // Applications
    // ---------------------------------------------------------------------

    /// <summary>Applications collection.</summary>
    public const string Applications = "api/v1/applications";

    /// <summary>One application.</summary>
    public const string Application = "api/v1/applications/{applicationId}";

    /// <summary>Tagged versions of an application.</summary>
    public const string ApplicationVersions = "api/v1/applications/{applicationId}/versions";

    /// <summary>Deploys one tagged version.</summary>
    public const string ApplicationVersionDeploy =
        "api/v1/applications/{applicationId}/versions/{version}/deploy";

    /// <summary>Undeploys one tagged version from one environment.</summary>
    public const string ApplicationVersionEnvironment =
        "api/v1/applications/{applicationId}/versions/{version}/environments/{envId}";

    /// <summary>Promotes an application between environments.</summary>
    public const string ApplicationPromote = "api/v1/applications/{applicationId}/promote";

    /// <summary>Audit log of an application.</summary>
    public const string ApplicationAuditLog = "api/v1/applications/{applicationId}/audit-log";

    /// <summary>Commits of the application's repository.</summary>
    public const string ApplicationRepositoryCommits =
        "api/v1/applications/{applicationId}/repository/commits";

    /// <summary>Branches of the application's repository.</summary>
    public const string ApplicationRepositoryBranches =
        "api/v1/applications/{applicationId}/repository/branches";

    /// <summary>Builds and deploys an application from one commit.</summary>
    public const string ApplicationDeployFromCommit =
        "api/v1/applications/{applicationId}/deploy-from-commit";

    /// <summary>State of one build-and-deploy run.</summary>
    public const string ApplicationBuildDeploy =
        "api/v1/applications/{applicationId}/build-deploys/{buildDeployId}";

    /// <summary>
    /// Application settings, on the org-scoped route so the write resolves under RLS for any
    /// authorized caller (mirrors <c>OrganizationApplicationsController</c>).
    /// </summary>
    public const string OrganizationApplicationSettings =
        "api/v1/organizations/{orgId}/applications/{applicationId}/settings";

    // ---------------------------------------------------------------------
    // Environments
    // ---------------------------------------------------------------------

    /// <summary>Environments collection. Served with a capital E — see the class remarks.</summary>
    public const string Environments = "api/v1/Environments";

    /// <summary>One environment. Served with a capital E — see the class remarks.</summary>
    public const string Environment = "api/v1/Environments/{envId}";

    // ---------------------------------------------------------------------
    // Roles
    // ---------------------------------------------------------------------

    /// <summary>Roles of an organization.</summary>
    public const string OrganizationRoles = "api/v1/organizations/{orgId}/roles";

    /// <summary>One role.</summary>
    public const string OrganizationRole = "api/v1/organizations/{orgId}/roles/{roleId}";

    /// <summary>Attributes of one role.</summary>
    public const string OrganizationRoleAttributes =
        "api/v1/organizations/{orgId}/roles/{roleId}/attributes";

    /// <summary>One attribute of one role.</summary>
    public const string OrganizationRoleAttribute =
        "api/v1/organizations/{orgId}/roles/{roleId}/attributes/{attributeId}";

    /// <summary>Role assignments of one platform user in one organization.</summary>
    public const string OrganizationPlatformUserRoleAssignments =
        "api/v1/organizations/{orgId}/platform-users/{userId}/role-assignments";

    // ---------------------------------------------------------------------
    // GitHub credential
    // ---------------------------------------------------------------------

    /// <summary>The calling user's saved GitHub credential.</summary>
    public const string MyGitHubCredential = "api/v1/me/github-credential";

    // ---------------------------------------------------------------------
    // Platform resources
    // ---------------------------------------------------------------------

    /// <summary>Catalogue of provisionable resource offerings.</summary>
    public const string ResourceOfferings = "api/v1/resources/offerings";

    /// <summary>Resources owned by an organization.</summary>
    public const string OrganizationResources = "api/v1/organizations/{orgId}/resources";

    /// <summary>One resource owned by an organization.</summary>
    public const string OrganizationResource =
        "api/v1/organizations/{orgId}/resources/{resourceId}";

    /// <summary>Re-reads the provisioning status of one resource from its provider.</summary>
    public const string OrganizationResourceRefreshStatus =
        "api/v1/organizations/{orgId}/resources/{resourceId}/refresh-status";

    /// <summary>Resources bound to an application.</summary>
    public const string ApplicationResources = "api/v1/applications/{applicationId}/resources";

    /// <summary>Bindings between one application and one resource.</summary>
    public const string ApplicationResourceBindings =
        "api/v1/applications/{applicationId}/resources/{resourceId}/bindings";

    // ---------------------------------------------------------------------
    // Git connections
    // ---------------------------------------------------------------------

    /// <summary>Git connections of an organization.</summary>
    public const string OrganizationGitConnections =
        "api/v1/organizations/{orgId}/git/connections";

    /// <summary>One git connection.</summary>
    public const string OrganizationGitConnection =
        "api/v1/organizations/{orgId}/git/connections/{connectionId}";

    /// <summary>Repositories reachable through one git connection.</summary>
    public const string OrganizationGitConnectionRepositories =
        "api/v1/organizations/{orgId}/git/connections/{connectionId}/repositories";

    /// <summary>Starts the GitHub App install flow for an organization.</summary>
    public const string OrganizationGitConnectionsGitHubInstallUrl =
        "api/v1/organizations/{orgId}/git/connections/github/install-url";

    /// <summary>Attaches a GitHub organization to a Nexus organization.</summary>
    public const string OrganizationGitHubOrganizations =
        "api/v1/organizations/{orgId}/git/github/organizations";

    /// <summary>Completes the GitHub App setup handshake.</summary>
    public const string GitGitHubSetupComplete = "api/v1/git/github/setup/complete";

    /// <summary>Capabilities of the configured git providers.</summary>
    public const string GitCapabilities = "api/v1/git/capabilities";

    /// <summary>Reconciles platform-wide git state.</summary>
    public const string PlatformGitReconcile = "api/v1/platform/git/reconcile";
}
