// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Filters;
using Nexus.Sdk.Models;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Tests.Filters;

/// <summary>
/// three decisions, three responses. The one that matters: an unreachable platform
/// must not produce a 403 telling the caller they lack a permission nobody managed to check.
/// </summary>
public sealed class NexusAuthorizeFilterTests
{
    [Fact]
    public async Task Allowed_LetsTheRequestThrough()
    {
        var context = BuildContext();

        await BuildSut(NexusPermissionDecision.Allowed).OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task Denied_Answers403_NamingThePermission()
    {
        var context = BuildContext();

        await BuildSut(NexusPermissionDecision.Denied).OnAuthorizationAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);

        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("You do not have the required permission: reports.read");
    }

    [Fact]
    public async Task Indeterminate_ByDefault_Answers503_AndDoesNotBlameThePermission()
    {
        var context = BuildContext();

        await BuildSut(NexusPermissionDecision.Indeterminate).OnAuthorizationAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(503);

        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Nexus unreachable");
        problem.Extensions["code"].Should().Be(NexusAuthorizeFilter.IndeterminateProblemCode);

        // The whole point: the body must not accuse the caller of missing a permission.
        problem.Detail.Should().NotContain("required permission");
        context.HttpContext.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Indeterminate_WithAllowConfigured_LetsTheRequestThrough()
    {
        var context = BuildContext();

        await BuildSut(
                NexusPermissionDecision.Indeterminate,
                NexusPermissionUnavailableBehavior.Allow)
            .OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task NoAttribute_DoesNotCallNexusAtAll()
    {
        var client = new StubNexusClient(NexusPermissionDecision.Denied);
        var context = BuildContext(withAttribute: false);

        await BuildSut(client).OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        client.Calls.Should().Be(0);
    }

    // ------------------------------------------------------------------

    private static NexusAuthorizeFilter BuildSut(
        NexusPermissionDecision decision,
        NexusPermissionUnavailableBehavior onUnavailable = NexusPermissionUnavailableBehavior.Deny)
        => BuildSut(new StubNexusClient(decision), onUnavailable);

    private static NexusAuthorizeFilter BuildSut(
        StubNexusClient client,
        NexusPermissionUnavailableBehavior onUnavailable = NexusPermissionUnavailableBehavior.Deny)
        => new(
            client,
            Options.Create(new NexusOptions
            {
                BaseUrl = "http://nexus-test/",
                OnPermissionUnavailable = onUnavailable,
            }),
            NullLogger<NexusAuthorizeFilter>.Instance);

    private static AuthorizationFilterContext BuildContext(bool withAttribute = true)
    {
        var descriptor = new ActionDescriptor();
        if (withAttribute)
        {
            descriptor.EndpointMetadata = [new NexusAuthorizeAttribute("reports.read")];
        }

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
        return new AuthorizationFilterContext(actionContext, []);
    }

    /// <summary>
    /// A hand-written stub rather than a mocking library: this test project references none,
    /// and the interface is small enough that the stub is shorter than the setup would be.
    /// </summary>
    private sealed class StubNexusClient : INexusClient
    {
        private readonly NexusPermissionDecision _decision;

        public StubNexusClient(NexusPermissionDecision decision) => _decision = decision;

        public int Calls { get; private set; }

        public Task<NexusPermissionDecision> CheckPermissionAsync(string permission, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_decision);
        }

        public Task<FeatureConfig?> GetFeatureAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool?> CheckFeatureAsync(string key, NexusSubject subject, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<NexusResult> TrackAsync(string eventName, Dictionary<string, object>? metadata = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<NexusResult> BillAsync(string featureKey, int units = 1, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SdkRegistrationResponse?> RegisterAsync(SdkRegistrationRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
