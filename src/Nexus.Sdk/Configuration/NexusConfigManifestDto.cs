// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Configuration;

/// <summary>
/// The <c>GET api/v2/sdk/config</c> response the SDK deserializes. Mirrors the server's
/// <c>SdkConfigManifest</c> / <c>SdkConfigEntry</c> contract (see <c>SdkConfigV2Controller</c>). Kept
/// internal — consumers see <see cref="ConfigSnapshot"/> / <see cref="ConfigEntrySnapshot"/>.
/// </summary>
internal sealed record NexusConfigManifestDto(
    string? ETag,
    DateTimeOffset ResolvedAt,
    NexusConfigManifestEntryDto[]? Entries);

/// <summary>One resolved entry in the v2 manifest.</summary>
internal sealed record NexusConfigManifestEntryDto(
    string Key,
    string DeliveryMode,
    bool IsSecret,
    string? Value,
    int ValueVersion,
    string? Source);
