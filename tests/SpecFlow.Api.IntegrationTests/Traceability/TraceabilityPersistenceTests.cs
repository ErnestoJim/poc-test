using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityPersistenceTests
{
    [Fact]
    public async Task Links_RemainAvailableAfterApplicationRestart()
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
            Guid taskId;
            Guid criterionId;
            string entityTag;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var context = await TraceabilityTestData.CreateContextAsync(firstClient);
                projectId = context.Project.Id;
                proposalId = context.Proposal.Id;
                taskId = context.Task.Id;
                criterionId = context.Criterion.Id;
                var route = TraceabilityTestData.TaskLinksRoute(
                    projectId,
                    proposalId,
                    taskId);
                using var response = await TraceabilityTestData.ReplaceLinksAsync(
                    firstClient,
                    route,
                    await HttpPreconditionTestData.GetEntityTagAsync(firstClient, route),
                    criterionId);
                response.EnsureSuccessStatusCode();
                entityTag = HttpPreconditionTestData.GetRequiredEntityTag(response);
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync(
                TraceabilityTestData.TaskLinksRoute(projectId, proposalId, taskId));

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var links = await getResponse.Content
                .ReadFromJsonAsync<ImplementationTaskAcceptanceCriteriaResponse>();
            Assert.Equal([criterionId], links?.AcceptanceCriterionIds);
            Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
            var inverse = await secondClient
                .GetFromJsonAsync<AcceptanceCriterionImplementationTasksResponse>(
                    TraceabilityTestData.CriterionLinksRoute(
                        projectId,
                        proposalId,
                        criterionId));
            Assert.Equal([taskId], inverse?.ImplementationTaskIds);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
