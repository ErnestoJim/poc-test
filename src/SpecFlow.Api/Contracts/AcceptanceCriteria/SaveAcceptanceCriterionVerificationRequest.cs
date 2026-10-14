namespace SpecFlow.Api.Contracts.AcceptanceCriteria;

public sealed record SaveAcceptanceCriterionVerificationRequest(
    string? Status,
    string? Evidence);
