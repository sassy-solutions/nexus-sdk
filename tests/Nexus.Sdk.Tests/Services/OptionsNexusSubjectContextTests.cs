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
using Microsoft.Extensions.Options;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Services;
using Xunit;

namespace Nexus.Sdk.Tests.Services;

/// <summary>
/// The default subject context carries the app's deployed environment + version so [NexusFeature]
/// gates are environment/version-aware out of the box (routed to the /check path).
/// </summary>
public sealed class OptionsNexusSubjectContextTests
{
    private static OptionsNexusSubjectContext Context(string? environment, string? version)
        => new(Options.Create(new NexusOptions { Environment = environment, Version = version }));

    [Fact]
    public void GetCurrentSubject_WithEnvironmentAndVersion_ReturnsSubjectWithSignal()
    {
        var subject = Context("prod", "0.0.2").GetCurrentSubject();

        subject.Should().NotBeNull();
        subject!.Environment.Should().Be("prod");
        subject.Version.Should().Be("0.0.2");
        subject.HasSignal.Should().BeTrue();
    }

    [Theory]
    [InlineData("prod", null)]
    [InlineData(null, "0.0.2")]
    public void GetCurrentSubject_WithEitherSet_ReturnsSubject(string? env, string? version)
    {
        Context(env, version).GetCurrentSubject().Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "  ")]
    public void GetCurrentSubject_WithNeitherSet_ReturnsNull(string? env, string? version)
    {
        // No deployed identity (local/non-deployed run) → null subject → gate falls back to default.
        Context(env, version).GetCurrentSubject().Should().BeNull();
    }
}
