// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Management;

/// <summary>
/// Options specific to the governance surface. Address, API key, timeout and context all come
/// from <see cref="Configuration.NexusOptions"/>; only the bearer token lives here.
/// </summary>
/// <remarks>
/// <para>
/// this used to be a property on <c>NexusOptions</c>, which meant every tenant
/// application carried, in its own configuration surface, an option that only an administration
/// tool could ever set. Moving it here is what lets the client package stop describing the
/// platform's authorization model at all.
/// </para>
/// <para>
/// The governance routes are gated by administrator policies and reject an API-key principal
/// with 403: an API key carries scopes, not roles. They need a bearer token from the platform's
/// identity provider, which is what this provider supplies.
/// </para>
/// </remarks>
public sealed class NexusManagementOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Nexus:Management";

    /// <summary>
    /// Async provider for the administrator bearer token, invoked once per outgoing request.
    /// A null provider (the default) attaches no <c>Authorization</c> header, and an existing
    /// one is never overwritten.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; set; }
}
