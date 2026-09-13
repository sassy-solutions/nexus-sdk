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

/// <summary>HTTP-backed implementation of <see cref="IEnvironmentService"/>.</summary>
public sealed class HttpEnvironmentService : IEnvironmentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpEnvironmentService> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpEnvironmentService"/> class.</summary>
    public HttpEnvironmentService(HttpClient httpClient, ILogger<HttpEnvironmentService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NexusResult<IReadOnlyList<EnvironmentDto>>> ListAsync(
        string organizationId,
        bool provisionedOnly = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<IReadOnlyList<EnvironmentDto>>.Failure("organizationId is required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusManagementRoutes.Environments + $"?organizationId={Uri.EscapeDataString(organizationId)}&provisionedOnly={provisionedOnly.ToString().ToLowerInvariant()}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<IReadOnlyList<EnvironmentDto>>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<EnvironmentListResponse>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            IReadOnlyList<EnvironmentDto> environments = payload?.Environments ?? Array.Empty<EnvironmentDto>();
            return NexusResult<IReadOnlyList<EnvironmentDto>>.Success(environments, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ListAsync({OrganizationId}) failed", organizationId);
            return NexusResult<IReadOnlyList<EnvironmentDto>>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<EnvironmentDetailDto>> GetAsync(
        string environmentId,
        string organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(environmentId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return NexusResult<EnvironmentDetailDto>.Failure("environmentId and organizationId are required.", (int)HttpStatusCode.BadRequest);
        }

        try
        {
            var url = NexusRoutes.Of(NexusManagementRoutes.Environment, environmentId) + $"?organizationId={Uri.EscapeDataString(organizationId)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<EnvironmentDetailDto>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<EnvironmentDetailDto>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<EnvironmentDetailDto>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<EnvironmentDetailDto>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetAsync({EnvironmentId}) failed", environmentId);
            return NexusResult<EnvironmentDetailDto>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<NexusResult<CreateEnvironmentResult>> CreateAsync(CreateEnvironmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(NexusManagementRoutes.Environments, request, ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return await ManagementHttp.ToFailure<CreateEnvironmentResult>(response, ct).ConfigureAwait(false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<CreateEnvironmentResult>(ManagementHttp.JsonOptions, ct)
                .ConfigureAwait(false);

            return payload is null
                ? NexusResult<CreateEnvironmentResult>.Failure("Empty response body.", (int)response.StatusCode)
                : NexusResult<CreateEnvironmentResult>.Success(payload, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "CreateAsync failed");
            return NexusResult<CreateEnvironmentResult>.Failure(ex.Message, (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed record EnvironmentListResponse(EnvironmentDto[]? Environments);
}
