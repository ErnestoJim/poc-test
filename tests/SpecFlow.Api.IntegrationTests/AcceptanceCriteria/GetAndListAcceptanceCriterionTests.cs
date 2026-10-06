using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class GetAndListAcceptanceCriterionTests
{
    [Fact]
    public async Task List_WithNoCriteria_ReturnsEmptyArray()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.GetAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var criteria = await response.Content
            .ReadFromJsonAsync<List<AcceptanceCriterionResponse>>();
        Assert.NotNull(criteria);
        Assert.Empty(criteria);
    }

    [Fact]
    public async Task List_ReturnsCriteriaByPosition()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        using var response = await client.GetAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id));
        var criteria = await response.Content
            .ReadFromJsonAsync<List<AcceptanceCriterionResponse>>();

        Assert.NotNull(criteria);
        Assert.Collection(
            criteria,
            criterion =>
            {
                Assert.Equal("First", criterion.Content);
                Assert.Equal(1, criterion.Position);
            },
            criterion =>
            {
                Assert.Equal("Second", criterion.Content);
                Assert.Equal(2, criterion.Position);
            });
    }

    [Fact]
    public async Task Get_UsingDifferentSpecification_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client, "First");
        var second = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client, "Second");
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            first.Project.Id,
            first.Proposal.Id,
            "Criterion");

        using var response = await client.GetAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(second.Project.Id, second.Proposal.Id)}/{criterion.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion not found", problem.Title);
    }

    [Fact]
    public async Task Get_WithInvalidCriterionIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.GetAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/not-a-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("criterionId", problem.Errors);
    }
}
