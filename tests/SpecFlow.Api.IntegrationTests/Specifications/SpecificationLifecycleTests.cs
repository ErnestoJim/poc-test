using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Traceability;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationLifecycleTests
{
    [Fact]
    public async Task Create_ReturnsDraftWithoutTransitionDates()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);

        Assert.Equal("draft", context.Specification.Status);
        Assert.Null(context.Specification.ApprovedAtUtc);
        Assert.Null(context.Specification.ImplementedAtUtc);
        Assert.Null(context.Specification.VerifiedAtUtc);
    }

    [Fact]
    public async Task Approve_RequiresCriteriaTasksAndCompleteCriterionCoverage()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Covered task");
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Auxiliary task");
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Observable behavior");
        var specificationRoute = SpecificationTestData.SpecificationRoute(
            context.Project.Id,
            context.Proposal.Id);

        using var uncoveredResponse = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(
                client,
                specificationRoute,
                $"{specificationRoute}/approve");

        Assert.Equal(HttpStatusCode.Conflict, uncoveredResponse.StatusCode);
        var problem = await uncoveredResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Specification is not ready for approval", problem?.Title);

        var linksRoute = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            task.Id);
        using var linksResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            linksRoute,
            await HttpPreconditionTestData.GetEntityTagAsync(client, linksRoute),
            criterion.Id);
        linksResponse.EnsureSuccessStatusCode();

        var approved = await SpecificationTestData.ApproveAsync(
            client,
            context.Project.Id,
            context.Proposal.Id);

        Assert.Equal("approved", approved.Status);
        Assert.NotNull(approved.ApprovedAtUtc);
    }

    [Fact]
    public async Task Lifecycle_AdvancesAfterApprovedTasksAreCompleted()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var approved = await SpecificationTestData.ApproveWithCoverageAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            task.Id);

        using var incompleteResponse = await PostSpecificationTransitionAsync(
            "mark-implemented");
        Assert.Equal(HttpStatusCode.Conflict, incompleteResponse.StatusCode);
        var incompleteProblem = await incompleteResponse.Content
            .ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Specification implementation is incomplete", incompleteProblem?.Title);

        await ImplementationTaskTestData.StartTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            task.Id);
        await ImplementationTaskTestData.CompleteTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            task.Id);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var implementedResponse = await PostSpecificationTransitionAsync(
            "mark-implemented");
        Assert.Equal(HttpStatusCode.OK, implementedResponse.StatusCode);
        var implemented = await implementedResponse.Content
            .ReadFromJsonAsync<SpecificationResponse>();
        Assert.NotNull(implemented);
        Assert.Equal("implemented", implemented.Status);
        Assert.Equal(approved.ApprovedAtUtc, implemented.ApprovedAtUtc);
        Assert.NotNull(implemented.ImplementedAtUtc);
        Assert.Null(implemented.VerifiedAtUtc);

        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using var verifiedResponse = await PostSpecificationTransitionAsync("verify");
        Assert.Equal(HttpStatusCode.OK, verifiedResponse.StatusCode);
        var verified = await verifiedResponse.Content
            .ReadFromJsonAsync<SpecificationResponse>();
        Assert.NotNull(verified);
        Assert.Equal("verified", verified.Status);
        Assert.Equal(implemented.ApprovedAtUtc, verified.ApprovedAtUtc);
        Assert.Equal(implemented.ImplementedAtUtc, verified.ImplementedAtUtc);
        Assert.NotNull(verified.VerifiedAtUtc);

        Task<HttpResponseMessage> PostSpecificationTransitionAsync(string action)
        {
            var route = SpecificationTestData.SpecificationRoute(
                context.Project.Id,
                context.Proposal.Id);
            return HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/{action}");
        }
    }

    [Fact]
    public async Task ApprovedSpecification_FreezesPlanningAndDraftDoesNotRunTasks()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{task.Id}";

        using var draftStartResponse = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(client, taskRoute, $"{taskRoute}/start");
        Assert.Equal(HttpStatusCode.Conflict, draftStartResponse.StatusCode);
        var startProblem = await draftStartResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Specification is not approved for implementation", startProblem?.Title);

        await SpecificationTestData.ApproveWithCoverageAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            task.Id);
        var specificationRoute = SpecificationTestData.SpecificationRoute(
            context.Project.Id,
            context.Proposal.Id);

        using var updateSpecificationResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                specificationRoute,
                specificationRoute,
                new SaveSpecificationRequest("# Changed"));
        using var createCriterionResponse = await client.PostAsJsonAsync(
            $"{specificationRoute}/acceptance-criteria",
            new SaveAcceptanceCriterionRequest("Another criterion"));
        using var updateTaskResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                taskRoute,
                taskRoute,
                new SaveImplementationTaskRequest("Changed", null));
        var linksRoute = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            task.Id);
        using var replaceLinksResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            linksRoute,
            await HttpPreconditionTestData.GetEntityTagAsync(client, linksRoute));

        await AssertSpecificationNotEditableAsync(updateSpecificationResponse);
        await AssertSpecificationNotEditableAsync(createCriterionResponse);
        await AssertSpecificationNotEditableAsync(updateTaskResponse);
        await AssertSpecificationNotEditableAsync(replaceLinksResponse);
    }

    [Fact]
    public async Task PlanningMutation_InvalidatesSpecificationEntityTagButNoOpPreservesIt()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var specificationRoute = SpecificationTestData.SpecificationRoute(
            context.Project.Id,
            context.Proposal.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            specificationRoute);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");
        var afterCreateEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            specificationRoute);

        Assert.NotEqual(originalEntityTag, afterCreateEntityTag);

        var taskRoute =
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{task.Id}";
        using var noOpResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                taskRoute,
                taskRoute,
                new SaveImplementationTaskRequest(task.Title, task.Description));
        noOpResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            afterCreateEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, specificationRoute));

        using var staleApprovalResponse = await HttpPreconditionTestData
            .SendWithEntityTagAsync(
                client,
                HttpMethod.Post,
                $"{specificationRoute}/approve",
                originalEntityTag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleApprovalResponse.StatusCode);
    }

    private static async Task AssertSpecificationNotEditableAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Specification is not editable", problem?.Title);
    }
}
