using System.Net.Http.Json;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.FeatureProposals;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

internal static class TechnicalDecisionTestData
{
    public static string DecisionsRoute(Guid projectId) =>
        $"/api/projects/{projectId}/technical-decisions";

    public static string DecisionRoute(Guid projectId, Guid decisionId) =>
        $"{DecisionsRoute(projectId)}/{decisionId}";

    public static Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        string name = "Test project") =>
        FeatureProposalTestData.CreateProjectAsync(client, name);

    public static async Task<TechnicalDecisionResponse> CreateDecisionAsync(
        HttpClient client,
        Guid projectId,
        string title = "Use SQLite",
        string content = "# Decision\n\nUse SQLite.")
    {
        using var response = await client.PostAsJsonAsync(
            DecisionsRoute(projectId),
            new SaveTechnicalDecisionRequest(title, content));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain a technical decision.");
    }
}
