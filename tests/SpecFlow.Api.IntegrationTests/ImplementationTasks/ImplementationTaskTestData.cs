using System.Net.Http.Json;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.Specifications;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

internal static class ImplementationTaskTestData
{
    public static Task<(
        ProjectResponse Project,
        FeatureProposalResponse Proposal,
        SpecificationResponse Specification)> CreateSpecificationContextAsync(
            HttpClient client,
            string projectName = "Test project") =>
        SpecificationTestData.CreateSpecificationContextAsync(client, projectName);

    public static async Task<ImplementationTaskResponse> CreateTaskAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        string title,
        string? description = null)
    {
        using var response = await client.PostAsJsonAsync(
            TasksRoute(projectId, proposalId),
            new SaveImplementationTaskRequest(title, description));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>()
            ?? throw new InvalidOperationException(
                "The create response did not contain an implementation task.");
    }

    public static string TasksRoute(Guid projectId, Guid proposalId) =>
        $"/api/projects/{projectId}/proposals/{proposalId}/specification/tasks";

    public static async Task<ImplementationTaskResponse> StartTaskAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        Guid taskId)
    {
        using var response = await client.PostAsync(
            $"{TasksRoute(projectId, proposalId)}/{taskId}/start",
            content: null);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>()
            ?? throw new InvalidOperationException(
                "The start response did not contain an implementation task.");
    }

    public static async Task<ImplementationTaskResponse> CompleteTaskAsync(
        HttpClient client,
        Guid projectId,
        Guid proposalId,
        Guid taskId)
    {
        using var response = await client.PostAsync(
            $"{TasksRoute(projectId, proposalId)}/{taskId}/complete",
            content: null);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImplementationTaskResponse>()
            ?? throw new InvalidOperationException(
                "The complete response did not contain an implementation task.");
    }
}
