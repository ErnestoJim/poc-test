using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ReorderImplementationTasksTests
{
    [Fact]
    public async Task Reorder_WithCompleteIdentifierList_ReturnsNoContentAndPersistsOrder()
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
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                TasksRoute(),
                $"{TasksRoute()}/order",
                new ReorderImplementationTasksRequest(
                    [third.Id.ToString(), first.Id.ToString(), second.Id.ToString()]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
        var tasks = await ListTasksAsync();
        Assert.Collection(
            tasks,
            implementationTask => AssertMoved(implementationTask, third.Id, 1),
            implementationTask => AssertMoved(implementationTask, first.Id, 2),
            implementationTask => AssertMoved(implementationTask, second.Id, 3));

        void AssertMoved(ImplementationTaskResponse implementationTask, Guid id, int position)
        {
            Assert.Equal(id, implementationTask.Id);
            Assert.Equal(position, implementationTask.Position);
            Assert.Equal(expectedTimestamp, implementationTask.UpdatedAtUtc);
        }

        Task<ImplementationTaskResponse> CreateTaskAsync(string title) =>
            ImplementationTaskTestData.CreateTaskAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                title);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);

        async Task<List<ImplementationTaskResponse>> ListTasksAsync() =>
            await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(TasksRoute())
                ?? throw new InvalidOperationException("The list response was empty.");
    }

    [Fact]
    public async Task Reorder_WithCurrentOrder_PreservesTaskTimestamps()
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
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                TasksRoute(),
                $"{TasksRoute()}/order",
                new ReorderImplementationTasksRequest(
                    [first.Id.ToString(), second.Id.ToString()]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            TasksRoute());
        Assert.Equal([first, second], tasks);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Reorder_WithDifferentIdentifierSet_ReturnsConflictWithoutChanges()
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

        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                TasksRoute(),
                $"{TasksRoute()}/order",
                new ReorderImplementationTasksRequest(
                    [first.Id.ToString(), Guid.NewGuid().ToString()]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation tasks collection changed", problem.Title);
        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            TasksRoute());
        Assert.Equal([first, second], tasks);

        string TasksRoute() => ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Theory]
    [InlineData(null, "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData("", "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData("not-a-uuid", "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData(
        "de305d54-75b4-431b-adb2-eb6b9e546014",
        "de305d54-75b4-431b-adb2-eb6b9e546014")]
    public async Task Reorder_WithInvalidIdentifierList_ReturnsValidationProblem(
        string? firstId,
        string secondId)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/order",
            new ReorderImplementationTasksRequest([firstId, secondId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("taskIds", problem.Errors);
    }

    [Fact]
    public async Task Reorder_EmptyCollection_ReturnsNoContent()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        var route = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/order",
                new ReorderImplementationTasksRequest([]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        using var content = new StringContent(
            """{"taskIds":[]}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PutAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/order",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
