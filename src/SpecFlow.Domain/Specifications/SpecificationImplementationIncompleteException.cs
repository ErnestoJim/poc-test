namespace SpecFlow.Domain.Specifications;

public sealed class SpecificationImplementationIncompleteException()
    : InvalidOperationException(
        "A specification requires completed tasks and complete traceability before implementation can be declared.");
