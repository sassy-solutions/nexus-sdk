// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.FeatureFlags;
using Compendium.Abstractions.FeatureFlags.Models;
using Compendium.Core.Results;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Services;

namespace Nexus.Sdk.Compendium;

/// <summary>
/// Nexus as a <see cref="IFeatureFlags"/> provider: the Compendium feature-flag
/// port, served by the Nexus flag engine over <see cref="INexusClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Before it, the only way to read a Nexus flag was
/// <c>[NexusFeature]</c> / <see cref="INexusClient"/> — Nexus-shaped types, all the way down.
/// An application that wanted to move off Nexus (or stand a fake in front of it in a test) had
/// to edit every call site. Against <see cref="IFeatureFlags"/> it edits one DI registration:
/// the port is provider-agnostic by design, and LaunchDarkly, ConfigCat, GrowthBook or a test
/// double satisfy it just as well.
/// </para>
/// <para>
/// <b>What Nexus does not answer.</b> The Nexus flag engine evaluates <em>booleans</em>:
/// matrix gate, then version gate, then environment gate, then user override, then attribute
/// override, then tier override, then the flag default
/// (<c>Nexus.Core.Domain.Policies.FeatureFlagPolicy</c>).
/// It carries no typed variants and no experiment registry, so <see cref="GetVariantAsync{T}"/>
/// answers beyond <see cref="bool"/> with the caller's default, and
/// <see cref="GetExperimentAsync"/> always fails. Both are documented on the members themselves;
/// see also the SDK README.
/// </para>
/// <para>
/// <b>The subject is half caller, half deployment.</b> <see cref="FlagContext"/> carries exactly
/// three things — tenant, user, attributes — while Nexus's three highest-priority signals (the
/// matrix, version and environment gates) are built from two values it has no field for. Those
/// are properties of the running deployment, not of a call, so they come from
/// <see cref="NexusOptions"/>: the same source
/// <see cref="OptionsNexusSubjectContext"/> reads for <c>[NexusFeature]</c> gates. Without that,
/// a flag gated Off in production would read On through this port while the gate on the very
/// same flag held — one flag, two answers, in one process. See <see cref="ToSubject"/>.
/// </para>
/// <para>
/// <b>Tenancy.</b> <see cref="FlagContext.TenantId"/> is mandatory on the Compendium side and is
/// supplied by the caller. Nexus does not take the caller's word for it — see
/// <see cref="ToSubject"/>.
/// </para>
/// </remarks>
public sealed class NexusFeatureFlags : IFeatureFlags
{
    private readonly INexusClient _client;
    private readonly IOptions<NexusOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="NexusFeatureFlags"/> class.</summary>
    /// <param name="client">The Nexus client the evaluation is delegated to.</param>
    /// <param name="options">
    /// The SDK options, read for the deployed environment and version — the gate signals
    /// <see cref="FlagContext"/> has no field for. See <see cref="ToSubject"/>.
    /// </param>
    public NexusFeatureFlags(INexusClient client, IOptions<NexusOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
    }

    /// <summary>
    /// Evaluates a boolean Nexus flag for <paramref name="ctx"/>.
    /// </summary>
    /// <param name="flagKey">The flag key, as declared in Nexus.</param>
    /// <param name="ctx">The Compendium evaluation context. See the remarks for what is used.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The evaluated enablement, or a failure carrying
    /// <see cref="FeatureFlagErrors.FlagNotFound(string)"/> when no configuration could be
    /// obtained for the key at all.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The per-subject check (<c>GET api/v1/feature-flags/{key}/check</c>) is asked first: it is
    /// the only path that applies Nexus's own precedence (gates &gt; user override &gt; attribute
    /// override &gt; tier override &gt; default). When it cannot answer —
    /// <see cref="INexusClient.CheckFeatureAsync"/> returns <see langword="null"/> on a network or
    /// transient error — the flag's own configuration is read instead and its default enablement
    /// returned. That fallback is the flag's <em>global</em> default: it deliberately ignores every
    /// gate and override, because none of them can be evaluated without the server.
    /// </para>
    /// <para>
    /// <b>Why the failure is always <c>FlagNotFound</c> and never <c>ProviderUnreachable</c>.</b>
    /// <see cref="INexusClient"/> answers <see langword="null"/> for both "no such flag" and
    /// "Nexus is unreachable", so at this point the two are genuinely indistinguishable.
    /// Reporting <see cref="FeatureFlagErrors.ProviderUnreachable(string)"/> would claim more
    /// than is known; <c>FlagNotFound</c> states what was observed — no configuration could be
    /// obtained for this key. Telling them apart means widening the
    /// <see cref="INexusClient"/> contract, which is a separate change.
    /// </para>
    /// </remarks>
    public async Task<Result<bool>> IsOnAsync(
        string flagKey,
        FlagContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var evaluated = await _client.CheckFeatureAsync(flagKey, ToSubject(ctx), ct);
        if (evaluated.HasValue)
        {
            return Result.Success(evaluated.Value);
        }

        var config = await _client.GetFeatureAsync(flagKey, ct);
        return config is not null
            ? Result.Success(config.Enabled)
            : Result.Failure<bool>(FeatureFlagErrors.FlagNotFound(flagKey));
    }

    /// <summary>
    /// Resolves a typed variant. <b>Nexus has no typed variants</b>: only <c>T = bool</c> (and
    /// <c>bool?</c>) is actually evaluated, every other <typeparamref name="T"/> gets
    /// <paramref name="defaultValue"/> back without a network call.
    /// </summary>
    /// <typeparam name="T">The CLR type of the variant value.</typeparam>
    /// <param name="flagKey">The flag key, as declared in Nexus.</param>
    /// <param name="defaultValue">The value returned when Nexus has no opinion.</param>
    /// <param name="ctx">The Compendium evaluation context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// For <c>T = bool</c>, the result of <see cref="IsOnAsync"/> — including its failure.
    /// Otherwise <paramref name="defaultValue"/>, successfully: the port defines the default as
    /// "what comes back when the provider has no opinion", which is exactly Nexus's situation
    /// here, so this is the contract rather than a way around it.
    /// </returns>
    /// <remarks>
    /// The type is compared through <see cref="Nullable.GetUnderlyingType(Type)"/>, so
    /// <c>T = bool?</c> is evaluated like <c>bool</c>. A plain <c>typeof(T) != typeof(bool)</c>
    /// would send <c>GetVariantAsync&lt;bool?&gt;</c> down the "not a boolean" branch and hand
    /// back the default without ever asking Nexus — silently, for a caller who spelled the one
    /// question Nexus can answer, merely with a nullable. <c>bool?</c> is the natural spelling
    /// for "a flag with no default", so this is the shape most likely to be hit.
    /// </remarks>
    public async Task<Result<T>> GetVariantAsync<T>(
        string flagKey,
        T defaultValue,
        FlagContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if ((Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)) != typeof(bool))
        {
            return Result.Success(defaultValue);
        }

        var evaluated = await IsOnAsync(flagKey, ctx, ct);
        return evaluated.IsSuccess
            ? Result.Success((T)(object)evaluated.Value)
            : Result.Failure<T>(evaluated.Error);
    }

    /// <summary>
    /// Always fails: <b>Nexus runs no experiments</b> — the flag engine has no experiment
    /// registry, so there is nothing here to assign a caller to.
    /// </summary>
    /// <param name="experimentKey">The experiment key.</param>
    /// <param name="ctx">The Compendium evaluation context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A failure carrying <see cref="FeatureFlagErrors.FlagNotFound(string)"/>. No call is made.
    /// </returns>
    /// <remarks>
    /// A failure rather than a fabricated fallback assignment, and the difference matters:
    /// <see cref="ExperimentAssignment.InExperiment"/> set to <see langword="false"/> is a
    /// positive answer — "you were evaluated and left out of the cohort". Nexus cannot say that,
    /// because there is no experiment to be left out of. The caller has to be able to tell the
    /// two apart, and only a failure tells it.
    /// </remarks>
    public Task<Result<ExperimentAssignment>> GetExperimentAsync(
        string experimentKey,
        FlagContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return Task.FromResult(
            Result.Failure<ExperimentAssignment>(FeatureFlagErrors.FlagNotFound(experimentKey)));
    }

    /// <summary>
    /// Maps a Compendium <see cref="FlagContext"/> onto the Nexus targeting subject, completed
    /// with the deployment's own environment and version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="FlagContext.TenantId"/> is deliberately dropped, and must stay dropped.</b>
    /// It is mandatory on the Compendium side, where it is the caller's declaration of the scope
    /// it wants evaluated. Nexus does not accept such a declaration: the server derives the
    /// organization from the authenticated caller, and the <c>X-Organization-Id</c> header was
    /// removed outright for that reason (NXS-SEC-02 / POM-402, see
    /// <c>FeatureFlagsController</c>). Forwarding <c>ctx.TenantId</c> in any shape — query
    /// parameter, header, route segment — would reintroduce a caller-supplied tenant claim. The
    /// scope of an evaluation is, and remains, the scope of the SDK's API key.
    /// </para>
    /// <para>
    /// <b><see cref="NexusSubject.Environment"/> and <see cref="NexusSubject.Version"/> come from
    /// <see cref="NexusOptions"/>, not from the caller.</b> They are the three highest-priority
    /// signals in <c>Nexus.Core.Domain.Policies.FeatureFlagPolicy</c> (matrix gate, version gate,
    /// environment gate — all ranked above the user override), and <see cref="FlagContext"/> has
    /// no field for either. Reading them from the options the app was deployed with is what makes
    /// this port agree with <c>[NexusFeature]</c> on the same flag in the same process: that path
    /// builds its subject from the same two values through
    /// <see cref="OptionsNexusSubjectContext"/>. Left unset, an environment gate simply would not
    /// match, and a flag switched Off for production would read On here.
    /// </para>
    /// <para>
    /// <b><see cref="NexusSubject.Tier"/> is left unset, and that is not neutral.</b> The server
    /// binds an absent <c>tier</c> query parameter to <c>SubscriptionTier.Free</c>
    /// (<c>FeatureFlagsController.CheckFlag</c>) — it does <em>not</em> derive the tier from the
    /// API key — so a flag carrying tier overrides is evaluated against <c>Free</c> through this
    /// port. <see cref="FlagContext"/> offers no tier, and inferring one from an attribute would
    /// mean duplicating a server-side enum in the SDK, where an unparseable value costs a 400.
    /// The attribute override branch is ranked above the tier branch and <em>is</em> reachable:
    /// per-tier targeting through this port goes through <see cref="FlagContext.Attributes"/>
    /// (e.g. <c>tier=pro</c>), matched against the flag's attribute overrides.
    /// </para>
    /// </remarks>
    private NexusSubject ToSubject(FlagContext ctx)
    {
        var options = _options.Value;

        return new NexusSubject(
            EndUserId: ctx.UserId,
            Attributes: ctx.Attributes?.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal),
            Environment: NullIfBlank(options.Environment),
            Version: NullIfBlank(options.Version));
    }

    /// <summary>
    /// Blank is absence, mirroring <see cref="OptionsNexusSubjectContext"/>: an unset
    /// <c>Nexus__Environment</c> binds to <see cref="string.Empty"/>, and sending
    /// <c>environment=</c> would ask Nexus to resolve an empty slug.
    /// </summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
