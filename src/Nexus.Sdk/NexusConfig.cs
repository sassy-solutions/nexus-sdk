// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Nexus.Sdk.Client;

namespace Nexus.Sdk;

/// <summary>
/// The one-line sugar over <see cref="INexusConfig"/> — a static facade in the style of Serilog's
/// <c>Log.Logger</c>. <c>AddNexus(...)</c> publishes the ambient accessor at host startup, after which
/// application code can write:
/// <code>
/// using static Nexus.Sdk.NexusConfig;   // top of file (or a global using in the template)
/// var key = Config("STRIPE_KEY");        // string?  — synchronous, never does IO
/// var db  = ConfigRequired("DB_URL");    // string   — throws if unresolved
/// </code>
/// It is documented sugar on top of the canonical forms (<c>IConfiguration["KEY"]</c> and
/// <see cref="INexusConfig"/>); prefer those where testability of the ambient state matters. Tests set
/// the accessor directly via <see cref="SetAccessor"/>.
/// </summary>
public static class NexusConfig
{
    private static INexusConfig? _accessor;

    /// <summary>
    /// Publishes the ambient <see cref="INexusConfig"/> the static helpers delegate to. Called by
    /// <c>AddNexus</c> at host startup; call it directly in tests to inject a fake.
    /// </summary>
    public static void SetAccessor(INexusConfig config) =>
        _accessor = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Gets a configuration value, or <c>null</c> when unresolved. Synchronous, never does IO.
    /// </summary>
    public static string? Config(string key) => Accessor.Get(key);

    /// <summary>
    /// Gets a configuration value, throwing <see cref="Configuration.NexusConfigKeyMissingException"/>
    /// when unresolved.
    /// </summary>
    public static string ConfigRequired(string key) => Accessor.GetRequired(key);

    private static INexusConfig Accessor =>
        _accessor ?? throw new InvalidOperationException(
            "NexusConfig is not initialized. Call builder.Services.AddNexus(...) at startup, or NexusConfig.SetAccessor(...) in tests, before using Config()/ConfigRequired().");
}
