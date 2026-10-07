using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class UpdateImplementationTaskTests
{
    [Fact]
    public async Task Update_WithDifferentData_PreservesIdentityAndUpdatesTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original",
            "Original description");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        const string Description = "  Updated **Markdown**\r\n";

        var route = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveImplementationTaskRequest("  Updated  ", Description));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.SpecificationId, updated.SpecificationId);
        Assert.Equal(created.Position, updated.Position);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal(Description, updated.Description);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), updated.UpdatedAtUtc);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Update_WithSameNormalizedState_PreservesUpdatedTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task",
            "Description");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        var route =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{created.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveImplementationTaskRequest("  Task  ", created.Description));

        var unchanged = await response.Content
            .ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, unchanged);
    }

    [Fact]
    public async Task Update_WithNullDescription_ClearsDescription()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task",
            "Description");

        var route =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{created.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveImplementationTaskRequest(created.Title, null));

        var updated = await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task Update_ToDuplicateNormalizedTitle_ReturnsConflictAndPreservesTask()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var first = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        var second = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        var route = $"{TasksRoute()}/{second.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveImplementationTaskRequest("  FIRST  ", "Changed"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task already exists", problem.Title);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{second.Id}");
        Assert.Equal(second, persisted);
        Assert.NotEqual(first.Id, persisted?.Id);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Update_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        using var content = new StringContent(
            """{"title":"Updated","description":null}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PutAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{implementationTask.Id}",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
