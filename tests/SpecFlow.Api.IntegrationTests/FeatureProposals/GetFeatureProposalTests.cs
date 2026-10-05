using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class GetFeatureProposalTests
{
    [Fact]
    public async Task Get_ForUnknownProject_ReturnsProjectNotFoundProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/proposals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }

    [Fact]
    public async Task Get_ForUnknownProposal_ReturnsFeatureProposalNotFoundProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Fact]
    public async Task Get_UsingDifferentProject_ReturnsFeatureProposalNotFoundProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await FeatureProposalTestData.CreateProjectAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            firstProject.Id,
            "Proposal");

        using var response = await client.GetAsync(
            $"/api/projects/{secondProject.Id}/proposals/{proposal.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Fact]
    public async Task Get_WithInvalidProjectIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/not-a-uuid/proposals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("projectId", problem.Errors);
    }

    [Fact]
    public async Task Get_WithInvalidProposalIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/not-a-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("proposalId", problem.Errors);
    }
}
