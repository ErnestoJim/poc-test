using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityTests
{
    [Fact]
    public async Task Get_ForTaskWithoutLinks_ReturnsEmptyCollectionAndStrongEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var links = await response.Content
            .ReadFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>();
        Assert.NotNull(links);
        Assert.Empty(links.AcceptanceCriterionIds);
        Assert.Matches(
            "^\"[0-9a-f]{64}\"$",
            HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Replace_WithSeveralCriteria_PersistsDeterministicSetAndInverseLinks()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var secondCriterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "A second observable behavior.");
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            originalEntityTag,
            secondCriterion.Id,
            context.Criterion.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updatedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(response);
        Assert.NotEqual(originalEntityTag, updatedEntityTag);
        using var getResponse = await client.GetAsync(route);
        var links = await getResponse.Content
            .ReadFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>();
        Assert.NotNull(links);
        Assert.Equal(
            new[] { context.Criterion.Id, secondCriterion.Id }.OrderBy(id => id),
            links.AcceptanceCriterionIds);
        Assert.Equal(updatedEntityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));

        foreach (var criterionId in links.AcceptanceCriterionIds)
        {
            var inverse = await client
                .GetFromJsonAsync<AcceptanceCriterionImplementationTasksResponse>(
                    TraceabilityTestData.CriterionLinksRoute(
                        context.Project.Id,
                        context.Proposal.Id,
                        criterionId));
            Assert.Equal([context.Task.Id], inverse?.ImplementationTaskIds);
        }
    }

    [Fact]
    public async Task Replace_SupportsManyTasksForOneCriterion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var secondTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Implement another part");
        var taskIds = new[] { context.Task.Id, secondTask.Id };

        foreach (var taskId in taskIds)
        {
            var route = TraceabilityTestData.TaskLinksRoute(
                context.Project.Id,
                context.Proposal.Id,
                taskId);
            using var response = await TraceabilityTestData.ReplaceLinksAsync(
                client,
                route,
                await HttpPreconditionTestData.GetEntityTagAsync(client, route),
                context.Criterion.Id);
            response.EnsureSuccessStatusCode();
        }

        var inverse = await client
            .GetFromJsonAsync<AcceptanceCriterionImplementationTasksResponse>(
                TraceabilityTestData.CriterionLinksRoute(
                    context.Project.Id,
                    context.Proposal.Id,
                    context.Criterion.Id));
        Assert.Equal(taskIds.OrderBy(id => id), inverse?.ImplementationTaskIds);
    }

    [Fact]
    public async Task Replace_WithSameSet_PreservesEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        using var firstResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            await HttpPreconditionTestData.GetEntityTagAsync(client, route),
            context.Criterion.Id);
        firstResponse.EnsureSuccessStatusCode();
        var entityTag = HttpPreconditionTestData.GetRequiredEntityTag(firstResponse);

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            entityTag,
            context.Criterion.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Replace_WithEmptySet_RemovesAllLinks()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        using var addResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            await HttpPreconditionTestData.GetEntityTagAsync(client, route),
            context.Criterion.Id);
        addResponse.EnsureSuccessStatusCode();

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            HttpPreconditionTestData.GetRequiredEntityTag(addResponse));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var links = await client
            .GetFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>(route);
        Assert.Empty(links?.AcceptanceCriterionIds ?? []);
    }

    [Theory]
    [MemberData(nameof(InvalidCriterionSets))]
    public async Task Replace_WithInvalidCriterionSet_ReturnsValidationProblem(
        IReadOnlyList<string?>? criterionIds)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new ReplaceImplementationTaskAcceptanceCriteriaRequest(criterionIds),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("acceptanceCriterionIds", problem.Errors);
    }

    [Fact]
    public async Task Replace_WithCriterionFromAnotherSpecification_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client, "First");
        var other = await TraceabilityTestData.CreateContextAsync(client, "Second");
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            await HttpPreconditionTestData.GetEntityTagAsync(client, route),
            other.Criterion.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion not found", problem.Title);
    }

    [Fact]
    public async Task Replace_WithoutBody_ReturnsBadRequest()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replace_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);
        using var content = new StringContent(
            """{"acceptanceCriterionIds":[]}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            entityTag,
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Get_UsingTaskFromAnotherSpecification_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await TraceabilityTestData.CreateContextAsync(client, "First");
        var second = await TraceabilityTestData.CreateContextAsync(client, "Second");

        using var response = await client.GetAsync(
            TraceabilityTestData.TaskLinksRoute(
                first.Project.Id,
                first.Proposal.Id,
                second.Task.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task not found", problem.Title);
    }

    [Fact]
    public async Task Replace_CompletedTask_DoesNotChangeResourcesOrIndividualEntityTags()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        await ImplementationTaskTestData.StartTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var completed = await ImplementationTaskTestData.CompleteTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var taskRoute = $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{context.Task.Id}";
        var criterionRoute = $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{context.Criterion.Id}";
        var taskEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, taskRoute);
        var criterionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criterionRoute);
        var route = TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);

        using var response = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            await HttpPreconditionTestData.GetEntityTagAsync(client, route),
            context.Criterion.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(taskEntityTag, await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            taskRoute));
        Assert.Equal(criterionEntityTag, await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criterionRoute));
        Assert.Equal(
            completed,
            await client.GetFromJsonAsync<ImplementationTaskResponse>(
                taskRoute));
    }

    public static TheoryData<IReadOnlyList<string?>?> InvalidCriterionSets => new()
    {
        null,
        NullCriterionIds,
        InvalidCriterionIds,
        EmptyCriterionIds,
        DuplicateCriterionIds
    };

    private static readonly Guid KnownCriterionId =
        Guid.Parse("de305d54-75b4-431b-adb2-eb6b9e546014");

    private static readonly IReadOnlyList<string?> NullCriterionIds =
        new string?[] { null };

    private static readonly IReadOnlyList<string?> InvalidCriterionIds =
        new string?[] { "not-a-uuid" };

    private static readonly IReadOnlyList<string?> EmptyCriterionIds =
        new string?[] { Guid.Empty.ToString() };

    private static readonly IReadOnlyList<string?> DuplicateCriterionIds =
        new string?[] { KnownCriterionId.ToString(), KnownCriterionId.ToString() };

    private static string TaskLinksRoute(
        Guid projectId,
        Guid proposalId,
        Guid taskId) =>
        TraceabilityTestData.TaskLinksRoute(projectId, proposalId, taskId);
}
