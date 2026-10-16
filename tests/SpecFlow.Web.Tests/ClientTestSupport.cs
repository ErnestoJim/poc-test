using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;

namespace SpecFlow.Web.Tests;

// Shared data and fakes for the HTTP client tests.
internal static class ClientTestSupport
{
    public static readonly Guid ProjectId = Guid.Parse("3c72246d-20b5-4c81-ae9b-b27460a64070");
    public static readonly Guid ProposalId = Guid.Parse("9a5e5c85-84a3-4a64-99f9-6031825e6e5a");
    public static readonly string[] InvalidMessages = ["Invalid"];
    public static readonly DateTimeOffset CreatedAt = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static SpecFlowApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5194") });

    public static HttpResponseMessage WithEntityTag(HttpResponseMessage response, string entityTag)
    {
        response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    // A Problem Details response, with validation errors under the given fields when there are any.
    public static HttpResponseMessage Problem(HttpStatusCode status, params string[] fields) => new(status)
    {
        Content = JsonContent.Create(new
        {
            title = "Error",
            errors = fields.Length > 0 ? fields.ToDictionary(field => field, _ => InvalidMessages) : null
        })
    };
}

internal sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responseQueue = new(responses);

    public List<(string Route, JsonElement? Body, string? IfMatch)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        JsonElement? body = null;
        if (request.Content is not null)
        {
            using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            body = document.RootElement.Clone();
        }
        var ifMatch = request.Headers.TryGetValues("If-Match", out var values) ? string.Join(",", values) : null;
        Requests.Add(($"{request.Method} {request.RequestUri!.PathAndQuery}", body, ifMatch));
        return responseQueue.Dequeue();
    }
}

internal sealed class FailingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Connection refused");
}
