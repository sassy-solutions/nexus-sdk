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

/// <summary>Unit tests for <see cref="HttpResourceService"/> — route/payload + error mapping.</summary>
public sealed class HttpResourceServiceTests
{
    private const string Org = "11111111-1111-1111-1111-111111111111";
    private const string Resource = "22222222-2222-2222-2222-222222222222";
    private const string App = "33333333-3333-3333-3333-333333333333";
    private const string Env = "44444444-4444-4444-4444-444444444444";

    private const string ResourceJson = """
        {"id":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","projectId":null,"kind":"relational-database","provider":"managed-relational","name":"orders-db","status":"Ready","externalId":"ext-123","secretName":"nexus-resource-orders-db","credentialKeys":["DATABASE_URL"],"metadata":{"host":"db.example"},"statusMessage":null,"provisionedAt":"2026-01-01T00:00:00+00:00","createdBy":"admin@acme.io","createdAt":"2026-01-01T00:00:00+00:00","updatedAt":"2026-01-01T00:00:00+00:00"}
        """;

    private const string OfferingsJson = """
        [{"kind":"relational-database","provider":"managed-relational","displayName":"PostgreSQL","description":"A managed database.","quotaFeatureCode":"resources.relational-database.max"}]
        """;

    private const string SummariesJson = """
        [{"id":"22222222-2222-2222-2222-222222222222","kind":"relational-database","provider":"managed-relational","name":"orders-db","status":"Ready","createdAt":"2026-01-01T00:00:00+00:00","provisionedAt":"2026-01-01T00:00:00+00:00"}]
        """;

    private const string BindingsJson = """
        [{"id":"55555555-5555-5555-5555-555555555555","resourceId":"22222222-2222-2222-2222-222222222222","resourceName":"orders-db","kind":"relational-database","provider":"managed-relational","resourceStatus":"Ready","applicationId":"33333333-3333-3333-3333-333333333333","environmentId":null,"secretName":"nexus-resource-orders-db","credentialKeys":["DATABASE_URL"],"boundBy":"admin@acme.io","boundAt":"2026-01-01T00:00:00+00:00"}]
        """;

    // --- ListOfferingsAsync ---

    [Fact]
    public async Task ListOfferingsAsync_OnSuccess_MapsOfferings_AndHitsCatalogRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, OfferingsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListOfferingsAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Kind.Should().Be("relational-database");
        result.Value![0].QuotaFeatureCode.Should().Be("resources.relational-database.max");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/resources/offerings");
    }

    // --- ListAsync ---

    [Fact]
    public async Task ListAsync_OnSuccess_MapsSummaries_AndHitsOrgScopedRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, SummariesJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(Org, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Name.Should().Be("orders-db");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/v1/organizations/{Org}/resources");
    }

    [Fact]
    public async Task ListAsync_WithEmptyOrg_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, SummariesJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync("  ", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- GetAsync ---

    [Fact]
    public async Task GetAsync_OnSuccess_MapsDetail()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, Resource, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(Guid.Parse(Resource));
        result.Value.Kind.Should().Be("relational-database");
        result.Value.CredentialKeys.Should().ContainSingle().Which.Should().Be("DATABASE_URL");
        handler.LastRequest!.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/resources/{Resource}");
    }

    [Fact]
    public async Task GetAsync_OnNotFound_ReturnsFailureWithoutThrowing()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, """{"title":"Not Found","detail":"Resource not found."}""");
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, Resource, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetAsync_WithEmptyResourceId_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(Org, "  ", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- ProvisionAsync ---

    [Fact]
    public async Task ProvisionAsync_PostsBody_AndMapsResource()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, ResourceJson);
        var sut = BuildSut(handler);

        var request = new ProvisionResourceRequest(
            "relational-database",
            "orders-db",
            Options: new Dictionary<string, string> { ["region"] = "fr-par" });
        var result = await sut.ProvisionAsync(Org, request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("orders-db");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/v1/organizations/{Org}/resources");
        handler.LastRequestBody.Should().Contain("\"kind\":\"relational-database\"")
            .And.Contain("\"name\":\"orders-db\"")
            .And.Contain("fr-par");
    }

    [Fact]
    public async Task ProvisionAsync_OnConflict_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.Conflict, """{"title":"Conflict","detail":"A resource with this name already exists."}""");
        var sut = BuildSut(handler);

        var result = await sut.ProvisionAsync(Org, new ProvisionResourceRequest("relational-database", "orders-db"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task ProvisionAsync_WithEmptyKind_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.ProvisionAsync(Org, new ProvisionResourceRequest("  ", "orders-db"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- RefreshStatusAsync ---

    [Fact]
    public async Task RefreshStatusAsync_PostsToRefreshRoute_AndMapsResource()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.RefreshStatusAsync(Org, Resource, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Ready");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/organizations/{Org}/resources/{Resource}/refresh-status");
    }

    // --- BindAsync ---

    [Fact]
    public async Task BindAsync_PostsSignedBody_AndHitsBindingsRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.BindAsync(App, Resource, Org, Guid.Parse(Env), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be($"/api/v1/applications/{App}/resources/{Resource}/bindings");
        handler.LastRequestBody.Should().Contain($"\"organizationId\":\"{Org}\"").And.Contain(Env);
    }

    [Fact]
    public async Task BindAsync_WithEmptyOrg_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.BindAsync(App, Resource, "  ", null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    // --- UnbindAsync ---

    [Fact]
    public async Task UnbindAsync_DeletesWithOrgAndEnvironmentQuery()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.UnbindAsync(App, Resource, Org, Guid.Parse(Env), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/v1/applications/{App}/resources/{Resource}/bindings");
        handler.LastRequest.RequestUri.Query.Should().Contain($"orgId={Org}").And.Contain($"environmentId={Env}");
    }

    [Fact]
    public async Task UnbindAsync_WithoutEnvironmentId_OmitsEnvironmentQuery()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ResourceJson);
        var sut = BuildSut(handler);

        var result = await sut.UnbindAsync(App, Resource, Org, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.RequestUri!.Query.Should().Contain($"orgId={Org}");
        handler.LastRequest.RequestUri.Query.Should().NotContain("environmentId");
    }

    // --- ListApplicationResourcesAsync ---

    [Fact]
    public async Task ListApplicationResourcesAsync_OnSuccess_MapsBindings_AndHitsAppScopedRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, BindingsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListApplicationResourcesAsync(App, Org, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.ResourceName.Should().Be("orders-db");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/applications/{App}/resources");
        handler.LastRequest.RequestUri.Query.Should().Contain($"orgId={Org}");
    }

    [Fact]
    public async Task ListApplicationResourcesAsync_WithEmptyApp_ReturnsBadRequest_WithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, BindingsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListApplicationResourcesAsync("  ", Org, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    private static HttpResourceService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpResourceService>.Instance);
}
