using System.Net.Http.Json;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;

namespace SpecFlow.Api.IntegrationTests.Traceability;

internal static class TraceabilityTestData
{
    public static async Task<(
        ProjectResponse Project,
        FeatureProposalResponse Proposal,
        SpecificationResponse Specification,
        ImplementationTaskResponse Task,
        AcceptanceCriterionResponse Criterion)> CreateContextAsync(
            HttpClient client,
            string projectName = "Test project")
    {
        var context = await ImplementationTaskTestData.CreateSpecificationContextAsync(
            client,
            projectName);
        var task = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Implement behavior");
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "The behavior is observable.");

        return (
            context.Project,
            context.Proposal,
            context.Specification,
            task,
            criterion);
    }

    public static string TaskLinksRoute(
        Guid projectId,
        Guid proposalId,
        Guid taskId) =>
        $"{ImplementationTaskTestData.TasksRoute(projectId, proposalId)}/{taskId}/acceptance-criteria";

    public static string CriterionLinksRoute(
        Guid projectId,
        Guid proposalId,
        Guid criterionId) =>
        $"{AcceptanceCriterionTestData.CriteriaRoute(projectId, proposalId)}/{criterionId}/tasks";

    public static async Task<HttpResponseMessage> ReplaceLinksAsync(
        HttpClient client,
        string route,
        string entityTag,
        params Guid[] criterionIds) =>
        await Infrastructure.HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new ReplaceImplementationTaskAcceptanceCriteriaRequest(
                criterionIds.Select(identifier => (string?)identifier.ToString()).ToList()),
            entityTag);
}
