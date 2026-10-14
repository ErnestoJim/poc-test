namespace SpecFlow.Domain.Specifications;

public sealed class SpecificationVerificationIncompleteException()
    : InvalidOperationException("All acceptance criteria must pass before verification.");
