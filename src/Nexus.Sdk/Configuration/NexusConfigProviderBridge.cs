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
/// The rendezvous between the <see cref="NexusConfigurationProvider"/> instances (created pre-host,
/// inside <c>ConfigurationBuilder</c>, where DI does not yet exist) and the running host's config
/// refresh loop. <see cref="Services.NexusConfigRefreshService"/> calls <see cref="Publish"/> on every
/// snapshot swap; each registered provider re-projects the snapshot into <c>IConfiguration</c> and
/// raises its reload token so <c>IOptionsMonitor</c> reacts. A late-registering provider immediately
/// receives the latest snapshot.
/// </summary>
/// <remarks>
/// Process-wide static state, deliberately: the provider lives in the configuration root for the whole
/// host lifetime and cannot be reached through DI. Production runs one host per process. Tests that
/// exercise a provider directly should call <c>ApplySnapshot</c> on the instance rather than routing
/// through this bridge.
/// </remarks>
internal static class NexusConfigProviderBridge
{
    private static readonly object Gate = new();
    private static readonly List<NexusConfigurationProvider> Providers = new();
    private static ConfigSnapshot? _latest;

    /// <summary>Registers a provider and immediately hands it the latest snapshot, if any.</summary>
    public static void Register(NexusConfigurationProvider provider)
    {
        lock (Gate)
        {
            Providers.Add(provider);
            if (_latest is not null)
            {
                provider.ApplySnapshot(_latest);
            }
        }
    }

    /// <summary>Pushes a freshly-swapped snapshot to every registered provider.</summary>
    public static void Publish(ConfigSnapshot snapshot)
    {
        lock (Gate)
        {
            _latest = snapshot;
            foreach (var provider in Providers)
            {
                provider.ApplySnapshot(snapshot);
            }
        }
    }
}
