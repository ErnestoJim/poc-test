namespace SpecFlow.Domain.ImplementationTasks;

public sealed class ImplementationTaskTransitionException(
    ImplementationTaskStatus currentStatus,
    ImplementationTaskStatus targetStatus)
    : InvalidOperationException(
        $"An implementation task in status '{currentStatus}' cannot transition to '{targetStatus}'.")
{
    public ImplementationTaskStatus CurrentStatus { get; } = currentStatus;

    public ImplementationTaskStatus TargetStatus { get; } = targetStatus;
}
