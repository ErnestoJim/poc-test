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

    [Fact]
    public async Task AcceptProposalUsesProjectRouteWithoutBodyAndReadsDecision()
    {
        var decidedAt = CreatedAt.AddMinutes(3);
        var accepted = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "accepted", decidedAt, null, CreatedAt);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(accepted)
        });

        var result = await CreateClient(handler).AcceptProposalAsync(ProjectId, ProposalId);

        Assert.Equal("accepted", result.Value?.Status);
        Assert.Equal(decidedAt, result.Value?.DecidedAtUtc);
        Assert.Equal($"POST /api/projects/{ProjectId}/proposals/{ProposalId}/accept", Assert.Single(handler.Requests).Route);
        Assert.Null(handler.Requests[0].Body);
    }

    [Fact]
    public async Task RejectProposalSendsUnchangedReasonAndReadsDecision()
    {
        const string reason = "  Falta valor  ";
        var decidedAt = CreatedAt.AddMinutes(4);
        var rejected = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "rejected", decidedAt, "Falta valor", CreatedAt);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rejected)
        });

        var result = await CreateClient(handler).RejectProposalAsync(ProjectId, ProposalId, reason);

        Assert.Equal("rejected", result.Value?.Status);
        Assert.Equal("Falta valor", result.Value?.RejectionReason);
        Assert.Equal(decidedAt, result.Value?.DecidedAtUtc);
        Assert.Equal($"POST /api/projects/{ProjectId}/proposals/{ProposalId}/reject", Assert.Single(handler.Requests).Route);
        Assert.Equal(reason, handler.Requests[0].Body!.Value.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    public async Task DecisionErrorsKeepTheirStatusAndValidationField(HttpStatusCode status, int expectedStatus, bool hasReason)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasReason ? new Dictionary<string, string[]> { ["reason"] = ["Invalid"] } : null
            })
        });

        var result = await CreateClient(handler).RejectProposalAsync(ProjectId, ProposalId, "");

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasReason, result.Error?.HasField("reason"));
    }

    [Fact]
    public async Task DecisionNetworkFailureReturnsUnknownStatus()
    {
        var result = await CreateClient(new FailingHandler()).AcceptProposalAsync(ProjectId, ProposalId);

        Assert.Null(result.Value);
        Assert.Null(result.Error?.StatusCode);
        Assert.NotEmpty(result.Error!.Message);
    }

    [Fact]
    public async Task SpecificationOperationsUseSingularRouteAndPreserveMarkdown()
    {
        const string markdown = "# Objetivo\r\n\r\n  Texto literal  ";
        var specification = new SpecificationDto(
            Guid.NewGuid(), ProposalId, markdown, "draft", null, null, null, CreatedAt, CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { title = "Specification not found" })
            },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(specification) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(specification) });
        var client = CreateClient(handler);

        var missing = await client.GetSpecificationAsync(ProjectId, ProposalId);
        var created = await client.CreateSpecificationAsync(ProjectId, ProposalId, markdown);
        var fetched = await client.GetSpecificationAsync(ProjectId, ProposalId);

        Assert.Equal(404, missing.Error?.StatusCode);
        Assert.Equal(markdown, created.Value?.Content);
        Assert.Equal("draft", created.Value?.Status);
        Assert.Equal(CreatedAt, fetched.Value?.UpdatedAtUtc);
        Assert.Equal(
            [$"GET /api/projects/{ProjectId}/proposals/{ProposalId}/specification",
             $"POST /api/projects/{ProjectId}/proposals/{ProposalId}/specification",
             $"GET /api/projects/{ProjectId}/proposals/{ProposalId}/specification"],
            handler.Requests.Select(r => r.Route));
        Assert.Equal(markdown, handler.Requests[1].Body!.Value.GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    public async Task SpecificationCreationErrorsKeepStatusAndContentField(HttpStatusCode status, int expectedStatus, bool hasContent)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasContent ? new Dictionary<string, string[]> { ["content"] = ["Invalid"] } : null
            })
        });

        var result = await CreateClient(handler).CreateSpecificationAsync(ProjectId, ProposalId, "");

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasContent, result.Error?.HasField("content"));
    }

    [Fact]
    public async Task SpecificationCreationNetworkFailureHasUnknownStatus()
    {
        var result = await CreateClient(new FailingHandler()).CreateSpecificationAsync(ProjectId, ProposalId, "# Documento");

        Assert.Null(result.Value);
        Assert.Null(result.Error?.StatusCode);
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
