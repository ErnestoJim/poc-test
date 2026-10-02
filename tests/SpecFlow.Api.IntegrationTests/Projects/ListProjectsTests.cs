using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Projects;

public sealed class ListProjectsTests
{
    [Fact]
    public async Task List_WithNoProjects_ReturnsEmptyArray()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();
        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task List_ReturnsNewestProjectsFirst()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("First", null));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        using var secondResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Second", null));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        using var response = await client.GetAsync("/api/projects");
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();

        Assert.NotNull(projects);
        Assert.Collection(
            projects,
            project => Assert.Equal("Second", project.Name),
            project => Assert.Equal("First", project.Name));
    }
}
