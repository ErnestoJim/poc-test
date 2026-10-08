namespace SpecFlow.Domain.TechnicalDecisions;

public sealed class TechnicalDecision
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 50_000;
    public const int MaxRejectionReasonLength = 1_000;

    private TechnicalDecision()
    {
    }

    private TechnicalDecision(
        Guid id,
        Guid projectId,
        string title,
        string content,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        Title = title.Trim();
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public TechnicalDecisionStatus Status { get; private set; } =
        TechnicalDecisionStatus.Draft;

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? SupersededAtUtc { get; private set; }

    public Guid? SupersededByDecisionId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public static TechnicalDecision Create(
        Guid id,
        Guid projectId,
        string? title,
        string? content,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The technical decision identifier cannot be empty.",
                nameof(id));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "The project identifier cannot be empty.",
                nameof(projectId));
        }

        var errors = Validate(title, content);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The technical decision data is invalid.",
                nameof(title));
        }

        return new TechnicalDecision(
            id,
            projectId,
            title!,
            content!,
            NormalizeTimestamp(createdAtUtc));
    }

    public bool Update(
        string? title,
        string? content,
        DateTimeOffset updatedAtUtc)
    {
        var errors = Validate(title, content);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The technical decision data is invalid.",
                nameof(title));
        }

        if (Status != TechnicalDecisionStatus.Draft)
        {
            throw new TechnicalDecisionStateException(Status, "edited");
        }

        var trimmedTitle = title!.Trim();
        if (string.Equals(Title, trimmedTitle, StringComparison.Ordinal) &&
            string.Equals(Content, content, StringComparison.Ordinal))
        {
            return false;
        }

        Title = trimmedTitle;
        Content = content!;
        UpdatedAtUtc = NormalizeTimestamp(updatedAtUtc);
        Version++;
        return true;
    }

    public void Accept(DateTimeOffset decidedAtUtc)
    {
        EnsureTransitionFromDraft(TechnicalDecisionStatus.Accepted);

        var normalizedTimestamp = NormalizeTimestamp(decidedAtUtc);
        Status = TechnicalDecisionStatus.Accepted;
        DecidedAtUtc = normalizedTimestamp;
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void Reject(string? reason, DateTimeOffset decidedAtUtc)
    {
        var errors = ValidateRejectionReason(reason);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The rejection reason is invalid.",
                nameof(reason));
        }

        EnsureTransitionFromDraft(TechnicalDecisionStatus.Rejected);

        var normalizedTimestamp = NormalizeTimestamp(decidedAtUtc);
        Status = TechnicalDecisionStatus.Rejected;
        DecidedAtUtc = normalizedTimestamp;
        RejectionReason = reason!.Trim();
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void SupersedeWith(
        TechnicalDecision replacement,
        DateTimeOffset supersededAtUtc)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (Status != TechnicalDecisionStatus.Accepted ||
            replacement.Status != TechnicalDecisionStatus.Accepted ||
            replacement.ProjectId != ProjectId ||
            replacement.Id == Id ||
            !IsLaterInAdoptionOrder(replacement))
        {
            throw new TechnicalDecisionTransitionException(
                Status,
                TechnicalDecisionStatus.Superseded);
        }

        var normalizedTimestamp = NormalizeTimestamp(supersededAtUtc);
        Status = TechnicalDecisionStatus.Superseded;
        SupersededAtUtc = normalizedTimestamp;
        SupersededByDecisionId = replacement.Id;
        UpdatedAtUtc = normalizedTimestamp;
        Version++;
    }

    public void EnsureCanDelete()
    {
        if (Status != TechnicalDecisionStatus.Draft)
        {
            throw new TechnicalDecisionStateException(Status, "deleted");
        }
    }

    public static Dictionary<string, string[]> Validate(string? title, string? content)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            errors["title"] = ["The technical decision title is required."];
        }
        else if (trimmedTitle.Length > MaxTitleLength)
        {
            errors["title"] =
                [$"The technical decision title cannot exceed {MaxTitleLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errors["content"] = ["The technical decision content is required."];
        }
        else if (content.Length > MaxContentLength)
        {
            errors["content"] =
                [$"The technical decision content cannot exceed {MaxContentLength} characters."];
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

    private void EnsureTransitionFromDraft(TechnicalDecisionStatus targetStatus)
    {
        if (Status != TechnicalDecisionStatus.Draft)
        {
            throw new TechnicalDecisionTransitionException(Status, targetStatus);
        }
    }

    private bool IsLaterInAdoptionOrder(TechnicalDecision replacement)
    {
        if (!DecidedAtUtc.HasValue || !replacement.DecidedAtUtc.HasValue)
        {
            return false;
        }

        var timestampComparison = replacement.DecidedAtUtc.Value.CompareTo(DecidedAtUtc.Value);
        return timestampComparison > 0 ||
            (timestampComparison == 0 && replacement.Id.CompareTo(Id) > 0);
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
