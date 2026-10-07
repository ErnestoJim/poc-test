using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class DeleteImplementationTaskTests
{
    [Fact]
    public async Task Delete_MiddleTask_RemovesItAndCompactsFollowingPositions()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var first = await CreateTaskAsync("First");
        var second = await CreateTaskAsync("Second");
        var third = await CreateTaskAsync("Third");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();

        using var response = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, $"{TasksRoute()}/{second.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var getDeletedResponse = await client.GetAsync($"{TasksRoute()}/{second.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            TasksRoute());
        Assert.NotNull(tasks);
        Assert.Collection(
            tasks,
            implementationTask => Assert.Equal(first, implementationTask),
            implementationTask =>
            {
                Assert.Equal(third.Id, implementationTask.Id);
                Assert.Equal(2, implementationTask.Position);
                Assert.Equal(expectedTimestamp, implementationTask.UpdatedAtUtc);
            });

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
    public async Task Delete_LastTask_DoesNotUpdateEarlierTasks()
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
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, $"{TasksRoute()}/{second.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            TasksRoute());
        var remaining = Assert.Single(tasks!);
        Assert.Equal(first, remaining);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Delete_UnknownTask_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.DeleteAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task not found", problem.Title);
    }
}
