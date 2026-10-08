using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Domain.Tests.TechnicalDecisions;

public sealed class TechnicalDecisionTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 8, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesTitleAndTimestampAndPreservesContent()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string Content = "  # Context\r\n\r\nDecision  \r\n";
        var timestamp = new DateTimeOffset(
            2026,
            10,
            8,
            10,
            30,
            0,
            TimeSpan.FromHours(2)).AddTicks(1_234);

        var decision = TechnicalDecision.Create(
            id,
            projectId,
            "  Use SQLite  ",
            Content,
            timestamp);

        Assert.Equal(id, decision.Id);
        Assert.Equal(projectId, decision.ProjectId);
        Assert.Equal("Use SQLite", decision.Title);
        Assert.Equal(Content, decision.Content);
        Assert.Equal(CreatedAtUtc, decision.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, decision.UpdatedAtUtc);
        Assert.Equal(0, decision.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingTitle_ReturnsTitleError(string? title)
    {
        var errors = TechnicalDecision.Validate(title, "# Decision");

        Assert.Contains("title", errors);
    }

    [Fact]
    public void Validate_WithTitleOverMaximumLength_ReturnsTitleError()
    {
        var errors = TechnicalDecision.Validate(
            new string('a', TechnicalDecision.MaxTitleLength + 1),
            "# Decision");

        Assert.Contains("title", errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Validate_WithMissingContent_ReturnsContentError(string? content)
    {
        var errors = TechnicalDecision.Validate("Decision", content);

        Assert.Contains("content", errors);
    }

    [Fact]
    public void Validate_WithContentOverMaximumLength_ReturnsContentError()
    {
        var errors = TechnicalDecision.Validate(
            "Decision",
            new string('a', TechnicalDecision.MaxContentLength + 1));

        Assert.Contains("content", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => TechnicalDecision.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "Decision",
            "# Decision",
            CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyProjectIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => TechnicalDecision.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "Decision",
            "# Decision",
            CreatedAtUtc));

        Assert.Equal("projectId", exception.ParamName);
    }

    [Fact]
    public void Update_WithDifferentData_ReplacesDataAndAdvancesTimestampAndVersion()
    {
        var decision = CreateDecision();
        var id = decision.Id;
        var projectId = decision.ProjectId;

        var changed = decision.Update(
            "  Updated decision  ",
            "  # Updated\r\n",
            CreatedAtUtc.AddHours(1).AddTicks(1_234));

        Assert.True(changed);
        Assert.Equal(id, decision.Id);
        Assert.Equal(projectId, decision.ProjectId);
        Assert.Equal("Updated decision", decision.Title);
        Assert.Equal("  # Updated\r\n", decision.Content);
        Assert.Equal(CreatedAtUtc, decision.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.UpdatedAtUtc);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void Update_WithEquivalentData_PreservesTimestampAndVersion()
    {
        var decision = CreateDecision();

        var changed = decision.Update(
            $"  {decision.Title}  ",
            decision.Content,
            CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, decision.UpdatedAtUtc);
        Assert.Equal(0, decision.Version);
    }

    private static TechnicalDecision CreateDecision() =>
        TechnicalDecision.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Decision",
            "# Decision",
            CreatedAtUtc);
}
