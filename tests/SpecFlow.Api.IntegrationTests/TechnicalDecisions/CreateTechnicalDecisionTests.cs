using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class CreateTechnicalDecisionTests
{
    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedDecisionThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();
        const string Content = "  # Context\r\n\r\nUse SQLite.  \r\n";

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest("  Use SQLite  ", Content));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(decision);
        Assert.Equal(project.Id, decision.ProjectId);
        Assert.Equal("Use SQLite", decision.Title);
        Assert.Equal(Content, decision.Content);
        Assert.Equal(expectedTimestamp, decision.CreatedAtUtc);
        Assert.Equal(expectedTimestamp, decision.UpdatedAtUtc);
        Assert.Equal(
            TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id),
            response.Headers.Location?.AbsolutePath.TrimEnd('/'));
        Assert.Matches(
            "^\"[0-9a-f]{64}\"$",
            HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Create_WithRepeatedTitle_CreatesIndependentDecisions()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            project.Id,
            "Generic decision",
            "# First");

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest("  Generic decision  ", "# Second"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var decisions = await client.GetFromJsonAsync<List<TechnicalDecisionResponse>>(
            TechnicalDecisionTestData.DecisionsRoute(project.Id));
        Assert.NotNull(decisions);
        Assert.Equal(2, decisions.Count);
        Assert.All(decisions, decision => Assert.Equal("Generic decision", decision.Title));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingTitle_ReturnsValidationProblem(string? title)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest(title, "# Decision"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
    }

    [Fact]
    public async Task Create_WithTitleOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest(
                new string('a', TechnicalDecision.MaxTitleLength + 1),
                "# Decision"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingContent_ReturnsValidationProblem(string? content)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest("Decision", content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Fact]
    public async Task Create_WithContentOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            new SaveTechnicalDecisionRequest(
                "Decision",
                new string('a', TechnicalDecision.MaxContentLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Fact]
    public async Task Create_ForUnknownProject_ReturnsProjectNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            TechnicalDecisionTestData.DecisionsRoute(Guid.NewGuid()),
            new SaveTechnicalDecisionRequest("Decision", "# Decision"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project not found", problem.Title);
    }

    [Fact]
    public async Task Create_WithInvalidProjectIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/projects/not-a-uuid/technical-decisions",
            new SaveTechnicalDecisionRequest("Decision", "# Decision"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("projectId", problem.Errors);
    }

    [Fact]
    public async Task Create_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        using var content = new StringContent(
            """{"title":"Decision","content":"# Decision"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            TechnicalDecisionTestData.DecisionsRoute(project.Id),
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
