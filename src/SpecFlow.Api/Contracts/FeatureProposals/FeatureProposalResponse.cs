using SpecFlow.Domain.FeatureProposals;

namespace SpecFlow.Api.Contracts.FeatureProposals;

public sealed record FeatureProposalResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset? DecidedAtUtc,
    string? RejectionReason,
    DateTimeOffset CreatedAtUtc)
{
    public static FeatureProposalResponse FromDomain(FeatureProposal proposal) =>
        new(
            proposal.Id,
            proposal.ProjectId,
            proposal.Title,
            proposal.Description,
            ToContractValue(proposal.Status),
            proposal.DecidedAtUtc,
            proposal.RejectionReason,
            proposal.CreatedAtUtc);

    private static string ToContractValue(FeatureProposalStatus status) => status switch
    {
        FeatureProposalStatus.Pending => "pending",
        FeatureProposalStatus.Accepted => "accepted",
        FeatureProposalStatus.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown proposal status.")
    };
}
