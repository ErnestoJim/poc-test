using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class ListFeatureProposalTests
{
    [Fact]
    public async Task List_WithNoProposals_ReturnsEmptyArray()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.GetAsync($"/api/projects/{project.Id}/proposals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposals = await response.Content.ReadFromJsonAsync<List<FeatureProposalResponse>>();
        Assert.NotNull(proposals);
        Assert.Empty(proposals);
    }

    [Fact]
    public async Task List_ReturnsOnlyProposalsFromRequestedProject()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await FeatureProposalTestData.CreateProjectAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");
        var expected = await FeatureProposalTestData.CreateProposalAsync(
            client,
            firstProject.Id,
            "Expected");
        await FeatureProposalTestData.CreateProposalAsync(client, secondProject.Id, "Excluded");

        using var response = await client.GetAsync(
            $"/api/projects/{firstProject.Id}/proposals");
        var proposals = await response.Content.ReadFromJsonAsync<List<FeatureProposalResponse>>();

        var proposal = Assert.Single(proposals!);
        Assert.Equal(expected, proposal);
    }

    [Fact]
    public async Task List_ReturnsNewestProposalsFirst()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        await FeatureProposalTestData.CreateProposalAsync(client, project.Id, "First");

        factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        await FeatureProposalTestData.CreateProposalAsync(client, project.Id, "Second");

        using var response = await client.GetAsync($"/api/projects/{project.Id}/proposals");
        var proposals = await response.Content.ReadFromJsonAsync<List<FeatureProposalResponse>>();

        Assert.NotNull(proposals);
        Assert.Collection(
            proposals,
            proposal => Assert.Equal("Second", proposal.Title),
            proposal => Assert.Equal("First", proposal.Title));
    }

    [Fact]
    public async Task List_WithMatchingCreationTimes_OrdersProposalsByIdentifier()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var first = await FeatureProposalTestData.CreateProposalAsync(client, project.Id, "First");
        var second = await FeatureProposalTestData.CreateProposalAsync(client, project.Id, "Second");
        var expectedIdentifiers = new[] { first.Id, second.Id }.Order().ToArray();

        using var response = await client.GetAsync($"/api/projects/{project.Id}/proposals");
        var proposals = await response.Content.ReadFromJsonAsync<List<FeatureProposalResponse>>();

        Assert.NotNull(proposals);
        Assert.Equal(expectedIdentifiers, proposals.Select(proposal => proposal.Id));
    }

    [Fact]
    public async Task List_ForUnknownProject_ReturnsNotFoundProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/proposals");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }
}
