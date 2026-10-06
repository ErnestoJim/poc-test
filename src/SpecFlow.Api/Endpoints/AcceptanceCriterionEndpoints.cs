using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class AcceptanceCriterionEndpoints
{
    public static RouteGroupBuilder MapAcceptanceCriterionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria")
            .WithTags("Acceptance Criteria");

        group.MapPost("/", CreateAcceptanceCriterionAsync)
            .WithName("CreateAcceptanceCriterion")
            .Accepts<SaveAcceptanceCriterionRequest>("application/json")
            .Produces<AcceptanceCriterionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/", ListAcceptanceCriteriaAsync)
            .WithName("ListAcceptanceCriteria")
            .Produces<IReadOnlyList<AcceptanceCriterionResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/order", ReorderAcceptanceCriteriaAsync)
            .WithName("ReorderAcceptanceCriteria")
            .Accepts<ReorderAcceptanceCriteriaRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{criterionId}", GetAcceptanceCriterionAsync)
            .WithName("GetAcceptanceCriterion")
            .Produces<AcceptanceCriterionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{criterionId}", UpdateAcceptanceCriterionAsync)
            .WithName("UpdateAcceptanceCriterion")
            .Accepts<SaveAcceptanceCriterionRequest>("application/json")
            .Produces<AcceptanceCriterionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapDelete("/{criterionId}", DeleteAcceptanceCriterionAsync)
            .WithName("DeleteAcceptanceCriterion")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> CreateAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        SaveAcceptanceCriterionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseParentIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = AcceptanceCriterion.ValidateContent(request.Content);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: true,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var contentHash = AcceptanceCriterion.CalculateContentHash(request.Content!);
        var duplicateExists = await dbContext.AcceptanceCriteria
            .AnyAsync(
                criterion =>
                    criterion.SpecificationId == specification.Id &&
                    criterion.ContentHash == contentHash,
                cancellationToken);

        if (duplicateExists)
        {
            return AcceptanceCriterionAlreadyExistsProblem();
        }

        var lastPosition = await dbContext.AcceptanceCriteria
            .Where(criterion => criterion.SpecificationId == specification.Id)
            .Select(criterion => (int?)criterion.Position)
            .MaxAsync(cancellationToken) ?? 0;
        var criterion = AcceptanceCriterion.Create(
            Guid.NewGuid(),
            specification.Id,
            request.Content,
            lastPosition + 1,
            timeProvider.GetUtcNow());
        specification.MarkAcceptanceCriteriaChanged();
        dbContext.AcceptanceCriteria.Add(criterion);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveCreateConflictAsync(
                specification.Id,
                contentHash,
                dbContext,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return await ResolveCreateConflictAsync(
                specification.Id,
                contentHash,
                dbContext,
                cancellationToken);
        }

        return Results.CreatedAtRoute(
            "GetAcceptanceCriterion",
            new
            {
                projectId = identifiers.ProjectId,
                proposalId = identifiers.ProposalId,
                criterionId = criterion.Id
            },
            AcceptanceCriterionResponse.FromDomain(criterion));
    }

    private static async Task<IResult> GetAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, criterionId);
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

        var criterion = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingCriterion =>
                    existingCriterion.SpecificationId == context.Specification!.Id &&
                    existingCriterion.Id == identifiers.CriterionId,
                cancellationToken);

        return criterion is null
            ? AcceptanceCriterionNotFoundProblem(identifiers.CriterionId)
            : Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion));
    }

    private static async Task<IResult> ListAcceptanceCriteriaAsync(
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

        var criteria = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .Where(criterion => criterion.SpecificationId == context.Specification!.Id)
            .OrderBy(criterion => criterion.Position)
            .ThenBy(criterion => criterion.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(criteria.Select(AcceptanceCriterionResponse.FromDomain));
    }

    private static async Task<IResult> UpdateAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SaveAcceptanceCriterionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, criterionId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = AcceptanceCriterion.ValidateContent(request.Content);
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
        var criterion = await dbContext.AcceptanceCriteria
            .SingleOrDefaultAsync(
                existingCriterion =>
                    existingCriterion.SpecificationId == specificationId &&
                    existingCriterion.Id == identifiers.CriterionId,
                cancellationToken);

        if (criterion is null)
        {
            return AcceptanceCriterionNotFoundProblem(identifiers.CriterionId);
        }

        var contentHash = AcceptanceCriterion.CalculateContentHash(request.Content!);
        var duplicateExists = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .AnyAsync(
                existingCriterion =>
                    existingCriterion.SpecificationId == specificationId &&
                    existingCriterion.Id != criterion.Id &&
                    existingCriterion.ContentHash == contentHash,
                cancellationToken);

        if (duplicateExists)
        {
            return AcceptanceCriterionAlreadyExistsProblem();
        }

        var changed = criterion.UpdateContent(request.Content, timeProvider.GetUtcNow());
        if (!changed)
        {
            return Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AcceptanceCriterionUpdateConflictProblem(identifiers.CriterionId);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return AcceptanceCriterionAlreadyExistsProblem();
        }

        return Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion));
    }

    private static async Task<IResult> ReorderAcceptanceCriteriaAsync(
        string projectId,
        string proposalId,
        ReorderAcceptanceCriteriaRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseParentIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var order = ParseCriterionOrder(request.CriterionIds);
        if (order.Error is not null)
        {
            return order.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: true,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var criteria = await dbContext.AcceptanceCriteria
            .Where(criterion => criterion.SpecificationId == specification.Id)
            .OrderBy(criterion => criterion.Position)
            .ToListAsync(cancellationToken);
        var requestedIds = order.CriterionIds!;

        if (criteria.Count != requestedIds.Count ||
            !criteria.Select(criterion => criterion.Id).ToHashSet().SetEquals(requestedIds))
        {
            return AcceptanceCriteriaCollectionChangedProblem();
        }

        var criteriaById = criteria.ToDictionary(criterion => criterion.Id);
        var movedCriteria = requestedIds
            .Select((id, index) => (Criterion: criteriaById[id], Position: index + 1))
            .Where(item => item.Criterion.Position != item.Position)
            .ToList();
        var timestamp = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            specification.MarkAcceptanceCriteriaChanged();

            if (movedCriteria.Count > 0)
            {
                var temporaryPosition = criteria.Count + 1;
                foreach (var item in movedCriteria)
                {
                    item.Criterion.MoveTo(temporaryPosition++, timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (var item in movedCriteria)
                {
                    item.Criterion.MoveTo(item.Position, timestamp);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            return AcceptanceCriteriaCollectionChangedProblem();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return AcceptanceCriteriaCollectionChangedProblem();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId, criterionId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var context = await FindSpecificationAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            tracking: true,
            cancellationToken);

        if (context.Error is not null)
        {
            return context.Error;
        }

        var specification = context.Specification!;
        var criterion = await dbContext.AcceptanceCriteria
            .SingleOrDefaultAsync(
                existingCriterion =>
                    existingCriterion.SpecificationId == specification.Id &&
                    existingCriterion.Id == identifiers.CriterionId,
                cancellationToken);

        if (criterion is null)
        {
            return AcceptanceCriterionNotFoundProblem(identifiers.CriterionId);
        }

        var shiftedCriteria = await dbContext.AcceptanceCriteria
            .Where(existingCriterion =>
                existingCriterion.SpecificationId == specification.Id &&
                existingCriterion.Position > criterion.Position)
            .OrderBy(existingCriterion => existingCriterion.Position)
            .ToListAsync(cancellationToken);
        var finalPositions = shiftedCriteria.ToDictionary(
            shiftedCriterion => shiftedCriterion.Id,
            shiftedCriterion => shiftedCriterion.Position - 1);
        var timestamp = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            specification.MarkAcceptanceCriteriaChanged();
            dbContext.AcceptanceCriteria.Remove(criterion);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (shiftedCriteria.Count > 0)
            {
                var temporaryPosition = finalPositions.Values.Max() + 2;
                foreach (var shiftedCriterion in shiftedCriteria)
                {
                    shiftedCriterion.MoveTo(temporaryPosition++, timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (var shiftedCriterion in shiftedCriteria)
                {
                    shiftedCriterion.MoveTo(finalPositions[shiftedCriterion.Id], timestamp);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            return AcceptanceCriteriaCollectionChangedProblem();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return AcceptanceCriteriaCollectionChangedProblem();
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
        Guid CriterionId,
        IResult? Error) ParseIdentifiers(
            string projectId,
            string proposalId,
            string criterionId)
    {
        var parentIdentifiers = ParseParentIdentifiers(projectId, proposalId);
        if (parentIdentifiers.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, parentIdentifiers.Error);
        }

        if (!Guid.TryParse(criterionId, out var parsedCriterionId))
        {
            return (
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                InvalidIdentifierProblem(
                    "criterionId",
                    "The acceptance criterion identifier must be a valid UUID."));
        }

        return (
            parentIdentifiers.ProjectId,
            parentIdentifiers.ProposalId,
            parsedCriterionId,
            null);
    }

    private static (IReadOnlyList<Guid>? CriterionIds, IResult? Error) ParseCriterionOrder(
        IReadOnlyList<string?>? criterionIds)
    {
        if (criterionIds is null)
        {
            return (
                null,
                InvalidIdentifierProblem(
                    "criterionIds",
                    "The complete criterion order is required."));
        }

        var parsedIds = new List<Guid>(criterionIds.Count);
        foreach (var criterionId in criterionIds)
        {
            if (!Guid.TryParse(criterionId, out var parsedId) || parsedId == Guid.Empty)
            {
                return (
                    null,
                    InvalidIdentifierProblem(
                        "criterionIds",
                        "Every acceptance criterion identifier must be a non-empty UUID."));
            }

            parsedIds.Add(parsedId);
        }

        if (parsedIds.Count != parsedIds.Distinct().Count())
        {
            return (
                null,
                InvalidIdentifierProblem(
                    "criterionIds",
                    "Acceptance criterion identifiers cannot be repeated."));
        }

        return (parsedIds, null);
    }

    private static async Task<IResult> ResolveCreateConflictAsync(
        Guid specificationId,
        string contentHash,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var duplicateExists = await dbContext.AcceptanceCriteria
            .AsNoTracking()
            .AnyAsync(
                criterion =>
                    criterion.SpecificationId == specificationId &&
                    criterion.ContentHash == contentHash,
                cancellationToken);

        return duplicateExists
            ? AcceptanceCriterionAlreadyExistsProblem()
            : AcceptanceCriteriaCollectionChangedProblem();
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

    private static IResult AcceptanceCriterionNotFoundProblem(Guid criterionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Acceptance criterion not found",
            detail: $"No acceptance criterion with identifier '{criterionId}' was found in this specification.");

    private static IResult AcceptanceCriterionAlreadyExistsProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Acceptance criterion already exists",
            detail: "An acceptance criterion with identical content already exists in this specification.");

    private static IResult AcceptanceCriterionUpdateConflictProblem(Guid criterionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Acceptance criterion update conflict",
            detail: $"Acceptance criterion '{criterionId}' changed while it was being updated.");

    private static IResult AcceptanceCriteriaCollectionChangedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Acceptance criteria collection changed",
            detail: "The acceptance criteria collection changed while the operation was in progress.");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
