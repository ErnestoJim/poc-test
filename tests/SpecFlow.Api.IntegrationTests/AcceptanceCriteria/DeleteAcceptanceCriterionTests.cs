using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class DeleteAcceptanceCriterionTests
{
    [Fact]
    public async Task Delete_MiddleCriterion_RemovesItAndCompactsFollowingPositions()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var first = await CreateCriterionAsync("First");
        var second = await CreateCriterionAsync("Second");
        var third = await CreateCriterionAsync("Third");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();

        using var response = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, $"{CriteriaRoute()}/{second.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var getDeletedResponse = await client.GetAsync($"{CriteriaRoute()}/{second.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        var criteria = await client.GetFromJsonAsync<List<AcceptanceCriterionResponse>>(
            CriteriaRoute());
        Assert.NotNull(criteria);
        Assert.Collection(
            criteria,
            criterion => Assert.Equal(first, criterion),
            criterion =>
            {
                Assert.Equal(third.Id, criterion.Id);
                Assert.Equal(2, criterion.Position);
                Assert.Equal(expectedTimestamp, criterion.UpdatedAtUtc);
            });

        async Task<AcceptanceCriterionResponse> CreateCriterionAsync(string content) =>
            await AcceptanceCriterionTestData.CreateCriterionAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                content);

        string CriteriaRoute() => AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
    }

    [Fact]
    public async Task Delete_LastCriterion_DoesNotUpdateEarlierCriteria()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var first = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        var second = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(
                client,
                $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{second.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var criteria = await client.GetFromJsonAsync<List<AcceptanceCriterionResponse>>(
            AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id));
        var remaining = Assert.Single(criteria!);
        Assert.Equal(first, remaining);
    }

    [Fact]
    public async Task Delete_UnknownCriterion_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var criterionId = Guid.NewGuid();

        using var response = await client.DeleteAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{criterionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion not found", problem.Title);
    }
}
