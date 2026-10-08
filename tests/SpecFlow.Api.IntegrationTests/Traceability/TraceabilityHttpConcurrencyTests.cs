using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityHttpConcurrencyTests
{
    [Fact]
    public async Task Replace_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);

        using var response = await client.PutAsJsonAsync(
            route,
            CreateRequest(context.Criterion.Id));

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Precondition required", problem.Title);
    }

    [Theory]
    [InlineData("W/\"weak\"")]
    [InlineData("*")]
    [InlineData("\"first\", \"second\"")]
    [InlineData("not-an-etag")]
    public async Task Replace_WithUnsupportedIfMatch_ReturnsBadRequest(string entityTag)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            TraceabilityTestData.TaskLinksRoute(
                context.Project.Id,
                context.Proposal.Id,
                context.Task.Id),
            CreateRequest(context.Criterion.Id),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid If-Match header", problem.Title);
    }

    [Fact]
    public async Task CompetingReplacements_WithSameEntityTag_OnlyFirstCompletes()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var secondCriterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second criterion");
        var route = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var firstResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            entityTag,
            context.Criterion.Id);
        using var staleResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            entityTag,
            secondCriterion.Id);

        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        var persisted = await client
            .GetFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>(route);
        Assert.Equal([context.Criterion.Id], persisted?.AcceptanceCriterionIds);
    }

    [Fact]
    public async Task DeleteCriterion_InvalidatesLinkedTaskCollectionEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        var route = TraceabilityTestData.TaskLinksRoute(
            context.Project.Id,
            context.Proposal.Id,
            context.Task.Id);
        using var linkResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            await HttpPreconditionTestData.GetEntityTagAsync(client, route),
            context.Criterion.Id);
        linkResponse.EnsureSuccessStatusCode();
        var linkedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(linkResponse);
        var criterionRoute = $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{context.Criterion.Id}";

        using var deleteResponse = await HttpPreconditionTestData
            .DeleteWithCurrentEntityTagAsync(client, criterionRoute);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var getResponse = await client.GetAsync(route);
        var links = await getResponse.Content
            .ReadFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>();
        Assert.Empty(links?.AcceptanceCriterionIds ?? []);
        Assert.NotEqual(
            linkedEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(getResponse));

        using var staleResponse = await TraceabilityTestData.ReplaceLinksAsync(
            client,
            route,
            linkedEntityTag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
    }

    private static ReplaceImplementationTaskAcceptanceCriteriaRequest CreateRequest(
        Guid criterionId) =>
        new([criterionId.ToString()]);
}
