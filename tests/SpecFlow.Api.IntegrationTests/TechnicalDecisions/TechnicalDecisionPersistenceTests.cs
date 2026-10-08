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

    [Fact]
    public async Task SupersededDecision_RemainsAvailableAfterApplicationRestart()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            Guid projectId;
            TechnicalDecisionResponse superseded;
            TechnicalDecisionResponse replacement;
            string supersededEntityTag;

            using (var firstFactory = new SpecFlowApiFactory(connectionString))
            using (var firstClient = firstFactory.CreateClient())
            {
                var project = await TechnicalDecisionTestData.CreateProjectAsync(firstClient);
                projectId = project.Id;
                var original = await TechnicalDecisionTestData.CreateDecisionAsync(
                    firstClient,
                    projectId,
                    "Original");
                var originalRoute = TechnicalDecisionTestData.DecisionRoute(
                    projectId,
                    original.Id);
                using var acceptOriginal = await HttpPreconditionTestData
                    .PostWithCurrentEntityTagAsync(
                        firstClient,
                        originalRoute,
                        TechnicalDecisionTestData.AcceptRoute(projectId, original.Id));
                acceptOriginal.EnsureSuccessStatusCode();
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));
                var replacementDraft = await TechnicalDecisionTestData.CreateDecisionAsync(
                    firstClient,
                    projectId,
                    "Replacement");
                var replacementRoute = TechnicalDecisionTestData.DecisionRoute(
                    projectId,
                    replacementDraft.Id);
                using var acceptReplacement = await HttpPreconditionTestData
                    .PostWithCurrentEntityTagAsync(
                        firstClient,
                        replacementRoute,
                        TechnicalDecisionTestData.AcceptRoute(
                            projectId,
                            replacementDraft.Id));
                acceptReplacement.EnsureSuccessStatusCode();
                replacement = await acceptReplacement.Content
                    .ReadFromJsonAsync<TechnicalDecisionResponse>()
                    ?? throw new InvalidOperationException(
                        "The accept response did not contain a replacement decision.");
                firstFactory.TimeProvider.Advance(TimeSpan.FromHours(1));
                using var supersedeResponse = await HttpPreconditionTestData
                    .SendAsJsonWithEntityTagAsync(
                        firstClient,
                        HttpMethod.Post,
                        TechnicalDecisionTestData.SupersedeRoute(projectId, original.Id),
                        new SupersedeTechnicalDecisionRequest(replacement.Id),
                        HttpPreconditionTestData.GetRequiredEntityTag(acceptOriginal));
                supersedeResponse.EnsureSuccessStatusCode();
                supersededEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(
                    supersedeResponse);
                superseded = await supersedeResponse.Content
                    .ReadFromJsonAsync<TechnicalDecisionResponse>()
                    ?? throw new InvalidOperationException(
                        "The supersede response did not contain a technical decision.");
            }

            using var secondFactory = new SpecFlowApiFactory(connectionString);
            using var secondClient = secondFactory.CreateClient();
            using var getSupersededResponse = await secondClient.GetAsync(
                TechnicalDecisionTestData.DecisionRoute(projectId, superseded.Id));

            Assert.Equal(HttpStatusCode.OK, getSupersededResponse.StatusCode);
            Assert.Equal(
                superseded,
                await getSupersededResponse.Content
                    .ReadFromJsonAsync<TechnicalDecisionResponse>());
            Assert.Equal(
                supersededEntityTag,
                HttpPreconditionTestData.GetRequiredEntityTag(getSupersededResponse));
            Assert.Equal(
                replacement,
                await secondClient.GetFromJsonAsync<TechnicalDecisionResponse>(
                    TechnicalDecisionTestData.DecisionRoute(projectId, replacement.Id)));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
