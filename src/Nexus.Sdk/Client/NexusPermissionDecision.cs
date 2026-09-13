// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Client;

/// <summary>
/// The three outcomes of a permission check. Not a <c>bool</c>, and not a <c>bool?</c>:
/// <c>null</c> would say "we do not know" without saying that "we do not know" is different in
/// kind from "no" — which is exactly the collapse is undoing. Not an exception
/// either: the SDK never throws for an expected failure.
/// </summary>
public enum NexusPermissionDecision
{
    /// <summary>Nexus answered, and the answer is no.</summary>
    Denied = 0,

    /// <summary>Nexus answered, and the answer is yes.</summary>
    Allowed = 1,

    /// <summary>
    /// Nexus did not answer the question: unreachable, timed out, 401 on an expired key, or 5xx.
    /// The host decides what happens next via
    /// <see cref="Configuration.NexusOptions.OnPermissionUnavailable"/>.
    /// </summary>
    Indeterminate = 2,
}
