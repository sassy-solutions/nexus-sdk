// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Client;

/// <summary>
/// The one place a Nexus <see cref="HttpClient"/> gets its address and timeout.
/// </summary>
/// <remarks>
/// <para>
/// Resolving <see cref="IOptions{TOptions}"/> here rather than reading
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> at registration time is what
/// makes <see cref="NexusOptions.BaseUrl"/> late-bound and validated: <c>ValidateOnStart</c> has
/// already run by the time a client is built, so <see cref="Uri"/> never sees an empty string on
/// the host path.
/// </para>
/// <para>
/// this was a private method of <c>NexusServiceCollectionExtensions</c> until the
/// governance surface moved to its own package. It is public now for one reason: both packages
/// must configure their clients <b>identically</b>. A second copy would drift — a different
/// timeout, a missing <c>Accept</c> header — and the drift would only show as a wire-level
/// difference between two clients talking to the same API.
/// </para>
/// </remarks>
public static class NexusHttpClientDefaults
{
    /// <summary>Applies the Nexus address, <c>Accept</c> header and timeout to <paramref name="client"/>.</summary>
    public static void Configure(IServiceProvider serviceProvider, HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(client);

        var options = serviceProvider.GetRequiredService<IOptions<NexusOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}
