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
using Nexus.Sdk.Management.Services.Http;

namespace Nexus.Sdk.Tests.Management;

/// <summary>Unit tests for <see cref="HttpOrganizationService"/>.</summary>
public sealed class HttpOrganizationServiceTests
{
    private const string ListMineJson = """
        {"organizations":[{"organizationId":"11111111-1111-1111-1111-111111111111","name":"Acme","organizationStatus":"Active","myUserId":"22222222-2222-2222-2222-222222222222","myStatus":"Active","roles":["nexus-admin"]}]}
        """;

    // one page of a sub-tree, spelled as the controller spells it: the ids are
    // strings, `parentOrganizationId` is nullable, and the page metadata travels with the rows.
    private const string DescendantsJson = """
        {"descendants":[{"id":"33333333-3333-3333-3333-333333333333","name":"acme-eu","displayName":"Acme EU","status":"Active","parentOrganizationId":"11111111-1111-1111-1111-111111111111","depth":1},{"id":"44444444-4444-4444-4444-444444444444","name":"acme-eu-fr","displayName":null,"status":"Pending","parentOrganizationId":"33333333-3333-3333-3333-333333333333","depth":2}],"totalCount":7,"page":2,"pageSize":2,"hasMore":true}
        """;

    private const string ReconcileJson = """
        {"organizationId":"11111111-1111-1111-1111-111111111111","namespaces":[{"namespace":"nexus-tenant-acme","kind":"Organization","succeeded":true,"created":["role","rolebinding"],"alreadyPresent":["resourcequota"],"errorMessage":null,"notRequired":["registrycredentials"]}],"reconciledAt":"2026-06-11T00:00:00+00:00","succeededCount":1,"failedCount":0,"createdCount":2,"alreadyPresentCount":1,"notRequiredCount":1}
        """;

    [Fact]
    public async Task ListMineAsync_OnSuccess_MapsOrganizations()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ListMineJson);
        var sut = BuildSut(handler);

        var result = await sut.ListMineAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle();
        result.Value![0].Name.Should().Be("Acme");
        result.Value[0].Roles.Should().Contain("nexus-admin");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/me/organizations");
    }

    // the six tests that pinned ListAsync, GetAsync and ToggleVipAsync are gone
    // with the methods. One of them (`ToggleVipAsync_PatchesVipRoute_AndMapsResultingState`)
    // asserted `PathAndQuery == "/api/v1/organizations/…/vip"`, i.e. it made an unserved route
    // a *tested* behaviour. The two invariants they carried that are not about those routes —
    // a failing status becomes a failed NexusResult instead of an exception, and a 404 keeps
    // the server's detail — are re-asserted below on `api/me/organizations`, which is served.

    [Fact]
    public async Task ListMineAsync_OnNotFound_ReturnsFailureWithoutThrowing()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.NotFound,
            """{"title":"Organization Not Found","detail":"Organization with ID 'x' not found"}""");
        var sut = BuildSut(handler);

        var result = await sut.ListMineAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ListMineAsync_OnServerError_ReturnsFailureResult()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.InternalServerError, """{"title":"boom"}""");
        var sut = BuildSut(handler);

        var result = await sut.ListMineAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetCurrentAsync_OnEmptyList_ReturnsNotFound()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, """{"organizations":[]}""");
        var sut = BuildSut(handler);

        var result = await sut.GetCurrentAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ReconcileOrganizationNamespacesAsync_PostsToAdminRoute_AndMapsReport()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, ReconcileJson);
        var sut = BuildSut(handler);

        var result = await sut.ReconcileOrganizationNamespacesAsync("11111111-1111-1111-1111-111111111111", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SucceededCount.Should().Be(1);
        result.Value.CreatedCount.Should().Be(2);
        result.Value.Namespaces.Should().ContainSingle().Which.Created.Should().Contain("role");

        // le troisieme panier traverse le fil. Rien ne relie ce DTO au record de
        // Nexus.Core a la compilation : sans cette assertion, la derive serait silencieuse et
        // le SDK lirait 0 la ou l'API dit 1.
        result.Value.NotRequiredCount.Should().Be(1);
        result.Value.Namespaces.Should().ContainSingle()
            .Which.NotRequired.Should().Contain("registrycredentials");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be("/api/v1/admin/organizations/11111111-1111-1111-1111-111111111111/reconcile-namespaces");
    }

    [Fact]
    public async Task ReconcileOrganizationNamespacesAsync_WithEmptyId_ReturnsBadRequestWithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, "{}");
        var sut = BuildSut(handler);

        var result = await sut.ReconcileOrganizationNamespacesAsync("  ", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ListDescendantsAsync_GetsTheSubTreeRoute_AndMapsThePage()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, DescendantsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListDescendantsAsync(
            "11111111-1111-1111-1111-111111111111",
            directOnly: false,
            page: 2,
            pageSize: 2,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Descendants.Should().HaveCount(2);
        result.Value.Descendants[0].Name.Should().Be("acme-eu");
        result.Value.Descendants[0].Depth.Should().Be(1);
        result.Value.Descendants[1].DisplayName.Should().BeNull();
        result.Value.Descendants[1].Depth.Should().Be(2);

        // The page metadata is what tells a caller the sub-tree did not fit. Dropping it — as
        // the other list methods in this SDK do — would make a truncated tree look complete.
        result.Value.TotalCount.Should().Be(7);
        result.Value.HasMore.Should().BeTrue();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/v1/organizations/11111111-1111-1111-1111-111111111111/descendants"
            + "?directOnly=false&page=2&pageSize=2");
    }

    [Fact]
    public async Task ListDescendantsAsync_WithDirectOnly_SendsTheFlagLowercased()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK,
            """{"descendants":[],"totalCount":0,"page":1,"pageSize":20,"hasMore":false}""");
        var sut = BuildSut(handler);

        var result = await sut.ListDescendantsAsync(
            "11111111-1111-1111-1111-111111111111", directOnly: true, ct: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // A leaf organization is a success with an empty page, never a failure: "owns nothing"
        // is an answer, not an error.
        result.Value!.Descendants.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.HasMore.Should().BeFalse();

        // Lowercased on purpose. `True` binds just as well — ASP.NET parses booleans
        // case-insensitively — so this pins the shape, not the semantics: every list method in
        // this SDK sends its flags lowercased, and a query string that differs by case is a
        // needless difference in the logs and in any cache keyed on the URL.
        handler.LastRequest!.RequestUri!.Query.Should().StartWith("?directOnly=true&");
    }

    [Fact]
    public async Task ListDescendantsAsync_WhenTheBodyOmitsTheList_ReturnsAnEmptyListNotNull()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK, """{"totalCount":0,"page":1,"pageSize":20,"hasMore":false}""");
        var sut = BuildSut(handler);

        var result = await sut.ListDescendantsAsync("11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Descendants.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task ListDescendantsAsync_WithEmptyId_ReturnsBadRequestWithoutCallingApi()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.OK, DescendantsJson);
        var sut = BuildSut(handler);

        var result = await sut.ListDescendantsAsync("  ");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ListDescendantsAsync_OnForbidden_KeepsTheServerDetailAndDoesNotThrow()
    {
        // The route is tenant-scoped: asking about an organization you are not a member of is
        // refused rather than answered with a filtered list. The SDK must surface that refusal
        // as a failed result, not as an empty sub-tree.
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.Forbidden,
            """{"title":"Forbidden","detail":"Caller is not a member of organization 'x'"}""");
        var sut = BuildSut(handler);

        var result = await sut.ListDescendantsAsync("11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Contain("not a member");
        result.Value.Should().BeNull();
    }

    private static HttpOrganizationService BuildSut(ManagementTestHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://nexus-test/") },
            NullLogger<HttpOrganizationService>.Instance);
}
