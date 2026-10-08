using System.Net.Http.Json;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Specifications;

internal static class SpecificationTestData
{
    public static async Task<(
        ProjectResponse Project,
        FeatureProposalResponse Proposal,
        SpecificationResponse Specification)> CreateSpecificationContextAsync(
            HttpClient client,
            string projectName = "Test project")
    {
        var (project, proposal) = await CreateAcceptedProposalAsync(client, projectName);
        var specification = await CreateSpecificationAsync(client, project.Id, proposal.Id);

        return (project, proposal, specification);
    }

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

    public static string SpecificationRoute(Guid projectId, Guid proposalId) =>
        $"/api/projects/{projectId}/proposals/{proposalId}/specification";

    public static async Task<SpecificationResponse> ApproveWithCoverageAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        Guid taskId)
    {
        using var createCriterionResponse = await client.PostAsJsonAsync(
            $"{SpecificationRoute(projectId, proposalId)}/acceptance-criteria",
            new SaveAcceptanceCriterionRequest("The implementation satisfies the specification."));
        createCriterionResponse.EnsureSuccessStatusCode();
        var criterion = await createCriterionResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain an acceptance criterion.");

        var linksRoute =
            $"{SpecificationRoute(projectId, proposalId)}/tasks/{taskId}/acceptance-criteria";
        var linksEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            linksRoute);
        using var linksResponse = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            linksRoute,
            new ReplaceImplementationTaskAcceptanceCriteriaRequest(
                [criterion.Id.ToString()]),
            linksEntityTag);
        linksResponse.EnsureSuccessStatusCode();

        return await ApproveAsync(client, projectId, proposalId);
    }

    public static async Task<SpecificationResponse> ApproveAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId)
    {
        var specificationRoute = SpecificationRoute(projectId, proposalId);
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            specificationRoute,
            $"{specificationRoute}/approve");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SpecificationResponse>()
            ?? throw new InvalidOperationException(
                "The approval response did not contain a specification.");
    }
}
