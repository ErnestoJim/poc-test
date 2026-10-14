using System.Net.Http.Json;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

internal static class AcceptanceCriterionVerificationTestData
{
    public static string VerificationRoute(Guid projectId, Guid proposalId, Guid criterionId) =>
        $"{AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId)}/{criterionId}/verification";

    public static async Task PassAllCriteriaAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId)
    {
        var criteria = await client.GetFromJsonAsync<AcceptanceCriterionResponse[]>(
            AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId))
            ?? throw new InvalidOperationException("The criteria response was empty.");

        foreach (var criterion in criteria)
        {
            var route = VerificationRoute(projectId, proposalId, criterion.Id);
            using var response = await HttpPreconditionTestData
                .PutAsJsonWithCurrentEntityTagAsync(
                    client,
                    route,
                    route,
                    new SaveAcceptanceCriterionVerificationRequest("passed", null));
            response.EnsureSuccessStatusCode();
        }
    }
}
