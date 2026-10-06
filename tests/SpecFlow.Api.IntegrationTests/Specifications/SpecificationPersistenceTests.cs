using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationPersistenceTests
{
    [Fact]
    public async Task UpdatedSpecification_RemainsAvailableAfterApplicationRestart()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            Guid projectId;
            Guid proposalId;
            SpecificationResponse updated;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var (project, proposal) = await SpecificationTestData
                    .CreateAcceptedProposalAsync(firstClient);
                projectId = project.Id;
                proposalId = proposal.Id;
                await SpecificationTestData.CreateSpecificationAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "# Original");
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));

                using var updateResponse = await firstClient.PutAsJsonAsync(
                    $"/api/projects/{projectId}/proposals/{proposalId}/specification",
                    new SaveSpecificationRequest("# Updated"));
                updateResponse.EnsureSuccessStatusCode();
                updated = await updateResponse.Content
                    .ReadFromJsonAsync<SpecificationResponse>()
                    ?? throw new InvalidOperationException(
                        "The update response did not contain a specification.");
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync(
                $"/api/projects/{projectId}/proposals/{proposalId}/specification");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var persisted = await getResponse.Content
                .ReadFromJsonAsync<SpecificationResponse>();
            Assert.Equal(updated, persisted);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
