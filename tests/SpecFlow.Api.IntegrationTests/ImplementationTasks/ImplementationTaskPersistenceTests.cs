using System.Net.Http.Json;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskPersistenceTests
{
    [Fact]
    public async Task TasksAndOrder_RemainAvailableAfterApplicationRestart()
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
            ImplementationTaskResponse first;
            ImplementationTaskResponse second;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var context = await ImplementationTaskTestData
                    .CreateSpecificationContextAsync(firstClient);
                projectId = context.Project.Id;
                proposalId = context.Proposal.Id;
                first = await ImplementationTaskTestData.CreateTaskAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "First",
                    "Description");
                second = await ImplementationTaskTestData.CreateTaskAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "Second");
                using var reorderResponse = await firstClient.PutAsJsonAsync(
                    $"{ImplementationTaskTestData.TasksRoute(projectId, proposalId)}/order",
                    new ReorderImplementationTasksRequest(
                        [second.Id.ToString(), first.Id.ToString()]));
                reorderResponse.EnsureSuccessStatusCode();
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            var persisted = await secondClient
                .GetFromJsonAsync<List<ImplementationTaskResponse>>(
                    ImplementationTaskTestData.TasksRoute(projectId, proposalId));

            Assert.NotNull(persisted);
            Assert.Collection(
                persisted,
                implementationTask =>
                {
                    Assert.Equal(second.Id, implementationTask.Id);
                    Assert.Equal(1, implementationTask.Position);
                },
                implementationTask =>
                {
                    Assert.Equal(first.Id, implementationTask.Id);
                    Assert.Equal(first.Description, implementationTask.Description);
                    Assert.Equal(2, implementationTask.Position);
                });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
