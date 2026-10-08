namespace SpecFlow.Domain.TechnicalDecisions;

public sealed class TechnicalDecision
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 50_000;

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

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
