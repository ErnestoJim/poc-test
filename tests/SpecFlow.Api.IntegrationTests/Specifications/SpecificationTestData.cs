using System.Net.Http.Json;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.FeatureProposals;

namespace SpecFlow.Api.IntegrationTests.Specifications;

internal static class SpecificationTestData
{
    public static async Task<(ProjectResponse Project, FeatureProposalResponse Proposal)>
        CreateAcceptedProposalAsync(
            HttpClient client,
            string projectName = "Test project")
    {
        var project = await FeatureProposalTestData.CreateProjectAsync(client, projectName);
        var proposal = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Accepted proposal");
        proposal = await FeatureProposalTestData.AcceptProposalAsync(
            client,
            project.Id,
            proposal.Id);

        return (project, proposal);
    }

    public static async Task<SpecificationResponse> CreateSpecificationAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        string content = "# Specification")
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/proposals/{proposalId}/specification",
            new SaveSpecificationRequest(content));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SpecificationResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain a specification.");
    }
}
