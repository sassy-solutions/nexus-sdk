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
using Microsoft.Extensions.Options;
using Nexus.Sdk;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Services;
using Nexus.Sdk.Tests.Configuration;
using Xunit;

namespace Nexus.Sdk.Tests.Services;

/// <summary>
/// The refresh service's initial load populates the store before the host starts, honors 304 (no swap),
/// and enforces required keys fail-closed at startup — all against a fake manifest endpoint, no live HTTP.
/// </summary>
[Collection(NexusSdkConfigStaticCollection.Name)]
public sealed class NexusConfigRefreshServiceTests
{
    // A large refresh period keeps the background loop from ticking during the test; StopAsync tears it down.
    private static NexusOptions BaseOptions() => new()
    {
        ApiKey = "nxs_test",
        ConfigRefreshSeconds = 3600,
    };

    private static string Manifest(string etag, params (string Key, string Mode, bool Secret, string? Value)[] entries)
    {
        var items = entries.Select(e =>
            $$"""{"key":"{{e.Key}}","deliveryMode":"{{e.Mode}}","isSecret":{{(e.Secret ? "true" : "false")}},"value":{{(e.Value is null ? "null" : $"\"{e.Value}\"")}},"valueVersion":1,"source":"org"}""");
        return $$"""{"etag":"{{etag}}","resolvedAt":"2026-07-24T09:00:00Z","entries":[{{string.Join(",", items)}}]}""";
    }

    private static (NexusConfigRefreshService Service, NexusConfigSnapshotStore Store) Build(
        FakeConfigHandler handler, NexusOptions options)
    {
        var store = new NexusConfigSnapshotStore();
        var config = new NexusConfigService(store, Options.Create(options), NullLogger<NexusConfigService>.Instance);
        var factory = new StubHttpClientFactory(handler);
        var service = new NexusConfigRefreshService(
            factory, store, config, Options.Create(options), NullLogger<NexusConfigRefreshService>.Instance);
        return (service, store);
    }

    [Fact]
    public async Task StartAsync_InitialLoad_PopulatesTheStore()
    {
        var handler = new FakeConfigHandler().Enqueue(
            HttpStatusCode.OK, Manifest("v1", ("FEATURE_MODE", "RuntimePull", false, "fast")));
        var (service, store) = Build(handler, BaseOptions());

        try
        {
            await service.StartAsync(CancellationToken.None);

            store.Get("FEATURE_MODE")!.Value.Should().Be("fast");
            store.Current.ETag.Should().Be("v1");
            handler.IfNoneMatchValues[0].Should().BeNull(); // no prior etag on the first call
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_NotModified_DoesNotSwap_AndSendsIfNoneMatch()
    {
        var handler = new FakeConfigHandler().Enqueue(HttpStatusCode.NotModified);
        var (service, store) = Build(handler, BaseOptions());

        // Pre-seed the store as if a previous load had succeeded.
        var seeded = new ConfigSnapshot(
            new Dictionary<string, ConfigEntrySnapshot>(StringComparer.Ordinal)
            {
                ["KEY"] = new ConfigEntrySnapshot("KEY", "RuntimePull", false, "old", 1, "org"),
            },
            "\"v1\"",
            DateTimeOffset.UtcNow);
        store.Swap(seeded);

        try
        {
            await service.StartAsync(CancellationToken.None);

            store.Current.Should().BeSameAs(seeded); // 304 → no swap
            store.Get("KEY")!.Value.Should().Be("old");
            handler.IfNoneMatchValues[0].Should().Contain("v1");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_RequiredKeyMissing_Throws()
    {
        var handler = new FakeConfigHandler().Enqueue(HttpStatusCode.OK, Manifest("v1")); // no entries
        var options = BaseOptions().RequireKeys("NEXUS_TEST_REQUIRED_" + Guid.NewGuid().ToString("N"));
        var (service, _) = Build(handler, options);

        var act = () => service.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NexusConfigUnavailableException>();
    }

    [Fact]
    public async Task StartAsync_RequiredKeySatisfiedBySnapshot_DoesNotThrow()
    {
        var handler = new FakeConfigHandler().Enqueue(
            HttpStatusCode.OK, Manifest("v1", ("DB_URL", "RuntimePull", false, "postgres://db")));
        var (service, _) = Build(handler, BaseOptions().RequireKeys("DB_URL"));

        try
        {
            var act = () => service.StartAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_PublishesTheStaticAccessor()
    {
        var handler = new FakeConfigHandler().Enqueue(
            HttpStatusCode.OK, Manifest("v1", ("GREETING", "RuntimePull", false, "hi")));
        var (service, _) = Build(handler, BaseOptions());

        try
        {
            await service.StartAsync(CancellationToken.None);

            NexusConfig.Config("GREETING").Should().Be("hi");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
