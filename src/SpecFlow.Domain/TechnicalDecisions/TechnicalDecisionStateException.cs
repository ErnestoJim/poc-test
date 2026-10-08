namespace SpecFlow.Domain.TechnicalDecisions;

public sealed class TechnicalDecisionStateException(
    TechnicalDecisionStatus currentStatus,
    string operation)
    : InvalidOperationException(
        $"A technical decision in status '{currentStatus}' cannot be {operation}.")
{
    public TechnicalDecisionStatus CurrentStatus { get; } = currentStatus;

    public string Operation { get; } = operation;
}
