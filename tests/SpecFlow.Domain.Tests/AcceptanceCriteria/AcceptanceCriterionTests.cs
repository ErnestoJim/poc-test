using SpecFlow.Domain.AcceptanceCriteria;

namespace SpecFlow.Domain.Tests.AcceptanceCriteria;

public sealed class AcceptanceCriterionTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 8, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_PreservesContentAndCalculatesHash()
    {
        var id = Guid.NewGuid();
        var specificationId = Guid.NewGuid();
        const string Content = "# Criterion\n";

        var criterion = AcceptanceCriterion.Create(
            id,
            specificationId,
            Content,
            1,
            CreatedAtUtc.AddTicks(1_234));

        Assert.Equal(id, criterion.Id);
        Assert.Equal(specificationId, criterion.SpecificationId);
        Assert.Equal(Content, criterion.Content);
        Assert.Equal(
            "74a7c8e7136fd3d7e3af472d120515db95d4fca4b2472822b373469d1d70ccde",
            criterion.ContentHash);
        Assert.Equal(1, criterion.Position);
        Assert.Equal(CreatedAtUtc, criterion.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, criterion.UpdatedAtUtc);
        Assert.Equal(0, criterion.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateContent_WithMissingContent_ReturnsContentError(string? content)
    {
        var errors = AcceptanceCriterion.ValidateContent(content);

        var error = Assert.Single(errors);
        Assert.Equal("content", error.Key);
    }

    [Fact]
    public void ValidateContent_OverMaximumLength_ReturnsContentError()
    {
        var content = new string('a', AcceptanceCriterion.MaxContentLength + 1);

        var errors = AcceptanceCriterion.ValidateContent(content);

        Assert.Contains("content", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AcceptanceCriterion.Create(
                Guid.Empty,
                Guid.NewGuid(),
                "Criterion",
                1,
                CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptySpecificationIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AcceptanceCriterion.Create(
                Guid.NewGuid(),
                Guid.Empty,
                "Criterion",
                1,
                CreatedAtUtc));

        Assert.Equal("specificationId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidPosition_ThrowsArgumentOutOfRangeException(int position)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => AcceptanceCriterion.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Criterion",
                position,
                CreatedAtUtc));

        Assert.Equal("position", exception.ParamName);
    }

    [Fact]
    public void UpdateContent_WithDifferentContent_UpdatesHashTimestampAndVersion()
    {
        var criterion = CreateCriterion();
        var previousHash = criterion.ContentHash;

        var changed = criterion.UpdateContent("Updated", CreatedAtUtc.AddHours(1));

        Assert.True(changed);
        Assert.Equal("Updated", criterion.Content);
        Assert.NotEqual(previousHash, criterion.ContentHash);
        Assert.Equal(CreatedAtUtc.AddHours(1), criterion.UpdatedAtUtc);
        Assert.Equal(1, criterion.Version);
    }

    [Fact]
    public void UpdateContent_WithIdenticalContent_PreservesTimestampAndVersion()
    {
        var criterion = CreateCriterion();

        var changed = criterion.UpdateContent(
            criterion.Content,
            CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, criterion.UpdatedAtUtc);
        Assert.Equal(0, criterion.Version);
    }

    [Fact]
    public void MoveTo_WithDifferentPosition_UpdatesTimestampAndVersion()
    {
        var criterion = CreateCriterion();

        var changed = criterion.MoveTo(2, CreatedAtUtc.AddHours(1));

        Assert.True(changed);
        Assert.Equal(2, criterion.Position);
        Assert.Equal(CreatedAtUtc.AddHours(1), criterion.UpdatedAtUtc);
        Assert.Equal(1, criterion.Version);
    }

    [Fact]
    public void MoveTo_WithSamePosition_PreservesTimestampAndVersion()
    {
        var criterion = CreateCriterion();

        var changed = criterion.MoveTo(1, CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, criterion.UpdatedAtUtc);
        Assert.Equal(0, criterion.Version);
    }

    [Fact]
    public void Verification_ChangesIndependentlyOfCriterionContent()
    {
        var criterion = CreateCriterion();

        Assert.Equal(AcceptanceCriterionVerificationStatus.Unverified,
            criterion.VerificationStatus);
        Assert.Null(criterion.VerificationEvidence);
        Assert.Null(criterion.CheckedAtUtc);
        Assert.True(criterion.SetVerification(
            AcceptanceCriterionVerificationStatus.Passed,
            null,
            CreatedAtUtc.AddHours(1).AddTicks(1_234)));

        Assert.Equal(AcceptanceCriterionVerificationStatus.Passed,
            criterion.VerificationStatus);
        Assert.Null(criterion.VerificationEvidence);
        Assert.Equal(CreatedAtUtc.AddHours(1), criterion.CheckedAtUtc);
        Assert.Equal(1, criterion.VerificationVersion);
        Assert.Equal(0, criterion.Version);
        Assert.Equal(CreatedAtUtc, criterion.UpdatedAtUtc);

        Assert.False(criterion.SetVerification(
            AcceptanceCriterionVerificationStatus.Passed,
            null,
            CreatedAtUtc.AddHours(2)));
        Assert.Equal(CreatedAtUtc.AddHours(1), criterion.CheckedAtUtc);

        Assert.True(criterion.SetVerification(
            AcceptanceCriterionVerificationStatus.Unverified,
            null,
            CreatedAtUtc.AddHours(3)));
        Assert.Null(criterion.CheckedAtUtc);
        Assert.Equal(2, criterion.VerificationVersion);
    }

    [Theory]
    [InlineData(AcceptanceCriterionVerificationStatus.Unverified, "evidence")]
    [InlineData(AcceptanceCriterionVerificationStatus.Passed, " ")]
    [InlineData(AcceptanceCriterionVerificationStatus.Failed, "")]
    public void ValidateVerification_RejectsIncompatibleEvidence(
        AcceptanceCriterionVerificationStatus status,
        string evidence)
    {
        Assert.Contains("evidence", AcceptanceCriterion.ValidateVerification(status, evidence));
    }

    [Fact]
    public void ValidateVerification_AllowsMissingEvidenceForBothResults()
    {
        Assert.Empty(AcceptanceCriterion.ValidateVerification(
            AcceptanceCriterionVerificationStatus.Passed,
            null));
        Assert.Empty(AcceptanceCriterion.ValidateVerification(
            AcceptanceCriterionVerificationStatus.Failed,
            null));
    }

    private static AcceptanceCriterion CreateCriterion() =>
        AcceptanceCriterion.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Criterion",
            1,
            CreatedAtUtc);
}
