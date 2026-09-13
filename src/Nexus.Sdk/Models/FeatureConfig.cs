// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Models;

/// <summary>
/// Full feature configuration cached by the SDK.
/// Drives gating, tracking, billing, and trigger behavior.
/// </summary>
public sealed record FeatureConfig(
    string Key,
    bool Enabled,
    TrackingConfig? Tracking,
    BillingConfig? Billing,
    TriggerConfig[]? Triggers);

/// <summary>
/// Tracking configuration for a feature. When present, usage is automatically tracked.
/// </summary>
public sealed record TrackingConfig(
    string EventName,
    bool IncludeMetadata,
    bool TrackOnFailure);

/// <summary>
/// Billing configuration for a feature. When present, billable events are recorded.
/// </summary>
public sealed record BillingConfig(
    string Unit,
    decimal Rate,
    string Currency,
    int UnitsPerRequest);

/// <summary>
/// Trigger configuration for a feature. Evaluated server-side by Nexus.
/// </summary>
public sealed record TriggerConfig(
    string Type,
    string Target,
    string? Condition);
