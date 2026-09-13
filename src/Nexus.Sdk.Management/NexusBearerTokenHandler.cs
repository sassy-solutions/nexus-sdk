// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Nexus.Sdk.Management;

/// <summary>
/// DelegatingHandler that attaches a the platform identity provider JWT bearer token to outgoing requests,
/// resolved on each call from <see cref="NexusManagementOptions.BearerTokenProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The Nexus management surface (organizations, projects, applications, roles) is gated
/// by admin / permission policies that are only satisfied by a the platform identity provider JWT — an
/// <c>X-Api-Key</c> principal carries only <c>scope</c> claims and no role / UserContext,
/// so it 403s on those routes. This handler lets the management SDK forward a bearer token
/// without touching the API-key path used by the runtime config/feature SDK.
/// </para>
/// <para>
/// The handler is a no-op when <see cref="NexusManagementOptions.BearerTokenProvider"/> is unset or
/// returns null/empty, and it never overwrites an <c>Authorization</c> header already set on
/// the request. It resolves the token per request so short-lived tokens are always fresh.
/// </para>
/// </remarks>
public sealed class NexusBearerTokenHandler : DelegatingHandler
{
    private readonly NexusManagementOptions _options;

    /// <summary>Initializes a new instance of the <see cref="NexusBearerTokenHandler"/> class.</summary>
    public NexusBearerTokenHandler(IOptions<NexusManagementOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null && _options.BearerTokenProvider is not null)
        {
            var token = await _options.BearerTokenProvider(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
