using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpecFlow.Api.Contracts.Traceability;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Traceability;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class TraceabilityEndpoints
{
    public static RouteGroupBuilder MapTraceabilityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/projects/{projectId}/proposals/{proposalId}/specification")
            .WithTags("Traceability");

        group.MapGet(
                "/tasks/{taskId}/acceptance-criteria",
                GetImplementationTaskAcceptanceCriteriaAsync)
            .WithName("GetImplementationTaskAcceptanceCriteria")
            .Produces<ImplementationTaskAcceptanceCriteriaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut(
                "/tasks/{taskId}/acceptance-criteria",
                ReplaceImplementationTaskAcceptanceCriteriaAsync)
            .WithName("ReplaceImplementationTaskAcceptanceCriteria")
            .Accepts<ReplaceImplementationTaskAcceptanceCriteriaRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet(
                "/acceptance-criteria/{criterionId}/tasks",
                GetAcceptanceCriterionImplementationTasksAsync)
            .WithName("GetAcceptanceCriterionImplementationTasks")
            .Produces<AcceptanceCriterionImplementationTasksResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetImplementationTaskAcceptanceCriteriaAsync(
        string projectId,
        string proposalId,
        string taskId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseTaskIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var implementationTask = await dbContext.ImplementationTasks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                task =>
                    task.SpecificationId == context.SpecificationId &&
                    task.Id == identifiers.TaskId,
                cancellationToken);
        if (implementationTask is null)
        {
            return ImplementationTaskNotFoundProblem(identifiers.TaskId);
        }

        var criterionIds = await GetCriterionIdsAsync(
            implementationTask.Id,
            dbContext,
            cancellationToken);

        return WithEntityTag(
            httpContext,
            implementationTask.Id,
            implementationTask.AcceptanceCriteriaVersion,
            Results.Ok(new ImplementationTaskAcceptanceCriteriaResponse(criterionIds)));
    }

    private static async Task<IResult> ReplaceImplementationTaskAcceptanceCriteriaAsync(
        string projectId,
        string proposalId,
        string taskId,
        ReplaceImplementationTaskAcceptanceCriteriaRequest request,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseTaskIdentifiers(projectId, proposalId, taskId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var requestedCriteria = ParseCriterionIds(request.AcceptanceCriterionIds);
        if (requestedCriteria.Error is not null)
        {
            return requestedCriteria.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var implementationTask = await dbContext.ImplementationTasks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                task =>
                    task.SpecificationId == context.SpecificationId &&
                    task.Id == identifiers.TaskId,
                cancellationToken);
        if (implementationTask is null)
        {
            return ImplementationTaskNotFoundProblem(identifiers.TaskId);
        }

        var requestedIds = requestedCriteria.Identifiers!;
        var matchingCriteriaCount = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .CountAsync(
                criterion =>
                    criterion.SpecificationId == context.SpecificationId &&
                    requestedIds.Contains(criterion.Id),
                cancellationToken);
        if (matchingCriteriaCount != requestedIds.Count)
        {
            return AcceptanceCriterionNotFoundProblem();
        }

        var currentIds = await GetCriterionIdsAsync(
            implementationTask.Id,
            dbContext,
            cancellationToken);
        var currentEntityTag = CreateEntityTag(
            implementationTask.Id,
            implementationTask.AcceptanceCriteriaVersion);
        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            currentEntityTag);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        if (currentIds.Count == requestedIds.Count &&
            currentIds.ToHashSet().SetEquals(requestedIds))
        {
            return HttpEntityTags.WithEntityTag(
                httpContext,
                currentEntityTag,
                Results.NoContent());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await TryAdvanceCollectionVersionAsync(
                    implementationTask,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return EndpointProblems.PreconditionFailed();
            }

            await dbContext.ImplementationTaskAcceptanceCriteria
                .Where(link => link.ImplementationTaskId == implementationTask.Id)
                .ExecuteDeleteAsync(cancellationToken);

            dbContext.ImplementationTaskAcceptanceCriteria.AddRange(
                requestedIds.Select(criterionId =>
                    ImplementationTaskAcceptanceCriterion.Create(
                        implementationTask.Id,
                        criterionId)));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return EndpointProblems.PreconditionFailed();
        }

        return WithEntityTag(
            httpContext,
            implementationTask.Id,
            implementationTask.AcceptanceCriteriaVersion + 1,
            Results.NoContent());
    }

    private static async Task<IResult> GetAcceptanceCriterionImplementationTasksAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseCriterionIdentifiers(projectId, proposalId, criterionId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var criterionExists = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .AnyAsync(
                criterion =>
                    criterion.SpecificationId == context.SpecificationId &&
                    criterion.Id == identifiers.CriterionId,
                cancellationToken);
        if (!criterionExists)
        {
            return AcceptanceCriterionNotFoundProblem(identifiers.CriterionId);
        }

        var taskIds = await (
                from link in dbContext.ImplementationTaskAcceptanceCriteria.AsNoTracking()
                join task in dbContext.ImplementationTasks.AsNoTracking()
                    on link.ImplementationTaskId equals task.Id
                where link.AcceptanceCriterionId == identifiers.CriterionId &&
                    task.SpecificationId == context.SpecificationId
                select task.Id)
            .ToListAsync(cancellationToken);
        taskIds.Sort();

        return Results.Ok(new AcceptanceCriterionImplementationTasksResponse(taskIds));
    }

    private static async Task<IReadOnlyList<Guid>> GetCriterionIdsAsync(
        Guid implementationTaskId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var criterionIds = await dbContext.ImplementationTaskAcceptanceCriteria
            .AsNoTracking()
            .Where(link => link.ImplementationTaskId == implementationTaskId)
            .Select(link => link.AcceptanceCriterionId)
            .ToListAsync(cancellationToken);
        criterionIds.Sort();
        return criterionIds;
    }

    private static async Task<(Guid SpecificationId, IResult? Error)>
        FindSpecificationAsync(
            Guid projectId,
            Guid proposalId,
            SpecFlowDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var context = await SpecificationContextResolver.FindAsync(
            projectId,
            proposalId,
            dbContext,
            tracking: false,
            cancellationToken);

        return context.Error is not null
            ? (Guid.Empty, context.Error)
            : (context.Specification!.Id, null);
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        Guid TaskId,
        IResult? Error) ParseTaskIdentifiers(
            string projectId,
            string proposalId,
            string taskId)
    {
        var parentIdentifiers = ParseParentIdentifiers(projectId, proposalId);
        if (parentIdentifiers.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, parentIdentifiers.Error);
        }

        var taskIdentifier = RouteIdentifierParser.ParseImplementationTask(taskId);
        return taskIdentifier.Error is not null
            ? (Guid.Empty, Guid.Empty, Guid.Empty, taskIdentifier.Error)
            : (
                parentIdentifiers.ProjectId,
                parentIdentifiers.ProposalId,
                taskIdentifier.Identifier,
                null);
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        Guid CriterionId,
        IResult? Error) ParseCriterionIdentifiers(
            string projectId,
            string proposalId,
            string criterionId)
    {
        var parentIdentifiers = ParseParentIdentifiers(projectId, proposalId);
        if (parentIdentifiers.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, parentIdentifiers.Error);
        }

        var criterionIdentifier = RouteIdentifierParser.ParseAcceptanceCriterion(criterionId);
        return criterionIdentifier.Error is not null
            ? (Guid.Empty, Guid.Empty, Guid.Empty, criterionIdentifier.Error)
            : (
                parentIdentifiers.ProjectId,
                parentIdentifiers.ProposalId,
                criterionIdentifier.Identifier,
                null);
    }

    private static (Guid ProjectId, Guid ProposalId, IResult? Error)
        ParseParentIdentifiers(string projectId, string proposalId)
    {
        var projectIdentifier = RouteIdentifierParser.ParseProject(projectId);
        if (projectIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, projectIdentifier.Error);
        }

        var proposalIdentifier = RouteIdentifierParser.ParseFeatureProposal(proposalId);
        return proposalIdentifier.Error is not null
            ? (Guid.Empty, Guid.Empty, proposalIdentifier.Error)
            : (projectIdentifier.Identifier, proposalIdentifier.Identifier, null);
    }

    private static (IReadOnlyList<Guid>? Identifiers, IResult? Error) ParseCriterionIds(
        IReadOnlyList<string?>? values)
    {
        if (values is null)
        {
            return (
                null,
                EndpointProblems.InvalidIdentifier(
                    "acceptanceCriterionIds",
                    "The complete acceptance criterion set is required."));
        }

        var identifiers = new List<Guid>(values.Count);
        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var identifier) || identifier == Guid.Empty)
            {
                return (
                    null,
                    EndpointProblems.InvalidIdentifier(
                        "acceptanceCriterionIds",
                        "Every acceptance criterion identifier must be a non-empty UUID."));
            }

            identifiers.Add(identifier);
        }

        if (identifiers.Count != identifiers.Distinct().Count())
        {
            return (
                null,
                EndpointProblems.InvalidIdentifier(
                    "acceptanceCriterionIds",
                    "Acceptance criterion identifiers cannot be repeated."));
        }

        return (identifiers, null);
    }

    private static async Task<bool> TryAdvanceCollectionVersionAsync(
        ImplementationTask implementationTask,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.ImplementationTasks
            .Where(task =>
                task.Id == implementationTask.Id &&
                task.AcceptanceCriteriaVersion ==
                implementationTask.AcceptanceCriteriaVersion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    task => task.AcceptanceCriteriaVersion,
                    task => task.AcceptanceCriteriaVersion + 1),
                cancellationToken);

        return affectedRows == 1;
    }

    private static Task RollbackAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken) =>
        transaction.RollbackAsync(cancellationToken);

    private static string CreateEntityTag(
        Guid implementationTaskId,
        int version) =>
        HttpEntityTags.CreateResource(
            "implementation-task-acceptance-criteria",
            implementationTaskId,
            version);

    private static IResult WithEntityTag(
        HttpContext httpContext,
        Guid implementationTaskId,
        int version,
        IResult result) =>
        HttpEntityTags.WithEntityTag(
            httpContext,
            CreateEntityTag(implementationTaskId, version),
            result);

    private static IResult ImplementationTaskNotFoundProblem(Guid taskId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Implementation task not found",
            detail: $"No implementation task with identifier '{taskId}' was found in this specification.");

    private static IResult AcceptanceCriterionNotFoundProblem(Guid criterionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Acceptance criterion not found",
            detail: $"No acceptance criterion with identifier '{criterionId}' was found in this specification.");

    private static IResult AcceptanceCriterionNotFoundProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Acceptance criterion not found",
            detail: "One or more acceptance criteria were not found in this specification.");
}
