// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Nexus.Sdk.Configuration;

/// <summary>
/// Configuration options for the Nexus SDK. Binds to the "Nexus" configuration section.
/// </summary>
public sealed class NexusOptions
{
    public const string SectionName = "Nexus";

    /// <summary>
    /// The configuration key carrying <see cref="BaseUrl"/>, spelled once so the validation
    /// message, the README and the tests cannot drift from each other.
    /// </summary>
    public const string BaseUrlKey = "Nexus:BaseUrl";

    /// <summary>
    /// Base URL of the Nexus API. <b>Required</b> — there is deliberately no default.
    /// </summary>
    /// <remarks>
    /// this used to default to an in-platform DNS name of the production API
    /// namespace, repeated on three sites (here and in both registration extensions). Outside
    /// that cluster, forgetting the key did not produce a configuration error: it produced a DNS
    /// resolution failure at the first request, surfaced as a 503 carrying an exception message,
    /// with nothing pointing at the configuration. Worse, the default lived on the property
    /// itself, so it survived a <em>successful</em> bind onto an empty section and
    /// <c>ValidateDataAnnotations</c> had nothing to complain about. Now the property starts
    /// empty and is <see cref="RequiredAttribute"/>, so <c>ValidateOnStart()</c> fails the host
    /// with a message naming <see cref="BaseUrlKey"/> before a single request is made.
    /// The literal old value is deliberately not repeated in this file: the acceptance criterion
    /// of is a grep over <c>src/SDK</c> returning nothing.
    /// </remarks>
    [Required(AllowEmptyStrings = false, ErrorMessage =
        "Nexus:BaseUrl is required and has no default. Set the configuration key 'Nexus:BaseUrl' "
        + "(environment variable 'Nexus__BaseUrl') to the address of the Nexus API — for example "
        + "https://api.nexus.example.com. The SDK no longer falls back to an in-platform DNS name: "
        + "outside the cluster that fallback failed as an unexplained 503 at the first request.")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key for authentication (nxs_ prefix).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Application identifier (auto-detected from API key or configured).</summary>
    public string? ApplicationId { get; set; }

    /// <summary>Environment name (e.g., "production", "staging").</summary>
    public string? Environment { get; set; }

    /// <summary>Application version (e.g., "1.0.0").</summary>
    public string? Version { get; set; }

    /// <summary>Whether to auto-register features on startup.</summary>
    public bool EnableAutoRegistration { get; set; } = true;

    /// <summary>Feature config cache TTL in seconds.</summary>
    public int FeatureCacheTtlSeconds { get; set; } = 300;

    /// <summary>
    /// How often (seconds) the background refresh polls <c>GET api/v2/sdk/config</c> for the config
    /// snapshot. A conditional <c>If-None-Match</c> request makes a no-change poll a cheap 304, so this
    /// can be low (default 60s) without material server cost. Separate from
    /// <see cref="FeatureCacheTtlSeconds"/>, which still governs the per-key feature-flag cache.
    /// </summary>
    public int ConfigRefreshSeconds { get; set; } = 60;

    /// <summary>
    /// When <c>false</c> (default), a process environment variable of the same name overrides the
    /// remote snapshot value for a key (the standard .NET "env vars are the latest layer" semantics —
    /// a docker-compose / <c>kubectl set env</c> / local override always wins, and an EnvVar/MountedSecret
    /// key delivered through the workload is read transparently). Set to <c>true</c> to make the remote
    /// snapshot authoritative and ignore process environment variables (guards against local spoofing).
    /// </summary>
    public bool DisableEnvironmentOverride { get; set; }

    /// <summary>
    /// Keys that MUST resolve to a value (from a process environment variable OR the remote snapshot)
    /// for the host to start. When any is unmet after the initial config load,
    /// <see cref="Nexus.Sdk.Services.NexusConfigRefreshService"/> throws at startup (same fail-closed
    /// semantics as <c>ValidateOnStart</c>) rather than letting the app run without required config.
    /// Empty (default) keeps the historical fail-soft behavior. Populate via <see cref="RequireKeys"/>.
    /// </summary>
    public string[] RequiredKeys { get; set; } = [];

    /// <summary>Default feature enabled state when Nexus is unreachable.</summary>
    public bool DefaultFeatureEnabled { get; set; } = true;

    /// <summary>
    /// What <c>[NexusAuthorize]</c> does when the permission check comes back
    /// <see cref="Client.NexusPermissionDecision.Indeterminate"/> — Nexus unreachable, the API
    /// key expired, or the platform answered 5xx. Defaults to
    /// <see cref="NexusPermissionUnavailableBehavior.Deny"/>: fail-closed, but honestly.
    /// </summary>
    /// <remarks>
    /// This is the permission twin of <see cref="DefaultFeatureEnabled"/> — "what we decide when
    /// Nexus is unreachable" — declared and settable rather than left as a side effect. The
    /// default is closed, so a network cut cannot become a privilege escalation; but the
    /// response it produces is <b>503 with a Retry-After</b>, not a 403 claiming the caller
    /// lacks a permission. See the SDK README for when to choose
    /// <see cref="NexusPermissionUnavailableBehavior.Allow"/>.
    /// </remarks>
    public NexusPermissionUnavailableBehavior OnPermissionUnavailable { get; set; }
        = NexusPermissionUnavailableBehavior.Deny;


    /// <summary>HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Assemblies to scan for Nexus attributes. Empty = entry assembly only.</summary>
    public string[] AssembliesToScan { get; set; } = [];


    /// <summary>
    /// Declares keys that must resolve at startup (fail-closed). Merges with any existing
    /// <see cref="RequiredKeys"/>, de-duplicated (ordinal). Fluent — returns this instance.
    /// </summary>
    /// <example>
    /// builder.Services.AddNexus(builder.Configuration, o =&gt; o.RequireKeys("DB_URL", "STRIPE_KEY"));
    /// </example>
    public NexusOptions RequireKeys(params string[] keys)
    {
        if (keys is { Length: > 0 })
        {
            RequiredKeys = RequiredKeys
                .Concat(keys.Where(k => !string.IsNullOrWhiteSpace(k)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return this;
    }
}
