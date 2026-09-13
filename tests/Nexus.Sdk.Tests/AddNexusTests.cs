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
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Compendium.Abstractions.FeatureFlags;
using Compendium.Abstractions.FeatureFlags.Models;
using Compendium.Core.Results;
using Nexus.Sdk.Compendium;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Management;
using Nexus.Sdk.Management.Services;
using Nexus.Sdk.Management.Services.Http;
using Nexus.Sdk.Tests.Configuration;

namespace Nexus.Sdk.Tests;

/// <summary>
/// the registration surface: one entry point, one address, and a management
/// surface that only starts when it is asked for.
/// </summary>
/// <remarks>
/// This replaces <c>AddNexusManagementTests</c>. The handler-chain assertions it carried are
/// kept verbatim (they are about the wire, not about which method registered the client); what
/// is gone is the second entry point they were named after.
/// <para>
/// in the <c>NexusSdkConfigStatic</c> collection: <c>AddNexus</c> registers
/// <c>NexusConfigRefreshService</c> as a hosted service, whose <c>StartAsync</c> writes the
/// process-wide static accessor <c>NexusConfig</c>. Every <c>host.StartAsync()</c> below therefore
/// overwrites it for the whole process — and the hosts built here carry no <c>Nexus:ApiKey</c>, so
/// their snapshot store stays empty and every key read through them reads <c>null</c>. Run in
/// parallel, that made
/// <c>NexusConfigRefreshServiceTests.StartAsync_PublishesTheStaticAccessor</c> fail under CI load.
/// The attribute is load-bearing, not decorative: do not remove it — and
/// <c>StaticStateIsolationGuardTests</c> now fails if a class reaching those statics drops it.
/// </para>
/// </remarks>
[Collection(NexusSdkConfigStaticCollection.Name)]
public sealed class AddNexusTests
{
    private const string BaseUrl = "http://nexus-test/";

    // ------------------------------------------------------------------
    // Nexus:BaseUrl is required and named in the failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WithoutBaseUrl_FailsNamingTheConfigurationKey()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["Nexus:ApiKey"] = "nxs_test_key",
        });

        var start = async () => await host.StartAsync();

        // Before this started happily on an implicit in-cluster DNS name and failed
        // later, as a DNS error on the first request, with nothing naming the configuration.
        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain(NexusOptions.BaseUrlKey);
    }

    [Fact]
    public async Task StartAsync_WithBaseUrlFromTheConfigureDelegateOnly_Succeeds()
    {
        // The address is read from IOptions inside each client factory, not from IConfiguration
        // at registration time — so a value that only ever exists in the delegate is honoured.
        using var host = BuildHost(
            new Dictionary<string, string?>(),
            o => o.BaseUrl = BaseUrl);

        var start = async () => await host.StartAsync();

        await start.Should().NotThrowAsync<OptionsValidationException>();
        await host.StopAsync();
    }

    [Fact]
    public void TheSdkCarriesNoImplicitAddress()
    {
        // The companion of the guard above, at the property level: a default that survives a
        // successful bind onto an empty section is a default ValidateDataAnnotations can never see.
        new NexusOptions().BaseUrl.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Management is opt-in
    // ------------------------------------------------------------------

    [Fact]
    public void AddNexus_RegistersNexusAsTheCompendiumFeatureFlagProvider()
    {
        var sp = BuildProvider(new Dictionary<string, string?> { ["Nexus:BaseUrl"] = BaseUrl });

        // Not opt-in, unlike the management surface: the port reads the same flags the SDK
        // already reads, over the same API key, and adds no route a tenant could be refused on.
        sp.GetRequiredService<IFeatureFlags>().Should().BeOfType<NexusFeatureFlags>();
    }

    [Fact]
    public void AddNexus_WithAnotherFeatureFlagProviderAlreadyRegistered_KeepsIt()
    {
        // The point of implementing a provider-agnostic port: an application must be able to
        // move off Nexus by changing a registration. TryAdd is what makes that true — with a
        // plain Add, AddNexus would silently win over the provider the application chose.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFeatureFlags, ForeignFeatureFlags>();

        var config = BuildConfig(new Dictionary<string, string?> { ["Nexus:BaseUrl"] = BaseUrl });
        services.AddSingleton(config);
        services.AddNexus(config);

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IFeatureFlags>().Should().BeOfType<ForeignFeatureFlags>();
    }

    /// <summary>
    /// The port must be resolvable without a request scope. <c>NexusFeatureFlags</c> needs the
    /// deployed environment and version for Nexus's gate signals, and takes them from
    /// <c>IOptions&lt;NexusOptions&gt;</c> (a singleton) rather than from the scoped
    /// <c>INexusSubjectContext</c> — precisely so a hosted service or a singleton can read a flag.
    /// Built with scope validation on, so a scoped dependency creeping in fails here.
    /// </summary>
    [Fact]
    public void AddNexus_FeatureFlagPort_ResolvesFromTheRootProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Nexus:BaseUrl"] = BaseUrl,
            ["Nexus:Environment"] = "production",
            ["Nexus:Version"] = "2.7.0",
        });
        services.AddSingleton(config);
        services.AddNexus(config);

        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        sp.GetRequiredService<IFeatureFlags>().Should().BeOfType<NexusFeatureFlags>();
    }

    // ------------------------------------------------------------------
    // The wire, unchanged — X-Nexus-* headers are pinned by deployed apps
    // ------------------------------------------------------------------

    [Fact]
    public async Task EveryClient_TakesItsAddressFromOptions_NotFromTheConfigurationSection()
    {
        // The delegate wins over the section for the address, on the management clients too —
        // which only holds because the base address is resolved lazily per client.
        var capture = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        var config = BuildConfig(new Dictionary<string, string?> { ["Nexus:BaseUrl"] = "http://from-section/" });
        services.AddSingleton(config);
        services.AddNexus(config, o => o.BaseUrl = "http://from-delegate/");
        services.AddNexusManagement(config);
        services.AddHttpClient<IOrganizationService, HttpOrganizationService>()
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IOrganizationService>().ListMineAsync(CancellationToken.None);

        capture.LastRequest!.RequestUri!.Host.Should().Be("from-delegate");
    }

    // ------------------------------------------------------------------

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values,
        Action<NexusOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = BuildConfig(values);
        services.AddSingleton(config);
        services.AddNexus(config, configure);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A real host, because <c>ValidateOnStart()</c> is only enforced by host startup — the
    /// exact moment the acceptance criterion is about.
    /// </summary>
    private static IHost BuildHost(
        Dictionary<string, string?> values,
        Action<NexusOptions>? configure = null)
        => new HostBuilder()
            .ConfigureServices(services =>
            {
                var config = BuildConfig(values);
                services.AddSingleton(config);
                services.AddNexus(config, configure);
            })
            .Build();

    /// <summary>Stands in for any non-Nexus Compendium flag provider (LaunchDarkly, a fake, …).</summary>
    private sealed class ForeignFeatureFlags : IFeatureFlags
    {
        public Task<Result<bool>> IsOnAsync(string flagKey, FlagContext ctx, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<T>> GetVariantAsync<T>(string flagKey, T defaultValue, FlagContext ctx, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(defaultValue));

        public Task<Result<ExperimentAssignment>> GetExperimentAsync(string experimentKey, FlagContext ctx, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(new ExperimentAssignment("control", true, InExperiment: true)));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"organizations":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
