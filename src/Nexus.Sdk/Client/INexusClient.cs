// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Nexus.Sdk.Models;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Client;

/// <summary>
/// Primary client interface for communicating with the Nexus Platform API.
/// </summary>
public interface INexusClient
{
    /// <summary>Gets full feature configuration.</summary>
    Task<FeatureConfig?> GetFeatureAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Evaluates a feature flag for a specific subject (tier/user/attributes) against
    /// Nexus (<c>GET /api/v1/feature-flags/{key}/check</c>). Returns the evaluated
    /// enablement, or <see langword="null"/> when the evaluation could not be performed
    /// (network/transient error) so the caller can fall back to the flag default.
    /// </summary>
    Task<bool?> CheckFeatureAsync(string key, NexusSubject subject, CancellationToken ct = default);

    /// <summary>Tracks a usage event.</summary>
    Task<NexusResult> TrackAsync(string eventName, Dictionary<string, object>? metadata = null, CancellationToken ct = default);

    /// <summary>Records a billable usage event.</summary>
    Task<NexusResult> BillAsync(string featureKey, int units = 1, CancellationToken ct = default);

    /// <summary>
    /// Asks Nexus whether the current context holds a permission. Answers with three values,
    /// never two: <see cref="NexusPermissionDecision.Allowed"/>,
    /// <see cref="NexusPermissionDecision.Denied"/>, or
    /// <see cref="NexusPermissionDecision.Indeterminate"/> when the question could not be
    /// answered at all.
    /// </summary>
    /// <remarks>
    /// this returned <c>bool</c>, and returned <c>false</c> for four unrelated
    /// situations: the server said no; the server said 401 (expired key); the server said 500;
    /// the server said nothing (network exception, swallowed). <see cref="NexusPermissionDecision"/>
    /// keeps the first two apart from the last two, which is what lets
    /// <c>NexusAuthorizeFilter</c> stop answering "you do not have the required permission" to a
    /// caller whose only problem is that Nexus is down.
    /// </remarks>
    Task<NexusPermissionDecision> CheckPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>Registers discovered features and permissions on startup.</summary>
    Task<SdkRegistrationResponse?> RegisterAsync(SdkRegistrationRequest request, CancellationToken ct = default);

    /// <summary>Checks if the Nexus API is healthy.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
