// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Filters;

/// <summary>
/// Processes <see cref="NexusAuthorizeAttribute"/> for permission-based authorization via Nexus.
/// </summary>
/// <remarks>
/// three outcomes, three responses. <see cref="NexusPermissionDecision.Denied"/> is
/// still a 403 naming the permission, byte-for-byte what it was. What changed is that
/// <see cref="NexusPermissionDecision.Indeterminate"/> no longer arrives here disguised as
/// <c>Denied</c>: the default is still to refuse the request (fail-closed — a network cut must not
/// become a privilege escalation), but it refuses with <b>503 + Retry-After</b> and a body naming
/// the platform, so the operator looks at reachability instead of at roles. A host that would
/// rather stay available sets
/// <see cref="NexusOptions.OnPermissionUnavailable"/> to
/// <see cref="NexusPermissionUnavailableBehavior.Allow"/>, and that choice is written in its
/// configuration rather than inherited from a package upgrade.
/// </remarks>
public sealed class NexusAuthorizeFilter : IAsyncAuthorizationFilter
{
    /// <summary>
    /// The <c>ProblemDetails.Extensions</c> key a caller can branch on, so distinguishing
    /// "Nexus is down" from a genuine 503 does not require string-matching a title.
    /// </summary>
    public const string IndeterminateProblemCode = "nexus.permission.indeterminate";

    private readonly INexusClient _client;
    private readonly NexusOptions _options;
    private readonly ILogger<NexusAuthorizeFilter> _logger;

    public NexusAuthorizeFilter(
        INexusClient client,
        IOptions<NexusOptions> options,
        ILogger<NexusAuthorizeFilter> logger)
    {
        _client = client;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attrs = context.ActionDescriptor.EndpointMetadata
            .OfType<NexusAuthorizeAttribute>()
            .ToList();

        if (attrs.Count == 0) return;

        foreach (var attr in attrs)
        {
            var permissionOrRole = attr.Permission ?? attr.Role;
            if (string.IsNullOrEmpty(permissionOrRole)) continue;

            var decision = await _client.CheckPermissionAsync(permissionOrRole, context.HttpContext.RequestAborted);

            switch (decision)
            {
                case NexusPermissionDecision.Allowed:
                    continue;

                case NexusPermissionDecision.Denied:
                    _logger.LogDebug("Permission denied: {Permission}", permissionOrRole);
                    context.Result = Forbidden(permissionOrRole);
                    return;

                default:
                    if (_options.OnPermissionUnavailable == NexusPermissionUnavailableBehavior.Allow)
                    {
                        _logger.LogWarning(
                            "Nexus could not answer the permission check for {Permission}; letting the "
                            + "request through because Nexus:OnPermissionUnavailable is set to Allow",
                            permissionOrRole);
                        continue;
                    }

                    _logger.LogWarning(
                        "Nexus could not answer the permission check for {Permission}; refusing with 503",
                        permissionOrRole);
                    context.Result = Unavailable();
                    context.HttpContext.Response.Headers.RetryAfter = "10";
                    return;
            }
        }
    }

    private static ObjectResult Forbidden(string permissionOrRole) =>
        new(new ProblemDetails
        {
            Title = "Forbidden",
            Detail = $"You do not have the required permission: {permissionOrRole}",
            Status = 403,
        })
        { StatusCode = 403 };

    /// <summary>
    /// The honest failure. The detail deliberately does <b>not</b> mention the permission: the
    /// caller's permissions are exactly what is not known here, and saying otherwise is what sent
    /// operators looking at roles during a platform outage.
    /// </summary>
    private static ObjectResult Unavailable()
    {
        var problem = new ProblemDetails
        {
            Title = "Nexus unreachable",
            Detail = "The Nexus platform could not be reached to authorize this request. "
                + "This is not a permission failure — retry once the platform is reachable.",
            Status = 503,
        };

        problem.Extensions["code"] = IndeterminateProblemCode;

        return new ObjectResult(problem) { StatusCode = 503 };
    }
}
