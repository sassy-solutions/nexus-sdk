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
/// Tracks a usage event in Nexus without feature gating.
/// Use this for standalone tracking (e.g., page views) where no feature gate is needed.
/// For gated features, use <see cref="NexusFeatureAttribute"/> which includes tracking.
/// </summary>
/// <example>
/// [NexusTrack("page.viewed")]
/// [HttpGet]
/// public IActionResult Index() { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NexusTrackAttribute : Attribute
{
    /// <summary>The event name to track.</summary>
    public string EventName { get; }

    /// <summary>Whether to track even when the action returns an error. Default: false.</summary>
    public bool TrackOnFailure { get; set; }

    public NexusTrackAttribute(string eventName) => EventName = eventName;
}
