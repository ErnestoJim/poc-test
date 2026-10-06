using System.Net.Http.Json;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class AcceptanceCriterionPersistenceTests
{
    [Fact]
    public async Task CriteriaAndOrder_RemainAvailableAfterApplicationRestart()
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
            AcceptanceCriterionResponse first;
            AcceptanceCriterionResponse second;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var context = await AcceptanceCriterionTestData
                    .CreateSpecificationContextAsync(firstClient);
                projectId = context.Project.Id;
                proposalId = context.Proposal.Id;
                first = await AcceptanceCriterionTestData.CreateCriterionAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "First");
                second = await AcceptanceCriterionTestData.CreateCriterionAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "Second");
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));
                using var reorderResponse = await firstClient.PutAsJsonAsync(
                    $"{AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId)}/order",
                    new ReorderAcceptanceCriteriaRequest(
                        [second.Id.ToString(), first.Id.ToString()]));
                reorderResponse.EnsureSuccessStatusCode();
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            var persisted = await secondClient
                .GetFromJsonAsync<List<AcceptanceCriterionResponse>>(
                    AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId));

            Assert.NotNull(persisted);
            Assert.Collection(
                persisted,
                criterion =>
                {
                    Assert.Equal(second.Id, criterion.Id);
                    Assert.Equal(1, criterion.Position);
                },
                criterion =>
                {
                    Assert.Equal(first.Id, criterion.Id);
                    Assert.Equal(2, criterion.Position);
                });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
