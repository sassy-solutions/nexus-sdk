// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Xunit;

namespace Nexus.Sdk.Tests.Configuration;

/// <summary>
/// Groups the tests that touch SDK config process-wide static state — the ambient
/// <c>NexusConfig</c> accessor and the <c>NexusConfigProviderBridge</c> — into one collection so xUnit
/// runs them sequentially (never in parallel with each other), avoiding cross-test interference.
/// </summary>
[CollectionDefinition(Name)]
public sealed class NexusSdkConfigStaticCollection
{
    public const string Name = "NexusSdkConfigStatic";
}
