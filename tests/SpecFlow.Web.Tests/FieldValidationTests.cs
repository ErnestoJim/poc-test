using SpecFlow.Web.Services;

namespace SpecFlow.Web.Tests;

public sealed class FieldValidationTests
{
    [Theory]
    [InlineData("name", "true")]
    [InlineData("title", "false")]
    public void AriaInvalid_DependsOnTheField(string field, string expected) =>
        Assert.Equal(expected, FieldValidation.AriaInvalid(ErrorFor("name"), field));

    [Fact]
    public void AriaInvalid_WithoutFieldErrors_IsFalse()
    {
        Assert.Equal("false", FieldValidation.AriaInvalid(null, "name"));
        Assert.Equal("false", FieldValidation.AriaInvalid(ErrorFor(), "name"));
    }

    [Fact]
    public void DescribedBy_ReferencesErrorOnlyWhenTheFieldIsInvalid()
    {
        Assert.Equal("project-name-hint", FieldValidation.DescribedBy(null, "name", "project-name"));
        Assert.Equal("project-name-hint", FieldValidation.DescribedBy(ErrorFor("title"), "name", "project-name"));
        Assert.Equal(
            "project-name-hint project-name-error",
            FieldValidation.DescribedBy(ErrorFor("name"), "name", "project-name"));
    }

    private static ApiError ErrorFor(params string[] fields) =>
        new(400, "Revisa los datos introducidos.", new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase));
}
