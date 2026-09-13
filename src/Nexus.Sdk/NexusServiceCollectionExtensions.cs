// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Compendium;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Filters;
using Nexus.Sdk.Health;
using Nexus.Sdk.Services;

namespace Nexus.Sdk;

/// <summary>
/// Extension methods for registering Nexus SDK services.
/// </summary>
public static class NexusServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Nexus SDK services with configuration from the "Nexus" section. This is the
    /// <b>only</b> registration entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default this registers the lean app-runtime surface (remote config, feature flags,
    /// usage, health, auto-registration) authenticated with <c>X-Api-Key</c>.
    /// </para>
    /// <para>
    /// <c>Nexus:BaseUrl</c> is required and has no default. It is read from
    /// <see cref="IOptions{TOptions}"/> <em>inside</em> each typed client's factory rather than
    /// from <see cref="IConfiguration"/> at registration time, so a value set through the
    /// <paramref name="configure"/> delegate (or any other options source) is honoured, and so
    /// the missing-key failure is <c>ValidateOnStart</c>'s — naming the key — instead of a DNS
    /// error on the first request.
    /// </para>
    /// </remarks>
    /// <example>
    /// builder.Services.AddNexus(builder.Configuration);
    /// builder.Services.AddNexus(builder.Configuration, o =&gt; o.RequireKeys("DB_URL"));
    /// </example>
    public static IServiceCollection AddNexus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<NexusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind and validate options. [Required] on BaseUrl + ValidateOnStart is what turns a
        // forgotten Nexus:BaseUrl into a startup failure naming the key.
        NexusOptionsRegistration.EnsureBound(services, configuration);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // HTTP client with API key and context handlers + resilience
        // TryAdd : le paquet de gouvernance enregistre les memes handlers. Appeler
        // AddNexus() et AddNexusManagement() dans le meme hote ne doit pas en creer deux.
        services.TryAddTransient<NexusApiKeyHandler>();
        services.TryAddTransient<NexusContextHandler>();

        services.AddHttpClient<INexusClient, NexusClient>(NexusHttpClientDefaults.Configure)
            .AddHttpMessageHandler<NexusApiKeyHandler>()
            .AddHttpMessageHandler<NexusContextHandler>()
            .AddStandardResilienceHandler();

        // Config & Secrets v2 — the snapshot store + refresh loop (replaces the per-key HTTP/cache
        // config fetcher). Sync reads on INexusConfig / NexusConfig.Config(...) never do IO: they read
        // the in-memory snapshot the NexusConfigRefreshService keeps fresh, fused with the process
        // environment. The refresh service polls GET api/v2/sdk/config via this named client (same
        // handlers + resilience), swaps the store on ETag change, and enforces required keys at startup.
        services.AddSingleton<NexusConfigSnapshotStore>();
        services.AddSingleton<INexusConfig, NexusConfigService>();

        services.AddHttpClient(NexusConfigRefreshService.HttpClientName, NexusHttpClientDefaults.Configure)
            .AddHttpMessageHandler<NexusApiKeyHandler>()
            .AddHttpMessageHandler<NexusContextHandler>()
            .AddStandardResilienceHandler();

        services.AddHostedService<NexusConfigRefreshService>();

        // Feature config cache
        services.AddMemoryCache();
        services.AddSingleton<IFeatureService, FeatureService>();

        // the same flags, reachable through the Compendium port. This is what makes
        // Nexus substitutable: an application resolving IFeatureFlags swaps it for LaunchDarkly,
        // ConfigCat or a test double by changing this one registration, with no call site edited.
        //
        // TryAdd, not Add: an application that registered its own IFeatureFlags BEFORE calling
        // AddNexus keeps it. Registering unconditionally would destroy, at registration time, the
        // very substitutability the port exists for.
        //
        // Transient, not Singleton: NexusFeatureFlags depends on INexusClient, which
        // AddHttpClient<TClient,TImpl> registers as transient. A singleton adapter would capture
        // it. The adapter is stateless, so transient costs nothing.
        //
        // Its other dependency is IOptions<NexusOptions> (a singleton), deliberately, and NOT
        // INexusSubjectContext: the adapter needs the deployed environment/version for Nexus's
        // gate signals, and taking them from the scoped subject context would make IFeatureFlags
        // unresolvable outside a request scope — a background service reading a flag would fail
        // at startup. Same two values, same source, no lifetime constraint.
        services.TryAddTransient<IFeatureFlags, NexusFeatureFlags>();

        // The default subject carries the app's deployed environment + version (from NexusOptions),
        // so [NexusFeature] gates are environment/version-aware out of the box (routed to the
        // gate-aware /check path). An app that wants per-user tier/attribute targeting registers its
        // own INexusSubjectContext BEFORE calling AddNexus — TryAdd keeps that override winning.
        services.TryAddScoped<INexusSubjectContext, OptionsNexusSubjectContext>();

        // MVC filters
        services.AddScoped<NexusFeatureFilter>();
        services.AddScoped<NexusTrackFilter>();
        services.AddScoped<NexusAuthorizeFilter>();

        // Auto-registration on startup
        services.AddHostedService<NexusRegistrationHostedService>();

        // Health check — tagged "nexus" (NOT "ready"). A tenant workload's readiness must
        // not hard-depend on the control-plane API being reachable: gating "/health/ready" on
        // it means a platform blip (or egress hiccup) drains traffic from otherwise-healthy
        // tenant pods, and the SDK's ~3s reachability timeout can exceed the readiness probe's
        // timeout → pods stuck 0/1. The check still surfaces on "/health" (all checks) for
        // observability; consumers that WANT it in readiness can re-tag explicitly.
        services.AddHealthChecks()
            .AddCheck<NexusHealthCheck>("nexus", tags: ["nexus"]);

        return services;
    }
}
