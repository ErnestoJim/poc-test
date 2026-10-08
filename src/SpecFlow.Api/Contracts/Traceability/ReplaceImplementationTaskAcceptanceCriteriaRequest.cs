namespace SpecFlow.Api.Contracts.Traceability;

public sealed record ReplaceImplementationTaskAcceptanceCriteriaRequest(
    IReadOnlyList<string?>? AcceptanceCriterionIds);
