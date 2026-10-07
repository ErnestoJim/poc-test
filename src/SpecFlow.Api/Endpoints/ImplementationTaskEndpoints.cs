using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class ImplementationTaskEndpoints
{
    public static RouteGroupBuilder MapImplementationTaskEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks")
            .WithTags("Implementation Tasks");

        group.MapPost("/", CreateImplementationTaskAsync)
            .WithName("CreateImplementationTask")
            .Accepts<SaveImplementationTaskRequest>("application/json")
            .Produces<ImplementationTaskResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/", ListImplementationTasksAsync)
            .WithName("ListImplementationTasks")
            .Produces<IReadOnlyList<ImplementationTaskResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/order", ReorderImplementationTasksAsync)
            .WithName("ReorderImplementationTasks")
            .Accepts<ReorderImplementationTasksRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{taskId}", GetImplementationTaskAsync)
            .WithName("GetImplementationTask")
            .Produces<ImplementationTaskResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{taskId}", UpdateImplementationTaskAsync)
            .WithName("UpdateImplementationTask")
            .Accepts<SaveImplementationTaskRequest>("application/json")
            .Produces<ImplementationTaskResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapDelete("/{taskId}", DeleteImplementationTaskAsync)
            .WithName("DeleteImplementationTask")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{taskId}/start", StartImplementationTaskAsync)
            .WithName("StartImplementationTask")
            .Produces<ImplementationTaskResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{taskId}/complete", CompleteImplementationTaskAsync)
            .WithName("CompleteImplementationTask")
            .Produces<ImplementationTaskResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> CreateImplementationTaskAsync(
        string projectId,
        string proposalId,
        SaveImplementationTaskRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseParentIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = ImplementationTask.Validate(
            request.Title,
            request.Description);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var normalizedTitle = ImplementationTask.NormalizeTitle(request.Title!);
        var duplicateExists = await dbContext.ImplementationTasks
            .AnyAsync(
                implementationTask =>
                    implementationTask.SpecificationId == specification.Id &&
                    implementationTask.NormalizedTitle == normalizedTitle,
                cancellationToken);

        if (duplicateExists)
        {
            return ImplementationTaskAlreadyExistsProblem();
        }

        var lastPosition = await dbContext.ImplementationTasks
            .Where(implementationTask =>
                implementationTask.SpecificationId == specification.Id)
            .Select(implementationTask => (int?)implementationTask.Position)
            .MaxAsync(cancellationToken) ?? 0;
        var implementationTask = ImplementationTask.Create(
            Guid.NewGuid(),
            specification.Id,
            request.Title,
            request.Description,
            lastPosition + 1,
            timeProvider.GetUtcNow());
        dbContext.ImplementationTasks.Add(implementationTask);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await TryAdvanceCollectionVersionAsync(
                    specification,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return await ResolveCreateConflictAsync(
                    specification.Id,
                    normalizedTitle,
                    dbContext,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            return await ResolveCreateConflictAsync(
                specification.Id,
                normalizedTitle,
                dbContext,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return await ResolveCreateConflictAsync(
                specification.Id,
                normalizedTitle,
                dbContext,
                cancellationToken);
        }

        return Results.CreatedAtRoute(
            "GetImplementationTask",
            new
            {
                projectId = identifiers.ProjectId,
                proposalId = identifiers.ProposalId,
                taskId = implementationTask.Id
            },
            ImplementationTaskResponse.FromDomain(implementationTask));
    }

    private static async Task<IResult> GetImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var implementationTask = await dbContext.ImplementationTasks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingTask =>
                    existingTask.SpecificationId == context.Specification!.Id &&
                    existingTask.Id == identifiers.TaskId,
                cancellationToken);

        return implementationTask is null
            ? ImplementationTaskNotFoundProblem(identifiers.TaskId)
            : Results.Ok(ImplementationTaskResponse.FromDomain(implementationTask));
    }

    private static async Task<IResult> ListImplementationTasksAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseParentIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var implementationTasks = await dbContext.ImplementationTasks
            .AsNoTracking()
            .Where(implementationTask =>
                implementationTask.SpecificationId == context.Specification!.Id)
            .OrderBy(implementationTask => implementationTask.Position)
            .ThenBy(implementationTask => implementationTask.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(
            implementationTasks.Select(ImplementationTaskResponse.FromDomain));
    }

    private static async Task<IResult> UpdateImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SaveImplementationTaskRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = ImplementationTask.Validate(
            request.Title,
            request.Description);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specificationId = context.Specification!.Id;
        var implementationTask = await dbContext.ImplementationTasks
            .SingleOrDefaultAsync(
                existingTask =>
                    existingTask.SpecificationId == specificationId &&
                    existingTask.Id == identifiers.TaskId,
                cancellationToken);

        if (implementationTask is null)
        {
            return ImplementationTaskNotFoundProblem(identifiers.TaskId);
        }

        var normalizedTitle = ImplementationTask.NormalizeTitle(request.Title!);
        var duplicateExists = await dbContext.ImplementationTasks
            .AsNoTracking()
            .AnyAsync(
                existingTask =>
                    existingTask.SpecificationId == specificationId &&
                    existingTask.Id != implementationTask.Id &&
                    existingTask.NormalizedTitle == normalizedTitle,
                cancellationToken);

        if (duplicateExists)
        {
            return ImplementationTaskAlreadyExistsProblem();
        }

        var changed = implementationTask.Update(
            request.Title,
            request.Description,
            timeProvider.GetUtcNow());
        if (!changed)
        {
            return Results.Ok(ImplementationTaskResponse.FromDomain(implementationTask));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ImplementationTaskUpdateConflictProblem(identifiers.TaskId);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return ImplementationTaskAlreadyExistsProblem();
        }

        return Results.Ok(ImplementationTaskResponse.FromDomain(implementationTask));
    }

    private static async Task<IResult> ReorderImplementationTasksAsync(
        string projectId,
        string proposalId,
        ReorderImplementationTasksRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseParentIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var order = ParseTaskOrder(request.TaskIds);
        if (order.Error is not null)
        {
            return order.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var implementationTasks = await dbContext.ImplementationTasks
            .Where(implementationTask =>
                implementationTask.SpecificationId == specification.Id)
            .OrderBy(implementationTask => implementationTask.Position)
            .ToListAsync(cancellationToken);
        var requestedIds = order.TaskIds!;

        if (implementationTasks.Count != requestedIds.Count ||
            !implementationTasks
                .Select(implementationTask => implementationTask.Id)
                .ToHashSet()
                .SetEquals(requestedIds))
        {
            return ImplementationTasksCollectionChangedProblem();
        }

        var tasksById = implementationTasks.ToDictionary(
            implementationTask => implementationTask.Id);
        var movedTasks = requestedIds
            .Select((id, index) =>
                (ImplementationTask: tasksById[id], Position: index + 1))
            .Where(item => item.ImplementationTask.Position != item.Position)
            .ToList();
        var timestamp = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await TryAdvanceCollectionVersionAsync(
                    specification,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return ImplementationTasksCollectionChangedProblem();
            }

            if (movedTasks.Count > 0)
            {
                var temporaryPosition = implementationTasks.Count + 1;
                foreach (var item in movedTasks)
                {
                    item.ImplementationTask.MoveTo(temporaryPosition++, timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (var item in movedTasks)
                {
                    item.ImplementationTask.MoveTo(item.Position, timestamp);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            return ImplementationTasksCollectionChangedProblem();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return ImplementationTasksCollectionChangedProblem();
        }

        return Results.NoContent();
    }

    private static Task<IResult> StartImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionImplementationTaskAsync(
            projectId,
            proposalId,
            taskId,
            dbContext,
            timeProvider,
            static (implementationTask, timestamp) => implementationTask.Start(timestamp),
            cancellationToken);

    private static Task<IResult> CompleteImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionImplementationTaskAsync(
            projectId,
            proposalId,
            taskId,
            dbContext,
            timeProvider,
            static (implementationTask, timestamp) => implementationTask.Complete(timestamp),
            cancellationToken);

    private static async Task<IResult> TransitionImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        Action<ImplementationTask, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var implementationTask = await dbContext.ImplementationTasks
            .SingleOrDefaultAsync(
                existingTask =>
                    existingTask.SpecificationId == context.Specification!.Id &&
                    existingTask.Id == identifiers.TaskId,
                cancellationToken);

        if (implementationTask is null)
        {
            return ImplementationTaskNotFoundProblem(identifiers.TaskId);
        }

        try
        {
            transition(implementationTask, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ImplementationTaskTransitionException)
        {
            return ImplementationTaskTransitionConflictProblem(identifiers.TaskId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ImplementationTaskTransitionConflictProblem(identifiers.TaskId);
        }

        return Results.Ok(ImplementationTaskResponse.FromDomain(implementationTask));
    }

    private static async Task<IResult> DeleteImplementationTaskAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var implementationTask = await dbContext.ImplementationTasks
            .SingleOrDefaultAsync(
                existingTask =>
                    existingTask.SpecificationId == specification.Id &&
                    existingTask.Id == identifiers.TaskId,
                cancellationToken);

        if (implementationTask is null)
        {
            return ImplementationTaskNotFoundProblem(identifiers.TaskId);
        }

        var shiftedTasks = await dbContext.ImplementationTasks
            .Where(existingTask =>
                existingTask.SpecificationId == specification.Id &&
                existingTask.Position > implementationTask.Position)
            .OrderBy(existingTask => existingTask.Position)
            .ToListAsync(cancellationToken);
        var finalPositions = shiftedTasks.ToDictionary(
            shiftedTask => shiftedTask.Id,
            shiftedTask => shiftedTask.Position - 1);
        var timestamp = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await TryAdvanceCollectionVersionAsync(
                    specification,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return ImplementationTasksCollectionChangedProblem();
            }

            dbContext.ImplementationTasks.Remove(implementationTask);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (shiftedTasks.Count > 0)
            {
                var temporaryPosition = finalPositions.Values.Max() + 2;
                foreach (var shiftedTask in shiftedTasks)
                {
                    shiftedTask.MoveTo(temporaryPosition++, timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (var shiftedTask in shiftedTasks)
                {
                    shiftedTask.MoveTo(finalPositions[shiftedTask.Id], timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            return ImplementationTasksCollectionChangedProblem();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return ImplementationTasksCollectionChangedProblem();
        }

        return Results.NoContent();
    }

    private static async Task<(Specification? Specification, IResult? Error)>
        FindSpecificationAsync(
            Guid projectId,
            Guid proposalId,
            SpecFlowDbContext dbContext,
            bool tracking,
            CancellationToken cancellationToken)
    {
        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            return (null, ProjectNotFoundProblem(projectId));
        }

        var proposalExists = await dbContext.FeatureProposals
            .AnyAsync(
                proposal => proposal.Id == proposalId && proposal.ProjectId == projectId,
                cancellationToken);

        if (!proposalExists)
        {
            return (null, FeatureProposalNotFoundProblem(proposalId));
        }

        IQueryable<Specification> query = dbContext.Specifications;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var specification = await query.SingleOrDefaultAsync(
            existingSpecification => existingSpecification.FeatureProposalId == proposalId,
            cancellationToken);

        return specification is null
            ? (null, SpecificationNotFoundProblem(proposalId))
            : (specification, null);
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        IResult? Error) ParseParentIdentifiers(string projectId, string proposalId)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return (
                Guid.Empty,
                Guid.Empty,
                InvalidIdentifierProblem(
                    "projectId",
                    "The project identifier must be a valid UUID."));
        }

        if (!Guid.TryParse(proposalId, out var parsedProposalId))
        {
            return (
                Guid.Empty,
                Guid.Empty,
                InvalidIdentifierProblem(
                    "proposalId",
                    "The feature proposal identifier must be a valid UUID."));
        }

        return (parsedProjectId, parsedProposalId, null);
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        Guid TaskId,
        IResult? Error) ParseIdentifiers(
            string projectId,
            string proposalId,
            string taskId)
    {
        var parentIdentifiers = ParseParentIdentifiers(projectId, proposalId);
        if (parentIdentifiers.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, parentIdentifiers.Error);
        }

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return (
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                InvalidIdentifierProblem(
                    "taskId",
                    "The implementation task identifier must be a valid UUID."));
        }

        return (
            parentIdentifiers.ProjectId,
            parentIdentifiers.ProposalId,
            parsedTaskId,
            null);
    }

    private static (IReadOnlyList<Guid>? TaskIds, IResult? Error) ParseTaskOrder(
        IReadOnlyList<string?>? taskIds)
    {
        if (taskIds is null)
        {
            return (
                null,
                InvalidIdentifierProblem(
                    "taskIds",
                    "The complete implementation task order is required."));
        }

        var parsedIds = new List<Guid>(taskIds.Count);
        foreach (var taskId in taskIds)
        {
            if (!Guid.TryParse(taskId, out var parsedId) || parsedId == Guid.Empty)
            {
                return (
                    null,
                    InvalidIdentifierProblem(
                        "taskIds",
                        "Every implementation task identifier must be a non-empty UUID."));
            }

            parsedIds.Add(parsedId);
        }

        if (parsedIds.Count != parsedIds.Distinct().Count())
        {
            return (
                null,
                InvalidIdentifierProblem(
                    "taskIds",
                    "Implementation task identifiers cannot be repeated."));
        }

        return (parsedIds, null);
    }

    private static async Task<IResult> ResolveCreateConflictAsync(
        Guid specificationId,
        string normalizedTitle,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var duplicateExists = await dbContext.ImplementationTasks
            .AsNoTracking()
            .AnyAsync(
                implementationTask =>
                    implementationTask.SpecificationId == specificationId &&
                    implementationTask.NormalizedTitle == normalizedTitle,
                cancellationToken);

        return duplicateExists
            ? ImplementationTaskAlreadyExistsProblem()
            : ImplementationTasksCollectionChangedProblem();
    }

    private static async Task<bool> TryAdvanceCollectionVersionAsync(
        Specification specification,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.Specifications
            .Where(existingSpecification =>
                existingSpecification.Id == specification.Id &&
                existingSpecification.ImplementationTasksVersion ==
                specification.ImplementationTasksVersion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    existingSpecification => existingSpecification.ImplementationTasksVersion,
                    existingSpecification =>
                        existingSpecification.ImplementationTasksVersion + 1),
                cancellationToken);

        return affectedRows == 1;
    }

    private static Task RollbackAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken) =>
        transaction.RollbackAsync(cancellationToken);

    private static IResult InvalidIdentifierProblem(string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [message]
            });

    private static IResult ProjectNotFoundProblem(Guid projectId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Project not found",
            detail: $"No project with identifier '{projectId}' was found.");

    private static IResult FeatureProposalNotFoundProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Feature proposal not found",
            detail: $"No feature proposal with identifier '{proposalId}' was found in this project.");

    private static IResult SpecificationNotFoundProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Specification not found",
            detail: $"Feature proposal '{proposalId}' does not have a specification.");

    private static IResult ImplementationTaskNotFoundProblem(Guid taskId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Implementation task not found",
            detail: $"No implementation task with identifier '{taskId}' was found in this specification.");

    private static IResult ImplementationTaskAlreadyExistsProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Implementation task already exists",
            detail: "An implementation task with the same title already exists in this specification.");

    private static IResult ImplementationTaskUpdateConflictProblem(Guid taskId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Implementation task update conflict",
            detail: $"Implementation task '{taskId}' changed while it was being updated.");

    private static IResult ImplementationTaskTransitionConflictProblem(Guid taskId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Implementation task transition conflict",
            detail: $"Implementation task '{taskId}' cannot perform the requested transition.");

    private static IResult ImplementationTasksCollectionChangedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Implementation tasks collection changed",
            detail: "The implementation tasks collection changed while the operation was in progress.");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
