using SpecFlow.Domain.ImplementationTasks;

namespace SpecFlow.Api.Contracts.ImplementationTasks;

public sealed record ImplementationTaskResponse(
    Guid Id,
    Guid SpecificationId,
    string Title,
    string? Description,
    int Position,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static ImplementationTaskResponse FromDomain(ImplementationTask implementationTask) =>
        new(
            implementationTask.Id,
            implementationTask.SpecificationId,
            implementationTask.Title,
            implementationTask.Description,
            implementationTask.Position,
            implementationTask.CreatedAtUtc,
            implementationTask.UpdatedAtUtc);
}
