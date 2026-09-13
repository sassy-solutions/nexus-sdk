// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;
using Nexus.Sdk.Configuration;

namespace Nexus.Sdk.Services;

/// <summary>
/// Default <see cref="INexusSubjectContext"/> that carries the app's deployed
/// <b>environment</b> and <b>version</b> (from <see cref="NexusOptions"/>, populated at deploy time
/// via <c>Nexus__Environment</c> / <c>Nexus__Version</c>). This makes <c>[NexusFeature]</c> gates
/// environment/version-aware out of the box: with a non-null subject the filter evaluates via the
/// gate-aware <c>/check</c> path instead of the flag's global default.
/// </summary>
/// <remarks>
/// Returns <see langword="null"/> when neither environment nor version is configured, preserving
/// the prior "gate falls back to the flag default" behaviour for local/non-deployed runs. An app
/// that needs per-user tier/attribute targeting still registers its own context, which wins.
/// </remarks>
public sealed class OptionsNexusSubjectContext : INexusSubjectContext
{
    private readonly IOptions<NexusOptions> _options;

    public OptionsNexusSubjectContext(IOptions<NexusOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public NexusSubject? GetCurrentSubject()
    {
        var opts = _options.Value;
        var env = string.IsNullOrWhiteSpace(opts.Environment) ? null : opts.Environment;
        var version = string.IsNullOrWhiteSpace(opts.Version) ? null : opts.Version;

        if (env is null && version is null)
        {
            return null;
        }

        return new NexusSubject(Environment: env, Version: version);
    }
}
