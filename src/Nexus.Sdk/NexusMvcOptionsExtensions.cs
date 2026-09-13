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
using Nexus.Sdk.Filters;

namespace Nexus.Sdk;

/// <summary>
/// Extension methods for adding Nexus filters to MVC options.
/// </summary>
public static class NexusMvcOptionsExtensions
{
    /// <summary>
    /// Adds all Nexus SDK action filters to the MVC pipeline.
    /// </summary>
    /// <example>
    /// builder.Services.AddControllers(options => options.Filters.AddNexusFilters());
    /// </example>
    public static MvcOptions AddNexusFilters(this MvcOptions options)
    {
        options.Filters.AddService<NexusFeatureFilter>();
        options.Filters.AddService<NexusTrackFilter>();
        options.Filters.AddService<NexusAuthorizeFilter>();
        return options;
    }
}
