namespace SpecFlow.Domain.Traceability;

public sealed class ImplementationTaskAcceptanceCriterion
{
    private ImplementationTaskAcceptanceCriterion()
    {
    }

    private ImplementationTaskAcceptanceCriterion(
        Guid implementationTaskId,
        Guid acceptanceCriterionId)
    {
        ImplementationTaskId = implementationTaskId;
        AcceptanceCriterionId = acceptanceCriterionId;
    }

    public Guid ImplementationTaskId { get; private set; }

    public Guid AcceptanceCriterionId { get; private set; }

    public static ImplementationTaskAcceptanceCriterion Create(
        Guid implementationTaskId,
        Guid acceptanceCriterionId)
    {
        if (implementationTaskId == Guid.Empty)
        {
            throw new ArgumentException(
                "The implementation task identifier cannot be empty.",
                nameof(implementationTaskId));
        }

        if (acceptanceCriterionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The acceptance criterion identifier cannot be empty.",
                nameof(acceptanceCriterionId));
        }

        return new ImplementationTaskAcceptanceCriterion(
            implementationTaskId,
            acceptanceCriterionId);
    }
}
