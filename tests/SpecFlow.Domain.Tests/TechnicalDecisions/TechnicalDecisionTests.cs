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
        Assert.Equal(TechnicalDecisionStatus.Draft, decision.Status);
        Assert.Null(decision.DecidedAtUtc);
        Assert.Null(decision.RejectionReason);
        Assert.Null(decision.SupersededAtUtc);
        Assert.Null(decision.SupersededByDecisionId);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRejectionReason_WithMissingReason_ReturnsReasonError(string? reason)
    {
        var errors = TechnicalDecision.ValidateRejectionReason(reason);

        Assert.Contains("reason", errors);
    }

    [Fact]
    public void ValidateRejectionReason_WithReasonOverMaximumLength_ReturnsReasonError()
    {
        var errors = TechnicalDecision.ValidateRejectionReason(
            new string('a', TechnicalDecision.MaxRejectionReasonLength + 1));

        Assert.Contains("reason", errors);
    }

    [Fact]
    public void Accept_FromDraft_RecordsDecisionAndAdvancesVersion()
    {
        var decision = CreateDecision();

        decision.Accept(CreatedAtUtc.AddHours(1).AddTicks(1_234));

        Assert.Equal(TechnicalDecisionStatus.Accepted, decision.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.DecidedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.UpdatedAtUtc);
        Assert.Null(decision.RejectionReason);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void Reject_FromDraft_NormalizesReasonAndRecordsDecision()
    {
        var decision = CreateDecision();

        decision.Reject("  Too expensive.  ", CreatedAtUtc.AddHours(1).AddTicks(1_234));

        Assert.Equal(TechnicalDecisionStatus.Rejected, decision.Status);
        Assert.Equal("Too expensive.", decision.RejectionReason);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.DecidedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.UpdatedAtUtc);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void Accept_WhenAlreadyDecided_ThrowsAndPreservesState()
    {
        var decision = CreateDecision();
        decision.Reject("Not selected", CreatedAtUtc.AddHours(1));

        var exception = Assert.Throws<TechnicalDecisionTransitionException>(
            () => decision.Accept(CreatedAtUtc.AddHours(2)));

        Assert.Equal(TechnicalDecisionStatus.Rejected, exception.CurrentStatus);
        Assert.Equal(TechnicalDecisionStatus.Accepted, exception.TargetStatus);
        Assert.Equal(TechnicalDecisionStatus.Rejected, decision.Status);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void Update_WhenAccepted_ThrowsAndPreservesData()
    {
        var decision = CreateDecision();
        decision.Accept(CreatedAtUtc.AddHours(1));

        Assert.Throws<TechnicalDecisionStateException>(() => decision.Update(
            "Updated",
            "# Updated",
            CreatedAtUtc.AddHours(2)));

        Assert.Equal("Decision", decision.Title);
        Assert.Equal("# Decision", decision.Content);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void EnsureCanDelete_WhenDraft_DoesNotThrow()
    {
        var decision = CreateDecision();

        decision.EnsureCanDelete();
    }

    [Fact]
    public void EnsureCanDelete_WhenRejected_Throws()
    {
        var decision = CreateDecision();
        decision.Reject("Not selected", CreatedAtUtc.AddHours(1));

        Assert.Throws<TechnicalDecisionStateException>(decision.EnsureCanDelete);
    }

    [Fact]
    public void SupersedeWith_LaterAcceptedDecision_RecordsRelationAndPreservesDecisionDate()
    {
        var projectId = Guid.NewGuid();
        var decision = CreateDecision(projectId: projectId);
        var replacement = CreateDecision(projectId: projectId);
        decision.Accept(CreatedAtUtc.AddHours(1));
        replacement.Accept(CreatedAtUtc.AddHours(2));

        decision.SupersedeWith(replacement, CreatedAtUtc.AddHours(3).AddTicks(1_234));

        Assert.Equal(TechnicalDecisionStatus.Superseded, decision.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), decision.DecidedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(3), decision.SupersededAtUtc);
        Assert.Equal(replacement.Id, decision.SupersededByDecisionId);
        Assert.Equal(CreatedAtUtc.AddHours(3), decision.UpdatedAtUtc);
        Assert.Equal(2, decision.Version);
        Assert.Equal(TechnicalDecisionStatus.Accepted, replacement.Status);
        Assert.Equal(1, replacement.Version);
    }

    [Fact]
    public void SupersedeWith_SameDecisionDate_UsesIdentifierAsTieBreaker()
    {
        var projectId = Guid.NewGuid();
        var decision = CreateDecision(
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            projectId);
        var replacement = CreateDecision(
            Guid.Parse("00000002-0000-0000-0000-000000000000"),
            projectId);
        decision.Accept(CreatedAtUtc.AddHours(1));
        replacement.Accept(CreatedAtUtc.AddHours(1));

        decision.SupersedeWith(replacement, CreatedAtUtc.AddHours(2));

        Assert.Equal(TechnicalDecisionStatus.Superseded, decision.Status);
        Assert.Equal(replacement.Id, decision.SupersededByDecisionId);
    }

    [Fact]
    public void SupersedeWith_EarlierAcceptedDecision_ThrowsAndPreservesState()
    {
        var projectId = Guid.NewGuid();
        var decision = CreateDecision(projectId: projectId);
        var replacement = CreateDecision(projectId: projectId);
        replacement.Accept(CreatedAtUtc.AddHours(1));
        decision.Accept(CreatedAtUtc.AddHours(2));

        Assert.Throws<TechnicalDecisionTransitionException>(
            () => decision.SupersedeWith(replacement, CreatedAtUtc.AddHours(3)));

        Assert.Equal(TechnicalDecisionStatus.Accepted, decision.Status);
        Assert.Null(decision.SupersededAtUtc);
        Assert.Null(decision.SupersededByDecisionId);
        Assert.Equal(1, decision.Version);
    }

    [Fact]
    public void SupersedeWith_DecisionFromAnotherProject_Throws()
    {
        var decision = CreateDecision();
        var replacement = CreateDecision();
        decision.Accept(CreatedAtUtc.AddHours(1));
        replacement.Accept(CreatedAtUtc.AddHours(2));

        Assert.Throws<TechnicalDecisionTransitionException>(
            () => decision.SupersedeWith(replacement, CreatedAtUtc.AddHours(3)));
    }

    [Fact]
    public void SupersedeWith_DraftReplacement_Throws()
    {
        var projectId = Guid.NewGuid();
        var decision = CreateDecision(projectId: projectId);
        var replacement = CreateDecision(projectId: projectId);
        decision.Accept(CreatedAtUtc.AddHours(1));

        Assert.Throws<TechnicalDecisionTransitionException>(
            () => decision.SupersedeWith(replacement, CreatedAtUtc.AddHours(2)));
    }

    private static TechnicalDecision CreateDecision(
        Guid? id = null,
        Guid? projectId = null) =>
        TechnicalDecision.Create(
            id ?? Guid.NewGuid(),
            projectId ?? Guid.NewGuid(),
            "Decision",
            "# Decision",
            CreatedAtUtc);
}
