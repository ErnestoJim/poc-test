namespace SpecFlow.Web.Services;

public sealed record ProjectDto(Guid Id, string Name, string? Description, DateTimeOffset CreatedAtUtc);

public sealed record ProposalDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    Guid? SourceProposalId,
    string Status,
    DateTimeOffset? DecidedAtUtc,
    string? RejectionReason,
    DateTimeOffset CreatedAtUtc);

public sealed record SpecificationDto(
    Guid Id,
    Guid FeatureProposalId,
    string Content,
    string Status,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ImplementedAtUtc,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ApiError(int? StatusCode, string Message, IReadOnlySet<string> Fields)
{
    public bool HasField(string name) => Fields.Contains(name);
}

public sealed record ApiResult<T>(T? Value, ApiError? Error, string? EntityTag = null)
{
    public bool IsSuccess => Error is null;
}
