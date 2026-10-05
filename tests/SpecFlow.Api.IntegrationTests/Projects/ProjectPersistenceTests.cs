using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Projects;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task Project_RemainsAvailableAfterApplicationRestart()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            ProjectResponse createdProject;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            using (var createResponse = await firstClient.PostAsJsonAsync(
                       "/api/projects",
                       new CreateProjectRequest("Persistent project", null)))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                createdProject = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>()
                    ?? throw new InvalidOperationException("The create response did not contain a project.");
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync($"/api/projects/{createdProject.Id}");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var retrievedProject = await getResponse.Content.ReadFromJsonAsync<ProjectResponse>();
            Assert.Equal(createdProject, retrievedProject);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
