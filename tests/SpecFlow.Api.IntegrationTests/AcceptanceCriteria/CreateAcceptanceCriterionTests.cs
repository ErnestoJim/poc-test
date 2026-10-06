using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Specifications;
using SpecFlow.Domain.AcceptanceCriteria;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class CreateAcceptanceCriterionTests
{
    [Fact]
    public async Task Create_WithValidContent_AppendsCriterionThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        const string FirstContent = "  First criterion\r\n";
        const string SecondContent = "Second criterion";
        var first = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            FirstContent);

        using var createResponse = await client.PostAsJsonAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveAcceptanceCriterionRequest(SecondContent));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var second = await createResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.NotNull(second);
        Assert.Equal(context.Specification.Id, second.SpecificationId);
        Assert.Equal(SecondContent, second.Content);
        Assert.Equal(2, second.Position);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), second.CreatedAtUtc);
        Assert.Equal(second.CreatedAtUtc, second.UpdatedAtUtc);
        Assert.Equal(1, first.Position);
        Assert.Equal(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{second.Id}",
            createResponse.Headers.Location?.AbsolutePath);

        using var getResponse = await client.GetAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{second.Id}");
        var retrieved = await getResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(second, retrieved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingContent_ReturnsValidationProblem(string? content)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsJsonAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveAcceptanceCriterionRequest(content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Fact]
    public async Task Create_WithContentOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsJsonAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveAcceptanceCriterionRequest(
                new string('a', AcceptanceCriterion.MaxContentLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Fact]
    public async Task Create_WithDuplicateContent_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Duplicate");

        using var response = await client.PostAsJsonAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveAcceptanceCriterionRequest("Duplicate"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion already exists", problem.Title);
    }

    [Fact]
    public async Task Create_WithSameContentInDifferentSpecifications_Succeeds()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client, "First");
        var second = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client, "Second");

        var firstCriterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            first.Project.Id,
            first.Proposal.Id,
            "Shared content");
        var secondCriterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            second.Project.Id,
            second.Proposal.Id,
            "Shared content");

        Assert.NotEqual(firstCriterion.Id, secondCriterion.Id);
        Assert.Equal(firstCriterion.Content, secondCriterion.Content);
    }

    [Fact]
    public async Task Create_WhenSpecificationDoesNotExist_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.PostAsJsonAsync(
            AcceptanceCriterionTestData.CriteriaRoute(project.Id, proposal.Id),
            new SaveAcceptanceCriterionRequest("Criterion"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Specification not found", problem.Title);
    }

    [Fact]
    public async Task Create_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        using var content = new StringContent(
            """{"content":"Criterion"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            AcceptanceCriterionTestData.CriteriaRoute(
                context.Project.Id,
                context.Proposal.Id),
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
