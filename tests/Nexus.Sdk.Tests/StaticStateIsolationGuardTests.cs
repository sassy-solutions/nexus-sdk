// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using FluentAssertions;
using Nexus.Sdk.Tests.Configuration;

namespace Nexus.Sdk.Tests;

/// <summary>
/// the two guarantees that keep this assembly's process-wide static state from
/// racing with itself, asserted rather than commented.
/// </summary>
/// <remarks>
/// A race cannot be pinned by a test that reproduces it: the original defect was green most of
/// the time, which is what a timing test would also be. So these do not reproduce anything —
/// they assert the two invariants the fix installed, and fail deterministically the day either
/// one is lifted by accident.
/// </remarks>
public sealed class StaticStateIsolationGuardTests
{
    /// <summary>
    /// Call-shaped markers for reaching the SDK's process-wide statics: the ambient
    /// <c>NexusConfig</c> accessor and the <c>NexusConfigProviderBridge</c> (which
    /// <c>NexusConfigurationProvider</c>'s constructor and <c>NexusConfigRefreshService.StartAsync</c>
    /// both write). The trailing parenthesis is deliberate — it keeps a <c>&lt;see cref&gt;</c>
    /// mentioning one of these from counting as a use.
    /// </summary>
    private static readonly string[] StaticStateMarkers =
    [
        "NexusConfig.SetAccessor(",
        "NexusConfig.Config(",
        "NexusConfig.ConfigRequired(",
        "new NexusConfigurationProvider(",
        "new NexusConfigRefreshService(",
        "AddNexus(",
    ];

    private const string ProjectPath = "tests/Nexus.Sdk.Tests";

    /// <summary>
    /// The attribute the offending file must carry, spelled through the type itself so a rename
    /// of the collection breaks the compile instead of silently emptying this rule.
    /// </summary>
    private static readonly string CollectionMarker =
        $"[Collection({nameof(NexusSdkConfigStaticCollection)}.{nameof(NexusSdkConfigStaticCollection.Name)})]";

    [Fact]
    public void Assembly_DisablesTestParallelization()
    {
        var behavior = typeof(StaticStateIsolationGuardTests).Assembly
            .GetCustomAttribute<CollectionBehaviorAttribute>();

        behavior.Should().NotBeNull(
            "AssemblyInfo.cs carries [assembly: CollectionBehavior(DisableTestParallelization = true)]; "
            + "without it, classes from different collections run concurrently over the same statics");

        behavior!.DisableTestParallelization.Should().BeTrue(
            "this is the line that removes the whole class of race — read the comment above it in "
            + "AssemblyInfo.cs before deciding the intra-assembly parallelism is worth more");
    }

    [Fact]
    public void EveryFileReachingTheProcessWideStatics_JoinsTheSharedCollection()
    {
        var projectRoot = Path.Combine(
            RepositoryRoot(),
            ProjectPath.Replace('/', Path.DirectorySeparatorChar));

        var thisFile = $"{nameof(StaticStateIsolationGuardTests)}.cs";

        var scanned = 0;
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.GetFileName(file) == thisFile)
            {
                continue;
            }

            var text = File.ReadAllText(file);

            // Only files that declare tests can carry the collection attribute, so only they can
            // break the rule. This is what keeps AssemblyInfo.cs — which spells out the markers in
            // prose — from reporting itself. A shared helper that reached the statics would slip
            // through; none does today, and the class using it would still be the one to fix.
            var declaresTests = text.Contains("[Fact]", StringComparison.Ordinal)
                || text.Contains("[Theory]", StringComparison.Ordinal);

            if (!declaresTests
                || !StaticStateMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal)))
            {
                continue;
            }

            scanned++;
            if (!text.Contains(CollectionMarker, StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(projectRoot, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        scanned.Should().BeGreaterThan(0,
            "the scan found no file reaching the SDK statics, so it sealed nothing — the markers "
            + "have drifted away from the code they were written for");

        offenders.Should().BeEmpty(
            "a class that writes the ambient NexusConfig accessor or the provider bridge without "
            + "{0} is exactly how happened: AddNexusTests published the accessor from "
            + "outside the collection and flipped NexusConfigRefreshServiceTests red under load. "
            + "The assembly-wide serialization currently hides that, so this rule is what keeps the "
            + "collection membership true rather than decorative",
            CollectionMarker);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (global.json) from AppContext.BaseDirectory.");
    }
}
