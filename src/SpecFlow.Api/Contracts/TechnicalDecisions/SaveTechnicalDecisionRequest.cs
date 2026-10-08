namespace SpecFlow.Api.Contracts.TechnicalDecisions;

public sealed record SaveTechnicalDecisionRequest(string? Title, string? Content);
