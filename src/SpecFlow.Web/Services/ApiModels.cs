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

public sealed record AcceptanceCriterionDto(
    Guid Id,
    Guid SpecificationId,
    string Content,
    int Position,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ImplementationTaskDto(
    Guid Id,
    Guid SpecificationId,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int Position,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TechnicalDecisionDto(
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
    DateTimeOffset UpdatedAtUtc);

public sealed record AcceptanceCriterionVerificationDto(
    string Status,
    string? Evidence,
    DateTimeOffset? CheckedAtUtc);

public sealed record TaskAcceptanceCriteriaDto(IReadOnlyList<Guid> AcceptanceCriterionIds);

public sealed record ApiError(int? StatusCode, string Message, IReadOnlySet<string> Fields)
{
    public bool HasField(string name) => Fields.Contains(name);

    // An error produced by the web itself rather than read from a Problem Details response.
    public static ApiError Local(int? statusCode, string message, params string[] fields) =>
        new(statusCode, message, new HashSet<string>(fields));
}

public sealed record ApiResult<T>(T? Value, ApiError? Error, string? EntityTag = null)
{
    public bool IsSuccess => Error is null;
}
