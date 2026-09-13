// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Attributes;

/// <summary>
/// Gates an endpoint behind a Nexus feature and automatically handles
/// tracking, billing, and triggers as configured in the Nexus Platform.
/// </summary>
/// <example>
/// [NexusFeature("premium_analytics")]
/// [HttpGet("analytics")]
/// public IActionResult GetAnalytics() { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class NexusFeatureAttribute : Attribute
{
    /// <summary>The feature key as registered in Nexus.</summary>
    public string FeatureKey { get; }

    /// <summary>HTTP status code returned when the feature is disabled. Default: 404.</summary>
    public int DeniedStatusCode { get; set; } = 404;

    /// <summary>Custom message returned when the feature is disabled.</summary>
    public string? DeniedMessage { get; set; }

    public NexusFeatureAttribute(string featureKey) => FeatureKey = featureKey;
}
