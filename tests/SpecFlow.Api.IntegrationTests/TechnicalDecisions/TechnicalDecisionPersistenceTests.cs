using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class TechnicalDecisionPersistenceTests
{
    [Fact]
    public async Task UpdatedDecision_RemainsAvailableAfterApplicationRestart()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            Guid projectId;
            TechnicalDecisionResponse updated;
            string updatedEntityTag;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var project = await TechnicalDecisionTestData.CreateProjectAsync(firstClient);
                projectId = project.Id;
                var decision = await TechnicalDecisionTestData.CreateDecisionAsync(
                    firstClient,
                    projectId);
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));
                var route = TechnicalDecisionTestData.DecisionRoute(projectId, decision.Id);
                using var updateResponse = await HttpPreconditionTestData
                    .PutAsJsonWithCurrentEntityTagAsync(
                        firstClient,
                        route,
                        route,
                        new SaveTechnicalDecisionRequest("Updated", "# Updated"));
                updateResponse.EnsureSuccessStatusCode();
                updatedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(updateResponse);
                updated = await updateResponse.Content
                    .ReadFromJsonAsync<TechnicalDecisionResponse>()
                    ?? throw new InvalidOperationException(
                        "The update response did not contain a technical decision.");
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync(
                TechnicalDecisionTestData.DecisionRoute(projectId, updated.Id));

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Equal(
                updated,
                await getResponse.Content.ReadFromJsonAsync<TechnicalDecisionResponse>());
            Assert.Equal(
                updatedEntityTag,
                HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
