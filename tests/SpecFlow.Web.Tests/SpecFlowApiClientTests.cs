using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;

namespace SpecFlow.Web.Tests;

public sealed class SpecFlowApiClientTests
{
    private static readonly Guid ProjectId = Guid.Parse("3c72246d-20b5-4c81-ae9b-b27460a64070");
    private static readonly Guid ProposalId = Guid.Parse("9a5e5c85-84a3-4a64-99f9-6031825e6e5a");
    private static readonly string[] InvalidMessages = ["Invalid"];
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

    [Fact]
    public async Task ImplementationTaskOperationsUseSpecificationRoutesAndEntityTags()
    {
        const string description = "  Paso uno\r\n\r\nPaso dos  ";
        var first = new ImplementationTaskDto(
            Guid.NewGuid(), Guid.NewGuid(), "Modelo", description, "pending", null, null, 1, CreatedAt, CreatedAt);
        var second = first with { Id = Guid.NewGuid(), Title = "API", Description = null, Position = 2 };
        var basePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/tasks";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { first, second }) }, "\"list-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(second) }, "\"item-9\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-2\""),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.NoContent), "\"list-2\""));
        var client = CreateClient(handler);

        var listed = await client.ListImplementationTasksAsync(ProjectId, ProposalId);
        var created = await client.CreateImplementationTaskAsync(ProjectId, ProposalId, "  Modelo ", description);
        await client.CreateImplementationTaskAsync(ProjectId, ProposalId, "API", null);
        var fetched = await client.GetImplementationTaskAsync(ProjectId, ProposalId, first.Id);
        var updated = await client.UpdateImplementationTaskAsync(ProjectId, ProposalId, first.Id, "Modelo", description, "\"item-1\"");
        var deleted = await client.DeleteImplementationTaskAsync(ProjectId, ProposalId, first.Id, "\"item-2\"");
        var reordered = await client.ReorderImplementationTasksAsync(ProjectId, ProposalId, [second.Id, first.Id], "\"list-1\"");

        Assert.Equal([first.Id, second.Id], listed.Value!.Select(task => task.Id));
        Assert.Equal("\"list-1\"", listed.EntityTag);
        Assert.Equal(description, created.Value?.Description);
        Assert.Equal("\"item-1\"", fetched.EntityTag);
        Assert.Equal("\"item-2\"", updated.EntityTag);
        Assert.True(deleted.Value);
        Assert.True(reordered.Value);
        Assert.Equal("\"list-2\"", reordered.EntityTag);
        Assert.Equal(
            [
                $"GET {basePath}",
                $"POST {basePath}",
                $"POST {basePath}",
                $"GET {basePath}/{first.Id}",
                $"PUT {basePath}/{first.Id}",
                $"DELETE {basePath}/{first.Id}",
                $"PUT {basePath}/order"
            ],
            handler.Requests.Select(r => r.Route));
        Assert.Equal([null, null, null, null, "\"item-1\"", "\"item-2\"", "\"list-1\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Equal("  Modelo ", handler.Requests[1].Body!.Value.GetProperty("title").GetString());
        Assert.Equal(description, handler.Requests[1].Body!.Value.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, handler.Requests[2].Body!.Value.GetProperty("description").ValueKind);
        Assert.Equal(description, handler.Requests[4].Body!.Value.GetProperty("description").GetString());
        Assert.Null(handler.Requests[5].Body);
        Assert.Equal(
            [second.Id.ToString(), first.Id.ToString()],
            handler.Requests[6].Body!.Value.GetProperty("taskIds").EnumerateArray().Select(id => id.GetString()));
    }

    [Fact]
    public async Task ImplementationTaskTransitionsSendIfMatchWithoutBodyAndReadNewState()
    {
        var started = new ImplementationTaskDto(
            Guid.NewGuid(), Guid.NewGuid(), "Modelo", null, "in_progress", CreatedAt, null, 1, CreatedAt, CreatedAt);
        var completed = started with { Status = "completed", CompletedAtUtc = CreatedAt.AddHours(1) };
        var basePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/tasks/{started.Id}";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(started) }, "\"t-2\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(completed) }, "\"t-3\""));
        var client = CreateClient(handler);

        var start = await client.StartImplementationTaskAsync(ProjectId, ProposalId, started.Id, "\"t-1\"");
        var complete = await client.CompleteImplementationTaskAsync(ProjectId, ProposalId, started.Id, "\"t-2\"");

        Assert.Equal("in_progress", start.Value?.Status);
        Assert.Equal("\"t-2\"", start.EntityTag);
        Assert.Equal("completed", complete.Value?.Status);
        Assert.Equal(CreatedAt.AddHours(1), complete.Value?.CompletedAtUtc);
        Assert.Equal("\"t-3\"", complete.EntityTag);
        Assert.Equal([$"POST {basePath}/start", $"POST {basePath}/complete"], handler.Requests.Select(r => r.Route));
        Assert.Equal(["\"t-1\"", "\"t-2\""], handler.Requests.Select(r => r.IfMatch));
        Assert.All(handler.Requests, request => Assert.Null(request.Body));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task ImplementationTaskErrorsKeepStatusAndFields(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error(params string[] fields) => new(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasField ? fields.ToDictionary(field => field, _ => InvalidMessages) : null
            })
        };
        var handler = new RecordingHandler(
            Error("title", "description"), Error("title"), Error(), Error("taskIds"), Error(), Error());
        var client = CreateClient(handler);
        var taskId = Guid.NewGuid();

        var created = await client.CreateImplementationTaskAsync(ProjectId, ProposalId, "", " ");
        var updated = await client.UpdateImplementationTaskAsync(ProjectId, ProposalId, taskId, "", null, "\"v1\"");
        var deleted = await client.DeleteImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"");
        var reordered = await client.ReorderImplementationTasksAsync(ProjectId, ProposalId, [taskId], "\"v1\"");
        var started = await client.StartImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"");
        var completed = await client.CompleteImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"");

        Assert.All([created.Error, updated.Error, deleted.Error, reordered.Error, started.Error, completed.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, created.Error!.HasField("title"));
        Assert.Equal(hasField, created.Error.HasField("description"));
        Assert.Equal(hasField, updated.Error!.HasField("title"));
        Assert.Equal(hasField, reordered.Error!.HasField("taskIds"));
        Assert.Null(started.Value);
        Assert.Null(completed.EntityTag);
    }

    [Fact]
    public async Task ImplementationTaskNetworkFailuresHaveUnknownStatus()
    {
        var client = CreateClient(new FailingHandler());
        var taskId = Guid.NewGuid();

        ApiError?[] errors =
        [
            (await client.ListImplementationTasksAsync(ProjectId, ProposalId)).Error,
            (await client.CreateImplementationTaskAsync(ProjectId, ProposalId, "Tarea", null)).Error,
            (await client.UpdateImplementationTaskAsync(ProjectId, ProposalId, taskId, "Tarea", null, "\"v1\"")).Error,
            (await client.DeleteImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"")).Error,
            (await client.ReorderImplementationTasksAsync(ProjectId, ProposalId, [taskId], "\"v1\"")).Error,
            (await client.StartImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"")).Error,
            (await client.CompleteImplementationTaskAsync(ProjectId, ProposalId, taskId, "\"v1\"")).Error
        ];

        Assert.All(errors, error =>
        {
            Assert.NotNull(error);
            Assert.Null(error.StatusCode);
        });
    }

    [Fact]
    public async Task TaskAcceptanceCriteriaAreReadAndReplacedAsAFullSetWithTheLinksEntityTag()
    {
        var taskId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var path = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/tasks/{taskId}/acceptance-criteria";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { acceptanceCriterionIds = new[] { first } })
            }, "\"links-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.NoContent), "\"links-2\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.NoContent), "\"links-3\""));
        var client = CreateClient(handler);

        var read = await client.GetTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId);
        var replaced = await client.ReplaceTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId, [first, second], "\"links-1\"");
        var cleared = await client.ReplaceTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId, [], "\"links-2\"");

        Assert.Equal([first], read.Value!.AcceptanceCriterionIds);
        Assert.Equal("\"links-1\"", read.EntityTag);
        Assert.True(replaced.Value);
        Assert.Equal("\"links-2\"", replaced.EntityTag);
        Assert.True(cleared.Value);
        Assert.Equal("\"links-3\"", cleared.EntityTag);
        Assert.Equal([$"GET {path}", $"PUT {path}", $"PUT {path}"], handler.Requests.Select(r => r.Route));
        Assert.Equal([null, "\"links-1\"", "\"links-2\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Null(handler.Requests[0].Body);
        Assert.Equal(
            [first.ToString(), second.ToString()],
            handler.Requests[1].Body!.Value.GetProperty("acceptanceCriterionIds").EnumerateArray().Select(id => id.GetString()));
        Assert.Empty(handler.Requests[2].Body!.Value.GetProperty("acceptanceCriterionIds").EnumerateArray());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task TaskAcceptanceCriteriaErrorsKeepStatusAndField(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error() => new(status)
        {
            Content = JsonContent.Create(new
            {
                title = "Error",
                errors = hasField ? new Dictionary<string, string[]> { ["acceptanceCriterionIds"] = InvalidMessages } : null
            })
        };
        var handler = new RecordingHandler(Error(), Error());
        var client = CreateClient(handler);
        var taskId = Guid.NewGuid();

        var read = await client.GetTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId);
        var replaced = await client.ReplaceTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId, [Guid.NewGuid()], "\"v1\"");

        Assert.All([read.Error, replaced.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, replaced.Error!.HasField("acceptanceCriterionIds"));
        Assert.Null(read.Value);
        Assert.Null(replaced.EntityTag);
    }

    [Fact]
    public async Task TaskAcceptanceCriteriaNetworkFailuresHaveUnknownStatus()
    {
        var client = CreateClient(new FailingHandler());
        var taskId = Guid.NewGuid();

        ApiError?[] errors =
        [
            (await client.GetTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId)).Error,
            (await client.ReplaceTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, taskId, [], "\"v1\"")).Error
        ];

        Assert.All(errors, error =>
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
