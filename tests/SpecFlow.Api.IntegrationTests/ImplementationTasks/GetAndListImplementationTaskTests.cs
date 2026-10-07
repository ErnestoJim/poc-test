using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class GetAndListImplementationTaskTests
{
    [Fact]
    public async Task List_WithNoTasks_ReturnsEmptyArray()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id));

        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task List_ReturnsTasksByPosition()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        var tasks = await client.GetFromJsonAsync<List<ImplementationTaskResponse>>(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id));

        Assert.NotNull(tasks);
        Assert.Collection(
            tasks,
            implementationTask =>
            {
                Assert.Equal("First", implementationTask.Title);
                Assert.Equal(1, implementationTask.Position);
            },
            implementationTask =>
            {
                Assert.Equal("Second", implementationTask.Title);
                Assert.Equal(2, implementationTask.Position);
            });
    }

    [Fact]
    public async Task Get_UsingDifferentSpecification_ReturnsNotFound()
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

        using var response = await client.GetAsync(
            $"{ImplementationTaskTestData.TasksRoute(second.Project.Id, second.Proposal.Id)}/{implementationTask.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task not found", problem.Title);
    }

    [Fact]
    public async Task Get_WithInvalidTaskIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.GetAsync(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/not-a-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("taskId", problem.Errors);
    }
}
