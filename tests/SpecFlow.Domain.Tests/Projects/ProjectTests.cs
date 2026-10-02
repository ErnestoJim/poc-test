using SpecFlow.Domain.Projects;

namespace SpecFlow.Domain.Tests.Projects;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 2, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesValues()
    {
        var id = Guid.NewGuid();

        var project = Project.Create(
            id,
            "  SpecFlow  ",
            "  AI-assisted development POC  ",
            CreatedAtUtc);

        Assert.Equal(id, project.Id);
        Assert.Equal("SpecFlow", project.Name);
        Assert.Equal("SPECFLOW", project.NormalizedName);
        Assert.Equal("AI-assisted development POC", project.Description);
        Assert.Equal(CreatedAtUtc, project.CreatedAtUtc);
    }

    [Fact]
    public void Create_WithWhitespaceDescription_StoresNull()
    {
        var project = Project.Create(Guid.NewGuid(), "SpecFlow", "   ", CreatedAtUtc);

        Assert.Null(project.Description);
    }

    [Fact]
    public void Create_WithNonUtcTimestamp_ConvertsTimestampToUtc()
    {
        var localTimestamp = new DateTimeOffset(2026, 10, 2, 10, 30, 0, TimeSpan.FromHours(2));

        var project = Project.Create(Guid.NewGuid(), "SpecFlow", null, localTimestamp);

        Assert.Equal(CreatedAtUtc, project.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, project.CreatedAtUtc.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingName_ReturnsNameError(string? name)
    {
        var errors = Project.Validate(name, null);

        var error = Assert.Single(errors);
        Assert.Equal("name", error.Key);
        Assert.Equal("The project name is required.", Assert.Single(error.Value));
    }

    [Fact]
    public void Validate_WithNameOverMaximumLength_ReturnsNameError()
    {
        var name = new string('a', Project.MaxNameLength + 1);

        var errors = Project.Validate(name, null);

        Assert.Contains("name", errors);
    }

    [Fact]
    public void Validate_WithDescriptionOverMaximumLength_ReturnsDescriptionError()
    {
        var description = new string('a', Project.MaxDescriptionLength + 1);

        var errors = Project.Validate("SpecFlow", description);

        Assert.Contains("description", errors);
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Project.Create(Guid.Empty, "SpecFlow", null, CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }
}
