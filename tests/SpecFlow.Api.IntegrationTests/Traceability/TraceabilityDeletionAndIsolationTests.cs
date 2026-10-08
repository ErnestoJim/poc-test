using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityDeletionAndIsolationTests
{
    [Fact]
    public async Task DeleteTask_RemovesLinksAndPreservesCriterion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var linksRoute = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        using var linkResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            linksRoute,
            await HttpPreconditionTestData.GetEntityTagAsync(client, linksRoute),
            context.Criterion.Id);
        linkResponse.EnsureSuccessStatusCode();
        var taskRoute = $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{context.Task.Id}";

        using var deleteResponse = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, taskRoute);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var inverse = await client
            .GetFromJsonAsync<AcceptanceCriterionImplementationTasksResponse>(
                TraceabilityTestData.CriterionLinksRoute(
                    context.Project.Id,
                    context.Proposal.Id,
                    context.Criterion.Id));
        Assert.Empty(inverse?.ImplementationTaskIds ?? []);
        Assert.NotNull(await client.GetFromJsonAsync<AcceptanceCriterionResponse>(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{context.Criterion.Id}"));
    }

    [Fact]
    public async Task Replace_WithUnknownCriterion_ReturnsNotFoundAndPreservesLinks()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            entityTag,
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion not found", problem.Title);
        Assert.Equal(entityTag, await HttpPreconditionTestData.GetEntityTagAsync(client, route));
    }

    [Fact]
    public async Task GetInverse_UsingCriterionFromAnotherSpecification_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await TraceabilityTestData.CreateContextAsync(client, "First");
        var second = await TraceabilityTestData.CreateContextAsync(client, "Second");

        using var response = await client.GetAsync(
            TraceabilityTestData.CriterionLinksRoute(
                first.Project.Id,
                first.Proposal.Id,
                second.Criterion.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion not found", problem.Title);
    }
}
