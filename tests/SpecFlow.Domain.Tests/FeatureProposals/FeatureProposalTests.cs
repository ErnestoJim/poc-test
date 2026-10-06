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
        Assert.Equal(FeatureProposalStatus.Pending, proposal.Status);
        Assert.Null(proposal.DecidedAtUtc);
        Assert.Null(proposal.RejectionReason);
        Assert.Equal(CreatedAtUtc, proposal.CreatedAtUtc);
    }

    [Fact]
    public void Accept_WhenPending_RecordsNormalizedDecision()
    {
        var proposal = CreateProposal();
        var timestamp = new DateTimeOffset(
            2026,
            10,
            6,
            11,
            15,
            0,
            TimeSpan.FromHours(2)).AddTicks(1_234);

        proposal.Accept(timestamp);

        Assert.Equal(FeatureProposalStatus.Accepted, proposal.Status);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 6, 9, 15, 0, TimeSpan.Zero),
            proposal.DecidedAtUtc);
        Assert.Null(proposal.RejectionReason);
        Assert.Equal(1, proposal.Version);
    }

    [Fact]
    public void Reject_WhenPending_NormalizesReasonAndDecision()
    {
        var proposal = CreateProposal();
        var decidedAtUtc = new DateTimeOffset(2026, 10, 6, 9, 20, 0, TimeSpan.Zero);

        proposal.Reject("  Not enough value  ", decidedAtUtc);

        Assert.Equal(FeatureProposalStatus.Rejected, proposal.Status);
        Assert.Equal(decidedAtUtc, proposal.DecidedAtUtc);
        Assert.Equal("Not enough value", proposal.RejectionReason);
        Assert.Equal(1, proposal.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRejectionReason_WithMissingReason_ReturnsReasonError(string? reason)
    {
        var errors = FeatureProposal.ValidateRejectionReason(reason);

        var error = Assert.Single(errors);
        Assert.Equal("reason", error.Key);
    }

    [Fact]
    public void ValidateRejectionReason_OverMaximumLength_ReturnsReasonError()
    {
        var reason = new string('a', FeatureProposal.MaxRejectionReasonLength + 1);

        var errors = FeatureProposal.ValidateRejectionReason(reason);

        Assert.Contains("reason", errors);
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_PreservesOriginalDecision()
    {
        var proposal = CreateProposal();
        var originalDecision = new DateTimeOffset(2026, 10, 6, 9, 15, 0, TimeSpan.Zero);
        proposal.Accept(originalDecision);

        Assert.Throws<FeatureProposalTransitionException>(
            () => proposal.Accept(originalDecision.AddHours(1)));

        Assert.Equal(FeatureProposalStatus.Accepted, proposal.Status);
        Assert.Equal(originalDecision, proposal.DecidedAtUtc);
        Assert.Equal(1, proposal.Version);
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_PreservesOriginalDecision()
    {
        var proposal = CreateProposal();
        var originalDecision = new DateTimeOffset(2026, 10, 6, 9, 20, 0, TimeSpan.Zero);
        proposal.Reject("Original reason", originalDecision);

        Assert.Throws<FeatureProposalTransitionException>(
            () => proposal.Reject("Replacement reason", originalDecision.AddHours(1)));

        Assert.Equal(FeatureProposalStatus.Rejected, proposal.Status);
        Assert.Equal(originalDecision, proposal.DecidedAtUtc);
        Assert.Equal("Original reason", proposal.RejectionReason);
        Assert.Equal(1, proposal.Version);
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

    private static FeatureProposal CreateProposal() =>
        FeatureProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proposal",
            null,
            CreatedAtUtc);
}
