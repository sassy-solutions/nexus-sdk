// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Services;

/// <summary>
/// The singleton that holds the current config <see cref="ConfigSnapshot"/> and swaps it by reference.
/// This is the heart of the "sync reads never do IO" design: readers (<see cref="Client.INexusConfig"/>,
/// the static <c>NexusConfig</c> facade, the IConfiguration provider) always observe a fully-built
/// snapshot with a single volatile read, and <see cref="NexusConfigRefreshService"/> is the only writer.
/// </summary>
public sealed class NexusConfigSnapshotStore
{
    private volatile ConfigSnapshot _current = ConfigSnapshot.Empty;

    // Renewed on every swap; mirrors the IConfiguration reload-token idiom (a consumer that observed the
    // previous token gets it cancelled, re-subscribes, and receives the fresh one).
    private ConfigurationReloadToken _reloadToken = new();

    /// <summary>The current snapshot (lock-free single volatile read).</summary>
    public ConfigSnapshot Current => _current;

    /// <summary>The entry for <paramref name="key"/> in the current snapshot, or <c>null</c> when absent.</summary>
    public ConfigEntrySnapshot? Get(string key) =>
        _current.Entries.TryGetValue(key, out var entry) ? entry : null;

    /// <summary>All entries in the current snapshot.</summary>
    public IReadOnlyDictionary<string, ConfigEntrySnapshot> GetAll() => _current.Entries;

    /// <summary>
    /// A change token that fires the next time the snapshot is swapped. Consumed by
    /// <c>INexusConfig.Watch()</c> and the IConfiguration provider (so <c>IOptionsMonitor</c> reacts).
    /// </summary>
    public IChangeToken GetReloadToken() => _reloadToken;

    /// <summary>
    /// Atomically replaces the current snapshot and fires the reload token. Called by the refresh
    /// service only when the ETag actually changed (a 304 poll never swaps).
    /// </summary>
    public void Swap(ConfigSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _current = next;
        var previous = Interlocked.Exchange(ref _reloadToken, new ConfigurationReloadToken());
        previous.OnReload();
    }
}
