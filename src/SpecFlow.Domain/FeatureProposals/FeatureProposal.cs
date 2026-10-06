namespace SpecFlow.Domain.FeatureProposals;

public sealed class FeatureProposal
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4_000;
    public const int MaxRejectionReasonLength = 1_000;

    private FeatureProposal()
    {
    }

    private FeatureProposal(
        Guid id,
        Guid projectId,
        string title,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        Title = title;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public FeatureProposalStatus Status { get; private set; } = FeatureProposalStatus.Pending;

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public static FeatureProposal Create(
        Guid id,
        Guid projectId,
        string? title,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The feature proposal identifier cannot be empty.", nameof(id));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("The project identifier cannot be empty.", nameof(projectId));
        }

        var errors = Validate(title, description);
        if (errors.Count > 0)
        {
            throw new ArgumentException("The feature proposal data is invalid.", nameof(title));
        }

        var trimmedTitle = title!.Trim();
        var trimmedDescription = NormalizeDescription(description);
        var normalizedCreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(
            createdAtUtc.ToUnixTimeMilliseconds());

        return new FeatureProposal(
            id,
            projectId,
            trimmedTitle,
            trimmedDescription,
            normalizedCreatedAtUtc);
    }

    public void Accept(DateTimeOffset decidedAtUtc)
    {
        EnsurePending();

        Status = FeatureProposalStatus.Accepted;
        DecidedAtUtc = NormalizeTimestamp(decidedAtUtc);
        RejectionReason = null;
        Version++;
    }

    public void Reject(string? reason, DateTimeOffset decidedAtUtc)
    {
        var errors = ValidateRejectionReason(reason);
        if (errors.Count > 0)
        {
            throw new ArgumentException("The rejection reason is invalid.", nameof(reason));
        }

        EnsurePending();

        Status = FeatureProposalStatus.Rejected;
        DecidedAtUtc = NormalizeTimestamp(decidedAtUtc);
        RejectionReason = reason!.Trim();
        Version++;
    }

    public static Dictionary<string, string[]> Validate(string? title, string? description)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedTitle = title?.Trim();
        var trimmedDescription = NormalizeDescription(description);

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            errors["title"] = ["The feature proposal title is required."];
        }
        else if (trimmedTitle.Length > MaxTitleLength)
        {
            errors["title"] =
                [$"The feature proposal title cannot exceed {MaxTitleLength} characters."];
        }

        if (trimmedDescription?.Length > MaxDescriptionLength)
        {
            errors["description"] =
                [$"The feature proposal description cannot exceed {MaxDescriptionLength} characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateRejectionReason(string? reason)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedReason = reason?.Trim();

        if (string.IsNullOrEmpty(trimmedReason))
        {
            errors["reason"] = ["The rejection reason is required."];
        }
        else if (trimmedReason.Length > MaxRejectionReasonLength)
        {
            errors["reason"] =
                [$"The rejection reason cannot exceed {MaxRejectionReasonLength} characters."];
        }

        return errors;
    }

    private void EnsurePending()
    {
        if (Status != FeatureProposalStatus.Pending)
        {
            throw new FeatureProposalTransitionException(Status);
        }
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());

    private static string? NormalizeDescription(string? description)
    {
        var trimmedDescription = description?.Trim();
        return string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
    }
}
