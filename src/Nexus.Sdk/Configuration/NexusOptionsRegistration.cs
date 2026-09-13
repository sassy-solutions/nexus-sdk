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

namespace Nexus.Sdk.Configuration;

/// <summary>
/// Binds <see cref="NexusOptions"/> exactly once, however many Nexus packages an application
/// registers.
/// </summary>
/// <remarks>
/// <para>
/// this exists because of a defect the split introduced and the tests caught.
/// <c>AddNexus</c> binds the <c>Nexus</c> section and then applies the caller's delegate on top;
/// options sources run in registration order, so a second <c>.Bind(section)</c> from
/// <c>AddNexusManagement</c> ran <b>last</b> and silently overwrote a <c>BaseUrl</c> the delegate
/// had set. An application calling both extensions would have talked to the wrong host, with
/// nothing to indicate why.
/// </para>
/// <para>
/// The marker is a service descriptor rather than a static field: static state is
/// process-wide, and two hosts in one process (a test run, a multi-host worker) would see each
/// other's registration. The container is the correct scope.
/// </para>
/// </remarks>
public static class NexusOptionsRegistration
{
    /// <summary>Idempotent. Binds and validates <see cref="NexusOptions"/> on first call only.</summary>
    public static IServiceCollection EnsureBound(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(d => d.ServiceType == typeof(Marker)))
        {
            return services;
        }

        services.AddSingleton<Marker>();
        services.AddOptions<NexusOptions>()
            .Bind(configuration.GetSection(NexusOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>Presence of this descriptor means the options were already bound.</summary>
    public sealed class Marker;
}
