// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Management.Services;
using Nexus.Sdk.Management.Services.Http;

namespace Nexus.Sdk.Management;

/// <summary>
/// Registration entry point for the Nexus governance surface.
/// </summary>
public static class ManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers the eight typed governance clients: organizations, projects, applications,
    /// environments, roles, git credentials, git connections and platform resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// this was <c>AddNexus(o =&gt; o.EnableManagement = true)</c> until the SDK
    /// split in two. It is a separate call in a separate package now, and that is the point: a
    /// tenant application can no longer reach this surface by flipping a boolean it happens to
    /// find on its own options object.
    /// </para>
    /// <para>
    /// Self-sufficient by design — it binds <see cref="NexusOptions"/> itself and uses
    /// <c>TryAdd</c> throughout, so it may be called before <c>AddNexus</c>, after it, or
    /// entirely on its own in a tool that never touches the client surface.
    /// </para>
    /// <para>
    /// These routes are gated by administrator policies and reject an API-key principal with
    /// 403. Supply <see cref="NexusManagementOptions.BearerTokenProvider"/>, or every call will
    /// be refused.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddNexusManagement(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<NexusManagementOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        NexusOptionsRegistration.EnsureBound(services, configuration);

        var management = services.AddOptions<NexusManagementOptions>()
            .Bind(configuration.GetSection(NexusManagementOptions.SectionName));

        if (configure is not null)
        {
            management.Configure(configure);
        }

        // TryAdd : AddNexus enregistre les deux memes handlers. Appeler les deux
        // extensions dans le meme hote ne doit pas en creer deux exemplaires.
        services.TryAddTransient<NexusApiKeyHandler>();
        services.TryAddTransient<NexusContextHandler>();
        services.TryAddTransient<NexusBearerTokenHandler>();

        services.AddManagementClient<IOrganizationService, HttpOrganizationService>();
        services.AddManagementClient<IProjectService, HttpProjectService>();
        services.AddManagementClient<IApplicationService, HttpApplicationService>();
        services.AddManagementClient<IEnvironmentService, HttpEnvironmentService>();
        services.AddManagementClient<IRoleService, HttpRoleService>();
        services.AddManagementClient<IGitHubCredentialService, HttpGitHubCredentialService>();
        services.AddManagementClient<IGitConnectionService, HttpGitConnectionService>();
        services.AddManagementClient<IResourceService, HttpResourceService>();

        return services;
    }

    private static void AddManagementClient<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(NexusHttpClientDefaults.Configure)
            .AddHttpMessageHandler<NexusApiKeyHandler>()
            .AddHttpMessageHandler<NexusContextHandler>()
            .AddHttpMessageHandler<NexusBearerTokenHandler>()
            .AddStandardResilienceHandler();
    }
}
