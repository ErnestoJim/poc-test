using SpecFlow.Domain.Specifications;

namespace SpecFlow.Api.Contracts.Specifications;

public sealed record SpecificationResponse(
    Guid Id,
    Guid FeatureProposalId,
    string Content,
    string Status,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ImplementedAtUtc,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static SpecificationResponse FromDomain(Specification specification) =>
        new(
            specification.Id,
            specification.FeatureProposalId,
            specification.Content,
            ToContractValue(specification.Status),
            specification.ApprovedAtUtc,
            specification.ImplementedAtUtc,
            specification.VerifiedAtUtc,
            specification.CreatedAtUtc,
            specification.UpdatedAtUtc);

    private static string ToContractValue(SpecificationStatus status) => status switch
    {
        SpecificationStatus.Draft => "draft",
        SpecificationStatus.Approved => "approved",
        SpecificationStatus.Implemented => "implemented",
        SpecificationStatus.Verified => "verified",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status,
            "Unknown specification status.")
    };
}
