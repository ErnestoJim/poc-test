using SpecFlow.Domain.Specifications;

namespace SpecFlow.Api.Contracts.Specifications;

public sealed record SpecificationResponse(
    Guid Id,
    Guid FeatureProposalId,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static SpecificationResponse FromDomain(Specification specification) =>
        new(
            specification.Id,
            specification.FeatureProposalId,
            specification.Content,
            specification.CreatedAtUtc,
            specification.UpdatedAtUtc);
}
