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
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Services;
using Xunit;

namespace Nexus.Sdk.Tests.Services;

/// <summary>
/// <see cref="NexusConfigService"/> fuses the process environment (first, by default) with the remote
/// snapshot, never doing IO on a read, and treats K8sSecret entries (null value on this channel) as
/// absent off-cluster.
/// </summary>
public sealed class NexusConfigServiceTests
{
    private static NexusConfigService Service(NexusConfigSnapshotStore store, NexusOptions? options = null) =>
        new(store, Options.Create(options ?? new NexusOptions()), NullLogger<NexusConfigService>.Instance);

    private static NexusConfigSnapshotStore StoreWith(params ConfigEntrySnapshot[] entries)
    {
        var store = new NexusConfigSnapshotStore();
        store.Swap(new ConfigSnapshot(
            entries.ToDictionary(e => e.Key, StringComparer.Ordinal),
            "etag",
            DateTimeOffset.UtcNow));
        return store;
    }

    private static ConfigEntrySnapshot Variable(string key, string? value) =>
        new(key, "RuntimePull", IsSecret: false, value, ValueVersion: 1, Source: "org");

    [Fact]
    public void Get_ReturnsSnapshotValue_WhenNoEnvOverride()
    {
        var service = Service(StoreWith(Variable("FEATURE_MODE", "fast")));

        service.Get("FEATURE_MODE").Should().Be("fast");
    }

    [Fact]
    public void Get_UnknownKey_ReturnsNull()
    {
        var service = Service(StoreWith(Variable("KNOWN", "x")));

        service.Get("UNKNOWN").Should().BeNull();
    }

    [Fact]
    public void Get_ProcessEnvironmentWins_OverSnapshot()
    {
        var key = "NEXUS_TEST_ENV_WINS_" + Guid.NewGuid().ToString("N");
        var service = Service(StoreWith(Variable(key, "from-snapshot")));

        RunWithEnv(key, "from-env", () =>
            service.Get(key).Should().Be("from-env"));
    }

    [Fact]
    public void Get_DisableEnvironmentOverride_SnapshotWins()
    {
        var key = "NEXUS_TEST_OPTOUT_" + Guid.NewGuid().ToString("N");
        var service = Service(
            StoreWith(Variable(key, "from-snapshot")),
            new NexusOptions { DisableEnvironmentOverride = true });

        RunWithEnv(key, "from-env", () =>
            service.Get(key).Should().Be("from-snapshot"));
    }

    [Fact]
    public void Get_ColonKey_MatchesDoubleUnderscoreEnvVar()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var colonKey = $"ConnectionStrings:Default_{suffix}";
        var envKey = $"ConnectionStrings__Default_{suffix}";
        var service = Service(new NexusConfigSnapshotStore());

        RunWithEnv(envKey, "server=db", () =>
            service.Get(colonKey).Should().Be("server=db"));
    }

    [Fact]
    public void Get_K8sSecretWithNullValue_ReturnsNull()
    {
        var store = StoreWith(new ConfigEntrySnapshot(
            "DB_PASSWORD", "K8sSecret", IsSecret: true, Value: null, ValueVersion: 4, Source: "env"));
        var service = Service(store);

        service.Get("DB_PASSWORD").Should().BeNull();
    }

    [Fact]
    public void GetRequired_MissingKey_Throws()
    {
        var service = Service(new NexusConfigSnapshotStore());

        var act = () => service.GetRequired("DB_URL");

        act.Should().Throw<NexusConfigKeyMissingException>()
            .Which.Key.Should().Be("DB_URL");
    }

    [Fact]
    public void GetRequired_PresentKey_ReturnsValue()
    {
        var service = Service(StoreWith(Variable("DB_URL", "postgres://db")));

        service.GetRequired("DB_URL").Should().Be("postgres://db");
    }

    [Fact]
    public async Task GetAsync_ServesFromSnapshot_WithoutIo()
    {
        var service = Service(StoreWith(Variable("KEY", "value")));

        (await service.GetAsync("KEY")).Should().Be("value");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsResolvedValues_SkippingK8sSecretNulls()
    {
        var store = StoreWith(
            Variable("A", "1"),
            new ConfigEntrySnapshot("SECRET", "K8sSecret", IsSecret: true, Value: null, ValueVersion: 1, Source: "org"));
        var service = Service(store);

        var all = await service.GetAllAsync();

        all.Should().ContainKey("A").WhoseValue.Should().Be("1");
        all.Should().NotContainKey("SECRET");
    }

    [Fact]
    public async Task GetAsyncOfT_DeserializesJsonValue()
    {
        var service = Service(StoreWith(Variable("OBJ", """{"Name":"acme"}""")));

        var result = await service.GetAsync<Sample>("OBJ");

        result.Should().NotBeNull();
        result!.Name.Should().Be("acme");
    }

    [Fact]
    public void Watch_ReturnsTokenThatFiresOnSwap()
    {
        var store = new NexusConfigSnapshotStore();
        var service = Service(store);
        var token = service.Watch();

        token.HasChanged.Should().BeFalse();
        store.Swap(new ConfigSnapshot(
            new Dictionary<string, ConfigEntrySnapshot>(StringComparer.Ordinal), "e", DateTimeOffset.UtcNow));

        token.HasChanged.Should().BeTrue();
    }

    private static void RunWithEnv(string key, string? value, Action assert)
    {
        var previous = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
        try
        {
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    private sealed class Sample
    {
        public string? Name { get; set; }
    }
}
