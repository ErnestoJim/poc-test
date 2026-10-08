namespace SpecFlow.Domain.Specifications;

public sealed class SpecificationTransitionException(
    SpecificationStatus currentStatus,
    SpecificationStatus targetStatus)
    : InvalidOperationException(
        $"A specification in status '{currentStatus}' cannot transition to '{targetStatus}'.")
{
    public SpecificationStatus CurrentStatus { get; } = currentStatus;

    public SpecificationStatus TargetStatus { get; } = targetStatus;
}
