// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Nexus.Sdk.Management.Models;
using Nexus.Sdk.Models;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Management.Services.Http;

/// <summary>HTTP-backed implementation of <see cref="IGitHubCredentialService"/>.</summary>
public sealed class HttpGitHubCredentialService : IGitHubCredentialService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpGitHubCredentialService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpGitHubCredentialService"/> class.</summary>
    public HttpGitHubCredentialService(HttpClient httpClient, ILogger<HttpGitHubCredentialService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<GitHubCredentialMetadata>> GetAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(NexusManagementRoutes.MyGitHubCredential, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<GitHubCredentialMetadata>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitHubCredentialMetadata>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<GitHubCredentialMetadata>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<GitHubCredentialMetadata>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAsync (github-credential) failed");
            return NexusResult<GitHubCredentialMetadata>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    [Obsolete("User PAT writes are deprecated; connect the organization's git server via IGitConnectionService instead. The server rejects this by default (GitHubCredential.WriteDeprecated).")]
    public async Task<NexusResult<GitHubCredentialMetadata>> SaveAsync(
        SaveGitHubCredentialInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.PersonalAccessToken))
        {
            return NexusResult<GitHubCredentialMetadata>.Failure(
                "personalAccessToken is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            using var response = await _httpClient
                .PutAsJsonAsync(NexusManagementRoutes.MyGitHubCredential, input, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<GitHubCredentialMetadata>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GitHubCredentialMetadata>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<GitHubCredentialMetadata>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<GitHubCredentialMetadata>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "SaveAsync (github-credential) failed");
            return NexusResult<GitHubCredentialMetadata>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult> RemoveAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(NexusManagementRoutes.MyGitHubCredential, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var failure = await ManagementHttp.ToFailure<object>(response, ct).ConfigureAwait(false);
                return NexusResult.Failure(failure.Error ?? "Request failed.", failure.StatusCode);
            }

            return NexusResult.Success((int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RemoveAsync (github-credential) failed");
            return NexusResult.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }
}
