using System.Net;
using System.Net.Http.Json;
using SpecFlow.Web.Services;

namespace SpecFlow.Web.Tests.Components;

// A fake API for component tests: each "METHOD path" answers with scripted responses in order,
// repeating the last one. Unexpected requests fail the test.
internal sealed class ScriptedApi : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> routes = [];

    public List<string> Requests { get; } = [];

    public SpecFlowApiClient Client => new(new HttpClient(this) { BaseAddress = new Uri("http://localhost:5194") });

    public ScriptedApi On(HttpMethod method, string path, params Func<HttpResponseMessage>[] responses)
    {
        routes[$"{method} {path}"] = new Queue<Func<HttpResponseMessage>>(responses);
        return this;
    }

    public static Func<HttpResponseMessage> Json<T>(T value, string? entityTag = null) => () =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        if (entityTag is not null)
        {
            response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(entityTag);
        }
        return response;
    };

    public static Func<HttpResponseMessage> Status(HttpStatusCode status) => () =>
        new HttpResponseMessage(status) { Content = JsonContent.Create(new { title = "Error" }) };

    public static Func<HttpResponseMessage> Offline { get; } = () => throw new HttpRequestException("Connection refused");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Requests.Add(key);
        if (!routes.TryGetValue(key, out var queue))
        {
            throw new InvalidOperationException($"Unexpected request {key}");
        }

        var next = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return Task.FromResult(next());
    }
}
