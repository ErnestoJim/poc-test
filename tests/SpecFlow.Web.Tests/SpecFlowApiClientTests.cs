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

    [Fact]
    public async Task SpecificationReadAndCreationExposeEntityTag()
    {
        var specification = new SpecificationDto(
            Guid.NewGuid(), ProposalId, "# Documento", "draft", null, null, null, CreatedAt, CreatedAt);
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(specification) }, "\"v1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(specification) }, "\"v2\""));
        var client = CreateClient(handler);

        var created = await client.CreateSpecificationAsync(ProjectId, ProposalId, "# Documento");
        var fetched = await client.GetSpecificationAsync(ProjectId, ProposalId);

        Assert.Equal("\"v1\"", created.EntityTag);
        Assert.Equal("\"v2\"", fetched.EntityTag);
        Assert.All(handler.Requests, request => Assert.Null(request.IfMatch));
    }

    [Fact]
    public async Task UpdateSpecificationSendsExactContentAndIfMatchAndReadsNewEntityTag()
    {
        const string markdown = "# Objetivo\r\n\r\n  Texto editado  ";
        const string entityTag = "\"c3BlYy0xOjQ=\"";
        var updated = new SpecificationDto(
            Guid.NewGuid(), ProposalId, markdown, "draft", null, null, null, CreatedAt, CreatedAt.AddMinutes(5));
        var handler = new RecordingHandler(WithEntityTag(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(updated) },
            "\"c3BlYy0xOjU=\""));

        var result = await CreateClient(handler).UpdateSpecificationAsync(ProjectId, ProposalId, markdown, entityTag);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"PUT /api/projects/{ProjectId}/proposals/{ProposalId}/specification", request.Route);
        Assert.Equal(entityTag, request.IfMatch);
        Assert.Equal(markdown, request.Body!.Value.GetProperty("content").GetString());
        Assert.Equal(markdown, result.Value?.Content);
        Assert.Equal(CreatedAt.AddMinutes(5), result.Value?.UpdatedAtUtc);
        Assert.Equal("\"c3BlYy0xOjU=\"", result.EntityTag);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task SpecificationUpdateErrorsKeepStatusAndContentField(HttpStatusCode status, int expectedStatus, bool hasContent)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasContent ? new Dictionary<string, string[]> { ["content"] = ["Invalid"] } : null
            })
        });

        var result = await CreateClient(handler).UpdateSpecificationAsync(ProjectId, ProposalId, "", "\"v1\"");

        Assert.Null(result.Value);
        Assert.Null(result.EntityTag);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasContent, result.Error?.HasField("content"));
        Assert.NotEmpty(result.Error!.Message);
    }

    [Fact]
    public async Task SpecificationUpdateNetworkFailureHasUnknownStatus()
    {
        var result = await CreateClient(new FailingHandler()).UpdateSpecificationAsync(ProjectId, ProposalId, "# Documento", "\"v1\"");

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

    [Fact]
    public async Task AcceptanceCriterionOperationsUseSpecificationRoutesAndEntityTags()
    {
        const string text = "  Dado un criterio\r\nentonces se conserva  ";
        var first = new AcceptanceCriterionDto(Guid.NewGuid(), Guid.NewGuid(), text, 1, CreatedAt, CreatedAt);
        var second = first with { Id = Guid.NewGuid(), Content = "Otro", Position = 2 };
        var basePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/acceptance-criteria";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { first, second }) }, "\"list-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-2\""),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.NoContent), "\"list-2\""));
        var client = CreateClient(handler);

        var listed = await client.ListAcceptanceCriteriaAsync(ProjectId, ProposalId);
        var created = await client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, text);
        var fetched = await client.GetAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id);
        var updated = await client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id, text, "\"item-1\"");
        var deleted = await client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id, "\"item-2\"");
        var reordered = await client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [second.Id, first.Id], "\"list-1\"");

        Assert.Equal([first.Id, second.Id], listed.Value!.Select(criterion => criterion.Id));
        Assert.Equal("\"list-1\"", listed.EntityTag);
        Assert.Equal(text, created.Value?.Content);
        Assert.Equal("\"item-1\"", fetched.EntityTag);
        Assert.Equal("\"item-2\"", updated.EntityTag);
        Assert.True(deleted.IsSuccess);
        Assert.True(deleted.Value);
        Assert.True(reordered.Value);
        Assert.Equal("\"list-2\"", reordered.EntityTag);
        Assert.Equal(
            [
                $"GET {basePath}",
                $"POST {basePath}",
                $"GET {basePath}/{first.Id}",
                $"PUT {basePath}/{first.Id}",
                $"DELETE {basePath}/{first.Id}",
                $"PUT {basePath}/order"
            ],
            handler.Requests.Select(r => r.Route));
        Assert.Equal([null, null, null, "\"item-1\"", "\"item-2\"", "\"list-1\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Equal(text, handler.Requests[1].Body!.Value.GetProperty("content").GetString());
        Assert.Equal(text, handler.Requests[3].Body!.Value.GetProperty("content").GetString());
        Assert.Null(handler.Requests[4].Body);
        Assert.Equal(
            [second.Id.ToString(), first.Id.ToString()],
            handler.Requests[5].Body!.Value.GetProperty("criterionIds").EnumerateArray().Select(id => id.GetString()));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task AcceptanceCriterionModificationErrorsKeepStatusAndField(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error(string field) => new(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasField ? new Dictionary<string, string[]> { [field] = ["Invalid"] } : null
            })
        };
        var handler = new RecordingHandler(Error("content"), Error("content"), Error("content"), Error("criterionIds"));
        var client = CreateClient(handler);
        var criterionId = Guid.NewGuid();

        var created = await client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, "");
        var updated = await client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "", "\"v1\"");
        var deleted = await client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "\"v1\"");
        var reordered = await client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [criterionId], "\"v1\"");

        Assert.All([created.Error, updated.Error, deleted.Error, reordered.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, created.Error!.HasField("content"));
        Assert.Equal(hasField, updated.Error!.HasField("content"));
        Assert.Equal(hasField, reordered.Error!.HasField("criterionIds"));
        Assert.False(deleted.Value);
        Assert.Null(reordered.EntityTag);
    }

    [Fact]
    public async Task AcceptanceCriterionNetworkFailuresHaveUnknownStatus()
    {
        var client = CreateClient(new FailingHandler());
        var criterionId = Guid.NewGuid();

        var listed = await client.ListAcceptanceCriteriaAsync(ProjectId, ProposalId);
        var created = await client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, "Criterio");
        var updated = await client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "Criterio", "\"v1\"");
        var deleted = await client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "\"v1\"");
        var reordered = await client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [criterionId], "\"v1\"");

        Assert.All([listed.Error, created.Error, updated.Error, deleted.Error, reordered.Error], error =>
        {
            Assert.NotNull(error);
            Assert.Null(error.StatusCode);
        });
    }

    private static HttpResponseMessage WithEntityTag(HttpResponseMessage response, string entityTag)
    {
        response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    private static SpecFlowApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5194") });

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
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

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
