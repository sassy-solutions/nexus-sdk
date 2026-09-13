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
/// Configuration source that loads values from the Nexus API into IConfiguration.
/// Usage: builder.Configuration.AddNexus(builder.Configuration);
/// </summary>
public sealed class NexusConfigurationSource : IConfigurationSource
{
    private readonly NexusOptions _options;

    public NexusConfigurationSource(NexusOptions options)
    {
        _options = options;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new NexusConfigurationProvider(_options);
}
