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
using Nexus.Sdk.Services;
using Xunit;

namespace Nexus.Sdk.Tests.Services;

/// <summary>
/// The snapshot store swaps by reference and renews its reload token on every swap — the lock-free heart
/// of the "sync reads never do IO" design.
/// </summary>
public sealed class NexusConfigSnapshotStoreTests
{
    private static ConfigSnapshot Snapshot(string key, string? value, string? etag) =>
        new(
            new Dictionary<string, ConfigEntrySnapshot>(StringComparer.Ordinal)
            {
                [key] = new ConfigEntrySnapshot(key, "RuntimePull", IsSecret: false, value, ValueVersion: 1, Source: "org"),
            },
            etag,
            DateTimeOffset.UtcNow);

    [Fact]
    public void Current_StartsEmpty()
    {
        var store = new NexusConfigSnapshotStore();

        store.Current.Should().BeSameAs(ConfigSnapshot.Empty);
        store.Get("anything").Should().BeNull();
        store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Swap_ReplacesCurrentByReference()
    {
        var store = new NexusConfigSnapshotStore();
        var next = Snapshot("KEY", "v1", "etag-1");

        store.Swap(next);

        store.Current.Should().BeSameAs(next);
        store.Get("KEY")!.Value.Should().Be("v1");
        store.Current.ETag.Should().Be("etag-1");
    }

    [Fact]
    public void Swap_FiresTheReloadToken_AndIssuesAFreshOne()
    {
        var store = new NexusConfigSnapshotStore();
        var token = store.GetReloadToken();
        var fired = false;
        token.RegisterChangeCallback(_ => fired = true, state: null);

        token.HasChanged.Should().BeFalse();

        store.Swap(Snapshot("KEY", "v1", "etag-1"));

        fired.Should().BeTrue();
        token.HasChanged.Should().BeTrue();

        // A fresh token is handed out after the swap and has not fired yet.
        var next = store.GetReloadToken();
        next.Should().NotBeSameAs(token);
        next.HasChanged.Should().BeFalse();
    }
}
