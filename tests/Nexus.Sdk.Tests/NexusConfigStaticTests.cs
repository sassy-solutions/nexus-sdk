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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Sdk;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Services;
using Nexus.Sdk.Tests.Configuration;
using Xunit;

namespace Nexus.Sdk.Tests;

/// <summary>
/// The static <c>NexusConfig</c> facade delegates to the ambient accessor set at startup (or by tests
/// via <see cref="NexusConfig.SetAccessor"/>) — the "one line" <c>Config("KEY")</c> sugar.
/// </summary>
[Collection(NexusSdkConfigStaticCollection.Name)]
public sealed class NexusConfigStaticTests
{
    private static INexusConfig AccessorWith(params (string Key, string Value)[] pairs)
    {
        var store = new NexusConfigSnapshotStore();
        store.Swap(new ConfigSnapshot(
            pairs.ToDictionary(
                p => p.Key,
                p => new ConfigEntrySnapshot(p.Key, "RuntimePull", false, p.Value, 1, "org"),
                StringComparer.Ordinal),
            "etag",
            DateTimeOffset.UtcNow));
        return new NexusConfigService(store, Options.Create(new NexusOptions()), NullLogger<NexusConfigService>.Instance);
    }

    [Fact]
    public void SetAccessor_ThenConfig_ReturnsValue()
    {
        NexusConfig.SetAccessor(AccessorWith(("STRIPE_KEY", "sk_live_x")));

        NexusConfig.Config("STRIPE_KEY").Should().Be("sk_live_x");
        NexusConfig.Config("MISSING").Should().BeNull();
    }

    [Fact]
    public void ConfigRequired_MissingKey_Throws()
    {
        NexusConfig.SetAccessor(AccessorWith(("PRESENT", "1")));

        NexusConfig.ConfigRequired("PRESENT").Should().Be("1");

        var act = () => NexusConfig.ConfigRequired("ABSENT");
        act.Should().Throw<NexusConfigKeyMissingException>()
            .Which.Key.Should().Be("ABSENT");
    }

    [Fact]
    public void SetAccessor_Null_Throws()
    {
        var act = () => NexusConfig.SetAccessor(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
