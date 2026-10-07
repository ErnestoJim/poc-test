using SpecFlow.Domain.ImplementationTasks;

namespace SpecFlow.Api.Contracts.ImplementationTasks;

public sealed record ImplementationTaskResponse(
    Guid Id,
    Guid SpecificationId,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
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
            ToContractValue(implementationTask.Status),
            implementationTask.StartedAtUtc,
            implementationTask.CompletedAtUtc,
            implementationTask.Position,
            implementationTask.CreatedAtUtc,
            implementationTask.UpdatedAtUtc);

    private static string ToContractValue(ImplementationTaskStatus status) => status switch
    {
        ImplementationTaskStatus.Pending => "pending",
        ImplementationTaskStatus.InProgress => "in_progress",
        ImplementationTaskStatus.Completed => "completed",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status,
            "Unknown implementation task status.")
    };
}
