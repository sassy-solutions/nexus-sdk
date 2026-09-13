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
using Nexus.Sdk.Client;
using Nexus.Sdk.Services;
using Nexus.Sdk.Tests.Management;
using Xunit;

namespace Nexus.Sdk.Tests.Client;

/// <summary>
/// WS4 tranche 2b — the SDK per-subject feature check. The consuming app supplies
/// the subject (tier/user/attributes) via INexusSubjectContext; the client evaluates
/// it against Nexus (<c>GET /api/v1/feature-flags/{key}/check</c>).
/// </summary>
public sealed class NexusClientCheckFeatureTests
{
    private static NexusClient ClientWith(ManagementTestHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new NexusClient(http, NullLogger<NexusClient>.Instance);
    }

    [Fact]
    public async Task CheckFeature_BuildsCheckUrlWithSubject_AndReturnsEnablement()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK, """{"key":"premium-report","isEnabled":true,"resolvedBy":"AttributeOverride"}""");
        var client = ClientWith(handler);

        var subject = new NexusSubject(
            Tier: "Pro",
            EndUserId: "user-1",
            Attributes: new Dictionary<string, string> { ["tier"] = "premium" });

        var result = await client.CheckFeatureAsync("premium-report", subject);

        result.Should().BeTrue();
        var uri = handler.LastRequest!.RequestUri!.ToString();
        uri.Should().Contain("api/v1/feature-flags/premium-report/check");
        uri.Should().Contain("tier=Pro");
        uri.Should().Contain("endUserId=user-1");
        uri.Should().Contain("attributes[tier]=premium"); // ASP.NET binds attributes[name]=value into a Dictionary
    }

    [Fact]
    public async Task CheckFeature_DisabledResponse_ReturnsFalse()
    {
        var handler = ManagementTestHandler.Returning(
            HttpStatusCode.OK, """{"key":"x","isEnabled":false,"resolvedBy":"Default"}""");
        var client = ClientWith(handler);

        var result = await client.CheckFeatureAsync(
            "x", new NexusSubject(Attributes: new Dictionary<string, string> { ["tier"] = "free" }));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckFeature_HttpError_ReturnsNull_SoCallerFallsBackToDefault()
    {
        var handler = ManagementTestHandler.Returning(HttpStatusCode.ServiceUnavailable);
        var client = ClientWith(handler);

        var result = await client.CheckFeatureAsync("x", new NexusSubject(Tier: "Pro"));

        result.Should().BeNull();
    }

    [Fact]
    public void NexusSubject_HasSignal_TrueOnlyWhenTargetingPresent()
    {
        new NexusSubject().HasSignal.Should().BeFalse();
        new NexusSubject(Tier: "Pro").HasSignal.Should().BeTrue();
        new NexusSubject(EndUserId: "u").HasSignal.Should().BeTrue();
        new NexusSubject(Attributes: new Dictionary<string, string> { ["a"] = "b" }).HasSignal.Should().BeTrue();
        new NexusSubject(Attributes: new Dictionary<string, string>()).HasSignal.Should().BeFalse();
    }
}
