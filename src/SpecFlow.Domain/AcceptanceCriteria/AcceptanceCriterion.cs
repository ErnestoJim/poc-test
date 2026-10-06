using System.Security.Cryptography;
using System.Text;

namespace SpecFlow.Domain.AcceptanceCriteria;

public sealed class AcceptanceCriterion
{
    public const int MaxContentLength = 10_000;
    public const int ContentHashLength = 64;

    private AcceptanceCriterion()
    {
    }

    private AcceptanceCriterion(
        Guid id,
        Guid specificationId,
        string content,
        int position,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SpecificationId = specificationId;
        Content = content;
        ContentHash = CalculateContentHash(content!);
        Position = position;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SpecificationId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public static AcceptanceCriterion Create(
        Guid id,
        Guid specificationId,
        string? content,
        int position,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The acceptance criterion identifier cannot be empty.",
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
                "The acceptance criterion position must be greater than zero.");
        }

        var errors = ValidateContent(content);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The acceptance criterion content is invalid.",
                nameof(content));
        }

        return new AcceptanceCriterion(
            id,
            specificationId,
            content!,
            position,
            NormalizeTimestamp(createdAtUtc));
    }

    public bool UpdateContent(string? content, DateTimeOffset updatedAtUtc)
    {
        var errors = ValidateContent(content);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The acceptance criterion content is invalid.",
                nameof(content));
        }

        if (string.Equals(Content, content, StringComparison.Ordinal))
        {
            return false;
        }

        Content = content!;
        ContentHash = CalculateContentHash(content!);
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
                "The acceptance criterion position must be greater than zero.");
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

    public static Dictionary<string, string[]> ValidateContent(string? content)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(content))
        {
            errors["content"] = ["The acceptance criterion content is required."];
        }
        else if (content.Length > MaxContentLength)
        {
            errors["content"] =
                [$"The acceptance criterion content cannot exceed {MaxContentLength} characters."];
        }

        return errors;
    }

    public static string CalculateContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
}
