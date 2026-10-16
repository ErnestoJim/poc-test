using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

// Every client operation turns a lost connection into an error with no HTTP status, so the UI never resends blindly.
public sealed class NetworkFailureClientTests
{
    private static readonly Guid ItemId = Guid.Parse("5f0d1c2e-7a43-4b8e-9d61-2c3b4a5e6f70");

    private static readonly Dictionary<string, Func<SpecFlowApiClient, Task<ApiError?>>> Operations = new()
    {
        ["AcceptProposalAsync"] = client => ErrorOf(client.AcceptProposalAsync(ProjectId, ProposalId)),
        ["CreateSpecificationAsync"] = client => ErrorOf(client.CreateSpecificationAsync(ProjectId, ProposalId, "# Documento")),
        ["UpdateSpecificationAsync"] = client => ErrorOf(client.UpdateSpecificationAsync(ProjectId, ProposalId, "# Documento", "\"v1\"")),
        ["ListImplementationTasksAsync"] = client => ErrorOf(client.ListImplementationTasksAsync(ProjectId, ProposalId)),
        ["CreateImplementationTaskAsync"] = client => ErrorOf(client.CreateImplementationTaskAsync(ProjectId, ProposalId, "Tarea", null)),
        ["UpdateImplementationTaskAsync"] = client => ErrorOf(client.UpdateImplementationTaskAsync(ProjectId, ProposalId, ItemId, "Tarea", null, "\"v1\"")),
        ["DeleteImplementationTaskAsync"] = client => ErrorOf(client.DeleteImplementationTaskAsync(ProjectId, ProposalId, ItemId, "\"v1\"")),
        ["ReorderImplementationTasksAsync"] = client => ErrorOf(client.ReorderImplementationTasksAsync(ProjectId, ProposalId, [ItemId], "\"v1\"")),
        ["StartImplementationTaskAsync"] = client => ErrorOf(client.StartImplementationTaskAsync(ProjectId, ProposalId, ItemId, "\"v1\"")),
        ["CompleteImplementationTaskAsync"] = client => ErrorOf(client.CompleteImplementationTaskAsync(ProjectId, ProposalId, ItemId, "\"v1\"")),
        ["GetTaskAcceptanceCriteriaAsync"] = client => ErrorOf(client.GetTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, ItemId)),
        ["ReplaceTaskAcceptanceCriteriaAsync"] = client => ErrorOf(client.ReplaceTaskAcceptanceCriteriaAsync(ProjectId, ProposalId, ItemId, [], "\"v1\"")),
        ["ApproveSpecificationAsync"] = client => ErrorOf(client.ApproveSpecificationAsync(ProjectId, ProposalId, "\"v1\"")),
        ["MarkSpecificationImplementedAsync"] = client => ErrorOf(client.MarkSpecificationImplementedAsync(ProjectId, ProposalId, "\"v1\"")),
        ["GetAcceptanceCriterionVerificationAsync"] = client => ErrorOf(client.GetAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, ItemId)),
        ["SaveAcceptanceCriterionVerificationAsync"] = client => ErrorOf(client.SaveAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, ItemId, "failed", null, "\"v1\"")),
        ["VerifySpecificationAsync"] = client => ErrorOf(client.VerifySpecificationAsync(ProjectId, ProposalId, "\"v1\"")),
        ["ListTechnicalDecisionsAsync"] = client => ErrorOf(client.ListTechnicalDecisionsAsync(ProjectId)),
        ["CreateTechnicalDecisionAsync"] = client => ErrorOf(client.CreateTechnicalDecisionAsync(ProjectId, "T", "C")),
        ["GetTechnicalDecisionAsync"] = client => ErrorOf(client.GetTechnicalDecisionAsync(ProjectId, ItemId)),
        ["UpdateTechnicalDecisionAsync"] = client => ErrorOf(client.UpdateTechnicalDecisionAsync(ProjectId, ItemId, "T", "C", "\"v1\"")),
        ["AcceptTechnicalDecisionAsync"] = client => ErrorOf(client.AcceptTechnicalDecisionAsync(ProjectId, ItemId, "\"v1\"")),
        ["RejectTechnicalDecisionAsync"] = client => ErrorOf(client.RejectTechnicalDecisionAsync(ProjectId, ItemId, "R", "\"v1\"")),
        ["SupersedeTechnicalDecisionAsync"] = client => ErrorOf(client.SupersedeTechnicalDecisionAsync(ProjectId, ItemId, ItemId, "\"v1\"")),
        ["DeleteTechnicalDecisionAsync"] = client => ErrorOf(client.DeleteTechnicalDecisionAsync(ProjectId, ItemId, "\"v1\"")),
        ["ListAcceptanceCriteriaAsync"] = client => ErrorOf(client.ListAcceptanceCriteriaAsync(ProjectId, ProposalId)),
        ["CreateAcceptanceCriterionAsync"] = client => ErrorOf(client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, "Criterio")),
        ["UpdateAcceptanceCriterionAsync"] = client => ErrorOf(client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, ItemId, "Criterio", "\"v1\"")),
        ["DeleteAcceptanceCriterionAsync"] = client => ErrorOf(client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, ItemId, "\"v1\"")),
        ["ReorderAcceptanceCriteriaAsync"] = client => ErrorOf(client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [ItemId], "\"v1\"")),
        ["ListProjectsAsync"] = client => ErrorOf(client.ListProjectsAsync()),
    };

    public static TheoryData<string> OperationNames => [.. Operations.Keys];

    [Theory]
    [MemberData(nameof(OperationNames))]
    public async Task NetworkFailureHasUnknownStatusAndAMessage(string operation)
    {
        var error = await Operations[operation](CreateClient(new FailingHandler()));

        Assert.NotNull(error);
        Assert.Null(error.StatusCode);
        Assert.NotEmpty(error.Message);
    }

    private static async Task<ApiError?> ErrorOf<T>(Task<ApiResult<T>> call) => (await call).Error;
}
