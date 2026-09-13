// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Models;

namespace Nexus.Sdk.Management.Services;

/// <summary>
/// Manages the calling user's saved GitHub credential ("connect your GitHub account").
/// All operations are scoped to the authenticated caller (derived from the bearer
/// token / API key the SDK is configured with) — there is no user-id parameter.
/// </summary>
/// <remarks>
/// The PAT is never returned by any method; <see cref="GetAsync"/> returns metadata only.
/// </remarks>
public interface IGitHubCredentialService
{
    /// <summary>Gets the caller's GitHub connection status + metadata. Never returns the PAT.</summary>
    Task<NexusResult<GitHubCredentialMetadata>> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Connects the caller's GitHub account by saving a personal access token.
    /// The token is validated against GitHub and encrypted before storage.
    /// </summary>
    /// <remarks>
    /// DEPRECATED (P4/N6): the server rejects this by default with
    /// <c>GitHubCredential.WriteDeprecated</c> — user PATs are demoted to a read-only fallback in
    /// favour of the organization's git connection. Use <c>IGitConnectionService.GetInstallUrlAsync</c>
    /// to connect the org's git server instead. The method is retained for back-compat (and the
    /// emergency <c>GitHub:AllowUserPatWrites</c> override) but should not be used by new code.
    /// </remarks>
    [Obsolete("User PAT writes are deprecated; connect the organization's git server via IGitConnectionService instead. The server rejects this by default (GitHubCredential.WriteDeprecated).")]
    Task<NexusResult<GitHubCredentialMetadata>> SaveAsync(
        SaveGitHubCredentialInput input,
        CancellationToken ct = default);

    /// <summary>Disconnects the caller's GitHub account (removes the saved credential).</summary>
    Task<NexusResult> RemoveAsync(CancellationToken ct = default);
}
