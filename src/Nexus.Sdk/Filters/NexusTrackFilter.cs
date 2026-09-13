// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Filters;

/// <summary>
/// Processes <see cref="NexusTrackAttribute"/> for standalone usage tracking (no feature gating).
/// </summary>
public sealed class NexusTrackFilter : IAsyncActionFilter
{
    private readonly INexusClient _client;
    private readonly ILogger<NexusTrackFilter> _logger;

    public NexusTrackFilter(INexusClient client, ILogger<NexusTrackFilter> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<NexusTrackAttribute>()
            .FirstOrDefault();

        var result = await next();

        if (attr is null) return;

        var success = result.Exception is null && context.HttpContext.Response.StatusCode < 400;
        if (!success && !attr.TrackOnFailure) return;

        var eventName = attr.EventName;
        var metadata = new Dictionary<string, object>
        {
            ["path"] = context.HttpContext.Request.Path.Value ?? "",
            ["method"] = context.HttpContext.Request.Method,
            ["statusCode"] = context.HttpContext.Response.StatusCode
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await _client.TrackAsync(eventName, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track event {Event}", eventName);
            }
        });
    }
}
