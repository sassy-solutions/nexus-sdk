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
/// What the host application wants <c>[NexusAuthorize]</c> to do when the permission check
/// cannot be answered — Nexus unreachable, key expired, platform 5xx.
/// </summary>
/// <remarks>
/// before this existed the question had no answer, because it had no shape:
/// <c>CheckPermissionAsync</c> returned <c>bool</c>, and returned <c>false</c> for "denied",
/// for 401, for 500 and for "no response at all". <c>NexusAuthorizeFilter</c> turned that single
/// <c>false</c> into <c>403 "You do not have the required permission: X"</c>. A network cut
/// between a tenant application and Nexus therefore made every annotated endpoint of that
/// application state something false, and the operator went looking at roles.
/// </remarks>
public enum NexusPermissionUnavailableBehavior
{
    /// <summary>
    /// Do not let the request through (default). The filter answers <b>503</b> with a
    /// <c>Retry-After</c> header and a body naming the platform, never the permission — the
    /// request fails, and it fails saying what is actually wrong.
    /// </summary>
    Deny = 0,

    /// <summary>
    /// Let the request through and log at <c>Warning</c>. Choose this only for surfaces where
    /// availability outranks the permission being enforced — it means a Nexus outage grants
    /// every caller every annotated permission on this application.
    /// </summary>
    Allow = 1,
}
