namespace SpecFlow.Api.Contracts.Traceability;

public sealed record ImplementationTaskAcceptanceCriteriaResponse(
    IReadOnlyList<Guid> AcceptanceCriterionIds);
