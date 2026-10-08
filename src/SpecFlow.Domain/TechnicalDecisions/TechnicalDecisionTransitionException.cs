namespace SpecFlow.Domain.TechnicalDecisions;

public sealed class TechnicalDecisionTransitionException(
    TechnicalDecisionStatus currentStatus,
    TechnicalDecisionStatus targetStatus)
    : InvalidOperationException(
        $"A technical decision in status '{currentStatus}' cannot transition to '{targetStatus}'.")
{
    public TechnicalDecisionStatus CurrentStatus { get; } = currentStatus;

    public TechnicalDecisionStatus TargetStatus { get; } = targetStatus;
}
