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

namespace Nexus.Sdk.Services;

/// <summary>
/// Manages cached feature configurations for the SDK.
/// </summary>
public interface IFeatureService
{
    /// <summary>Gets the full feature configuration, checking cache first then API.</summary>
    Task<FeatureConfig?> GetConfigAsync(string featureKey, CancellationToken ct = default);

    /// <summary>Pre-populates the cache with multiple feature configs (used after registration).</summary>
    void SetBulk(Dictionary<string, FeatureConfig> features);
}
