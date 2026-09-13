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

/// <summary>Unit tests for <see cref="HttpApplicationService"/>.</summary>
public sealed class HttpApplicationServiceTests
{
    private const string AppId = "44444444-4444-4444-4444-444444444444";
    private const string EnvId = "55555555-5555-5555-5555-555555555555";

    private const string ListJson = """
        {"applications":[{"id":"44444444-4444-4444-4444-444444444444","ownerOrganizationId":"11111111-1111-1111-1111-111111111111","name":"api","subdomain":"api","status":"Active","createdAt":"2026-01-01T00:00:00+00:00"}],"totalCount":1,"page":1,"pageSize":20,"hasMore":false}
        """;

    private const string VersionsJson = """
        {"applicationId":"44444444-4444-4444-4444-444444444444","versions":[{"tag":"v1.0.0","commitSha":"abc123","description":"first","taggedBy":"ci","taggedAt":"2026-01-01T00:00:00+00:00"}]}
        """;

    private const string TagJson = """{"versionTag":"v1.0.0","commitSha":"abc123"}""";

    private const string DeployJson = """
        {"url":"https://api.acme.dev","argoAppName":"acme-api-prod","versionTag":"v1.0.0","environmentName":"prod"}
        """;

    private const string AuditJson = """
        {"applicationId":"44444444-4444-4444-4444-444444444444","entries":[{"eventType":"ApplicationVersionTagged","actor":"ci","occurredAt":"2026-01-01T00:00:00+00:00","version":"v1.0.0","summary":"Tagged v1.0.0"}],"totalCount":1}
        """;

    [Fact]
    public async Task ListAsync_AppendsOwnerOrgFilter_WhenProvided()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(ownerOrganizationId: "11111111-1111-1111-1111-111111111111", ct: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Name.Should().Be("api");
        handler.LastRequest!.RequestUri!.Query.Should().Contain("ownerOrganizationId=11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task ListVersionsAsync_BuildsSkipTakeQuery_AndMapsVersions()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, VersionsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListVersionsAsync(AppId, skip: 5, take: 25, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Tag.Should().Be("v1.0.0");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/applications/{AppId}/versions");
        handler.LastRequest.RequestUri.Query.Should().Contain("skip=5").And.Contain("take=25");
    }

    [Fact]
    public async Task TagVersionAsync_PostsBody_AndMapsResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, TagJson);
        var sut = BuildSut(handler);

        var result = await sut.TagVersionAsync(AppId, new TagVersionRequest("v1.0.0", "abc123", "first"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value!.VersionTag.Should().Be("v1.0.0");
        JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("versionTag").GetString().Should().Be("v1.0.0");
    }

    [Fact]
    public async Task DeployAsync_PostsEnvironmentIdToTagDeployRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, DeployJson);
        var sut = BuildSut(handler);

        var result = await sut.DeployAsync(AppId, Guid.Parse(EnvId), "v1.0.0", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ArgoAppName.Should().Be("acme-api-prod");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/v1/applications/{AppId}/versions/v1.0.0/deploy");
        JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("environmentId").GetString().Should().Be(EnvId);
    }

    [Fact]
    public async Task PromoteAsync_PostsToPromoteRoute_AndMapsDeploymentResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, DeployJson);
        var sut = BuildSut(handler);

        var request = new PromoteVersionRequest(Guid.Parse(EnvId), Guid.NewGuid(), "v1.0.0");
        var result = await sut.PromoteAsync(AppId, request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnvironmentName.Should().Be("prod");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/applications/{AppId}/promote");
    }

    [Fact]
    public async Task PromoteAsync_OnConflict_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Conflict, """{"detail":"Version not deployed in source env"}""");
        var sut = BuildSut(handler);

        var result = await sut.PromoteAsync(AppId, new PromoteVersionRequest(Guid.NewGuid(), Guid.NewGuid(), "v1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("source env");
    }

    [Fact]
    public async Task GetAuditLogAsync_BuildsQuery_AndMapsEntries()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, AuditJson);
        var sut = BuildSut(handler);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = await sut.GetAuditLogAsync(AppId, skip: 0, take: 10, from: from, ct: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Entries.Should().ContainSingle().Which.EventType.Should().Be("ApplicationVersionTagged");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/applications/{AppId}/audit-log");
        handler.LastRequest.RequestUri.Query.Should().Contain("skip=0").And.Contain("take=10").And.Contain("from=");
    }

    [Fact]
    public async Task GetAsync_On500_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, """{"detail":"boom"}""");
        var sut = BuildSut(handler);

        var result = await sut.GetAsync(AppId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    private static HttpApplicationService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpApplicationService>.Instance);
}
