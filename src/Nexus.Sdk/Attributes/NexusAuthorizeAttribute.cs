// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

namespace Nexus.Sdk.Attributes;

/// <summary>
/// Enforces Nexus-based authorization on an endpoint.
/// Validates that the caller has the required permission or role via Nexus.
/// </summary>
/// <example>
/// [NexusAuthorize(Permission = "orders:write")]
/// [HttpPost]
/// public IActionResult CreateOrder() { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class NexusAuthorizeAttribute : Attribute
{
    /// <summary>Required permission code (e.g., "orders:write", "reports:read").</summary>
    public string? Permission { get; set; }

    /// <summary>Required role (e.g., "admin", "manager").</summary>
    public string? Role { get; set; }

    public NexusAuthorizeAttribute() { }

    public NexusAuthorizeAttribute(string permission) => Permission = permission;
}
