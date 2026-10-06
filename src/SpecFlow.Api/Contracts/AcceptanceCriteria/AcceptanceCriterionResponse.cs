using SpecFlow.Domain.AcceptanceCriteria;

namespace SpecFlow.Api.Contracts.AcceptanceCriteria;

public sealed record AcceptanceCriterionResponse(
    Guid Id,
    Guid SpecificationId,
    string Content,
    int Position,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static AcceptanceCriterionResponse FromDomain(AcceptanceCriterion criterion) =>
        new(
            criterion.Id,
            criterion.SpecificationId,
            criterion.Content,
            criterion.Position,
            criterion.CreatedAtUtc,
            criterion.UpdatedAtUtc);
}
