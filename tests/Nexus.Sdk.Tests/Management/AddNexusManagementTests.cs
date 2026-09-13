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
using Nexus.Sdk.Management;
using Nexus.Sdk.Tests.Configuration;
using Nexus.Sdk.Management.Services;
using Xunit;

namespace Nexus.Sdk.Tests.Management;

/// <summary>
/// ce que <c>AddNexusManagement</c> enregistre, et ce que <c>AddNexus</c>
/// n'enregistre plus.
/// </summary>
/// <remarks>
/// <para>
/// Ces facts vivaient dans <c>AddNexusTests</c>, du temps où une seule extension enregistrait
/// les deux moitiés derrière <c>o =&gt; o.EnableManagement = true</c>. La scission en deux
/// paquets rend ce drapeau sans objet : un tenant ne peut plus atteindre la surface
/// d'administration en basculant un booléen qu'il trouve sur son propre objet d'options.
/// </para>
/// <para>
/// Le fait qui compte n'est pas le nombre de clients — c'est que la chaîne de handlers porte
/// bien les <b>trois</b> en-têtes. Ces routes refusent une clé d'API seule avec un 403 : une
/// clé porte des portées, pas des rôles.
/// </para>
/// </remarks>
[Collection(NexusSdkConfigStaticCollection.Name)]
public sealed class AddNexusManagementTests
{
    private const string BaseUrl = "http://nexus-test/";

    [Fact]
    public void AddNexus_Alone_RegistersNoManagementClient()
    {
        using var provider = Build(client: true, management: false);

        provider.GetService<IOrganizationService>().Should().BeNull(
            "le paquet client ne doit pas même pouvoir résoudre un service de gouvernance : "
            + "c'est la garantie que la scission achète");
    }

    [Fact]
    public void AddNexusManagement_RegistersAllEightTypedClients()
    {
        using var provider = Build(client: false, management: true);

        provider.GetService<IOrganizationService>().Should().NotBeNull();
        provider.GetService<IProjectService>().Should().NotBeNull();
        provider.GetService<IApplicationService>().Should().NotBeNull();
        provider.GetService<IEnvironmentService>().Should().NotBeNull();
        provider.GetService<IRoleService>().Should().NotBeNull();
        provider.GetService<IGitHubCredentialService>().Should().NotBeNull();
        provider.GetService<IGitConnectionService>().Should().NotBeNull();
        provider.GetService<IResourceService>().Should().NotBeNull();
    }

    [Fact]
    public void AddNexusManagement_IsSelfSufficient_WithoutAddNexus()
    {
        using var provider = Build(client: false, management: true);

        provider.GetService<IOrganizationService>().Should().NotBeNull(
            "un outil d'administration n'a aucune raison d'enregistrer la surface cliente ; "
            + "AddNexusManagement lie NexusOptions lui-même");
    }

    [Fact]
    public void BothExtensions_InTheSameHost_RegisterEachSharedHandlerOnce()
    {
        var services = NewServices(out var config);
        services.AddNexus(config);
        services.AddNexusManagement(config);

        services.Count(d => d.ServiceType == typeof(Nexus.Sdk.Client.NexusApiKeyHandler))
            .Should().Be(1, "les deux extensions utilisent TryAdd — appeler les deux ne doit pas doubler la chaîne");
    }

    [Fact]
    public async Task HandlerChain_AttachesApiKey_Context_AndBearer()
    {
        var capture = new CapturingHandler();
        var services = NewServices(out var config);
        services.AddNexusManagement(config, o => o.BearerTokenProvider = _ => Task.FromResult<string?>("jeton-admin"));
        services.AddHttpClient<IOrganizationService, Nexus.Sdk.Management.Services.Http.HttpOrganizationService>()
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrganizationService>().ListMineAsync(CancellationToken.None);

        capture.LastRequest.Should().NotBeNull();
        capture.LastRequest!.Headers.Authorization?.Parameter.Should().Be("jeton-admin");
    }

    [Fact]
    public async Task BearerHandler_IsNoOp_WhenProviderUnset()
    {
        var capture = new CapturingHandler();
        var services = NewServices(out var config);
        services.AddNexusManagement(config);
        services.AddHttpClient<IOrganizationService, Nexus.Sdk.Management.Services.Http.HttpOrganizationService>()
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrganizationService>().ListMineAsync(CancellationToken.None);

        capture.LastRequest!.Headers.Authorization.Should().BeNull(
            "sans fournisseur de jeton, le handler ne doit rien attacher — pas attacher du vide");
    }

    // ------------------------------------------------------------------

    private static ServiceCollection NewServices(out IConfiguration config)
    {
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Nexus:BaseUrl"] = BaseUrl,
            ["Nexus:ApiKey"] = "nxs_test_key",
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        return services;
    }

    private static ServiceProvider Build(bool client, bool management)
    {
        var services = NewServices(out var config);
        if (client)
        {
            services.AddNexus(config);
        }

        if (management)
        {
            services.AddNexusManagement(config);
        }

        return services.BuildServiceProvider();
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
