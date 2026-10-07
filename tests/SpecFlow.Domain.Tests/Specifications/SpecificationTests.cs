using SpecFlow.Domain.Specifications;

namespace SpecFlow.Domain.Tests.Specifications;

public sealed class SpecificationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 7, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_PreservesContentAndNormalizesTimestamps()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var content = "  # Specification\r\n\r\nContent  \r\n";
        var timestamp = new DateTimeOffset(
            2026,
            10,
            7,
            10,
            30,
            0,
            TimeSpan.FromHours(2)).AddTicks(1_234);

        var specification = Specification.Create(id, proposalId, content, timestamp);

        Assert.Equal(id, specification.Id);
        Assert.Equal(proposalId, specification.FeatureProposalId);
        Assert.Equal(content, specification.Content);
        Assert.Equal(CreatedAtUtc, specification.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, specification.UpdatedAtUtc);
        Assert.Equal(0, specification.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void ValidateContent_WithMissingContent_ReturnsContentError(string? content)
    {
        var errors = Specification.ValidateContent(content);

        var error = Assert.Single(errors);
        Assert.Equal("content", error.Key);
    }

    [Fact]
    public void ValidateContent_OverMaximumLength_ReturnsContentError()
    {
        var content = new string('a', Specification.MaxContentLength + 1);

        var errors = Specification.ValidateContent(content);

        Assert.Contains("content", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Specification.Create(
                Guid.Empty,
                Guid.NewGuid(),
                "# Specification",
                CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyProposalIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Specification.Create(
                Guid.NewGuid(),
                Guid.Empty,
                "# Specification",
                CreatedAtUtc));

        Assert.Equal("featureProposalId", exception.ParamName);
    }

    [Fact]
    public void UpdateContent_WithDifferentContent_ReplacesContentAndUpdatesTimestamp()
    {
        var specification = CreateSpecification();
        var id = specification.Id;
        var proposalId = specification.FeatureProposalId;
        var timestamp = CreatedAtUtc.AddHours(1).AddTicks(1_234);

        var changed = specification.UpdateContent("# Updated\n", timestamp);

        Assert.True(changed);
        Assert.Equal(id, specification.Id);
        Assert.Equal(proposalId, specification.FeatureProposalId);
        Assert.Equal("# Updated\n", specification.Content);
        Assert.Equal(CreatedAtUtc, specification.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), specification.UpdatedAtUtc);
        Assert.Equal(1, specification.Version);
    }

    [Fact]
    public void UpdateContent_WithIdenticalContent_PreservesTimestamp()
    {
        var specification = CreateSpecification();

        var changed = specification.UpdateContent(
            specification.Content,
            CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, specification.UpdatedAtUtc);
        Assert.Equal(0, specification.Version);
    }

    private static Specification CreateSpecification() =>
        Specification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "# Specification",
            CreatedAtUtc);
}
