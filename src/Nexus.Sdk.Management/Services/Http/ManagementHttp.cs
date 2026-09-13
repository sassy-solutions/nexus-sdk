// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Sdk.Models;

namespace Nexus.Sdk.Management.Services.Http;

/// <summary>
/// Shared helpers for the management HTTP services: a single web-default JSON
/// configuration and uniform <see cref="NexusResult"/> failure mapping (problem-details
/// aware) so every typed client behaves identically and never throws on API failure.
/// </summary>
internal static class ManagementHttp
{
    /// <summary>Web-default JSON options used by every management HTTP client.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Builds a typed failure from a non-success response, preferring problem-details text.</summary>
    public static async Task<NexusResult<T>> ToFailure<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var detail = await TryReadProblemDetail(response, ct).ConfigureAwait(false);
        return NexusResult<T>.Failure(detail ?? response.ReasonPhrase ?? "Request failed.", (int)response.StatusCode);
    }

    /// <summary>Builds an untyped failure from a non-success response, preferring problem-details text.</summary>
    public static async Task<NexusResult> ToFailure(HttpResponseMessage response, CancellationToken ct)
    {
        var detail = await TryReadProblemDetail(response, ct).ConfigureAwait(false);
        return NexusResult.Failure(detail ?? response.ReasonPhrase ?? "Request failed.", (int)response.StatusCode);
    }

    private static async Task<string?> TryReadProblemDetail(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetailsLite>(JsonOptions, ct)
                .ConfigureAwait(false);

            return problem is null ? null : problem.Detail ?? problem.Title;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ProblemDetailsLite(string? Title, string? Detail);
}
