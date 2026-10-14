using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;

namespace SpecFlow.Web.Tests;

public sealed class SpecFlowApiClientTests
{
    private static readonly Guid ProjectId = Guid.Parse("3c72246d-20b5-4c81-ae9b-b27460a64070");
    private static readonly Guid ProposalId = Guid.Parse("9a5e5c85-84a3-4a64-99f9-6031825e6e5a");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectOperationsUsePublicRoutesAndDeserializeResponses()
    {
        var project = new ProjectDto(ProjectId, "Equipo", "Trabajo", CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { project }) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(project) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(project) });
        var client = CreateClient(handler);

        var listed = await client.ListProjectsAsync();
        var fetched = await client.GetProjectAsync(ProjectId);
        var created = await client.CreateProjectAsync("Equipo", "Trabajo");

        Assert.Equal(ProjectId, Assert.Single(listed.Value!).Id);
        Assert.Equal("Equipo", fetched.Value?.Name);
        Assert.Equal(ProjectId, created.Value?.Id);
        Assert.Equal(["GET /api/projects", $"GET /api/projects/{ProjectId}", "POST /api/projects"], handler.Requests.Select(r => r.Route));
        Assert.Equal("Equipo", handler.Requests[2].Body!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ProposalOperationsStayInProjectAndCreationOmitsSource()
    {
        var proposal = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "pending", null, null, CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { proposal }) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(proposal) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(proposal) });
        var client = CreateClient(handler);

        var listed = await client.ListProposalsAsync(ProjectId);
        var fetched = await client.GetProposalAsync(ProjectId, ProposalId);
        var created = await client.CreateProposalAsync(ProjectId, "Idea", null);

        Assert.Equal(ProposalId, Assert.Single(listed.Value!).Id);
        Assert.Equal("pending", fetched.Value?.Status);
        Assert.Equal(ProposalId, created.Value?.Id);
        Assert.Equal(
            [$"GET /api/projects/{ProjectId}/proposals", $"GET /api/projects/{ProjectId}/proposals/{ProposalId}", $"POST /api/projects/{ProjectId}/proposals"],
            handler.Requests.Select(r => r.Route));
        Assert.False(handler.Requests[2].Body!.Value.TryGetProperty("sourceProposalId", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "name", 400)]
    [InlineData(HttpStatusCode.Conflict, "name", 409)]
    public async Task ProblemDetailsFieldErrorsRemainAssociatedWithTheirField(HttpStatusCode status, string field, int expectedStatus)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new { title = "Error", errors = new Dictionary<string, string[]> { [field] = ["Invalid"] } })
        });
        var result = await CreateClient(handler).CreateProjectAsync("", null);

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.True(result.Error?.HasField(field));
    }

    [Fact]
    public async Task MissingResourceAndNetworkFailureReturnPresentableErrors()
    {
        var missingHandler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent.Create(new { title = "Project not found" })
        });
        var missing = await CreateClient(missingHandler).GetProjectAsync(ProjectId);
        var offline = await CreateClient(new FailingHandler()).ListProjectsAsync();

        Assert.Equal(404, missing.Error?.StatusCode);
        Assert.Null(offline.Error?.StatusCode);
        Assert.NotEmpty(offline.Error!.Message);
    }

    private static SpecFlowApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5194") });

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responseQueue = new(responses);

        public List<(string Route, JsonElement? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            JsonElement? body = null;
            if (request.Content is not null)
            {
                using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
                body = document.RootElement.Clone();
            }
            Requests.Add(($"{request.Method} {request.RequestUri!.PathAndQuery}", body));
            return responseQueue.Dequeue();
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
