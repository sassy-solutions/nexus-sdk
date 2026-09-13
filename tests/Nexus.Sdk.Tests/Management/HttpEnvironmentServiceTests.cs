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
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Management.Services.Http;

namespace Nexus.Sdk.Tests.Management;

/// <summary>Unit tests for <see cref="HttpEnvironmentService"/>.</summary>
public sealed class HttpEnvironmentServiceTests
{
    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string EnvId = "55555555-5555-5555-5555-555555555555";

    private const string ListJson = """
        {"environments":[{"id":"55555555-5555-5555-5555-555555555555","name":"Production","description":"prod env","slug":"prod","region":"eu","status":"Active","namespaceName":"nexus-tenant-acme-prod","isDefault":true,"provisionedAt":"2026-01-01T00:00:00+00:00","createdAt":"2026-01-01T00:00:00+00:00"}]}
        """;

    private const string GetJson = """
        {"environmentId":"55555555-5555-5555-5555-555555555555","name":"Production","description":"prod env","slug":"prod","templateId":null,"region":"eu","organizationId":"11111111-1111-1111-1111-111111111111","status":"Active","namespaceName":"nexus-tenant-acme-prod","isDefault":true,"provisionedAt":"2026-01-01T00:00:00+00:00"}
        """;

    private const string CreateJson = """{"environmentId":"55555555-5555-5555-5555-555555555555"}""";

    [Fact]
    public async Task ListAsync_RequiresOrganizationId_AndMapsList()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(OrgId, provisionedOnly: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Slug.Should().Be("prod");
        // capital E, because that is how the server spells it
        // (`GET api/v1/Environments` in api-surface-snapshot.json). Routing is case-insensitive,
        // so nothing changes on the wire; the registry is spelled like the contract so the
        // reconciliation between the two is a comparison and not an interpretation.
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v1/Environments");
        handler.LastRequest.RequestUri.Query.Should().Contain($"organizationId={OrgId}").And.Contain("provisionedOnly=true");
    }

    [Fact]
    public async Task ListAsync_WithMissingOrganizationId_ReturnsBadRequestWithoutHttpCall()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync("   ", ct: CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_PassesOrganizationIdQuery_AndMapsDetail()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, GetJson);
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(EnvId, OrgId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NamespaceName.Should().Be("nexus-tenant-acme-prod");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/Environments/{EnvId}");
        handler.LastRequest.RequestUri.Query.Should().Contain($"organizationId={OrgId}");
    }

    [Fact]
    public async Task GetAsync_OnNotFound_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, """{"detail":"Environment Not Found"}""");
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(EnvId, OrgId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateAsync_PostsBody_AndMapsResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreateJson);
        var sut = BuildSut(handler);

        var request = new CreateEnvironmentRequest("Production", "prod env", "prod", "eu", OrgId, "admin@acme.io");
        var result = await sut.CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnvironmentId.Should().Be(Guid.Parse(EnvId));
        JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("slug").GetString().Should().Be("prod");
    }

    [Fact]
    public async Task CreateAsync_OnServerError_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, """{"detail":"boom"}""");
        var sut = BuildSut(handler);

        var request = new CreateEnvironmentRequest("Production", "prod env", "prod", "eu", OrgId, "admin@acme.io");
        var result = await sut.CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    private static HttpEnvironmentService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpEnvironmentService>.Instance);
}
