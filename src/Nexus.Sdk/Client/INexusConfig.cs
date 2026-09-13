// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Primitives;

namespace Nexus.Sdk.Client;

/// <summary>
/// Accesses the application's resolved configuration and secrets from Nexus. Values are scoped by the
/// SDK's app/env/version context and served from an in-memory snapshot that a background service keeps
/// fresh — so the synchronous reads (<see cref="Get"/> / <see cref="GetRequired"/>) never perform IO
/// (no sync-over-async, no per-call HTTP, no deadlock hazard under a SynchronizationContext).
/// </summary>
/// <remarks>
/// Resolution fuses two layers, process environment variable FIRST, then the remote snapshot (see
/// <c>NexusOptions.DisableEnvironmentOverride</c> to invert). This lets a local override win and lets
/// EnvVar/MountedSecret-delivered keys (which arrive through the process environment) be read the same way as
/// RuntimePull keys — the calling code never needs to know a key's delivery mode.
/// </remarks>
public interface INexusConfig
{
    /// <summary>
    /// Gets a configuration value (synchronous, snapshot + process-environment fusion), or <c>null</c>
    /// when the key resolves to no value. A MountedSecret key that is absent from the process environment
    /// returns <c>null</c> (its value never travels the SDK channel — run in-platform or switch the
    /// delivery mode to RuntimePull).
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Gets a configuration value, throwing <see cref="Configuration.NexusConfigKeyMissingException"/>
    /// when it resolves to no value. Synchronous; same fusion as <see cref="Get"/>.
    /// </summary>
    string GetRequired(string key);

    /// <summary>A change token that fires when the underlying config snapshot is refreshed.</summary>
    IChangeToken Watch();

    /// <summary>
    /// Gets a configuration value by key. Retained for back-compat; now served from the snapshot (no
    /// per-call HTTP), so it completes synchronously.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets all configuration values for the current context (snapshot values; secrets delivered via
    /// MountedSecret are absent — their value is not on this channel).
    /// </summary>
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a configuration value and deserializes it to the specified type.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
}
