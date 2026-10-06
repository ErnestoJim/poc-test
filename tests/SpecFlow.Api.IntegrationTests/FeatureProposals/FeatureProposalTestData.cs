using System.Net.Http.Json;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.Projects;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

internal static class FeatureProposalTestData
{
    public static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        string name = "Test project")
    {
        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest(name, null));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProjectResponse>()
            ?? throw new InvalidOperationException("The create response did not contain a project.");
    }

    public static async Task<FeatureProposalResponse> CreateProposalAsync(
        HttpClient client,
        Guid projectId,
        string title)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/proposals",
            new CreateFeatureProposalRequest(title, null));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FeatureProposalResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain a feature proposal.");
    }

    public static async Task<FeatureProposalResponse> AcceptProposalAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId)
    {
        using var response = await client.PostAsync(
            $"/api/projects/{projectId}/proposals/{proposalId}/accept",
            content: null);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FeatureProposalResponse>()
            ?? throw new InvalidOperationException(
                "The accept response did not contain a feature proposal.");
    }

    public static async Task<FeatureProposalResponse> RejectProposalAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        string reason)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/proposals/{proposalId}/reject",
            new RejectFeatureProposalRequest(reason));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FeatureProposalResponse>()
            ?? throw new InvalidOperationException(
                "The reject response did not contain a feature proposal.");
    }
}
