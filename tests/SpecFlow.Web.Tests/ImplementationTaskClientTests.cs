using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

public sealed class ImplementationTaskClientTests
{
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
        HttpResponseMessage Error(params string[] fields) => hasField ? Problem(status, fields) : Problem(status);
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
        HttpResponseMessage Error() => hasField ? Problem(status, "acceptanceCriterionIds") : Problem(status);
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
}
