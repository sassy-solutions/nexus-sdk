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
/// One resolved entry as it arrives in the <c>GET api/v2/sdk/config</c> manifest. Mirrors the server's
/// <c>SdkConfigEntry</c> contract. <see cref="Value"/> is <c>null</c> for a <c>MountedSecret</c> entry (that
/// value only ever travels through the orchestrator, never this channel) — such entries are read from the workload
/// environment, not the snapshot.
/// </summary>
/// <param name="Key">The config key.</param>
/// <param name="DeliveryMode">How the value reaches the workload: <c>EnvVar</c>, <c>MountedSecret</c>, or <c>RuntimePull</c>.</param>
/// <param name="IsSecret">Whether the entry is a secret.</param>
/// <param name="Value">The resolved plaintext, or <c>null</c> for MountedSecret entries (and any unresolved key).</param>
/// <param name="ValueVersion">The winning binding's ordinal (changes when the value changes).</param>
/// <param name="Source">The scope level the value resolved from (debug only).</param>
public sealed record ConfigEntrySnapshot(
    string Key,
    string DeliveryMode,
    bool IsSecret,
    string? Value,
    int ValueVersion,
    string? Source);

/// <summary>
/// An immutable point-in-time view of the resolved config for the SDK's scope. Swapped by reference in
/// <see cref="Services.NexusConfigSnapshotStore"/> so readers are lock-free and never observe a torn set.
/// </summary>
/// <param name="Entries">The resolved entries, keyed by their key (ordinal).</param>
/// <param name="ETag">The manifest ETag, used for the next conditional (<c>If-None-Match</c>) poll.</param>
/// <param name="FetchedAt">When this snapshot was fetched.</param>
public sealed record ConfigSnapshot(
    IReadOnlyDictionary<string, ConfigEntrySnapshot> Entries,
    string? ETag,
    DateTimeOffset FetchedAt)
{
    /// <summary>The empty snapshot the store holds before the first successful load.</summary>
    public static readonly ConfigSnapshot Empty = new(
        new Dictionary<string, ConfigEntrySnapshot>(StringComparer.Ordinal),
        ETag: null,
        FetchedAt: DateTimeOffset.MinValue);
}
