using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.Specifications;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class CreateSpecificationTests
{
    [Fact]
    public async Task Create_ForAcceptedProposal_ReturnsCreatedSpecificationThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();
        const string Content = "  # Specification\r\n\r\nContent  \r\n";

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest(Content));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.NotNull(created);
        Assert.Equal(proposal.Id, created.FeatureProposalId);
        Assert.Equal(Content, created.Content);
        Assert.Equal(expectedTimestamp, created.CreatedAtUtc);
        Assert.Equal(expectedTimestamp, created.UpdatedAtUtc);
        Assert.Equal(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            createResponse.Headers.Location?.AbsolutePath.TrimEnd('/'));

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, retrieved);
    }

    [Fact]
    public async Task AcceptProposal_DoesNotCreateSpecification()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Specification not found", problem.Title);
    }

    [Fact]
    public async Task Create_ForPendingProposal_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Pending proposal");

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Specification"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal is not accepted", problem.Title);
    }

    [Fact]
    public async Task Create_ForRejectedProposal_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Rejected proposal");
        await FeatureProposalTestData.RejectProposalAsync(
            client,
            project.Id,
            proposal.Id,
            "Not selected");

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Specification"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal is not accepted", problem.Title);
    }

    [Fact]
    public async Task Create_WhenSpecificationExists_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Duplicate"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Specification already exists", problem.Title);
    }

    [Fact]
    public async Task Create_UsingDifferentProject_ReturnsFeatureProposalNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (firstProject, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{secondProject.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Specification"));

        Assert.NotEqual(firstProject.Id, secondProject.Id);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingContent_ReturnsValidationProblem(string? content)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest(content));

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
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest(new string('a', Specification.MaxContentLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Theory]
    [InlineData("not-a-uuid", "de305d54-75b4-431b-adb2-eb6b9e546014", "projectId")]
    [InlineData("de305d54-75b4-431b-adb2-eb6b9e546014", "not-a-uuid", "proposalId")]
    public async Task Create_WithInvalidIdentifier_ReturnsValidationProblem(
        string projectId,
        string proposalId,
        string expectedField)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/proposals/{proposalId}/specification",
            new SaveSpecificationRequest("# Specification"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(expectedField, problem.Errors);
    }

    [Fact]
    public async Task Create_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        using var content = new StringContent(
            """{"content":"# Specification"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
