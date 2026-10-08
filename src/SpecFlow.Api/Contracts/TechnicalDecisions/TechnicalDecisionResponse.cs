using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Api.Contracts.TechnicalDecisions;

public sealed record TechnicalDecisionResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Content,
    string Status,
    DateTimeOffset? DecidedAtUtc,
    string? RejectionReason,
    DateTimeOffset? SupersededAtUtc,
    Guid? SupersededByDecisionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static TechnicalDecisionResponse FromDomain(TechnicalDecision decision) =>
        new(
            decision.Id,
            decision.ProjectId,
            decision.Title,
            decision.Content,
            ToContractValue(decision.Status),
            decision.DecidedAtUtc,
            decision.RejectionReason,
            decision.SupersededAtUtc,
            decision.SupersededByDecisionId,
            decision.CreatedAtUtc,
            decision.UpdatedAtUtc);

    private static string ToContractValue(TechnicalDecisionStatus status) => status switch
    {
        TechnicalDecisionStatus.Draft => "draft",
        TechnicalDecisionStatus.Accepted => "accepted",
        TechnicalDecisionStatus.Rejected => "rejected",
        TechnicalDecisionStatus.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status,
            "Unknown technical decision status.")
    };
}
