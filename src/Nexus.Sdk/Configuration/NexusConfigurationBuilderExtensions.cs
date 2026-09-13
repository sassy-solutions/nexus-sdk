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

namespace Nexus.Sdk.Configuration;

/// <summary>
/// Extension methods for adding Nexus as an <see cref="IConfiguration"/> source.
/// </summary>
public static class NexusConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds Nexus remote config/secrets as an <see cref="IConfiguration"/> source. Values are loaded at
    /// startup and available via <c>IConfiguration["key"]</c>, with periodic refresh once the host runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Precedence.</b> The Nexus source is inserted immediately BEFORE the environment-variables
    /// source when one is present (otherwise appended). Because later sources win in .NET configuration,
    /// the effective order becomes: process environment (pod) &gt; Nexus &gt; appsettings. This is a change
    /// from the previous "appended last" behavior, where Nexus overrode the process environment — the new
    /// order lets a local/pod override (docker-compose, <c>kubectl set env</c>) win and lets
    /// EnvVar/MountedSecret-delivered keys be read transparently. Invert with
    /// <c>NexusOptions.DisableEnvironmentOverride</c> on the <c>INexusConfig</c> path.
    /// </para>
    /// </remarks>
    /// <example>
    /// builder.Configuration.AddNexus(builder.Configuration);
    /// // Then: builder.Configuration["ConnectionStrings:Database"]
    /// </example>
    public static IConfigurationBuilder AddNexus(this IConfigurationBuilder builder, IConfiguration existingConfig)
    {
        var options = new NexusOptions();
        existingConfig.GetSection(NexusOptions.SectionName).Bind(options);

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            return builder; // No API key configured, skip.
        }

        // this path runs BEFORE the host exists, so ValidateOnStart cannot reach it.
        // It gets the same contract by hand: an app that asked for remote configuration and did
        // not say where Nexus lives is misconfigured, and must hear the key's name rather than
        // `new Uri("")` throwing "Invalid URI: The URI is empty" from inside the provider.
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{NexusOptions.BaseUrlKey} is required and has no default. Set the configuration "
                + $"key '{NexusOptions.BaseUrlKey}' (environment variable 'Nexus__BaseUrl') to the "
                + "address of the Nexus API before adding Nexus as a configuration source.");
        }

        var source = new NexusConfigurationSource(options);

        // Insert just before the environment-variables source so the process environment wins over Nexus.
        // Matched by type name to avoid a hard package dependency on the env-vars provider assembly.
        var envIndex = IndexOfEnvironmentVariablesSource(builder);
        if (envIndex >= 0)
        {
            builder.Sources.Insert(envIndex, source);
        }
        else
        {
            builder.Add(source);
        }

        return builder;
    }

    private static int IndexOfEnvironmentVariablesSource(IConfigurationBuilder builder)
    {
        for (var i = 0; i < builder.Sources.Count; i++)
        {
            if (builder.Sources[i].GetType().Name == "EnvironmentVariablesConfigurationSource")
            {
                return i;
            }
        }

        return -1;
    }
}
