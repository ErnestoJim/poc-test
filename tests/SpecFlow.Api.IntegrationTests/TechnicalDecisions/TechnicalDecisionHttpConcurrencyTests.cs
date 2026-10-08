using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class TechnicalDecisionHttpConcurrencyTests
{
    [Fact]
    public async Task CreateAndGet_ReturnSameStrongEntityTagWithoutExposingVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var collectionRoute = TechnicalDecisionTestData.DecisionsRoute(project.Id);

        using var createResponse = await client.PostAsJsonAsync(
            collectionRoute,
            new SaveTechnicalDecisionRequest("Decision", "# Decision"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(createResponse);
        Assert.Matches("^\"[0-9a-f]{64}\"$", createdEntityTag);
        await using var jsonStream = await createResponse.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(jsonStream);
        Assert.False(document.RootElement.TryGetProperty("version", out _));
        var decisionId = document.RootElement.GetProperty("id").GetGuid();

        using var getResponse = await client.GetAsync(
            TechnicalDecisionTestData.DecisionRoute(project.Id, decisionId));

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(createdEntityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
    }

    [Fact]
    public async Task Update_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);

        using var response = await client.PutAsJsonAsync(
            route,
            new SaveTechnicalDecisionRequest("Updated", "# Updated"));

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
    public async Task Update_WithUnsupportedIfMatch_ReturnsBadRequest(string entityTag)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest("Updated", "# Updated"),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid If-Match header", problem.Title);
    }

    [Fact]
    public async Task Update_WithStaleEntityTag_ReturnsPreconditionFailedAndPreservesLatestData()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var firstResponse = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest("First update", "# First"),
            originalEntityTag);
        using var staleResponse = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest("Stale update", "# Stale"),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotEqual(
            originalEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(firstResponse));
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        var persisted = await client.GetFromJsonAsync<TechnicalDecisionResponse>(route);
        Assert.Equal("First update", persisted?.Title);
        Assert.Equal("# First", persisted?.Content);
    }

    [Fact]
    public async Task CompetingUpdates_AreDetectedByInternalVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var created = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var firstCopy = await firstDbContext.TechnicalDecisions.SingleAsync(
            decision => decision.Id == created.Id);
        var secondCopy = await secondDbContext.TechnicalDecisions.SingleAsync(
            decision => decision.Id == created.Id);
        var timestamp = factory.TimeProvider.GetUtcNow().AddHours(1);

        firstCopy.Update("First", "# First", timestamp);
        secondCopy.Update("Second", "# Second", timestamp);
        await firstDbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDbContext.SaveChangesAsync());
    }
}
