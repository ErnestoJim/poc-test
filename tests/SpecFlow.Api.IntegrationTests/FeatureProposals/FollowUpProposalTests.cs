using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Specifications;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FollowUpProposalTests
{
    [Fact]
    public async Task CreateAndListFollowUps_PreserveSourceAndReturnDirectChildren()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var source = await CreateVerifiedSourceAsync(client);
        var sourceRoute = ProposalRoute(source.ProjectId, source.ProposalId);
        var specificationRoute = SpecificationTestData.SpecificationRoute(
            source.ProjectId, source.ProposalId);
        var sourceBefore = await client.GetFromJsonAsync<FeatureProposalResponse>(sourceRoute);
        var specificationTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client, specificationRoute);

        using var firstResponse = await client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "First follow-up", null, source.ProposalId.ToString()));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(first);
        Assert.Equal(source.ProposalId, first.SourceProposalId);
        Assert.Equal("pending", first.Status);
        Assert.Equal(ProposalRoute(source.ProjectId, first.Id),
            firstResponse.Headers.Location?.AbsolutePath);

        factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        using var secondResponse = await client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "Second follow-up", null, source.ProposalId.ToString()));
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(second);
        await FeatureProposalTestData.RejectProposalAsync(
            client, source.ProjectId, second.Id, "No longer needed");

        var directChildren = await client.GetFromJsonAsync<FeatureProposalResponse[]>(
            $"{sourceRoute}/follow-ups");
        Assert.NotNull(directChildren);
        Assert.Equal([first.Id, second.Id], directChildren.Select(item => item.Id));
        Assert.Equal(["pending", "rejected"],
            directChildren.Select(item => item.Status));
        Assert.All(directChildren,
            item => Assert.Equal(source.ProposalId, item.SourceProposalId));
        Assert.Equal(first, await client.GetFromJsonAsync<FeatureProposalResponse>(
            ProposalRoute(source.ProjectId, first.Id)));
        Assert.Equal(sourceBefore, await client.GetFromJsonAsync<FeatureProposalResponse>(
            sourceRoute));
        Assert.Equal(specificationTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, specificationRoute));

        var projectList = await client.GetFromJsonAsync<FeatureProposalResponse[]>(
            ProposalsRoute(source.ProjectId));
        Assert.NotNull(projectList);
        Assert.Contains(projectList, item => item.Id == first.Id &&
            item.SourceProposalId == source.ProposalId);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Create_WithInvalidSourceIdentifier_ReturnsValidationProblem(
        string sourceProposalId)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            ProposalsRoute(project.Id),
            new CreateFeatureProposalRequest("Follow-up", null, sourceProposalId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("sourceProposalId", problem.Errors);
    }

    [Fact]
    public async Task Create_RejectsUnknownAndCrossProjectSourcesWithoutRevealingThem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var source = await CreateVerifiedSourceAsync(client);
        var otherProject = await FeatureProposalTestData.CreateProjectAsync(
            client, "Other project");

        foreach (var sourceId in new[] { Guid.NewGuid(), source.ProposalId })
        {
            using var response = await client.PostAsJsonAsync(
                ProposalsRoute(otherProject.Id),
                new CreateFeatureProposalRequest("Follow-up", null, sourceId.ToString()));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("Source proposal not found", problem?.Title);
        }

        using var crossContextList = await client.GetAsync(
            $"{ProposalRoute(otherProject.Id, source.ProposalId)}/follow-ups");
        Assert.Equal(HttpStatusCode.NotFound, crossContextList.StatusCode);
    }

    [Fact]
    public async Task Create_RequiresVerifiedSourceAtEveryLifecycleStage()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var sourceId = context.Proposal.Id;
        var projectId = context.Project.Id;
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client, projectId, sourceId, "Task");

        await AssertSourceNotVerifiedAsync();
        await SpecificationTestData.ApproveWithCoverageAsync(
            client, projectId, sourceId, task.Id);
        await AssertSourceNotVerifiedAsync();
        await ImplementationTaskTestData.StartTaskAsync(client, projectId, sourceId, task.Id);
        await ImplementationTaskTestData.CompleteTaskAsync(client, projectId, sourceId, task.Id);
        var specificationRoute = SpecificationTestData.SpecificationRoute(projectId, sourceId);
        using var implemented = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client, specificationRoute, $"{specificationRoute}/mark-implemented");
        implemented.EnsureSuccessStatusCode();
        await AssertSourceNotVerifiedAsync();

        async Task AssertSourceNotVerifiedAsync()
        {
            using var response = await client.PostAsJsonAsync(
                ProposalsRoute(projectId),
                new CreateFeatureProposalRequest(
                    "Follow-up", null, sourceId.ToString()));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("Source specification is not verified", problem?.Title);
        }
    }

    [Fact]
    public async Task VerifiedFollowUp_CanBecomeSourceOfAnotherProposal()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var source = await CreateVerifiedSourceAsync(client);
        using var response = await client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "Second delivery", null, source.ProposalId.ToString()));
        response.EnsureSuccessStatusCode();
        var followUp = await response.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(followUp);
        await FeatureProposalTestData.AcceptProposalAsync(
            client, source.ProjectId, followUp.Id);
        await SpecificationTestData.CreateSpecificationAsync(
            client, source.ProjectId, followUp.Id);
        await CompleteSpecificationAsync(client, source.ProjectId, followUp.Id);

        using var nextResponse = await client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "Third delivery", null, followUp.Id.ToString()));
        nextResponse.EnsureSuccessStatusCode();
        var next = await nextResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(next);
        Assert.Equal(followUp.Id, next.SourceProposalId);
        var directChildren = await client.GetFromJsonAsync<FeatureProposalResponse[]>(
            $"{ProposalRoute(source.ProjectId, source.ProposalId)}/follow-ups");
        Assert.NotNull(directChildren);
        Assert.Equal(followUp.Id, Assert.Single(directChildren).Id);
    }

    [Fact]
    public async Task FollowUp_PersistsAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var connectionString = $"Data Source={Path.Combine(directory, "specflow.db")}";

        try
        {
            Guid projectId;
            Guid sourceId;
            Guid followUpId;
            using (var factory = new SpecFlowApiFactory(connectionString))
            using (var client = factory.CreateClient())
            {
                var source = await CreateVerifiedSourceAsync(client);
                projectId = source.ProjectId;
                sourceId = source.ProposalId;
                using var response = await client.PostAsJsonAsync(
                    ProposalsRoute(projectId),
                    new CreateFeatureProposalRequest(
                        "Persistent follow-up", null, sourceId.ToString()));
                response.EnsureSuccessStatusCode();
                var followUp = await response.Content
                    .ReadFromJsonAsync<FeatureProposalResponse>();
                Assert.NotNull(followUp);
                followUpId = followUp.Id;
            }

            using var restartedFactory = new SpecFlowApiFactory(connectionString);
            using var restartedClient = restartedFactory.CreateClient();
            var persisted = await restartedClient
                .GetFromJsonAsync<FeatureProposalResponse>(
                    ProposalRoute(projectId, followUpId));
            Assert.Equal(sourceId, persisted?.SourceProposalId);
            var children = await restartedClient
                .GetFromJsonAsync<FeatureProposalResponse[]>(
                    $"{ProposalRoute(projectId, sourceId)}/follow-ups");
            Assert.Equal(followUpId, Assert.Single(children!).Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentFollowUps_CanShareVerifiedSource()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var source = await CreateVerifiedSourceAsync(client);
        var sourceRoute = ProposalRoute(source.ProjectId, source.ProposalId);
        var sourceBefore = await client.GetFromJsonAsync<FeatureProposalResponse>(sourceRoute);

        var firstRequest = client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "First concurrent follow-up", null, source.ProposalId.ToString()));
        var secondRequest = client.PostAsJsonAsync(
            ProposalsRoute(source.ProjectId),
            new CreateFeatureProposalRequest(
                "Second concurrent follow-up", null, source.ProposalId.ToString()));
        using var firstResponse = await firstRequest;
        using var secondResponse = await secondRequest;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var children = await client.GetFromJsonAsync<FeatureProposalResponse[]>(
            $"{sourceRoute}/follow-ups");
        Assert.Equal(2, children?.Length);
        Assert.Equal(sourceBefore,
            await client.GetFromJsonAsync<FeatureProposalResponse>(sourceRoute));
    }

    private static async Task<(Guid ProjectId, Guid ProposalId)> CreateVerifiedSourceAsync(
        HttpClient client)
    {
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        await CompleteSpecificationAsync(client, context.Project.Id, context.Proposal.Id);
        return (context.Project.Id, context.Proposal.Id);
    }

    private static async Task CompleteSpecificationAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId)
    {
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client, projectId, proposalId, "Task");
        await SpecificationTestData.ApproveWithCoverageAsync(
            client, projectId, proposalId, task.Id);
        await ImplementationTaskTestData.StartTaskAsync(client, projectId, proposalId, task.Id);
        await ImplementationTaskTestData.CompleteTaskAsync(client, projectId, proposalId, task.Id);
        var route = SpecificationTestData.SpecificationRoute(projectId, proposalId);
        using var implemented = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client, route, $"{route}/mark-implemented");
        implemented.EnsureSuccessStatusCode();
        await AcceptanceCriterionVerificationTestData.PassAllCriteriaAsync(
            client, projectId, proposalId);
        using var verified = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client, route, $"{route}/verify");
        verified.EnsureSuccessStatusCode();
    }

    private static string ProposalsRoute(Guid projectId) =>
        $"/api/projects/{projectId}/proposals";

    private static string ProposalRoute(Guid projectId, Guid proposalId) =>
        $"{ProposalsRoute(projectId)}/{proposalId}";
}
