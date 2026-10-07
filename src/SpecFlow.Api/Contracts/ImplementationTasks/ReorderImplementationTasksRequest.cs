namespace SpecFlow.Api.Contracts.ImplementationTasks;

public sealed record ReorderImplementationTasksRequest(IReadOnlyList<string?>? TaskIds);
