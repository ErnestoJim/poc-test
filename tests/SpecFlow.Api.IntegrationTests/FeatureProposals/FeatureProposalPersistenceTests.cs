using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FeatureProposalPersistenceTests
{
    [Fact]
    public async Task Proposal_RemainsAvailableAfterApplicationRestart()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            FeatureProposalResponse createdProposal;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var project = await FeatureProposalTestData.CreateProjectAsync(firstClient);
                createdProposal = await FeatureProposalTestData.CreateProposalAsync(
                    firstClient,
                    project.Id,
                    "Persistent proposal");
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));
                createdProposal = await FeatureProposalTestData.AcceptProposalAsync(
                    firstClient,
                    project.Id,
                    createdProposal.Id);
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync(
                $"/api/projects/{createdProposal.ProjectId}/proposals/{createdProposal.Id}");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var retrievedProposal =
                await getResponse.Content.ReadFromJsonAsync<FeatureProposalResponse>();
            Assert.Equal(createdProposal, retrievedProposal);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
