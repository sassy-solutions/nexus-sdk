// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Health;

/// <summary>
/// Health check that verifies connectivity to the Nexus API.
/// </summary>
public sealed class NexusHealthCheck : IHealthCheck
{
    private readonly INexusClient _client;

    public NexusHealthCheck(INexusClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var healthy = await _client.IsHealthyAsync(cancellationToken);
        return healthy
            ? HealthCheckResult.Healthy("Nexus API is reachable")
            : HealthCheckResult.Degraded("Nexus API is not reachable");
    }
}
