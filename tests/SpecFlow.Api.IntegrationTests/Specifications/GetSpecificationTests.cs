using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class GetSpecificationTests
{
    [Fact]
    public async Task Get_ForUnknownProject_ReturnsProjectNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/proposals/{Guid.NewGuid()}/specification");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }

    [Fact]
    public async Task Get_ForUnknownProposal_ReturnsFeatureProposalNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{Guid.NewGuid()}/specification");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Fact]
    public async Task Get_UsingDifferentProject_ReturnsFeatureProposalNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (firstProject, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            firstProject.Id,
            proposal.Id);

        using var response = await client.GetAsync(
            $"/api/projects/{secondProject.Id}/proposals/{proposal.Id}/specification");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Fact]
    public async Task Get_ReturnsPersistedMarkdownWithoutChanges()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        const string Content = "  # Specification\r\n\r\n- First\n- Second  \n";
        var expected = await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id,
            Content);

        using var response = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var specification = await response.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.Equal(expected, specification);
        Assert.Equal(Content, specification?.Content);
    }
}
