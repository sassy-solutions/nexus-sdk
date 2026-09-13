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
/// Thrown by <see cref="Client.INexusConfig.GetRequired"/> / <c>NexusConfig.ConfigRequired</c> when a
/// key has no value in either the process environment or the remote snapshot. Access-time failure for
/// keys not declared up front via <see cref="NexusOptions.RequireKeys"/>.
/// </summary>
public sealed class NexusConfigKeyMissingException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NexusConfigKeyMissingException"/> class.</summary>
    public NexusConfigKeyMissingException(string key)
        : base($"Required Nexus config key '{key}' resolved to no value (checked the process environment and the Nexus snapshot).")
    {
        Key = key;
    }

    /// <summary>The key that was missing.</summary>
    public string Key { get; }
}

/// <summary>
/// Thrown at startup by <see cref="Services.NexusConfigRefreshService"/> when one or more
/// <see cref="NexusOptions.RequiredKeys"/> are unmet after the initial load — the host does not start
/// (fail-closed, same intent as options <c>ValidateOnStart</c>). This includes the case where Nexus is
/// unreachable at boot and the missing keys are not covered by the process environment.
/// </summary>
public sealed class NexusConfigUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NexusConfigUnavailableException"/> class.</summary>
    public NexusConfigUnavailableException(IReadOnlyList<string> missingKeys)
        : base($"Nexus config is missing required key(s) at startup: {string.Join(", ", missingKeys)}. The host will not start.")
    {
        MissingKeys = missingKeys;
    }

    /// <summary>The required keys that were unmet.</summary>
    public IReadOnlyList<string> MissingKeys { get; }
}
