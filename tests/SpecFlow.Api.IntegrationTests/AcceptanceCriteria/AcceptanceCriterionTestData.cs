using System.Net.Http.Json;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.Specifications;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

internal static class AcceptanceCriterionTestData
{
    public static Task<(
        ProjectResponse Project,
        FeatureProposalResponse Proposal,
        SpecificationResponse Specification)> CreateSpecificationContextAsync(
            HttpClient client,
            string projectName = "Test project") =>
        SpecificationTestData.CreateSpecificationContextAsync(client, projectName);

    public static async Task<AcceptanceCriterionResponse> CreateCriterionAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        string content)
    {
        using var response = await client.PostAsJsonAsync(
            CriteriaRoute(projectId, proposalId),
            new SaveAcceptanceCriterionRequest(content));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AcceptanceCriterionResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain an acceptance criterion.");
    }

    public static string CriteriaRoute(Guid projectId, Guid proposalId) =>
        $"/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria";
}
