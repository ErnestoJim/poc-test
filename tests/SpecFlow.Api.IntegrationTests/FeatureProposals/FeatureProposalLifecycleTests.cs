using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FeatureProposalLifecycleTests
{
    [Fact]
    public async Task Accept_PendingProposal_ReturnsAndPersistsAcceptedProposal()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        var expectedDecision = factory.TimeProvider.GetUtcNow();

        using var response = await client.PostAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(accepted);
        Assert.Equal("accepted", accepted.Status);
        Assert.Equal(expectedDecision, accepted.DecidedAtUtc);
        Assert.Null(accepted.RejectionReason);

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}");
        var persisted = await getResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.Equal(accepted, persisted);
    }

    [Fact]
    public async Task Reject_PendingProposal_NormalizesReasonAndPersistsDecision()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        var expectedDecision = factory.TimeProvider.GetUtcNow();

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/reject",
            new RejectFeatureProposalRequest("  Not enough value  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(rejected);
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal(expectedDecision, rejected.DecidedAtUtc);
        Assert.Equal("Not enough value", rejected.RejectionReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithMissingReason_ReturnsValidationProblem(string? reason)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/reject",
            new RejectFeatureProposalRequest(reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("reason", problem.Errors);
    }

    [Fact]
    public async Task Reject_WithReasonOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/reject",
            new RejectFeatureProposalRequest(new string('a', 1_001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("reason", problem.Errors);
    }

    [Fact]
    public async Task Accept_AlreadyAcceptedProposal_ReturnsConflictWithoutChangingDecision()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        var route = $"/api/projects/{project.Id}/proposals/{proposal.Id}/accept";

        using var firstResponse = await client.PostAsync(route, content: null);
        var firstDecision = await firstResponse.Content
            .ReadFromJsonAsync<FeatureProposalResponse>();
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var secondResponse = await client.PostAsync(route, content: null);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal transition conflict", problem.Title);

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}");
        var persisted = await getResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.Equal(firstDecision, persisted);
    }

    [Fact]
    public async Task Reject_AcceptedProposal_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        await FeatureProposalTestData.AcceptProposalAsync(client, project.Id, proposal.Id);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/reject",
            new RejectFeatureProposalRequest("Too late"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Accept_ProposalFromDifferentProject_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await FeatureProposalTestData.CreateProjectAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            firstProject.Id,
            "Proposal");

        using var response = await client.PostAsync(
            $"/api/projects/{secondProject.Id}/proposals/{proposal.Id}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Fact]
    public async Task Accept_ForUnknownProject_ReturnsProjectNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/projects/{Guid.NewGuid()}/proposals/{Guid.NewGuid()}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }

    [Theory]
    [InlineData("not-a-uuid", "de305d54-75b4-431b-adb2-eb6b9e546014", "projectId")]
    [InlineData("de305d54-75b4-431b-adb2-eb6b9e546014", "not-a-uuid", "proposalId")]
    public async Task Accept_WithInvalidIdentifier_ReturnsValidationProblem(
        string projectId,
        string proposalId,
        string expectedField)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/projects/{projectId}/proposals/{proposalId}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(expectedField, problem.Errors);
    }

    [Fact]
    public async Task List_ReturnsProposalsInEveryStatusWithoutFiltering()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var pending = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Pending");
        var accepted = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Accepted");
        var rejected = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Rejected");
        await FeatureProposalTestData.AcceptProposalAsync(client, project.Id, accepted.Id);
        await FeatureProposalTestData.RejectProposalAsync(
            client,
            project.Id,
            rejected.Id,
            "Not selected");

        using var response = await client.GetAsync($"/api/projects/{project.Id}/proposals");
        var proposals = await response.Content.ReadFromJsonAsync<List<FeatureProposalResponse>>();

        Assert.NotNull(proposals);
        Assert.Equal(3, proposals.Count);
        Assert.Contains(proposals, item => item.Id == pending.Id && item.Status == "pending");
        Assert.Contains(proposals, item => item.Id == accepted.Id && item.Status == "accepted");
        Assert.Contains(proposals, item => item.Id == rejected.Id && item.Status == "rejected");
    }

    [Fact]
    public async Task Reject_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        using var content = new StringContent(
            """{"reason":"Not selected"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/reject",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
