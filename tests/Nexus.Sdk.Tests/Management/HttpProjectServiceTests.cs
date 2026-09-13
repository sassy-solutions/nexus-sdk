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

/// <summary>Unit tests for <see cref="HttpProjectService"/>.</summary>
public sealed class HttpProjectServiceTests
{
    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string ProjId = "33333333-3333-3333-3333-333333333333";

    private const string ListJson = """
        {"projects":[{"id":"33333333-3333-3333-3333-333333333333","name":"web","displayName":"Web","status":"Active","oidcAppCount":2,"createdAt":"2026-01-01T00:00:00+00:00"}],"totalCount":1,"page":1,"pageSize":20,"hasMore":false}
        """;

    private const string CreateJson = """
        {"id":"33333333-3333-3333-3333-333333333333","name":"web","displayName":"Web","createdAt":"2026-01-01T00:00:00+00:00"}
        """;

    [Fact]
    public async Task ListAsync_HitsOrgScopedRoute_AndMapsProjects()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(OrgId, includeArchived: true, ct: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Name.Should().Be("web");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/organizations/{OrgId}/projects");
        handler.LastRequest.RequestUri.Query.Should().Contain("includeArchived=true");
    }

    [Fact]
    public async Task CreateAsync_PostsBody_AndMapsResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreateJson);
        var sut = BuildSut(handler);

        var result = await sut.CreateAsync(OrgId, new CreateProjectRequest("web", "Web", "desc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value!.Id.Should().Be(ProjId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("name").GetString().Should().Be("web");
    }

    [Fact]
    public async Task GetAsync_OnNotFound_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, """{"detail":"Project with ID 'x' not found"}""");
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(OrgId, "x", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteWithReasonBody()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NoContent);
        var sut = BuildSut(handler);

        var result = await sut.DeleteAsync(OrgId, ProjId, "cleanup", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/v1/organizations/{OrgId}/projects/{ProjId}");
        JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("reason").GetString().Should().Be("cleanup");
    }

    [Fact]
    public async Task DeleteAsync_OnServerError_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, """{"detail":"boom"}""");
        var sut = BuildSut(handler);

        var result = await sut.DeleteAsync(OrgId, ProjId, "cleanup", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    private static HttpProjectService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpProjectService>.Instance);
}
