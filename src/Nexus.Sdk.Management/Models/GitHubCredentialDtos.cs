// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Management.Models;

/// <summary>
/// Input for connecting a GitHub account.
/// </summary>
/// <param name="PersonalAccessToken">The GitHub PAT to validate + save (never returned).</param>
/// <param name="OwnerLogin">Optional expected GitHub login; rejected if it doesn't match the PAT's identity.</param>
public sealed record SaveGitHubCredentialInput(
    string PersonalAccessToken,
    string? OwnerLogin = null);

/// <summary>
/// Metadata describing the caller's GitHub connection. NEVER carries the PAT.
/// Decoupled from server-side types so the SDK package carries no server dependency.
/// </summary>
/// <param name="Connected">Whether a GitHub account is currently connected.</param>
/// <param name="OwnerLogin">The connected GitHub login, or <see langword="null"/> when not connected.</param>
/// <param name="Scopes">The OAuth scopes the saved PAT carries (empty when not connected).</param>
/// <param name="SavedAt">When the credential was saved, or <see langword="null"/> when not connected.</param>
/// <param name="LastValidatedAt">When the credential was last validated against GitHub, or <see langword="null"/>.</param>
public sealed record GitHubCredentialMetadata(
    bool Connected,
    string? OwnerLogin,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? SavedAt,
    DateTimeOffset? LastValidatedAt);
