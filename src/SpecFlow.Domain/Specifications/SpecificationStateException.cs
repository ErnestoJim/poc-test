namespace SpecFlow.Domain.Specifications;

public sealed class SpecificationStateException(
    SpecificationStatus currentStatus,
    string operation)
    : InvalidOperationException(
        $"A specification in status '{currentStatus}' cannot be {operation}.")
{
    public SpecificationStatus CurrentStatus { get; } = currentStatus;

    public string Operation { get; } = operation;
}
