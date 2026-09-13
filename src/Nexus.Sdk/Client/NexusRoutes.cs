// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace Nexus.Sdk.Client;

/// <summary>
/// Every route this SDK calls, written down once. this file is the SDK's half of
/// the contract that <c>api-surface-snapshot.json</c> is the server's half of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a registry and not a test that scans for route literals.</b> A scan validates the
/// literals it manages to recognise; it says nothing about a URL assembled from a
/// <c>const</c> plus an interpolated tail, which is how most of this SDK used to build its
/// paths — and how two of its routes hid from the audit that produced this ticket. A registry
/// plus a rule forbidding path literals anywhere else changes the question from "are the
/// strings we found served?" to "can an unserved string exist at all?".
/// </para>
/// <para>
/// <b>The rules.</b> Every constant here is a route <em>template</em>, spelled exactly as the
/// served surface spells it — including case (<c>api/v1/Environments</c> really is served with
/// a capital E; ASP.NET routing is case-insensitive, the snapshot is not). Parameters are
/// <c>{name}</c> placeholders. Concrete paths are built with <see cref="Of"/>, which escapes
/// each segment, so a template is never re-typed at a call site and cannot drift from the
/// constant the reconciliation test reads.
/// </para>
/// <para>
/// <b>Adding a route.</b> Add the constant, use it through <see cref="Of"/>, and run
/// <c>dotnet test --filter SdkContractTests</c>. If the route is not in the snapshot the test
/// fails, and that is the whole point: the SDK may only promise what the API serves. Query
/// strings are not part of a template — append them at the call site.
/// </para>
/// </remarks>
public static class NexusRoutes
{
    // ---------------------------------------------------------------------
    // App runtime — the lean surface AddNexus always registers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Reachability probe used by the health check and <c>IsHealthyAsync</c>. A platform release
    /// moved <c>TenantApiController</c> under <c>api/v1/tenant</c>; the old path still
    /// answers through an <c>alias</c> tombstone, but the registry states what is served,
    /// not what is tolerated — <c>SdkContractTests</c> reads it against the snapshot.
    /// </summary>
    public const string Health = "api/v1/tenant/health";

    /// <summary>The config &amp; secrets v2 snapshot, polled with a conditional If-None-Match.</summary>
    public const string SdkConfig = "api/v2/sdk/config";

    /// <summary>Full configuration of one feature flag.</summary>
    public const string SdkFeature = "api/v1/sdk/features/{key}";

    /// <summary>Per-subject feature evaluation (tier / end-user / attributes / env / version).</summary>
    public const string FeatureFlagCheck = "api/v1/feature-flags/{key}/check";

    /// <summary>Usage event.</summary>
    public const string SdkTrack = "api/v1/sdk/track";

    /// <summary>Billable usage event.</summary>
    public const string SdkBill = "api/v1/sdk/bill";

    /// <summary>Permission check backing <c>[NexusAuthorize]</c>.</summary>
    public const string SdkPermissionsCheck = "api/v1/sdk/permissions/check";

    /// <summary>Startup registration of discovered features and permissions.</summary>
    public const string SdkRegister = "api/v1/sdk/register";

    // ---------------------------------------------------------------------

    /// <summary>
    /// Fills a template's <c>{name}</c> placeholders, left to right, escaping each value as a
    /// URI data segment. A template with no placeholder is returned as-is.
    /// </summary>
    /// <param name="template">One of the constants on this class.</param>
    /// <param name="segments">One value per placeholder, in order of appearance.</param>
    /// <exception cref="ArgumentException">
    /// The number of values does not match the number of placeholders. This is a programming
    /// error, not a request failure, so it throws rather than returning a failed
    /// <c>NexusResult</c> — it cannot depend on anything the platform does.
    /// </exception>
    public static string Of(string template, params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(template);
        segments ??= [];

        var built = new StringBuilder(template.Length + 32);
        var consumed = 0;
        var i = 0;

        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
            {
                built.Append(template, i, template.Length - i);
                break;
            }

            var close = template.IndexOf('}', open);
            if (close < 0)
            {
                built.Append(template, i, template.Length - i);
                break;
            }

            built.Append(template, i, open - i);

            if (consumed >= segments.Length)
            {
                throw new ArgumentException(
                    $"Route template '{template}' has more placeholders than the {segments.Length} "
                    + "value(s) supplied.",
                    nameof(segments));
            }

            built.Append(Uri.EscapeDataString(segments[consumed] ?? string.Empty));
            consumed++;
            i = close + 1;
        }

        if (consumed != segments.Length)
        {
            throw new ArgumentException(
                $"Route template '{template}' takes {consumed} value(s) but {segments.Length} "
                + "were supplied.",
                nameof(segments));
        }

        return built.ToString();
    }
}
