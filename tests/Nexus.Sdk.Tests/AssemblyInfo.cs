// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

// Nexus.Sdk.Tests exercises process-wide static state: the ambient NexusConfig
// accessor and the NexusConfigProviderBridge. Two classes from different collections touching
// it in parallel overwrite each other, and the failure only shows under CI load — which is what
// made StartAsync_PublishesTheStaticAccessor flaky, red on a saturated runner and green on a
// quiet one. Serializing this assembly removes the whole class of race, including the shared
// statics nobody has found yet.
//
// Cost, re-measured for the review of PR #561 on a self-hosted 4-vCPU runner (the `agents` scale
// set — sibling of the `dotnet4` pool every CI job in this repo runs on, same cluster, not the
// identical pool), Release, --no-build, 8 runs each, reading the duration xUnit reports for the
// assembly:
//
//     serialized  697-970 ms (median ~740)    parallel  531-672 ms (median ~575)
//
// So the price is ~200 ms and it IS separable from noise — the ranges barely touch. An earlier
// version of this comment read 3325-3694 vs 2720-3717 ms and concluded the two were
// indistinguishable: three runs of a TRX duration that also counts discovery and host startup,
// noisy enough to swallow the delta. Eight runs of the assembly duration alone separate it, so
// that conclusion is retired rather than left standing. Reproduce with:
//
//     dotnet build tests/Nexus.Sdk.Tests -c Release
//     dotnet test  tests/Nexus.Sdk.Tests -c Release --no-build   # read "Duration:"
//
// 200 ms is still the right trade: the `dotnet test` invocation around it pays ~4 s of build and
// host startup whatever we do, and the PR gate is counted in minutes.
//
// Each test assembly runs in its own process under VSTest, and `Nexus.Sdk.Tests` is the only test
// assembly that reaches these statics at all, so serializing per assembly is enough and no other
// assembly loses parallelism. `Nexus.MCP.Server.Tests` also references `Nexus.Sdk`, but touches
// neither the accessor nor the bridge. Re-check with:
//
//     grep -rl 'NexusConfig\|AddNexus(' --include=*.cs tests | grep -v Nexus.Sdk.Tests   # -> empty
//
// Inside this assembly the same question is asked by a test rather than by a grep: the marker
// list lives in StaticStateIsolationGuardTests.
//
// The NexusSdkConfigStatic collection stays: it says WHICH classes share the state. This
// attribute only makes the question moot — it does not answer it. If someone ever lifts this
// line to win back the 200 ms, the collection is what remains standing, and
// StaticStateIsolationGuardTests is what keeps that membership true: it fails when this line
// goes, and it fails when a new class reaches the statics from outside the collection — which is
// exactly the mistake that opened .
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
