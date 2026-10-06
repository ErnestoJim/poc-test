using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class CreateFeatureProposalTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedProposalThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var request = new CreateFeatureProposalRequest(
            "Add acceptance criteria",
            "Allow verifiable criteria");

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdProposal =
            await createResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(createdProposal);
        Assert.Equal(project.Id, createdProposal.ProjectId);
        Assert.Equal("Add acceptance criteria", createdProposal.Title);
        Assert.Equal("Allow verifiable criteria", createdProposal.Description);
        Assert.Equal("pending", createdProposal.Status);
        Assert.Null(createdProposal.DecidedAtUtc);
        Assert.Null(createdProposal.RejectionReason);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), createdProposal.CreatedAtUtc);
        Assert.Equal(
            $"/api/projects/{project.Id}/proposals/{createdProposal.Id}",
            createResponse.Headers.Location?.AbsolutePath);

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{createdProposal.Id}");
        var retrievedProposal =
            await getResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(createdProposal, retrievedProposal);
    }

    [Fact]
    public async Task Create_ForUnknownProject_ReturnsNotFoundProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var projectId = Guid.NewGuid();

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/proposals",
            new CreateFeatureProposalRequest("Proposal", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
            "/api/projects/not-a-uuid/proposals",
            new CreateFeatureProposalRequest("Proposal", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("projectId", problem.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingTitle_ReturnsValidationProblem(string? title)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            new CreateFeatureProposalRequest(title, null));

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
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            new CreateFeatureProposalRequest(new string('a', 201), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
    }

    [Fact]
    public async Task Create_WithDescriptionOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            new CreateFeatureProposalRequest("Proposal", new string('a', 4_001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("description", problem.Errors);
    }

    [Fact]
    public async Task Create_NormalizesTitleAndPersistsWhitespaceDescriptionAsNull()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            new CreateFeatureProposalRequest("  Proposal  ", "   "));
        var createdProposal =
            await createResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(createdProposal);

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{createdProposal.Id}");
        var retrievedProposal =
            await getResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();

        Assert.NotNull(retrievedProposal);
        Assert.Equal("Proposal", retrievedProposal.Title);
        Assert.Null(retrievedProposal.Description);
    }

    [Fact]
    public async Task Create_NormalizesNonEmptyDescription()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals",
            new CreateFeatureProposalRequest("Proposal", "  Description  "));

        var proposal = await response.Content.ReadFromJsonAsync<FeatureProposalResponse>();
        Assert.NotNull(proposal);
        Assert.Equal("Description", proposal.Description);
    }

    [Fact]
    public async Task Create_WithDuplicateTitle_CreatesDistinctProposals()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);

        var first = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Duplicate title");
        var second = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Duplicate title");

        Assert.Equal(first.Title, second.Title);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Create_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        using var content = new StringContent(
            """{"title":"Proposal"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            $"/api/projects/{project.Id}/proposals",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
