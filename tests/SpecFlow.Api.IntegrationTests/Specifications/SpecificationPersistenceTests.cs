using System.Net;
using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
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
            string updatedEntityTag;

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

                var route =
                    $"/api/projects/{projectId}/proposals/{proposalId}/specification";
                using var updateResponse = await HttpPreconditionTestData
                    .PutAsJsonWithCurrentEntityTagAsync(
                        firstClient,
                        route,
                        route,
                        new SaveSpecificationRequest("# Updated"));
                updateResponse.EnsureSuccessStatusCode();
                updatedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(updateResponse);
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
            Assert.Equal(
                updatedEntityTag,
                HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task VerifiedSpecification_RemainsAvailableAfterApplicationRestart()
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
            SpecificationResponse verified;
            string verifiedEntityTag;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var context = await SpecificationTestData
                    .CreateSpecificationContextAsync(firstClient);
                projectId = context.Project.Id;
                proposalId = context.Proposal.Id;
                var task = await ImplementationTaskTestData.CreateTaskAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    "Task");
                await SpecificationTestData.ApproveWithCoverageAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    task.Id);
                await ImplementationTaskTestData.StartTaskAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    task.Id);
                await ImplementationTaskTestData.CompleteTaskAsync(
                    firstClient,
                    projectId,
                    proposalId,
                    task.Id);
                var route = SpecificationTestData.SpecificationRoute(
                    projectId,
                    proposalId);
                using var implementedResponse = await HttpPreconditionTestData
                    .PostWithCurrentEntityTagAsync(
                        firstClient,
                        route,
                        $"{route}/mark-implemented");
                implementedResponse.EnsureSuccessStatusCode();
                using var verifiedResponse = await HttpPreconditionTestData
                    .PostWithCurrentEntityTagAsync(
                        firstClient,
                        route,
                        $"{route}/verify");
                verifiedResponse.EnsureSuccessStatusCode();
                verifiedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(
                    verifiedResponse);
                verified = await verifiedResponse.Content
                    .ReadFromJsonAsync<SpecificationResponse>()
                    ?? throw new InvalidOperationException(
                        "The verify response did not contain a specification.");
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getResponse = await secondClient.GetAsync(
                SpecificationTestData.SpecificationRoute(projectId, proposalId));

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Equal(
                verified,
                await getResponse.Content.ReadFromJsonAsync<SpecificationResponse>());
            Assert.Equal(
                verifiedEntityTag,
                HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
