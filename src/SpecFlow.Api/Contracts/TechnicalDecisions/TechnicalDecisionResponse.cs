using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Api.Contracts.TechnicalDecisions;

public sealed record TechnicalDecisionResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static TechnicalDecisionResponse FromDomain(TechnicalDecision decision) =>
        new(
            decision.Id,
            decision.ProjectId,
            decision.Title,
            decision.Content,
            decision.CreatedAtUtc,
            decision.UpdatedAtUtc);
}
