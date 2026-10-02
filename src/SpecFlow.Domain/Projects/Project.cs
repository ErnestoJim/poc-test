namespace SpecFlow.Domain.Projects;

public sealed class Project
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1_000;

    private Project()
    {
    }

    private Project(
        Guid id,
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Project Create(
        Guid id,
        string? name,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The project identifier cannot be empty.", nameof(id));
        }

        var errors = Validate(name, description);
        if (errors.Count > 0)
        {
            throw new ArgumentException("The project data is invalid.", nameof(name));
        }

        var trimmedName = name!.Trim();
        var trimmedDescription = NormalizeDescription(description);

        return new Project(
            id,
            trimmedName,
            NormalizeName(trimmedName),
            trimmedDescription,
            createdAtUtc.ToUniversalTime());
    }

    public static Dictionary<string, string[]> Validate(string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedName = name?.Trim();
        var trimmedDescription = NormalizeDescription(description);

        if (string.IsNullOrEmpty(trimmedName))
        {
            errors["name"] = ["The project name is required."];
        }
        else if (trimmedName.Length > MaxNameLength)
        {
            errors["name"] = [$"The project name cannot exceed {MaxNameLength} characters."];
        }

        if (trimmedDescription?.Length > MaxDescriptionLength)
        {
            errors["description"] =
                [$"The project description cannot exceed {MaxDescriptionLength} characters."];
        }

        return errors;
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static string? NormalizeDescription(string? description)
    {
        var trimmedDescription = description?.Trim();
        return string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
    }
}
