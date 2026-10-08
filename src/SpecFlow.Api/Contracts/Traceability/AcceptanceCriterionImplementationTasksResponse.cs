namespace SpecFlow.Api.Contracts.Traceability;

public sealed record AcceptanceCriterionImplementationTasksResponse(
    IReadOnlyList<Guid> ImplementationTaskIds);
