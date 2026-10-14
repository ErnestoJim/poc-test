using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Specifications;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class AcceptanceCriterionVerificationTests
{
    [Fact]
    public async Task Verify_RequiresPassedCriterionButNotEvidence()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (projectId, proposalId, criterionId) = await CreateImplementedContextAsync(client);
        var specificationRoute = SpecificationTestData.SpecificationRoute(projectId, proposalId);
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            projectId, proposalId, criterionId);

        using var initialResponse = await client.GetAsync(route);
        initialResponse.EnsureSuccessStatusCode();
        var initial = await initialResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionVerificationResponse>();
        Assert.NotNull(initial);
        Assert.Equal("unverified", initial.Status);
        Assert.Null(initial.Evidence);
        Assert.Null(initial.CheckedAtUtc);

        using var blocked = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client, specificationRoute, $"{specificationRoute}/verify");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("Specification verification incomplete",
            (await blocked.Content.ReadFromJsonAsync<ProblemDetails>())?.Title);

        var criterionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client, AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId) +
                    $"/{criterionId}");
        var specificationEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client, specificationRoute);
        using var passedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("passed", null));
        passedResponse.EnsureSuccessStatusCode();
        var passed = await passedResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionVerificationResponse>();
        Assert.NotNull(passed);
        Assert.Equal("passed", passed.Status);
        Assert.Null(passed.Evidence);
        Assert.NotNull(passed.CheckedAtUtc);
        Assert.NotEqual(HttpPreconditionTestData.GetRequiredEntityTag(initialResponse),
            HttpPreconditionTestData.GetRequiredEntityTag(passedResponse));
        Assert.NotEqual(specificationEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, specificationRoute));
        Assert.Equal(criterionEntityTag, await HttpPreconditionTestData.GetEntityTagAsync(
            client, AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId) +
                    $"/{criterionId}"));

        using var verifiedResponse = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(
                client, specificationRoute, $"{specificationRoute}/verify");
        verifiedResponse.EnsureSuccessStatusCode();

        using var editAfterVerified = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("failed", null));
        Assert.Equal(HttpStatusCode.Conflict, editAfterVerified.StatusCode);
    }

    [Fact]
    public async Task Evaluation_CanBeCorrectedClearedAndRepeatedWithoutChangingEtags()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (projectId, proposalId, criterionId) = await CreateImplementedContextAsync(client);
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            projectId, proposalId, criterionId);
        var specificationRoute = SpecificationTestData.SpecificationRoute(projectId, proposalId);
        var initialEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var failedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("failed", "# Failure\n"));
        failedResponse.EnsureSuccessStatusCode();
        var failed = await failedResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionVerificationResponse>();
        Assert.NotNull(failed);
        Assert.Equal("# Failure\n", failed.Evidence);
        var failedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(failedResponse);
        var specificationEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client, specificationRoute);

        using var repeatedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("failed", "# Failure\n"));
        repeatedResponse.EnsureSuccessStatusCode();
        Assert.Equal(failedEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(repeatedResponse));
        Assert.Equal(specificationEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, specificationRoute));

        using var staleResponse = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveAcceptanceCriterionVerificationRequest("passed", null),
            initialEntityTag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);

        using var clearedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("unverified", null));
        clearedResponse.EnsureSuccessStatusCode();
        var cleared = await clearedResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionVerificationResponse>();
        Assert.NotNull(cleared);
        Assert.Equal("unverified", cleared.Status);
        Assert.Null(cleared.CheckedAtUtc);
        Assert.NotEqual(initialEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(clearedResponse));
    }

    [Theory]
    [InlineData("passed", " ")]
    [InlineData("unverified", "unexpected")]
    [InlineData("unknown", null)]
    public async Task Evaluation_RejectsInvalidRequest(string status, string? evidence)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (projectId, proposalId, criterionId) = await CreateImplementedContextAsync(client);
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            projectId, proposalId, criterionId);

        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest(status, evidence));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(status == "unknown" ? "status" : "evidence", problem.Errors.Keys);
    }

    [Fact]
    public async Task Evaluation_RequiresCurrentEntityTagAndCorrectContext()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (projectId, proposalId, criterionId) = await CreateImplementedContextAsync(client);
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            projectId, proposalId, criterionId);

        using var missingTag = await client.PutAsJsonAsync(
            route, new SaveAcceptanceCriterionVerificationRequest("passed", null));
        Assert.Equal(HttpStatusCode.PreconditionRequired, missingTag.StatusCode);

        var otherContext = await SpecificationTestData.CreateSpecificationContextAsync(
            client, "Other project");
        using var crossContext = await client.GetAsync(
            AcceptanceCriterionVerificationTestData.VerificationRoute(
                otherContext.Project.Id, otherContext.Proposal.Id, criterionId));
        Assert.Equal(HttpStatusCode.NotFound, crossContext.StatusCode);
    }

    [Fact]
    public async Task FailedCriterion_BlocksVerificationUntilCorrected()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (projectId, proposalId, criterionId) = await CreateImplementedContextAsync(client);
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            projectId, proposalId, criterionId);
        var specificationRoute = SpecificationTestData.SpecificationRoute(projectId, proposalId);

        using var failedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("failed", null));
        failedResponse.EnsureSuccessStatusCode();
        using var blocked = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client, specificationRoute, $"{specificationRoute}/verify");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using var passedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("passed", null));
        passedResponse.EnsureSuccessStatusCode();
        using var verifiedResponse = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(
                client, specificationRoute, $"{specificationRoute}/verify");
        verifiedResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DraftAndApprovedSpecifications_RejectEvaluationChanges()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client, context.Project.Id, context.Proposal.Id, "Criterion");
        var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
            context.Project.Id, context.Proposal.Id, criterion.Id);
        using var draftResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("passed", null));
        Assert.Equal(HttpStatusCode.Conflict, draftResponse.StatusCode);

        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client, context.Project.Id, context.Proposal.Id, "Task");
        var linksRoute =
            $"{SpecificationTestData.SpecificationRoute(context.Project.Id, context.Proposal.Id)}/tasks/{task.Id}/acceptance-criteria";
        using var linkResponse = await HttpPreconditionTestData
            .SendAsJsonWithEntityTagAsync(
                client,
                HttpMethod.Put,
                linksRoute,
                new ReplaceImplementationTaskAcceptanceCriteriaRequest([criterion.Id.ToString()]),
                await HttpPreconditionTestData.GetEntityTagAsync(client, linksRoute));
        linkResponse.EnsureSuccessStatusCode();
        await SpecificationTestData.ApproveAsync(
            client, context.Project.Id, context.Proposal.Id);

        using var approvedResponse = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionVerificationRequest("passed", null));
        Assert.Equal(HttpStatusCode.Conflict, approvedResponse.StatusCode);
    }

    [Fact]
    public async Task CompetingEvaluations_WithSameEntityTag_OnlyOneSucceeds()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            using var factory = new SpecFlowApiFactory(connectionString);
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();
            var (projectId, proposalId, criterionId) =
                await CreateImplementedContextAsync(firstClient);
            var route = AcceptanceCriterionVerificationTestData.VerificationRoute(
                projectId, proposalId, criterionId);
            var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(
                firstClient, route);

            var responses = await Task.WhenAll(
                HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
                    firstClient,
                    HttpMethod.Put,
                    route,
                    new SaveAcceptanceCriterionVerificationRequest("passed", null),
                    entityTag),
                HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
                    secondClient,
                    HttpMethod.Put,
                    route,
                    new SaveAcceptanceCriterionVerificationRequest("failed", null),
                    entityTag));

            try
            {
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
                Assert.Single(responses,
                    response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static async Task<(Guid ProjectId, Guid ProposalId, Guid CriterionId)>
        CreateImplementedContextAsync(HttpClient client)
    {
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client, context.Project.Id, context.Proposal.Id, "Task");
        await SpecificationTestData.ApproveWithCoverageAsync(
            client, context.Project.Id, context.Proposal.Id, task.Id);
        await ImplementationTaskTestData.StartTaskAsync(
            client, context.Project.Id, context.Proposal.Id, task.Id);
        await ImplementationTaskTestData.CompleteTaskAsync(
            client, context.Project.Id, context.Proposal.Id, task.Id);
        var specificationRoute = SpecificationTestData.SpecificationRoute(
            context.Project.Id, context.Proposal.Id);
        using var implementedResponse = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(
                client, specificationRoute, $"{specificationRoute}/mark-implemented");
        implementedResponse.EnsureSuccessStatusCode();

        var criteria = await client.GetFromJsonAsync<AcceptanceCriterionResponse[]>(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id, context.Proposal.Id))
            ?? throw new InvalidOperationException("The criteria response was empty.");
        return (context.Project.Id, context.Proposal.Id, Assert.Single(criteria).Id);
    }
}
