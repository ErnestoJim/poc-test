using SpecFlow.Domain.AcceptanceCriteria;

namespace SpecFlow.Api.Contracts.AcceptanceCriteria;

public sealed record AcceptanceCriterionVerificationResponse(
    string Status,
    string? Evidence,
    DateTimeOffset? CheckedAtUtc)
{
    public static AcceptanceCriterionVerificationResponse FromDomain(
        AcceptanceCriterion criterion) =>
        new(
            criterion.VerificationStatus switch
            {
                AcceptanceCriterionVerificationStatus.Passed => "passed",
                AcceptanceCriterionVerificationStatus.Failed => "failed",
                _ => "unverified"
            },
            criterion.VerificationEvidence,
            criterion.CheckedAtUtc);
}
