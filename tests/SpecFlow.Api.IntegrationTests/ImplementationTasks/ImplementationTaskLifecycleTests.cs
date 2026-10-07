using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskLifecycleTests
{
    [Fact]
    public async Task Start_PendingTask_ReturnsAndPersistsInProgressTask()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await CreateTaskAsync("Task");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();

        var taskRoute = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.NotNull(started);
        Assert.Equal("in_progress", started.Status);
        Assert.Equal(expectedTimestamp, started.StartedAtUtc);
        Assert.Null(started.CompletedAtUtc);
        Assert.Equal(expectedTimestamp, started.UpdatedAtUtc);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{created.Id}");
        Assert.Equal(started, persisted);

        Task<ImplementationTaskResponse> CreateTaskAsync(string title) =>
            ImplementationTaskTestData.CreateTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                title);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Complete_InProgressTask_ReturnsAndPersistsCompletedTask()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var started = await ImplementationTaskTestData.StartTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            created.Id);
        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();

        var taskRoute = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/complete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = await response.Content
            .ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.NotNull(completed);
        Assert.Equal("completed", completed.Status);
        Assert.Equal(started.StartedAtUtc, completed.StartedAtUtc);
        Assert.Equal(expectedTimestamp, completed.CompletedAtUtc);
        Assert.Equal(expectedTimestamp, completed.UpdatedAtUtc);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{created.Id}");
        Assert.Equal(completed, persisted);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Complete_PendingTask_ReturnsConflictWithoutChanges()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");

        var taskRoute = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/complete");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task transition conflict", problem.Title);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{created.Id}");
        Assert.Equal(created, persisted);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Start_InProgressTask_ReturnsConflictAndPreservesFirstStart()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var started = await ImplementationTaskTestData.StartTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            created.Id);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        var taskRoute = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/start");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{created.Id}");
        Assert.Equal(started, persisted);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    public async Task Transition_CompletedTask_ReturnsConflict(string action)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        await ImplementationTaskTestData.StartTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            created.Id);
        var completed = await ImplementationTaskTestData.CompleteTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            created.Id);

        var taskRoute = $"{TasksRoute()}/{created.Id}";
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/{action}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{TasksRoute()}/{created.Id}");
        Assert.Equal(completed, persisted);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Transition_TaskFromDifferentSpecification_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client, "First");
        var second = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client, "Second");
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            first.Project.Id,
            first.Proposal.Id,
            "Task");

        using var response = await client.PostAsync(
            $"{ImplementationTaskTestData.TasksRoute(second.Project.Id, second.Proposal.Id)}/{implementationTask.Id}/start",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task not found", problem.Title);
    }

    [Fact]
    public async Task Transition_WithInvalidTaskIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/not-a-uuid/start",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("taskId", problem.Errors);
    }

    [Fact]
    public async Task List_ReturnsTasksInEveryStatusWithoutFiltering()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var pending = await CreateTaskAsync("Pending");
        var inProgress = await CreateTaskAsync("In progress");
        var completed = await CreateTaskAsync("Completed");
        await StartTaskAsync(inProgress.Id);
        await StartTaskAsync(completed.Id);
        await ImplementationTaskTestData.CompleteTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            completed.Id);

        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            TasksRoute());

        Assert.NotNull(tasks);
        Assert.Collection(
            tasks,
            implementationTask =>
                Assert.True(implementationTask.Id == pending.Id &&
                    implementationTask.Status == "pending"),
            implementationTask =>
                Assert.True(implementationTask.Id == inProgress.Id &&
                    implementationTask.Status == "in_progress"),
            implementationTask =>
                Assert.True(implementationTask.Id == completed.Id &&
                    implementationTask.Status == "completed"));

        Task<ImplementationTaskResponse> CreateTaskAsync(string title) =>
            ImplementationTaskTestData.CreateTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                title);

        Task<ImplementationTaskResponse> StartTaskAsync(Guid taskId) =>
            ImplementationTaskTestData.StartTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                taskId);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task CompletedTask_CanBeEditedReorderedAndDeletedWithoutLifecycleChanges()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var first = await CreateTaskAsync("First");
        var second = await CreateTaskAsync("Second");
        var third = await CreateTaskAsync("Third");
        await StartTaskAsync(second.Id);
        var completedSecond = await CompleteTaskAsync(second.Id);
        await StartTaskAsync(third.Id);
        var completedThird = await CompleteTaskAsync(third.Id);
        var route = TasksRoute();

        var secondRoute = $"{route}/{second.Id}";
        using var updateResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                secondRoute,
                secondRoute,
                new SaveImplementationTaskRequest("Second updated", "Description"));
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(completedSecond.Status, updated.Status);
        Assert.Equal(completedSecond.StartedAtUtc, updated.StartedAtUtc);
        Assert.Equal(completedSecond.CompletedAtUtc, updated.CompletedAtUtc);

        using var reorderResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/order",
                new ReorderImplementationTasksRequest(
                    [third.Id.ToString(), first.Id.ToString(), second.Id.ToString()]));
        Assert.Equal(HttpStatusCode.NoContent, reorderResponse.StatusCode);
        var reorderedThird = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            $"{route}/{third.Id}");
        Assert.NotNull(reorderedThird);
        Assert.Equal(completedThird.Status, reorderedThird.Status);
        Assert.Equal(completedThird.StartedAtUtc, reorderedThird.StartedAtUtc);
        Assert.Equal(completedThird.CompletedAtUtc, reorderedThird.CompletedAtUtc);

        using var deleteResponse = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, $"{route}/{second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var getDeletedResponse = await client.GetAsync($"{route}/{second.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);

        Task<ImplementationTaskResponse> CreateTaskAsync(string title) =>
            ImplementationTaskTestData.CreateTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                title);

        Task<ImplementationTaskResponse> StartTaskAsync(Guid taskId) =>
            ImplementationTaskTestData.StartTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                taskId);

        Task<ImplementationTaskResponse> CompleteTaskAsync(Guid taskId) =>
            ImplementationTaskTestData.CompleteTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                taskId);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }
}
