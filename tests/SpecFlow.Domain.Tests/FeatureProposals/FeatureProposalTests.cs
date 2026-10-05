using SpecFlow.Domain.FeatureProposals;

namespace SpecFlow.Domain.Tests.FeatureProposals;

public sealed class FeatureProposalTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 5, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesValues()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var proposal = FeatureProposal.Create(
            id,
            projectId,
            "  Add acceptance criteria  ",
            "  Allow verifiable criteria  ",
            CreatedAtUtc);

        Assert.Equal(id, proposal.Id);
        Assert.Equal(projectId, proposal.ProjectId);
        Assert.Equal("Add acceptance criteria", proposal.Title);
        Assert.Equal("Allow verifiable criteria", proposal.Description);
        Assert.Equal(CreatedAtUtc, proposal.CreatedAtUtc);
    }

    [Fact]
    public void Create_WithWhitespaceDescription_StoresNull()
    {
        var proposal = FeatureProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proposal",
            "   ",
            CreatedAtUtc);

        Assert.Null(proposal.Description);
    }

    [Fact]
    public void Create_WithNonUtcSubMillisecondTimestamp_NormalizesTimestamp()
    {
        var timestamp = new DateTimeOffset(
            2026,
            10,
            5,
            12,
            30,
            0,
            TimeSpan.FromHours(2)).AddTicks(1_234);

        var proposal = FeatureProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proposal",
            null,
            timestamp);

        Assert.Equal(CreatedAtUtc, proposal.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, proposal.CreatedAtUtc.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingTitle_ReturnsTitleError(string? title)
    {
        var errors = FeatureProposal.Validate(title, null);

        var error = Assert.Single(errors);
        Assert.Equal("title", error.Key);
    }

    [Fact]
    public void Validate_WithTitleOverMaximumLength_ReturnsTitleError()
    {
        var title = new string('a', FeatureProposal.MaxTitleLength + 1);

        var errors = FeatureProposal.Validate(title, null);

        Assert.Contains("title", errors);
    }

    [Fact]
    public void Validate_WithDescriptionOverMaximumLength_ReturnsDescriptionError()
    {
        var description = new string('a', FeatureProposal.MaxDescriptionLength + 1);

        var errors = FeatureProposal.Validate("Proposal", description);

        Assert.Contains("description", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FeatureProposal.Create(
                Guid.Empty,
                Guid.NewGuid(),
                "Proposal",
                null,
                CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyProjectIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FeatureProposal.Create(
                Guid.NewGuid(),
                Guid.Empty,
                "Proposal",
                null,
                CreatedAtUtc));

        Assert.Equal("projectId", exception.ParamName);
    }

    [Fact]
    public void Create_WithDuplicateTitles_CreatesDistinctProposals()
    {
        var projectId = Guid.NewGuid();

        var first = FeatureProposal.Create(
            Guid.NewGuid(), projectId, "Proposal", null, CreatedAtUtc);
        var second = FeatureProposal.Create(
            Guid.NewGuid(), projectId, "Proposal", null, CreatedAtUtc);

        Assert.Equal(first.Title, second.Title);
        Assert.NotEqual(first.Id, second.Id);
    }
}
