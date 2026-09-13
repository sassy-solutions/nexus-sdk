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
using Nexus.Sdk.Configuration;
using Xunit;

namespace Nexus.Sdk.Tests.Configuration;

/// <summary>
/// The v2 provider projects a refreshed snapshot into <c>IConfiguration</c>: it maps <c>__</c>→<c>:</c>
/// and SKIPS K8sSecret (null-value) entries rather than blanking them.
/// </summary>
[Collection(NexusSdkConfigStaticCollection.Name)]
public sealed class NexusConfigurationProviderTests
{
    private static ConfigSnapshot Snapshot(params ConfigEntrySnapshot[] entries) =>
        new(entries.ToDictionary(e => e.Key, StringComparer.Ordinal), "etag", DateTimeOffset.UtcNow);

    [Fact]
    public void ApplySnapshot_MapsDoubleUnderscoreToColon()
    {
        var provider = new NexusConfigurationProvider(new NexusOptions());

        provider.ApplySnapshot(Snapshot(
            new ConfigEntrySnapshot("ConnectionStrings__Database", "RuntimePull", false, "server=db", 1, "org")));

        provider.TryGet("ConnectionStrings:Database", out var value).Should().BeTrue();
        value.Should().Be("server=db");
    }

    [Fact]
    public void ApplySnapshot_SkipsK8sSecretNullValues()
    {
        var provider = new NexusConfigurationProvider(new NexusOptions());

        provider.ApplySnapshot(Snapshot(
            new ConfigEntrySnapshot("LOG_LEVEL", "EnvVar", false, "Information", 2, "org"),
            new ConfigEntrySnapshot("DB_PASSWORD", "K8sSecret", true, null, 4, "env")));

        provider.TryGet("LOG_LEVEL", out var logLevel).Should().BeTrue();
        logLevel.Should().Be("Information");

        // K8sSecret entry has a null value — skipped, not set to empty.
        provider.TryGet("DB_PASSWORD", out _).Should().BeFalse();
    }

    [Fact]
    public void ApplySnapshot_RaisesReloadToken()
    {
        var provider = new NexusConfigurationProvider(new NexusOptions());
        var token = provider.GetReloadToken();

        provider.ApplySnapshot(Snapshot(
            new ConfigEntrySnapshot("A", "RuntimePull", false, "1", 1, "org")));

        token.HasChanged.Should().BeTrue();
    }
}
