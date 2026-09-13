// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Models;

/// <summary>
/// Lightweight result wrapper for Nexus SDK API calls.
/// Uses Result pattern — never throws for expected failures.
/// </summary>
public sealed record NexusResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public static NexusResult Success(int statusCode = 200) =>
        new() { IsSuccess = true, StatusCode = statusCode };

    public static NexusResult Failure(string error, int statusCode) =>
        new() { IsSuccess = false, Error = error, StatusCode = statusCode };
}

/// <summary>
/// Typed result wrapper for Nexus SDK API calls.
/// </summary>
public sealed record NexusResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public static NexusResult<T> Success(T value, int statusCode = 200) =>
        new() { IsSuccess = true, Value = value, StatusCode = statusCode };

    public static NexusResult<T> Failure(string error, int statusCode) =>
        new() { IsSuccess = false, Error = error, StatusCode = statusCode };
}
