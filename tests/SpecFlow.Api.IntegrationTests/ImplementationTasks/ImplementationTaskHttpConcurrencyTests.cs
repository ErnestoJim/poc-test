using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskHttpConcurrencyTests
{
    [Fact]
    public async Task Start_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{implementationTask.Id}";

        using var response = await client.PostAsync($"{taskRoute}/start", content: null);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task CreateGetAndList_ReturnStrongEntityTags()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var collectionRoute = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);

        using var createResponse = await client.PostAsJsonAsync(
            collectionRoute,
            new SaveImplementationTaskRequest("Task", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ImplementationTaskResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        var createdEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(createResponse);
        Assert.Matches("^\"[0-9a-f]{64}\"$", createdEntityTag);

        using var getResponse = await client.GetAsync($"{collectionRoute}/{created.Id}");
        using var listResponse = await client.GetAsync(collectionRoute);

        Assert.Equal(createdEntityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        Assert.Matches(
            "^\"[0-9a-f]{64}\"$",
            HttpPreconditionTestData.GetRequiredEntityTag(listResponse));
    }

    [Fact]
    public async Task CreateAndDelete_ChangeCollectionEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var collectionRoute = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
        var emptyEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);

        using var createResponse = await client.PostAsJsonAsync(
            collectionRoute,
            new SaveImplementationTaskRequest("Task", null));
        var implementationTask = await createResponse.Content
            .ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.NotNull(implementationTask);
        var createdCollectionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        using var deleteResponse = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            $"{collectionRoute}/{implementationTask.Id}",
            HttpPreconditionTestData.GetRequiredEntityTag(createResponse));

        Assert.NotEqual(emptyEntityTag, createdCollectionEntityTag);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotEqual(
            createdCollectionEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, collectionRoute));
    }

    [Fact]
    public async Task Transition_WithStaleEntityTag_ReturnsPreconditionFailedBeforeDomainConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{implementationTask.Id}";
        var pendingEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, taskRoute);

        using var startResponse = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Post,
            $"{taskRoute}/start",
            pendingEntityTag);
        using var staleResponse = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Post,
            $"{taskRoute}/start",
            pendingEntityTag);

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotEqual(
            pendingEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(startResponse));
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        var problem = await staleResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Precondition failed", problem.Title);
    }

    [Fact]
    public async Task UpdateAndDelete_WithStaleEntityTag_ReturnPreconditionFailed()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{implementationTask.Id}";
        var staleEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, taskRoute);
        using var firstUpdate = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            taskRoute,
            new SaveImplementationTaskRequest("Current", null),
            staleEntityTag);

        using var staleUpdate = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            taskRoute,
            new SaveImplementationTaskRequest("Stale", null),
            staleEntityTag);
        using var staleDelete = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            taskRoute,
            staleEntityTag);

        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleDelete.StatusCode);
        var persisted = await client.GetFromJsonAsync<ImplementationTaskResponse>(taskRoute);
        Assert.Equal("Current", persisted?.Title);
    }

    [Fact]
    public async Task IdenticalUpdate_PreservesResourceEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task",
            "Description");
        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{implementationTask.Id}";
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, taskRoute);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            taskRoute,
            new SaveImplementationTaskRequest(
                implementationTask.Title,
                implementationTask.Description),
            entityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Reorder_WithCurrentEntityTag_ReturnsNewCollectionEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
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
        var third = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Third");
        var collectionRoute = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        var firstEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{first.Id}");
        var secondEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{second.Id}");
        var thirdEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{third.Id}");

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            $"{collectionRoute}/order",
            new ReorderImplementationTasksRequest(
                [second.Id.ToString(), first.Id.ToString(), third.Id.ToString()]),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var newEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(response);
        Assert.NotEqual(originalEntityTag, newEntityTag);
        Assert.Equal(
            newEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, collectionRoute));
        Assert.NotEqual(
            firstEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{first.Id}"));
        Assert.NotEqual(
            secondEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{second.Id}"));
        Assert.Equal(
            thirdEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{third.Id}"));
    }

    [Fact]
    public async Task Reorder_WithStaleCollectionEntityTag_ReturnsPreconditionFailed()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var first = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        var collectionRoute = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
        var staleEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        var second = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            $"{collectionRoute}/order",
            new ReorderImplementationTasksRequest(
                [second.Id.ToString(), first.Id.ToString()]),
            staleEntityTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task CriterionAndTaskCollectionEntityTags_AreIndependent()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Criterion");
        var implementationTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var criteriaRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        var tasksRoute = ImplementationTaskTestData.TasksRoute(
            context.Project.Id,
            context.Proposal.Id);
        var criterionRoute = $"{criteriaRoute}/{criterion.Id}";
        var taskRoute = $"{tasksRoute}/{implementationTask.Id}";
        var originalCriteriaEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criteriaRoute);
        var originalTasksEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            tasksRoute);

        using var criterionResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                criterionRoute,
                criterionRoute,
                new SaveAcceptanceCriterionRequest("Updated criterion"));
        var tasksAfterCriterionUpdate = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            tasksRoute);
        var criteriaAfterCriterionUpdate = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criteriaRoute);
        using var taskResponse = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            taskRoute,
            $"{taskRoute}/start");

        Assert.Equal(HttpStatusCode.OK, criterionResponse.StatusCode);
        Assert.Equal(originalTasksEntityTag, tasksAfterCriterionUpdate);
        Assert.NotEqual(originalCriteriaEntityTag, criteriaAfterCriterionUpdate);
        Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
        Assert.Equal(
            criteriaAfterCriterionUpdate,
            await HttpPreconditionTestData.GetEntityTagAsync(client, criteriaRoute));
        Assert.NotEqual(
            originalTasksEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, tasksRoute));
    }
}
