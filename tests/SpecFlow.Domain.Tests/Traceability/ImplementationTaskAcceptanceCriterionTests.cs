using SpecFlow.Domain.Traceability;

namespace SpecFlow.Domain.Tests.Traceability;

public sealed class ImplementationTaskAcceptanceCriterionTests
{
    [Fact]
    public void Create_WithValidIdentifiers_CreatesLink()
    {
        var taskId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();

        var link = ImplementationTaskAcceptanceCriterion.Create(taskId, criterionId);

        Assert.Equal(taskId, link.ImplementationTaskId);
        Assert.Equal(criterionId, link.AcceptanceCriterionId);
    }

    [Fact]
    public void Create_WithEmptyTaskIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ImplementationTaskAcceptanceCriterion.Create(Guid.Empty, Guid.NewGuid()));

        Assert.Equal("implementationTaskId", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyCriterionIdentifier_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ImplementationTaskAcceptanceCriterion.Create(Guid.NewGuid(), Guid.Empty));

        Assert.Equal("acceptanceCriterionId", exception.ParamName);
    }
}
