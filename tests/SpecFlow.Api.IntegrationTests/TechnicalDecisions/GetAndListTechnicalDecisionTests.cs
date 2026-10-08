using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class GetAndListTechnicalDecisionTests
{
    [Fact]
    public async Task Get_ReturnsPersistedDecisionAndEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        const string Content = "  # Decision\r\n\r\nPreserve this.  \n";
        var expected = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            project.Id,
            "Decision",
            Content);

        using var response = await client.GetAsync(
            TechnicalDecisionTestData.DecisionRoute(project.Id, expected.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>());
        Assert.Matches(
            "^\"[0-9a-f]{64}\"$",
            HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Get_UsingDifferentProject_ReturnsTechnicalDecisionNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "First");
        var secondProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "Second");
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            firstProject.Id);

        using var response = await client.GetAsync(
            TechnicalDecisionTestData.DecisionRoute(secondProject.Id, decision.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision not found", problem.Title);
    }

    [Theory]
    [InlineData("not-a-uuid", "de305d54-75b4-431b-adb2-eb6b9e546014", "projectId")]
    [InlineData("de305d54-75b4-431b-adb2-eb6b9e546014", "not-a-uuid", "decisionId")]
    public async Task Get_WithInvalidIdentifier_ReturnsValidationProblem(
        string projectId,
        string decisionId,
        string expectedField)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/technical-decisions/{decisionId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(expectedField, problem.Errors);
    }

    [Fact]
    public async Task List_ForEmptyProject_ReturnsEmptyArray()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);

        using var response = await client.GetAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decisions = await response.Content.ReadFromJsonAsync<List<TechnicalDecisionResponse>>();
        Assert.NotNull(decisions);
        Assert.Empty(decisions);
    }

    [Fact]
    public async Task List_ReturnsOnlyProjectDecisionsInChronologicalOrder()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "First");
        var secondProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "Second");
        var first = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            firstProject.Id,
            "First",
            "# First");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var second = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            firstProject.Id,
            "Second",
            "# Second");
        await TechnicalDecisionTestData.CreateDecisionAsync(client, secondProject.Id);

        var decisions = await client.GetFromJsonAsync<List<TechnicalDecisionResponse>>(
            TechnicalDecisionTestData.DecisionsRoute(firstProject.Id));

        Assert.NotNull(decisions);
        Assert.Equal([first.Id, second.Id], decisions.Select(decision => decision.Id));
    }

    [Fact]
    public async Task List_ForUnknownProject_ReturnsProjectNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            TechnicalDecisionTestData.DecisionsRoute(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }
}
