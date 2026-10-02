using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Projects;

public sealed class CreateProjectTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedProjectThatCanBeRetrieved()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var request = new CreateProjectRequest(
            "SpecFlow",
            "AI-assisted development POC");

        using var createResponse = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdProject = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(createdProject);
        Assert.Equal("SpecFlow", createdProject.Name);
        Assert.Equal("AI-assisted development POC", createdProject.Description);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), createdProject.CreatedAtUtc);
        Assert.Equal($"/api/projects/{createdProject.Id}", createResponse.Headers.Location?.AbsolutePath);

        using var getResponse = await client.GetAsync($"/api/projects/{createdProject.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrievedProject = await getResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal(createdProject, retrievedProject);
    }

    [Fact]
    public async Task Create_NormalizesNameAndDescription()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var request = new CreateProjectRequest("  SpecFlow  ", "  POC  ");

        using var response = await client.PostAsJsonAsync("/api/projects", request);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal("SpecFlow", project.Name);
        Assert.Equal("POC", project.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithMissingName_ReturnsValidationProblem(string? name)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var request = new CreateProjectRequest(name, null);

        using var response = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("name", problem.Errors);
    }

    [Fact]
    public async Task Create_WithDescriptionOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var request = new CreateProjectRequest("SpecFlow", new string('a', 1_001));

        using var response = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("description", problem.Errors);
    }

    [Fact]
    public async Task Create_WithDuplicateNameIgnoringCaseAndWhitespace_ReturnsConflictProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("SpecFlow", null));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("  specflow  ", null));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);

        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Project name already exists", problem.Title);
    }
}
