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

namespace Nexus.Sdk.Tests.Configuration;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> for the config-manifest endpoint: returns queued
/// responses in order (repeating the last), captures every request, and records the
/// <c>If-None-Match</c> header sent — no live HTTP.
/// </summary>
internal sealed class FakeConfigHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string? Body)> _responses = new();
    private (HttpStatusCode Status, string? Body) _last = (HttpStatusCode.OK, null);

    public int CallCount { get; private set; }

    public List<string?> IfNoneMatchValues { get; } = new();

    public FakeConfigHandler Enqueue(HttpStatusCode status, string? body = null)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        IfNoneMatchValues.Add(request.Headers.TryGetValues("If-None-Match", out var values)
            ? string.Join(",", values)
            : null);

        var next = _responses.Count > 0 ? _responses.Dequeue() : _last;
        _last = next;

        var response = new HttpResponseMessage(next.Status);
        if (next.Body is not null)
        {
            response.Content = new StringContent(next.Body, Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}

/// <summary>An <see cref="IHttpClientFactory"/> that hands out a client bound to a single handler.</summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly Uri _baseAddress;

    public StubHttpClientFactory(HttpMessageHandler handler, string baseAddress = "http://nexus.test/")
    {
        _handler = handler;
        _baseAddress = new Uri(baseAddress);
    }

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = _baseAddress };
}
