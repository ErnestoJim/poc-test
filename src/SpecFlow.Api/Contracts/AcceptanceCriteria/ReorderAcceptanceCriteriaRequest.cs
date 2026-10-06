namespace SpecFlow.Api.Contracts.AcceptanceCriteria;

public sealed record ReorderAcceptanceCriteriaRequest(IReadOnlyList<string?>? CriterionIds);
