namespace SpecFlow.Domain.Specifications;

public sealed class SpecificationReadinessException()
    : InvalidOperationException(
        "A specification requires criteria, tasks, and complete criterion coverage before approval.");
