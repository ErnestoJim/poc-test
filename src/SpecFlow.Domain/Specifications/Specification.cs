namespace SpecFlow.Domain.Specifications;

public sealed class Specification
{
    public const int MaxContentLength = 100_000;

    private Specification()
    {
    }

    private Specification(
        Guid id,
        Guid featureProposalId,
        string content,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        FeatureProposalId = featureProposalId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid FeatureProposalId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public SpecificationStatus Status { get; private set; } = SpecificationStatus.Draft;

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset? ImplementedAtUtc { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public int AcceptanceCriteriaVersion { get; private set; }

    public int ImplementationTasksVersion { get; private set; }

    public static Specification Create(
        Guid id,
        Guid featureProposalId,
        string? content,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The specification identifier cannot be empty.", nameof(id));
        }

        if (featureProposalId == Guid.Empty)
        {
            throw new ArgumentException(
                "The feature proposal identifier cannot be empty.",
                nameof(featureProposalId));
        }

        var errors = ValidateContent(content);
        if (errors.Count > 0)
        {
            throw new ArgumentException("The specification content is invalid.", nameof(content));
        }

        return new Specification(
            id,
            featureProposalId,
            content!,
            NormalizeTimestamp(createdAtUtc));
    }

    public bool UpdateContent(string? content, DateTimeOffset updatedAtUtc)
    {
        var errors = ValidateContent(content);
        if (errors.Count > 0)
        {
            throw new ArgumentException("The specification content is invalid.", nameof(content));
        }

        EnsureEditable();

        if (string.Equals(Content, content, StringComparison.Ordinal))
        {
            return false;
        }

        Content = content!;
        UpdatedAtUtc = NormalizeTimestamp(updatedAtUtc);
        Version++;
        return true;
    }

    public void Approve(
        bool hasAcceptanceCriteria,
        bool hasImplementationTasks,
        bool allAcceptanceCriteriaCovered,
        DateTimeOffset approvedAtUtc)
    {
        EnsureTransition(SpecificationStatus.Draft, SpecificationStatus.Approved);

        if (!hasAcceptanceCriteria ||
            !hasImplementationTasks ||
            !allAcceptanceCriteriaCovered)
        {
            throw new SpecificationReadinessException();
        }

        var normalizedTimestamp = NormalizeTimestamp(approvedAtUtc);
        Status = SpecificationStatus.Approved;
        ApprovedAtUtc = normalizedTimestamp;
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void MarkImplemented(
        bool allImplementationTasksCompleted,
        bool allAcceptanceCriteriaCovered,
        DateTimeOffset implementedAtUtc)
    {
        EnsureTransition(SpecificationStatus.Approved, SpecificationStatus.Implemented);

        if (!allImplementationTasksCompleted || !allAcceptanceCriteriaCovered)
        {
            throw new SpecificationImplementationIncompleteException();
        }

        var normalizedTimestamp = NormalizeTimestamp(implementedAtUtc);
        Status = SpecificationStatus.Implemented;
        ImplementedAtUtc = normalizedTimestamp;
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void Verify(bool allAcceptanceCriteriaPassed, DateTimeOffset verifiedAtUtc)
    {
        EnsureTransition(SpecificationStatus.Implemented, SpecificationStatus.Verified);

        if (!allAcceptanceCriteriaPassed)
        {
            throw new SpecificationVerificationIncompleteException();
        }

        var normalizedTimestamp = NormalizeTimestamp(verifiedAtUtc);
        Status = SpecificationStatus.Verified;
        VerifiedAtUtc = normalizedTimestamp;
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void EnsureEditable()
    {
        if (Status != SpecificationStatus.Draft)
        {
            throw new SpecificationStateException(Status, "edited");
        }
    }

    public void EnsureApprovedForImplementation()
    {
        if (Status != SpecificationStatus.Approved)
        {
            throw new SpecificationStateException(Status, "implemented");
        }
    }

    public static Dictionary<string, string[]> ValidateContent(string? content)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(content))
        {
            errors["content"] = ["The specification content is required."];
        }
        else if (content.Length > MaxContentLength)
        {
            errors["content"] =
                [$"The specification content cannot exceed {MaxContentLength} characters."];
        }

        return errors;
    }

    private void EnsureTransition(
        SpecificationStatus requiredStatus,
        SpecificationStatus targetStatus)
    {
        if (Status != requiredStatus)
        {
            throw new SpecificationTransitionException(Status, targetStatus);
        }
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
