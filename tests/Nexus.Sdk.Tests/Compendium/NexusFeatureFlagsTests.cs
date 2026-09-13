// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.FeatureFlags.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Compendium;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Models;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Tests.Compendium;

/// <summary>
/// the Compendium feature-flag port, served by Nexus.
/// </summary>
/// <remarks>
/// The one test nobody would write spontaneously is
/// <see cref="IsOnAsync_WithTenantIdInContext_ShouldNotLeakItToTheSubject"/>: the port makes
/// <see cref="FlagContext.TenantId"/> mandatory and caller-supplied, and Nexus derives the
/// organization from the authenticated caller instead (NXS-SEC-02). The mapping must drop it,
/// and that is a property of the adapter, not of a route — so only a test holds it.
/// </remarks>
public sealed class NexusFeatureFlagsTests
{
    private const string FlagKey = "reports.export";

    [Fact]
    public async Task IsOnAsync_WhenCheckReturnsTrue_ShouldSucceedWithTrue()
    {
        var client = new RecordingNexusClient { CheckResult = true };

        var result = await Flags(client).IsOnAsync(FlagKey, Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        client.GetFeatureCalls.Should().BeEmpty("the per-subject check answered, so no fallback is needed");
    }

    [Fact]
    public async Task IsOnAsync_WhenCheckReturnsFalse_ShouldSucceedWithFalse()
    {
        var client = new RecordingNexusClient { CheckResult = false };

        var result = await Flags(client).IsOnAsync(FlagKey, Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        client.GetFeatureCalls.Should().BeEmpty("false is an answer, not an absence of one");
    }

    [Fact]
    public async Task IsOnAsync_WhenCheckReturnsNullAndConfigEnabled_ShouldFallBackToConfig()
    {
        var client = new RecordingNexusClient
        {
            CheckResult = null,
            Feature = new FeatureConfig(FlagKey, Enabled: true, null, null, null),
        };

        var result = await Flags(client).IsOnAsync(FlagKey, Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        client.GetFeatureCalls.Should().ContainSingle().Which.Should().Be(FlagKey);
    }

    [Fact]
    public async Task IsOnAsync_WhenCheckAndConfigBothUnavailable_ShouldFailWithFlagNotFound()
    {
        var client = new RecordingNexusClient { CheckResult = null, Feature = null };

        var result = await Flags(client).IsOnAsync(FlagKey, Context());

        result.IsFailure.Should().BeTrue();

        // On the code, not the message: the message is prose and may be reworded upstream.
        result.Error.Code.Should().Be(FeatureFlagErrorCode(FlagKey));
    }

    [Fact]
    public async Task IsOnAsync_WithUserIdInContext_ShouldPassItAsEndUserId()
    {
        var client = new RecordingNexusClient { CheckResult = true };

        await Flags(client).IsOnAsync(FlagKey, Context(userId: "user-42"));

        client.CheckedSubjects.Should().ContainSingle().Which.EndUserId.Should().Be("user-42");
    }

    [Fact]
    public async Task IsOnAsync_WithAttributesInContext_ShouldPassThemAsStrings()
    {
        var client = new RecordingNexusClient { CheckResult = true };
        var attributes = new Dictionary<string, object> { ["role"] = "beta", ["seats"] = 12 };

        await Flags(client).IsOnAsync(FlagKey, Context(attributes: attributes));

        var subject = client.CheckedSubjects.Should().ContainSingle().Subject;
        subject.Attributes.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["role"] = "beta",
            ["seats"] = "12",
        });
    }

    /// <summary>
    /// NXS-SEC-02 — the caller's tenant claim must not reach Nexus in any shape. The subject is
    /// the only thing the adapter builds, so the whole of it is inspected: none of its five
    /// components may carry the tenant id.
    /// </summary>
    [Fact]
    public async Task IsOnAsync_WithTenantIdInContext_ShouldNotLeakItToTheSubject()
    {
        const string TenantId = "tenant-that-must-not-travel";
        var client = new RecordingNexusClient { CheckResult = true };

        await Flags(client).IsOnAsync(FlagKey, Context(tenantId: TenantId, userId: "user-42"));

        var subject = client.CheckedSubjects.Should().ContainSingle().Subject;

        subject.Tier.Should().NotBe(TenantId);
        subject.EndUserId.Should().NotBe(TenantId);
        subject.Environment.Should().NotBe(TenantId);
        subject.Version.Should().NotBe(TenantId);
        (subject.Attributes ?? new Dictionary<string, string>())
            .Should().NotContain(kv => kv.Value == TenantId || kv.Key == TenantId);
    }

    [Fact]
    public async Task GetVariantAsync_WithBoolean_ShouldFollowTheEvaluation()
    {
        var client = new RecordingNexusClient { CheckResult = true };

        var result = await Flags(client).GetVariantAsync(FlagKey, false, Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        client.CheckedSubjects.Should().ContainSingle("a boolean variant IS an evaluation");
    }

    [Fact]
    public async Task GetVariantAsync_WithBooleanAndNothingAvailable_ShouldPropagateTheFailure()
    {
        var client = new RecordingNexusClient { CheckResult = null, Feature = null };

        var result = await Flags(client).GetVariantAsync(FlagKey, false, Context());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FeatureFlagErrorCode(FlagKey));
    }

    [Fact]
    public async Task GetVariantAsync_WithNonBoolean_ShouldReturnTheDefaultWithoutCallingTheClient()
    {
        var client = new RecordingNexusClient();

        var result = await Flags(client).GetVariantAsync(FlagKey, "control", Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("control");
        client.CheckedSubjects.Should().BeEmpty();
        client.GetFeatureCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExperimentAsync_Always_ShouldFailWithoutCallingTheClient()
    {
        var client = new RecordingNexusClient();

        var result = await Flags(client).GetExperimentAsync("checkout-copy", Context());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FeatureFlagErrorCode("checkout-copy"));
        client.CheckedSubjects.Should().BeEmpty();
        client.GetFeatureCalls.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // The deployment half of the subject — environment and version gates
    // ------------------------------------------------------------------

    /// <summary>
    /// The environment and version gates outrank every other signal in
    /// <c>Nexus.Core.Domain.Policies.FeatureFlagPolicy</c>, and <see cref="FlagContext"/> carries neither.
    /// If the port omitted them, a flag switched Off for production would read On through
    /// <see cref="IFeatureFlags"/> while <c>[NexusFeature]</c> — which does send them, from these
    /// very same options — held the gate shut on the same flag in the same process.
    /// </summary>
    [Fact]
    public async Task IsOnAsync_ShouldCarryTheDeployedEnvironmentAndVersion()
    {
        var client = new RecordingNexusClient { CheckResult = true };

        await Flags(client, environment: "production", version: "2.7.0").IsOnAsync(FlagKey, Context());

        var subject = client.CheckedSubjects.Should().ContainSingle().Subject;
        subject.Environment.Should().Be("production");
        subject.Version.Should().Be("2.7.0");
    }

    /// <summary>
    /// Same two values, same source, same subject: the port and <c>[NexusFeature]</c> must not
    /// disagree about where the process is running. Asserted against
    /// <see cref="OptionsNexusSubjectContext"/> itself rather than against literals, so the two
    /// cannot drift apart without this failing.
    /// </summary>
    [Fact]
    public async Task IsOnAsync_ShouldBuildTheSameEnvironmentAndVersionAsTheNexusFeatureGate()
    {
        var options = Options(environment: "staging", version: "1.4.2");
        var client = new RecordingNexusClient { CheckResult = true };

        await new NexusFeatureFlags(client, options).IsOnAsync(FlagKey, Context());

        var gateSubject = new OptionsNexusSubjectContext(options).GetCurrentSubject();
        var portSubject = client.CheckedSubjects.Should().ContainSingle().Subject;

        portSubject.Environment.Should().Be(gateSubject!.Environment);
        portSubject.Version.Should().Be(gateSubject.Version);
    }

    /// <summary>
    /// Unset must stay unset: <c>Nexus__Environment</c> absent binds to the empty string, and
    /// forwarding it would ask Nexus to resolve an empty environment slug rather than skip the
    /// gate. Blank is absence, exactly as in <see cref="OptionsNexusSubjectContext"/>.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task IsOnAsync_WithBlankEnvironmentOrVersion_ShouldSendNeither(string? env, string? version)
    {
        var client = new RecordingNexusClient { CheckResult = true };

        await Flags(client, env, version).IsOnAsync(FlagKey, Context());

        var subject = client.CheckedSubjects.Should().ContainSingle().Subject;
        subject.Environment.Should().BeNull();
        subject.Version.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // bool? is the one question Nexus can answer, merely spelled nullable
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>bool?</c> is the natural spelling for "a flag with no default". A
    /// <c>typeof(T) != typeof(bool)</c> test would push it down the "Nexus has no typed variants"
    /// branch and return the default without ever asking — the caller asked the one question
    /// Nexus does answer, and would be silently refused it.
    /// </summary>
    [Fact]
    public async Task GetVariantAsync_WithNullableBoolean_ShouldEvaluateLikeBoolean()
    {
        var client = new RecordingNexusClient { CheckResult = true };

        var result = await Flags(client).GetVariantAsync<bool?>(FlagKey, null, Context());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        client.CheckedSubjects.Should().ContainSingle("a nullable boolean IS still an evaluation");
    }

    [Fact]
    public async Task GetVariantAsync_WithNullableBooleanAndNothingAvailable_ShouldPropagateTheFailure()
    {
        var client = new RecordingNexusClient { CheckResult = null, Feature = null };

        var result = await Flags(client).GetVariantAsync<bool?>(FlagKey, null, Context());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FeatureFlagErrorCode(FlagKey));
    }

    /// <summary>
    /// The expected error code, read off the port's own factory rather than spelled out here —
    /// a literal would only assert that this test and the adapter agree with each other.
    /// </summary>
    private static string FeatureFlagErrorCode(string key) =>
        global::Compendium.Abstractions.FeatureFlags.FeatureFlagErrors.FlagNotFound(key).Code;

    private static NexusFeatureFlags Flags(
        INexusClient client, string? environment = null, string? version = null) =>
        new(client, Options(environment, version));

    private static IOptions<NexusOptions> Options(string? environment, string? version) =>
        Microsoft.Extensions.Options.Options.Create(
            new NexusOptions { Environment = environment, Version = version });

    private static FlagContext Context(
        string tenantId = "org-123",
        string? userId = null,
        IReadOnlyDictionary<string, object>? attributes = null) =>
        new(tenantId, userId, attributes);

    /// <summary>
    /// A hand-written stub, matching this project's convention (see
    /// <c>NexusAuthorizeFilterTests.StubNexusClient</c>): no mocking library is referenced here,
    /// and recording the two calls that matter is shorter than the setup would be.
    /// </summary>
    private sealed class RecordingNexusClient : INexusClient
    {
        public bool? CheckResult { get; init; }

        public FeatureConfig? Feature { get; init; }

        public List<NexusSubject> CheckedSubjects { get; } = [];

        public List<string> GetFeatureCalls { get; } = [];

        public Task<bool?> CheckFeatureAsync(string key, NexusSubject subject, CancellationToken ct = default)
        {
            CheckedSubjects.Add(subject);
            return Task.FromResult(CheckResult);
        }

        public Task<FeatureConfig?> GetFeatureAsync(string key, CancellationToken ct = default)
        {
            GetFeatureCalls.Add(key);
            return Task.FromResult(Feature);
        }

        public Task<NexusResult> TrackAsync(string eventName, Dictionary<string, object>? metadata = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<NexusResult> BillAsync(string featureKey, int units = 1, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<NexusPermissionDecision> CheckPermissionAsync(string permission, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SdkRegistrationResponse?> RegisterAsync(SdkRegistrationRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
