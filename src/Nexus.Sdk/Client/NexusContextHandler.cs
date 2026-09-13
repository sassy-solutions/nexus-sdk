// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Client;

/// <summary>
/// DelegatingHandler that adds context headers (app ID, environment, version) to outgoing requests.
/// </summary>
public sealed class NexusContextHandler : DelegatingHandler
{
    private readonly NexusOptions _options;

    public NexusContextHandler(IOptions<NexusOptions> options)
    {
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.ApplicationId))
            request.Headers.TryAddWithoutValidation("X-Nexus-App-Id", _options.ApplicationId);

        if (!string.IsNullOrEmpty(_options.Environment))
            request.Headers.TryAddWithoutValidation("X-Nexus-Env", _options.Environment);

        if (!string.IsNullOrEmpty(_options.Version))
            request.Headers.TryAddWithoutValidation("X-Nexus-Version", _options.Version);

        return base.SendAsync(request, cancellationToken);
    }
}
