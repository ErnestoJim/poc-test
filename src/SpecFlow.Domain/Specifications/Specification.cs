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

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int AcceptanceCriteriaVersion { get; private set; }

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

        if (string.Equals(Content, content, StringComparison.Ordinal))
        {
            return false;
        }

        Content = content!;
        UpdatedAtUtc = NormalizeTimestamp(updatedAtUtc);
        return true;
    }

    public void MarkAcceptanceCriteriaChanged()
    {
        AcceptanceCriteriaVersion++;
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

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
