// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Services;

/// <summary>
/// The subject a feature flag is evaluated for: the current end-user's targeting
/// context. The consuming app is responsible only for <b>declaring</b> features
/// (<c>[NexusFeature]</c>) and <b>supplying</b> this subject (who the caller is and
/// which attributes they carry) — Nexus owns the actual targeting configuration
/// (which tier/attribute is on/off), and the SDK evaluates against it.
/// </summary>
/// <param name="Tier">Optional subscription tier of the subject (Free/Pro/Enterprise).</param>
/// <param name="EndUserId">Optional end-user id for user-specific overrides.</param>
/// <param name="Attributes">
/// The subject's ABAC attributes as <c>name → value</c> (e.g. <c>tier=premium</c>,
/// <c>role=beta</c>). These are matched against the flag's attribute overrides
/// configured in Nexus.
/// </param>
/// <param name="Environment">Optional environment slug (or id) the caller runs in — resolved to
/// the flag's environment gate. Typically the app's ASPNETCORE_ENVIRONMENT.</param>
/// <param name="Version">Optional deployed version tag the caller runs — resolved to the flag's
/// version gate. A deployment is active by default unless a version gate says otherwise.</param>
public sealed record NexusSubject(
    string? Tier = null,
    string? EndUserId = null,
    IReadOnlyDictionary<string, string>? Attributes = null,
    string? Environment = null,
    string? Version = null)
{
    /// <summary>True when the subject carries any targeting signal worth a per-subject check.</summary>
    public bool HasSignal =>
        !string.IsNullOrWhiteSpace(Tier)
        || !string.IsNullOrWhiteSpace(EndUserId)
        || (Attributes is { Count: > 0 })
        || !string.IsNullOrWhiteSpace(Environment)
        || !string.IsNullOrWhiteSpace(Version);
}

/// <summary>
/// Supplies the current request's <see cref="NexusSubject"/> so the SDK can evaluate
/// feature flags <b>per subject</b> (tier/user/attribute targeting) against Nexus.
/// </summary>
/// <remarks>
/// Opt-in and non-breaking: the SDK registers <see cref="NullNexusSubjectContext"/>
/// by default, which returns <see langword="null"/> — so a gate falls back to the
/// flag's default enablement exactly as before. An app that wants per-subject
/// targeting registers its own implementation (e.g. reading the signed-in user's
/// tier/roles from claims) via <c>services.AddScoped&lt;INexusSubjectContext, MyContext&gt;()</c>.
/// </remarks>
public interface INexusSubjectContext
{
    /// <summary>The current subject, or <see langword="null"/> when there is none (→ default enablement).</summary>
    NexusSubject? GetCurrentSubject();
}

/// <summary>Default no-op subject context — returns no subject, so gates use the flag default.</summary>
public sealed class NullNexusSubjectContext : INexusSubjectContext
{
    /// <inheritdoc />
    public NexusSubject? GetCurrentSubject() => null;
}
