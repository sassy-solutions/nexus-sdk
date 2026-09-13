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
using System.Text;

namespace Nexus.Sdk.Tests.Management;

/// <summary>
/// A capturing <see cref="HttpMessageHandler"/> stub for the management SDK tests:
/// records the last request and returns a canned status + body. Mirrors the
/// <c>StubHandler</c> pattern used across the existing Sdk.Tests suite.
/// </summary>
internal sealed class ManagementTestHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly string _mediaType;

    private ManagementTestHandler(HttpStatusCode status, string body, string mediaType)
    {
        _status = status;
        _body = body;
        _mediaType = mediaType;
    }

    public int CallCount { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public static ManagementTestHandler Returning(HttpStatusCode status, string body = "", string mediaType = "application/json") =>
        new(status, body, mediaType);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body ?? string.Empty, Encoding.UTF8, _mediaType),
        };
    }
}
