// Fake REST API for integration tests: an HttpMessageHandler that
// serves canned JSON per (method, path) route and records every request
// (URL, body, auth header) so tests can assert on the exact wire traffic.

using System.Net;
using System.Text;

namespace CardsExtension.Tests;

internal sealed record StubbedRequest(string Method, string Url, string? Body, string? AuthHeader);

internal sealed class StubCardsHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<(HttpStatusCode Status, string? Json)>> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every request the service sent, in order.</summary>
    public List<StubbedRequest> Requests { get; } = [];

    /// <summary>
    /// Registers a route ("GET", "/1/members/me/boards") with one or more
    /// sequential responses. Responses are consumed in order; a route with no
    /// responses left (or never registered) returns 404 with a diagnostic body.
    /// Matching is on method + path only (query strings are recorded, not matched).
    /// </summary>
    public void Stub(string method, string path, params (int Status, string? Json)[] responses)
    {
        lock (Requests)
        {
            _routes[$"{method.ToUpperInvariant()} {path}"] = new(responses.Select(r => ((HttpStatusCode)r.Status, r.Json)));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var key = $"{request.Method.Method} {request.RequestUri!.AbsolutePath}";
        (HttpStatusCode Status, string? Json)? stubbed = null;

        // Requests arrive from several background tasks; Requests doubles as the lock.
        lock (Requests)
        {
            Requests.Add(new StubbedRequest(
                request.Method.Method,
                request.RequestUri.ToString(),
                body,
                request.Headers.Authorization?.ToString()));

            if (_routes.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                stubbed = queue.Dequeue();
            }
        }

        if (stubbed is var (status, json))
        {
            return new HttpResponseMessage(status)
            {
                Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No stub registered for {key}", Encoding.UTF8, "text/plain"),
        };
    }
}
