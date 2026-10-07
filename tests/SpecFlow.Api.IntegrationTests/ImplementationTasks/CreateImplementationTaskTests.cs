using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Specifications;
using SpecFlow.Domain.ImplementationTasks;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class CreateImplementationTaskTests
{
    [Fact]
    public async Task Create_WithValidData_AppendsNormalizedTaskThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        const string Description = "  **Markdown**\r\n";

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveImplementationTaskRequest("  Implement API  ", Description));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>();
        Assert.NotNull(created);
        Assert.Equal(context.Specification.Id, created.SpecificationId);
        Assert.Equal("Implement API", created.Title);
        Assert.Equal(Description, created.Description);
        Assert.Equal(2, created.Position);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), created.CreatedAtUtc);
        Assert.Equal(created.CreatedAtUtc, created.UpdatedAtUtc);
        Assert.Equal(
            $"{ImplementationTaskTestData.TasksRoute(context.Project.Id, context.Proposal.Id)}/{created.Id}",
            response.Headers.Location?.AbsolutePath);

        var retrieved = await client.GetFromJsonAsync<ImplementationTaskResponse>(
            response.Headers.Location);
        Assert.Equal(created, retrieved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingTitle_ReturnsValidationProblem(string? title)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveImplementationTaskRequest(title, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
    }

    [Fact]
    public async Task Create_WithInvalidDescription_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveImplementationTaskRequest("Task", "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("description", problem.Errors);
    }

    [Fact]
    public async Task Create_WithFieldsOverMaximumLength_ReturnsBothValidationErrors()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveImplementationTaskRequest(
                new string('t', ImplementationTask.MaxTitleLength + 1),
                new string('d', ImplementationTask.MaxDescriptionLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors);
        Assert.Contains("description", problem.Errors);
    }

    [Fact]
    public async Task Create_WithDuplicateNormalizedTitle_ReturnsConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Implement API");

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id),
            new SaveImplementationTaskRequest("  implement api  ", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Implementation task already exists", problem.Title);
    }

    [Fact]
    public async Task Create_WithSameTitleInDifferentSpecifications_Succeeds()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var first = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client, "First");
        var second = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client, "Second");

        var firstTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            first.Project.Id,
            first.Proposal.Id,
            "Shared title");
        var secondTask = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            second.Project.Id,
            second.Proposal.Id,
            "shared title");

        Assert.NotEqual(firstTask.Id, secondTask.Id);
    }

    [Fact]
    public async Task Create_WhenSpecificationDoesNotExist_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.PostAsJsonAsync(
            ImplementationTaskTestData.TasksRoute(project.Id, proposal.Id),
            new SaveImplementationTaskRequest("Task", null));

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
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        using var content = new StringContent(
            """{"title":"Task","description":null}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PostAsync(
            ImplementationTaskTestData.TasksRoute(
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
