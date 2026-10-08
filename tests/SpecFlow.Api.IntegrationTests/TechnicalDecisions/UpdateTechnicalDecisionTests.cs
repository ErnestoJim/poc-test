using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class UpdateTechnicalDecisionTests
{
    [Fact]
    public async Task Update_WithDifferentData_ReplacesDataAndUpdatesTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var created = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, created.Id);
        const string Content = "  # Updated\r\n\r\nNew decision.  \n";

        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveTechnicalDecisionRequest("  Updated  ", Content));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.ProjectId, updated.ProjectId);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal(Content, updated.Content);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), updated.UpdatedAtUtc);
        Assert.Equal(updated, await client.GetFromJsonAsync<TechnicalDecisionResponse>(route));
    }

    [Fact]
    public async Task Update_WithEquivalentData_PreservesTimestampAndEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var created = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, created.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest($"  {created.Title}  ", created.Content),
            entityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.UpdatedAtUtc, updated.UpdatedAtUtc);
        Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Update_UsingDifferentProject_ReturnsTechnicalDecisionNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "First");
        var secondProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "Second");
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            firstProject.Id);
        var actualRoute = TechnicalDecisionTestData.DecisionRoute(
            firstProject.Id,
            decision.Id);
        var otherProjectRoute = TechnicalDecisionTestData.DecisionRoute(
            secondProject.Id,
            decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, actualRoute);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            otherProjectRoute,
            new SaveTechnicalDecisionRequest("Updated", "# Updated"),
            entityTag);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision not found", problem.Title);
    }

    [Fact]
    public async Task Update_WithInvalidData_ReturnsValidationProblemAndPreservesDecision()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var created = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, created.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest(" ", null),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
        Assert.Contains("content", problem.Errors);
        Assert.Equal(created, await client.GetFromJsonAsync<TechnicalDecisionResponse>(route));
    }

    [Fact]
    public async Task Update_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);
        using var content = new StringContent(
            """{"title":"Updated","content":"# Updated"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            entityTag,
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
