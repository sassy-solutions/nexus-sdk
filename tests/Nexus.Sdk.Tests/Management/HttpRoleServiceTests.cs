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

/// <summary>Unit tests for <see cref="HttpRoleService"/>.</summary>
public sealed class HttpRoleServiceTests
{
    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string RoleId = "66666666-6666-6666-6666-666666666666";
    private const string AttrId = "77777777-7777-7777-7777-777777777777";
    private const string UserId = "88888888-8888-8888-8888-888888888888";

    private const string ListJson = """
        {"roles":[{"id":"66666666-6666-6666-6666-666666666666","name":"Editor","description":"can edit","isSystem":false,"userCount":4}]}
        """;

    private const string CreateJson = """{"id":"66666666-6666-6666-6666-666666666666"}""";

    private const string AttributesJson = """
        {"attributes":[{"id":"77777777-7777-7777-7777-777777777777","roleId":"66666666-6666-6666-6666-666666666666","resource":"config","action":"read","conditionLeft":"resource.tenant_id","conditionOperator":"Equals","conditionRight":"${user.tenant_id}","conditionDisplay":"resource.tenant_id == ${user.tenant_id}","createdBy":"admin","createdAt":"2026-01-01T00:00:00+00:00"}]}
        """;

    private const string AddAttrJson = """{"attributeId":"77777777-7777-7777-7777-777777777777"}""";

    [Fact]
    public async Task ListAsync_HitsV1OrgScopedRoute_AndMapsRoles()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAsync(OrgId, includeDeleted: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Name.Should().Be("Editor");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/organizations/{OrgId}/roles");
        handler.LastRequest.RequestUri.Query.Should().Contain("includeDeleted=true");
    }

    [Fact]
    public async Task CreateAsync_ReturnsRoleId()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, CreateJson);
        var sut = BuildSut(handler);

        var request = new CreateRoleRequest("Editor", "can edit", new Dictionary<string, string[]> { ["config"] = ["read", "write"] });
        var result = await sut.CreateAsync(OrgId, request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Guid.Parse(RoleId));
        JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("name").GetString().Should().Be("Editor");
    }

    [Fact]
    public async Task UpdateAsync_PutsToRoleRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NoContent);
        var sut = BuildSut(handler);

        var request = new UpdateRoleRequest("Editor", "v2", new Dictionary<string, string[]>());
        var result = await sut.UpdateAsync(OrgId, RoleId, request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/v1/organizations/{OrgId}/roles/{RoleId}");
    }

    [Fact]
    public async Task DeleteAsync_OnNotFound_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NotFound, """{"detail":"Role Not Found"}""");
        var sut = BuildSut(handler);

        var result = await sut.DeleteAsync(OrgId, RoleId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task ListAttributesAsync_MapsTriples()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, AttributesJson);
        var sut = BuildSut(handler);

        var result = await sut.ListAttributesAsync(OrgId, RoleId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var attr = result.Value!.Should().ContainSingle().Subject;
        attr.Resource.Should().Be("config");
        attr.ConditionOperator.Should().Be("Equals");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/organizations/{OrgId}/roles/{RoleId}/attributes");
    }

    [Fact]
    public async Task AddAttributeAsync_PostsStructuredCondition_AndReturnsAttributeId()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.Created, AddAttrJson);
        var sut = BuildSut(handler);

        var request = new AddRoleAttributeRequest(
            "config", "read", new AttributeConditionPayload("resource.tenant_id", "Equals", "${user.tenant_id}"));
        var result = await sut.AddAttributeAsync(OrgId, RoleId, request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AttributeId.Should().Be(Guid.Parse(AttrId));
        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        body.GetProperty("resource").GetString().Should().Be("config");
        body.GetProperty("condition").GetProperty("operator").GetString().Should().Be("Equals");
    }

    [Fact]
    public async Task RemoveAttributeAsync_DeletesAttributeRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NoContent);
        var sut = BuildSut(handler);

        var result = await sut.RemoveAttributeAsync(OrgId, RoleId, AttrId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/v1/organizations/{OrgId}/roles/{RoleId}/attributes/{AttrId}");
    }

    [Fact]
    public async Task AssignToUserAsync_PutsAssignmentsToPlatformUsersRoute()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.NoContent);
        var sut = BuildSut(handler);

        var assignments = new[] { new RoleAssignmentRequest(Guid.Parse(RoleId), "Organization", null) };
        var result = await sut.AssignToUserAsync(OrgId, UserId, assignments, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/v1/organizations/{OrgId}/platform-users/{UserId}/role-assignments");
        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        body.GetProperty("assignments")[0].GetProperty("scopeType").GetString().Should().Be("Organization");
    }

    [Fact]
    public async Task AssignToUserAsync_OnServerError_ReturnsFailure()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, """{"detail":"boom"}""");
        var sut = BuildSut(handler);

        var result = await sut.AssignToUserAsync(OrgId, UserId, Array.Empty<RoleAssignmentRequest>(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    private static HttpRoleService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpRoleService>.Instance);
}
