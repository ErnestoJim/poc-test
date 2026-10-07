using SpecFlow.Domain.ImplementationTasks;

namespace SpecFlow.Domain.Tests.ImplementationTasks;

public sealed class ImplementationTaskTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 8, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesTitleAndPreservesDescription()
    {
        var id = Guid.NewGuid();
        var specificationId = Guid.NewGuid();
        const string Description = "  **Markdown**\r\n";

        var implementationTask = ImplementationTask.Create(
            id,
            specificationId,
            "  Implement API  ",
            Description,
            1,
            CreatedAtUtc.AddTicks(1_234));

        Assert.Equal(id, implementationTask.Id);
        Assert.Equal(specificationId, implementationTask.SpecificationId);
        Assert.Equal("Implement API", implementationTask.Title);
        Assert.Equal("IMPLEMENT API", implementationTask.NormalizedTitle);
        Assert.Equal(Description, implementationTask.Description);
        Assert.Equal(1, implementationTask.Position);
        Assert.Equal(CreatedAtUtc, implementationTask.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, implementationTask.UpdatedAtUtc);
        Assert.Equal(0, implementationTask.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingTitle_ReturnsTitleError(string? title)
    {
        var errors = ImplementationTask.Validate(title, null);

        Assert.Contains("title", errors);
    }

    [Fact]
    public void Validate_WithTitleOverMaximumLengthAfterTrimming_ReturnsTitleError()
    {
        var title = $" {new string('a', ImplementationTask.MaxTitleLength + 1)} ";

        var errors = ImplementationTask.Validate(title, null);

        Assert.Contains("title", errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyDescription_ReturnsDescriptionError(string description)
    {
        var errors = ImplementationTask.Validate("Task", description);

        Assert.Contains("description", errors);
    }

    [Fact]
    public void Validate_WithDescriptionOverMaximumLength_ReturnsDescriptionError()
    {
        var description = new string('a', ImplementationTask.MaxDescriptionLength + 1);

        var errors = ImplementationTask.Validate("Task", description);

        Assert.Contains("description", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ImplementationTask.Create(
                Guid.Empty,
                Guid.NewGuid(),
                "Task",
                null,
                1,
                CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptySpecificationIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ImplementationTask.Create(
                Guid.NewGuid(),
                Guid.Empty,
                "Task",
                null,
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
            () => ImplementationTask.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Task",
                null,
                position,
                CreatedAtUtc));

        Assert.Equal("position", exception.ParamName);
    }

    [Fact]
    public void Update_WithDifferentData_UpdatesValuesTimestampAndVersion()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.Update(
            " updated task ",
            "Updated **description**",
            CreatedAtUtc.AddHours(1).AddTicks(1_234));

        Assert.True(changed);
        Assert.Equal("updated task", implementationTask.Title);
        Assert.Equal("UPDATED TASK", implementationTask.NormalizedTitle);
        Assert.Equal("Updated **description**", implementationTask.Description);
        Assert.Equal(CreatedAtUtc.AddHours(1), implementationTask.UpdatedAtUtc);
        Assert.Equal(1, implementationTask.Version);
    }

    [Fact]
    public void Update_WithNullDescription_ClearsDescription()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.Update(
            implementationTask.Title,
            null,
            CreatedAtUtc.AddHours(1));

        Assert.True(changed);
        Assert.Null(implementationTask.Description);
    }

    [Fact]
    public void Update_WithIdenticalNormalizedInput_PreservesTimestampAndVersion()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.Update(
            $"  {implementationTask.Title}  ",
            implementationTask.Description,
            CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, implementationTask.UpdatedAtUtc);
        Assert.Equal(0, implementationTask.Version);
    }

    [Fact]
    public void Update_WithDifferentCasing_UpdatesVisibleTitleAndTimestamp()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.Update(
            implementationTask.Title.ToUpperInvariant(),
            implementationTask.Description,
            CreatedAtUtc.AddHours(1));

        Assert.True(changed);
        Assert.Equal("TASK", implementationTask.Title);
        Assert.Equal(CreatedAtUtc.AddHours(1), implementationTask.UpdatedAtUtc);
    }

    [Fact]
    public void MoveTo_WithDifferentPosition_UpdatesTimestampAndVersion()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.MoveTo(2, CreatedAtUtc.AddHours(1));

        Assert.True(changed);
        Assert.Equal(2, implementationTask.Position);
        Assert.Equal(CreatedAtUtc.AddHours(1), implementationTask.UpdatedAtUtc);
        Assert.Equal(1, implementationTask.Version);
    }

    [Fact]
    public void MoveTo_WithSamePosition_PreservesTimestampAndVersion()
    {
        var implementationTask = CreateTask();

        var changed = implementationTask.MoveTo(1, CreatedAtUtc.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, implementationTask.UpdatedAtUtc);
        Assert.Equal(0, implementationTask.Version);
    }

    private static ImplementationTask CreateTask() =>
        ImplementationTask.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Task",
            "Description",
            1,
            CreatedAtUtc);
}
