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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Filters;

/// <summary>
/// The core SDK filter. Processes <see cref="NexusFeatureAttribute"/> to:
/// 1. Gate: check if feature is enabled
/// 2. Track: record usage event (fire-and-forget)
/// 3. Bill: record billable event (fire-and-forget)
/// 4. Trigger: server-side evaluation (via tracking event)
/// </summary>
public sealed class NexusFeatureFilter : IAsyncActionFilter
{
    private readonly IFeatureService _featureService;
    private readonly INexusClient _client;
    private readonly INexusSubjectContext _subjectContext;
    private readonly ILogger<NexusFeatureFilter> _logger;

    public NexusFeatureFilter(
        IFeatureService featureService,
        INexusClient client,
        INexusSubjectContext subjectContext,
        ILogger<NexusFeatureFilter> logger)
    {
        _featureService = featureService;
        _client = client;
        _subjectContext = subjectContext;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attrs = context.ActionDescriptor.EndpointMetadata
            .OfType<NexusFeatureAttribute>()
            .ToList();

        if (attrs.Count == 0)
        {
            await next();
            return;
        }

        // 1. Gate check — all features must be enabled. When the app supplies a subject
        // (tier/user/attributes) via INexusSubjectContext, evaluate PER-SUBJECT against
        // Nexus (attribute/tier/user targeting); otherwise, and if the per-subject check
        // is unavailable, fall back to the flag's default enablement — so apps that don't
        // opt into a subject context keep the exact previous behaviour (non-breaking).
        var subject = _subjectContext.GetCurrentSubject();
        var ct = context.HttpContext.RequestAborted;

        foreach (var attr in attrs)
        {
            bool enabled;
            if (subject is { HasSignal: true })
            {
                var perSubject = await _client.CheckFeatureAsync(attr.FeatureKey, subject, ct);
                enabled = perSubject
                    ?? (await _featureService.GetConfigAsync(attr.FeatureKey, ct))?.Enabled
                    ?? false;
            }
            else
            {
                var config = await _featureService.GetConfigAsync(attr.FeatureKey, ct);
                enabled = config?.Enabled ?? false;
            }

            if (!enabled)
            {
                _logger.LogDebug("Feature {Key} is not available", attr.FeatureKey);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Feature Not Available",
                    Detail = attr.DeniedMessage ?? $"The feature '{attr.FeatureKey}' is not currently available.",
                    Status = attr.DeniedStatusCode
                })
                { StatusCode = attr.DeniedStatusCode };
                return;
            }
        }

        // 2. Execute the action
        var result = await next();
        var success = result.Exception is null && context.HttpContext.Response.StatusCode < 400;

        // 3. Post-execution: track + bill (fire-and-forget)
        foreach (var attr in attrs)
        {
            var config = await _featureService.GetConfigAsync(attr.FeatureKey, default);
            if (config is null) continue;

            var capturedConfig = config;
            var metadata = BuildMetadata(context.HttpContext);

            _ = Task.Run(async () =>
            {
                try
                {
                    // Tracking
                    if (capturedConfig.Tracking is not null && (success || capturedConfig.Tracking.TrackOnFailure))
                    {
                        if (capturedConfig.Tracking.IncludeMetadata)
                            await _client.TrackAsync(capturedConfig.Tracking.EventName, metadata);
                        else
                            await _client.TrackAsync(capturedConfig.Tracking.EventName);
                    }

                    // Billing
                    if (capturedConfig.Billing is not null && success)
                    {
                        await _client.BillAsync(capturedConfig.Key, capturedConfig.Billing.UnitsPerRequest);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Post-execution processing failed for feature {Key}", capturedConfig.Key);
                }
            });
        }
    }

    private static Dictionary<string, object> BuildMetadata(HttpContext context)
    {
        return new Dictionary<string, object>
        {
            ["path"] = context.Request.Path.Value ?? "",
            ["method"] = context.Request.Method,
            ["statusCode"] = context.Response.StatusCode
        };
    }
}
