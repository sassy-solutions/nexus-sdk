// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Management.Services.Http;

namespace Nexus.Sdk.Tests.Management;

/// <summary>Unit tests for <see cref="HttpGitConnectionService"/> — route/payload + error mapping.</summary>
public sealed class HttpGitConnectionServiceTests
{
    private const string Org = "11111111-1111-1111-1111-111111111111";
    private const string Conn = "22222222-2222-2222-2222-222222222222";

    private const string ConnectionJson = """
        {"id":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","provider":"github","authMode":"app-installation","accountLogin":"acme","accountType":"organization","installationRef":"12345","status":"active","createdBy":"admin@acme.io","createdAt":"2026-01-01T00:00:00+00:00","updatedBy":null,"updatedAt":null}
        """;

    private const string ListJson = """
        {"connections":[{"id":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","provider":"github","authMode":"app-installation","accountLogin":"acme","accountType":"organization","installationRef":"12345","status":"active","createdBy":"admin@acme.io","createdAt":"2026-01-01T00:00:00+00:00","updatedBy":null,"updatedAt":null}]}
        """;

    private const string ReposJson = """
        {"repositories":[{"fullName":"acme/web","htmlUrl":"https://github.com/acme/web","defaultBranch":"main","private":true}]}
        """;

    private const string InstallUrlJson = """
        {"url":"https://github.com/apps/nexus/installations/new?state=signed"}
        """;

    private const string ReconcileJson = """
        {"dryRun":true,"autoApply":false,"items":[{"connectionId":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","installationRef":"12345","currentAccountLogin":"acme","currentStatus":"active","proposedAction":2,"authoritativeAccountLogin":"acme-renamed","installationSuspended":false,"applied":false,"applyError":null}],"orphans":[],"appliedCount":0}
        """;

    private const string CreatedOrgJson = """
        {"login":"NXS-acme","htmlUrl":"https://github.com/NXS-acme","installRedirectUrl":"https://github.com/apps/nexus/installations/new/permissions?target_id=42"}
        """;

    private const string CapabilitiesJson = """
        {"providers":[{"provider":"github","connect":true,"createOrganization":false,"createOrganizationDisabledReason":"Enterprise credential not configured."}]}
        """;

    // --- ListAsync ---

    [Fact]
    public async Task ListAsync_OnSuccess_MapsConnections_AndHitsOrgScopedRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(Org, includeDisconnected: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle();
        result.Value![0].AccountLogin.Should().Be("acme");
        result.Value[0].Status.Should().Be("active");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/connections");
    }

    [Fact]
    public async Task ListAsync_WithIncludeDisconnected_AppendsQuery()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(Org, includeDisconnected: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.RequestUri!.Query.Should().Contain("includeDisconnected=true");
    }

    [Fact]
    public async Task ListAsync_WithEmptyOrg_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync("  ", includeDisconnected: false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- GetAsync ---

    [Fact]
    public async Task GetAsync_OnSuccess_MapsDetail()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ConnectionJson);
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(Guid.Parse(Conn));
        result.Value.OrganizationId.Should().Be(Guid.Parse(Org));
        result.Value.InstallationRef.Should().Be("12345");
        handler.LastRequest!.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/connections/{Conn}");
    }

    [Fact]
    public async Task GetAsync_OnNotFound_ReturnsFailureWithoutThrowing()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, "");
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetAsync_WithEmptyConnectionId_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ConnectionJson);
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, "  ", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- GetInstallUrlAsync ---

    [Fact]
    public async Task GetInstallUrlAsync_PostsToInstallUrlRoute_AndMapsUrl()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, InstallUrlJson);
        var sut = BuildSut(handler);

        var result = await sut.GetInstallUrlAsync(Org, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Url.Should().Contain("installations/new?state=signed");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/connections/github/install-url");
    }

    // --- CompleteGitHubSetupAsync ---

    [Fact]
    public async Task CompleteGitHubSetupAsync_PostsSignedStateBody_AndMapsConnection()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ConnectionJson);
        var sut = BuildSut(handler);

        var input = new CompleteGitHubSetupInput("999", "signed-state", SetupAction: "install", Code: "oauth-code");
        var result = await sut.CompleteGitHubSetupAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("active");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/git/github/setup/complete");
        handler.LastRequestBody.Should().Contain("\"installationId\":\"999\"").And.Contain("\"state\":\"signed-state\"");
    }

    [Fact]
    public async Task CompleteGitHubSetupAsync_WithMissingState_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ConnectionJson);
        var sut = BuildSut(handler);

        var result = await sut.CompleteGitHubSetupAsync(new CompleteGitHubSetupInput("999", "  "), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- DisconnectAsync ---

    [Fact]
    public async Task DisconnectAsync_OnNoContent_SucceedsAndUsesDelete()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NoContent, "");
        var sut = BuildSut(handler);

        var result = await sut.DisconnectAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/connections/{Conn}");
    }

    [Fact]
    public async Task DisconnectAsync_OnNotFound_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, """{"title":"Not Found"}""");
        var sut = BuildSut(handler);

        var result = await sut.DisconnectAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    // --- ListRepositoriesAsync ---

    [Fact]
    public async Task ListRepositoriesAsync_OnSuccess_MapsRepos_AndHitsRepositoriesRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ReposJson);
        var sut = BuildSut(handler);

        var result = await sut.ListRepositoriesAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.FullName.Should().Be("acme/web");
        result.Value![0].Private.Should().BeTrue();
        handler.LastRequest!.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/connections/{Conn}/repositories");
    }

    [Fact]
    public async Task ListRepositoriesAsync_OnConflict_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.Conflict, """{"title":"Conflict","detail":"Connection is 'pending', not active."}""");
        var sut = BuildSut(handler);

        var result = await sut.ListRepositoriesAsync(Org, Conn, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("not active");
    }

    // --- ReconcileAsync ---

    [Fact]
    public async Task ReconcileAsync_DefaultDryRun_PostsToPlatformRoute_AndMapsReport()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ReconcileJson);
        var sut = BuildSut(handler);

        var result = await sut.ReconcileAsync(ct: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DryRun.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.ProposedAction.Should().Be(GitReconcileAction.RefreshAccountLogin);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/platform/git/reconcile");
        handler.LastRequestBody.Should().Contain("\"dryRun\":true");
    }

    [Fact]
    public async Task ReconcileAsync_ApplyRun_SendsExplicitConnectionIds()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ReconcileJson);
        var sut = BuildSut(handler);

        var result = await sut.ReconcileAsync(dryRun: false, connectionIds: new[] { Conn }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequestBody.Should().Contain("\"dryRun\":false").And.Contain(Conn);
    }

    // --- CreateGithubOrganizationAsync ---

    [Fact]
    public async Task CreateGithubOrganizationAsync_OnCreated_PostsToOrgScopedRoute_AndMapsResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreatedOrgJson);
        var sut = BuildSut(handler);

        var result = await sut.CreateGithubOrganizationAsync(
            Org, new CreateGithubOrganizationInput("acme"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value!.Login.Should().Be("NXS-acme");
        result.Value.HtmlUrl.Should().Be("https://github.com/NXS-acme");
        result.Value.InstallRedirectUrl.Should().Contain("installations/new/permissions?target_id=42");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/git/github/organizations");
        handler.LastRequestBody.Should().Contain("\"name\":\"acme\"");
    }

    [Fact]
    public async Task CreateGithubOrganizationAsync_WithEmptyOrg_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreatedOrgJson);
        var sut = BuildSut(handler);

        var result = await sut.CreateGithubOrganizationAsync(
            "  ", new CreateGithubOrganizationInput("acme"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CreateGithubOrganizationAsync_WithEmptyName_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreatedOrgJson);
        var sut = BuildSut(handler);

        var result = await sut.CreateGithubOrganizationAsync(
            Org, new CreateGithubOrganizationInput("   "), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CreateGithubOrganizationAsync_OnLoginTaken_ReturnsConflict()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.Conflict, """{"title":"GithubOrg.LoginTaken","detail":"A GitHub org 'NXS-acme' already exists."}""");
        var sut = BuildSut(handler);

        var result = await sut.CreateGithubOrganizationAsync(
            Org, new CreateGithubOrganizationInput("acme"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateGithubOrganizationAsync_OnCreationUnavailable_ReturnsServiceUnavailable()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.ServiceUnavailable,
            """{"title":"GithubOrg.CreationUnavailable","detail":"Creating GitHub organizations is not configured on this host."}""");
        var sut = BuildSut(handler);

        var result = await sut.CreateGithubOrganizationAsync(
            Org, new CreateGithubOrganizationInput("acme"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Error.Should().Contain("not configured");
    }

    // --- GetCapabilitiesAsync ---

    [Fact]
    public async Task GetCapabilitiesAsync_OnSuccess_MapsProviders_AndHitsCapabilitiesRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, CapabilitiesJson);
        var sut = BuildSut(handler);

        var result = await sut.GetCapabilitiesAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var github = result.Value!.Providers.Should().ContainSingle().Subject;
        github.Provider.Should().Be("github");
        github.Connect.Should().BeTrue();
        github.CreateOrganization.Should().BeFalse();
        github.CreateOrganizationDisabledReason.Should().Contain("not configured");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/git/capabilities");
    }

    [Fact]
    public async Task GetCapabilitiesAsync_OnServerError_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, "");
        var sut = BuildSut(handler);

        var result = await sut.GetCapabilitiesAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    private static HttpGitConnectionService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpGitConnectionService>.Instance);
}
