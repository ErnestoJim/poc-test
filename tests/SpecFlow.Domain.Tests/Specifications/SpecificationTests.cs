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
        Assert.Equal(SpecificationStatus.Draft, specification.Status);
        Assert.Null(specification.ApprovedAtUtc);
        Assert.Null(specification.ImplementedAtUtc);
        Assert.Null(specification.VerifiedAtUtc);
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

    [Fact]
    public void Approve_WhenPlanIsReady_TransitionsAndNormalizesTimestamp()
    {
        var specification = CreateSpecification();
        var timestamp = CreatedAtUtc.AddHours(1).AddTicks(1_234);

        specification.Approve(
            hasAcceptanceCriteria: true,
            hasImplementationTasks: true,
            allAcceptanceCriteriaCovered: true,
            timestamp);

        Assert.Equal(SpecificationStatus.Approved, specification.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), specification.ApprovedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), specification.UpdatedAtUtc);
        Assert.Null(specification.ImplementedAtUtc);
        Assert.Null(specification.VerifiedAtUtc);
        Assert.Equal(1, specification.Version);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Approve_WhenPlanIsNotReady_ThrowsAndPreservesDraft(
        bool hasAcceptanceCriteria,
        bool hasImplementationTasks,
        bool allAcceptanceCriteriaCovered)
    {
        var specification = CreateSpecification();

        Assert.Throws<SpecificationReadinessException>(
            () => specification.Approve(
                hasAcceptanceCriteria,
                hasImplementationTasks,
                allAcceptanceCriteriaCovered,
                CreatedAtUtc.AddHours(1)));

        Assert.Equal(SpecificationStatus.Draft, specification.Status);
        Assert.Null(specification.ApprovedAtUtc);
        Assert.Equal(CreatedAtUtc, specification.UpdatedAtUtc);
        Assert.Equal(0, specification.Version);
    }

    [Fact]
    public void UpdateContent_AfterApproval_ThrowsAndPreservesContent()
    {
        var specification = CreateApprovedSpecification();

        var exception = Assert.Throws<SpecificationStateException>(
            () => specification.UpdateContent("# Changed", CreatedAtUtc.AddHours(2)));

        Assert.Equal(SpecificationStatus.Approved, exception.CurrentStatus);
        Assert.Equal("# Specification", specification.Content);
        Assert.Equal(1, specification.Version);
    }

    [Fact]
    public void MarkImplemented_WhenAllWorkIsComplete_TransitionsAndPreservesApproval()
    {
        var specification = CreateApprovedSpecification();

        specification.MarkImplemented(
            allImplementationTasksCompleted: true,
            allAcceptanceCriteriaCovered: true,
            CreatedAtUtc.AddHours(2).AddTicks(1_234));

        Assert.Equal(SpecificationStatus.Implemented, specification.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), specification.ApprovedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(2), specification.ImplementedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(2), specification.UpdatedAtUtc);
        Assert.Null(specification.VerifiedAtUtc);
        Assert.Equal(2, specification.Version);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MarkImplemented_WhenWorkIsIncomplete_ThrowsAndPreservesApproval(
        bool allImplementationTasksCompleted,
        bool allAcceptanceCriteriaCovered)
    {
        var specification = CreateApprovedSpecification();

        Assert.Throws<SpecificationImplementationIncompleteException>(
            () => specification.MarkImplemented(
                allImplementationTasksCompleted,
                allAcceptanceCriteriaCovered,
                CreatedAtUtc.AddHours(2)));

        Assert.Equal(SpecificationStatus.Approved, specification.Status);
        Assert.Null(specification.ImplementedAtUtc);
        Assert.Equal(1, specification.Version);
    }

    [Fact]
    public void Verify_AfterImplementation_TransitionsAndPreservesEarlierDates()
    {
        var specification = CreateImplementedSpecification();

        specification.Verify(true, CreatedAtUtc.AddHours(3).AddTicks(1_234));

        Assert.Equal(SpecificationStatus.Verified, specification.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), specification.ApprovedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(2), specification.ImplementedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(3), specification.VerifiedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(3), specification.UpdatedAtUtc);
        Assert.Equal(3, specification.Version);
    }

    [Fact]
    public void Verify_WithUnpassedCriterion_ThrowsAndPreservesImplementation()
    {
        var specification = CreateImplementedSpecification();

        Assert.Throws<SpecificationVerificationIncompleteException>(
            () => specification.Verify(false, CreatedAtUtc.AddHours(3)));

        Assert.Equal(SpecificationStatus.Implemented, specification.Status);
        Assert.Null(specification.VerifiedAtUtc);
        Assert.Equal(2, specification.Version);
    }

    [Fact]
    public void Verify_BeforeImplementation_ThrowsAndPreservesStatus()
    {
        var specification = CreateApprovedSpecification();

        var exception = Assert.Throws<SpecificationTransitionException>(
            () => specification.Verify(true, CreatedAtUtc.AddHours(2)));

        Assert.Equal(SpecificationStatus.Approved, exception.CurrentStatus);
        Assert.Equal(SpecificationStatus.Verified, exception.TargetStatus);
        Assert.Null(specification.VerifiedAtUtc);
        Assert.Equal(1, specification.Version);
    }

    [Fact]
    public void EnsureApprovedForImplementation_OnlyAllowsApproved()
    {
        var draft = CreateSpecification();
        var approved = CreateApprovedSpecification();
        var implemented = CreateImplementedSpecification();

        Assert.Throws<SpecificationStateException>(
            draft.EnsureApprovedForImplementation);
        approved.EnsureApprovedForImplementation();
        Assert.Throws<SpecificationStateException>(
            implemented.EnsureApprovedForImplementation);
    }

    private static Specification CreateSpecification() =>
        Specification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "# Specification",
            CreatedAtUtc);

    private static Specification CreateApprovedSpecification()
    {
        var specification = CreateSpecification();
        specification.Approve(
            hasAcceptanceCriteria: true,
            hasImplementationTasks: true,
            allAcceptanceCriteriaCovered: true,
            CreatedAtUtc.AddHours(1));
        return specification;
    }

    private static Specification CreateImplementedSpecification()
    {
        var specification = CreateApprovedSpecification();
        specification.MarkImplemented(
            allImplementationTasksCompleted: true,
            allAcceptanceCriteriaCovered: true,
            CreatedAtUtc.AddHours(2));
        return specification;
    }
}
