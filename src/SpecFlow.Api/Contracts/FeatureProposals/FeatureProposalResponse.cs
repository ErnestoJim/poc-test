using SpecFlow.Domain.FeatureProposals;

namespace SpecFlow.Api.Contracts.FeatureProposals;

public sealed record FeatureProposalResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    DateTimeOffset CreatedAtUtc)
{
    public static FeatureProposalResponse FromDomain(FeatureProposal proposal) =>
        new(
            proposal.Id,
            proposal.ProjectId,
            proposal.Title,
            proposal.Description,
            proposal.CreatedAtUtc);
}
