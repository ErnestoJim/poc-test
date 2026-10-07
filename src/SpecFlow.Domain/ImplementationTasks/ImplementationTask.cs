namespace SpecFlow.Domain.ImplementationTasks;

public sealed class ImplementationTask
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 10_000;

    private ImplementationTask()
    {
    }

    private ImplementationTask(
        Guid id,
        Guid specificationId,
        string title,
        string? description,
        int position,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SpecificationId = specificationId;
        Title = title.Trim();
        NormalizedTitle = NormalizeTitle(title);
        Description = description;
        Position = position;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SpecificationId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public static ImplementationTask Create(
        Guid id,
        Guid specificationId,
        string? title,
        string? description,
        int position,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The implementation task identifier cannot be empty.",
                nameof(id));
        }

        if (specificationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The specification identifier cannot be empty.",
                nameof(specificationId));
        }

        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "The implementation task position must be greater than zero.");
        }

        var errors = Validate(title, description);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The implementation task data is invalid.",
                nameof(title));
        }

        return new ImplementationTask(
            id,
            specificationId,
            title!,
            description,
            position,
            NormalizeTimestamp(createdAtUtc));
    }

    public bool Update(
        string? title,
        string? description,
        DateTimeOffset updatedAtUtc)
    {
        var errors = Validate(title, description);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The implementation task data is invalid.",
                nameof(title));
        }

        var trimmedTitle = title!.Trim();
        if (string.Equals(Title, trimmedTitle, StringComparison.Ordinal) &&
            string.Equals(Description, description, StringComparison.Ordinal))
        {
            return false;
        }

        Title = trimmedTitle;
        NormalizedTitle = NormalizeTitle(trimmedTitle);
        Description = description;
        UpdatedAtUtc = NormalizeTimestamp(updatedAtUtc);
        Version++;
        return true;
    }

    public bool MoveTo(int position, DateTimeOffset updatedAtUtc)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "The implementation task position must be greater than zero.");
        }

        if (Position == position)
        {
            return false;
        }

        Position = position;
        UpdatedAtUtc = NormalizeTimestamp(updatedAtUtc);
        Version++;
        return true;
    }

    public static Dictionary<string, string[]> Validate(string? title, string? description)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedTitle = title?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            errors["title"] = ["The implementation task title is required."];
        }
        else if (trimmedTitle.Length > MaxTitleLength)
        {
            errors["title"] =
                [$"The implementation task title cannot exceed {MaxTitleLength} characters."];
        }

        if (description is not null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                errors["description"] =
                    ["The implementation task description cannot be empty when provided."];
            }
            else if (description.Length > MaxDescriptionLength)
            {
                errors["description"] =
                    [$"The implementation task description cannot exceed {MaxDescriptionLength} characters."];
            }
        }

        return errors;
    }

    public static string NormalizeTitle(string title) => title.Trim().ToUpperInvariant();

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
