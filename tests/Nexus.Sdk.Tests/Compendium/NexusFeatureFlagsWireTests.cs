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
using Compendium.Abstractions.FeatureFlags.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Compendium;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Tests.Management;

namespace Nexus.Sdk.Tests.Compendium;

/// <summary>
/// what the port actually puts on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NexusFeatureFlagsTests"/> stands a fake <see cref="INexusClient"/> in front of the
/// adapter, so it pins the <em>mapping</em> and nothing else: a <see cref="NexusSubject"/> the
/// real client could not serialise would pass it. These cases run the adapter through the real
/// <see cref="NexusClient"/> over a stub <see cref="HttpMessageHandler"/>, so the request URI is
/// asserted as built — the route, and the query parameters
/// <c>FeatureFlagsController.CheckFlag</c> binds.
/// </para>
/// <para>
/// This is still not an integration test: no Nexus is running, and that the server accepts this
/// shape remains attested by <c>FeatureFlagsController</c>'s signature rather than by an
/// end-to-end run. What it does rule out is the whole class of failures where the adapter builds
/// a subject the client silently drops.
/// </para>
/// </remarks>
public sealed class NexusFeatureFlagsWireTests
{
    private const string FlagKey = "reports.export";

    [Fact]
    public async Task IsOnAsync_ShouldRequestTheCheckRouteWithTheSubjectOnTheQueryString()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK,
            """{"key":"reports.export","isEnabled":true,"resolvedBy":"EnvironmentGate"}""");

        var result = await Flags(handler, environment: "production", version: "2.7.0")
            .IsOnAsync(FlagKey, new FlagContext(
                "org-123",
                "user-42",
                new Dictionary<string, object> { ["tier"] = "pro" }));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var uri = handler.LastRequest!.RequestUri!.ToString();
        uri.Should().Contain("api/v1/feature-flags/reports.export/check");
        uri.Should().Contain("endUserId=user-42");
        uri.Should().Contain("environment=production");
        uri.Should().Contain("version=2.7.0");

        // ASP.NET binds attributes[name]=value into the Dictionary<string,string> parameter.
        uri.Should().Contain("attributes[tier]=pro");
    }

    /// <summary>
    /// The tenant must not reach the wire in any shape — not as a query parameter, not as a
    /// header, not in the path. <see cref="NexusFeatureFlagsTests"/> asserts the subject does not
    /// carry it; this asserts the request does not either, which is the property that actually
    /// matters (NXS-SEC-02 / POM-402).
    /// </summary>
    [Fact]
    public async Task IsOnAsync_ShouldNotPutTheTenantIdOnTheWire()
    {
        const string TenantId = "tenant-that-must-not-travel";
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK, """{"key":"reports.export","isEnabled":true,"resolvedBy":"Default"}""");

        await Flags(handler).IsOnAsync(FlagKey, new FlagContext(TenantId, "user-42"));

        var request = handler.LastRequest!;
        request.RequestUri!.ToString().Should().NotContain(TenantId);
        request.Headers.Should().NotContain(h => h.Value.Contains(TenantId));
        request.Headers.Should().NotContain(h => h.Key == "X-Organization-Id");
    }

    /// <summary>
    /// Nexus unreachable: the check answers nothing, so the adapter reads the flag's own
    /// configuration and returns its global default. Two requests, in that order — the fallback
    /// documented on <c>IsOnAsync</c>, observed on the wire rather than on a stub.
    /// </summary>
    [Fact]
    public async Task IsOnAsync_WhenCheckFails_ShouldFallBackToTheFlagConfigurationRoute()
    {
        var handler = new SequencedHandler(
            (HttpStatusCode.ServiceUnavailable, string.Empty),
            (HttpStatusCode.OK, """{"key":"reports.export","enabled":true}"""));

        var result = await Flags(handler).IsOnAsync(FlagKey, new FlagContext("org-123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        handler.Requested.Should().HaveCount(2);
        handler.Requested[0].Should().Contain("/check");
        handler.Requested[1].Should().NotContain("/check");
    }

    private static NexusFeatureFlags Flags(
        HttpMessageHandler handler, string? environment = null, string? version = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new NexusClient(http, NullLogger<NexusClient>.Instance);

        return new NexusFeatureFlags(
            client,
            Options.Create(new NexusOptions { Environment = environment, Version = version }));
    }

    /// <summary>
    /// <see cref="ManagementTestHandler"/> answers every request identically; the fallback needs
    /// the second call to differ from the first, so this one walks a scripted list.
    /// </summary>
    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public SequencedHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!.ToString());
            var (status, body) = _responses.Dequeue();

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
