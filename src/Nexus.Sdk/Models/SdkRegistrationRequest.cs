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
/// Request sent by the SDK on startup to register discovered features and permissions.
/// </summary>
public sealed record SdkRegistrationRequest(
    string? ApplicationId,
    string? Environment,
    string? Version,
    IReadOnlyList<FeatureRegistration> Features,
    IReadOnlyList<PermissionRegistration> Permissions);

public sealed record FeatureRegistration(
    string Key,
    string? Source);

public sealed record PermissionRegistration(
    string Code,
    string? Source);
