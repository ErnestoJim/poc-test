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
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
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
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapDelete("/{criterionId}", DeleteAcceptanceCriterionAsync)
            .WithName("DeleteAcceptanceCriterion")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return group;
    }

    private static async Task<IResult> CreateAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        SaveAcceptanceCriterionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
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
            tracking: false,
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
        dbContext.AcceptanceCriteria.Add(criterion);

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
                    contentHash,
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
                contentHash,
                dbContext,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return await ResolveCreateConflictAsync(
                specification.Id,
                contentHash,
                dbContext,
                cancellationToken);
        }

        return WithEntityTag(
            httpContext,
            criterion,
            Results.CreatedAtRoute(
                "GetAcceptanceCriterion",
                new
                {
                    projectId = identifiers.ProjectId,
                    proposalId = identifiers.ProposalId,
                    criterionId = criterion.Id
                },
                AcceptanceCriterionResponse.FromDomain(criterion)));
    }

    private static async Task<IResult> GetAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
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
            : WithEntityTag(
                httpContext,
                criterion,
                Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion)));
    }

    private static async Task<IResult> ListAcceptanceCriteriaAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
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

        return HttpEntityTags.WithEntityTag(
            httpContext,
            CreateCollectionEntityTag(context.Specification!, criteria),
            Results.Ok(criteria.Select(AcceptanceCriterionResponse.FromDomain)));
    }

    private static async Task<IResult> UpdateAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SaveAcceptanceCriterionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
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

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(criterion));
        if (preconditionError is not null)
        {
            return preconditionError;
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
            return WithEntityTag(
                httpContext,
                criterion,
                Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion)));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointProblems.PreconditionFailed();
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return AcceptanceCriterionAlreadyExistsProblem();
        }

        return WithEntityTag(
            httpContext,
            criterion,
            Results.Ok(AcceptanceCriterionResponse.FromDomain(criterion)));
    }

    private static async Task<IResult> ReorderAcceptanceCriteriaAsync(
        string projectId,
        string proposalId,
        ReorderAcceptanceCriteriaRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
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

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

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
        var criteria = await dbContext.AcceptanceCriteria
            .Where(criterion => criterion.SpecificationId == specification.Id)
            .OrderBy(criterion => criterion.Position)
            .ToListAsync(cancellationToken);
        var requestedIds = order.CriterionIds!;

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateCollectionEntityTag(specification, criteria));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

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

        try
        {
            if (!await TryAdvanceCollectionVersionAsync(
                    specification,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return EndpointProblems.PreconditionFailed();
            }

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
            return EndpointProblems.PreconditionFailed();
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return EndpointProblems.PreconditionFailed();
        }

        return HttpEntityTags.WithEntityTag(
            httpContext,
            CreateCollectionEntityTag(
                specification.Id,
                specification.AcceptanceCriteriaVersion + 1,
                criteria
                    .OrderBy(criterion => criterion.Position)
                    .ThenBy(criterion => criterion.Id)),
            Results.NoContent());
    }

    private static async Task<IResult> DeleteAcceptanceCriterionAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
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

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(criterion));
        if (preconditionError is not null)
        {
            return preconditionError;
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
            if (!await TryAdvanceCollectionVersionAsync(
                    specification,
                    dbContext,
                    cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return EndpointProblems.PreconditionFailed();
            }

            await InvalidateLinkedTaskAcceptanceCriteriaAsync(
                criterion.Id,
                dbContext,
                cancellationToken);

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
            return EndpointProblems.PreconditionFailed();
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await RollbackAsync(transaction, cancellationToken);
            return EndpointProblems.PreconditionFailed();
        }

        return Results.NoContent();
    }

    private static Task<(Specification? Specification, IResult? Error)>
        FindSpecificationAsync(
            Guid projectId,
            Guid proposalId,
            SpecFlowDbContext dbContext,
            bool tracking,
            CancellationToken cancellationToken) =>
        SpecificationContextResolver.FindAsync(
            projectId,
            proposalId,
            dbContext,
            tracking,
            cancellationToken);

    private static (
        Guid ProjectId,
        Guid ProposalId,
        IResult? Error) ParseParentIdentifiers(string projectId, string proposalId)
    {
        var projectIdentifier = RouteIdentifierParser.ParseProject(projectId);
        if (projectIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, projectIdentifier.Error);
        }

        var proposalIdentifier = RouteIdentifierParser.ParseFeatureProposal(proposalId);
        if (proposalIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, proposalIdentifier.Error);
        }

        return (projectIdentifier.Identifier, proposalIdentifier.Identifier, null);
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

        var criterionIdentifier = RouteIdentifierParser.ParseAcceptanceCriterion(criterionId);
        if (criterionIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, criterionIdentifier.Error);
        }

        return (
            parentIdentifiers.ProjectId,
            parentIdentifiers.ProposalId,
            criterionIdentifier.Identifier,
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

    private static async Task<bool> TryAdvanceCollectionVersionAsync(
        Specification specification,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.Specifications
            .Where(existingSpecification =>
                existingSpecification.Id == specification.Id &&
                existingSpecification.AcceptanceCriteriaVersion ==
                specification.AcceptanceCriteriaVersion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    existingSpecification => existingSpecification.AcceptanceCriteriaVersion,
                    existingSpecification =>
                        existingSpecification.AcceptanceCriteriaVersion + 1),
                cancellationToken);

        return affectedRows == 1;
    }

    private static Task<int> InvalidateLinkedTaskAcceptanceCriteriaAsync(
        Guid criterionId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.ImplementationTasks
            .Where(task => dbContext.ImplementationTaskAcceptanceCriteria.Any(link =>
                link.AcceptanceCriterionId == criterionId &&
                link.ImplementationTaskId == task.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    task => task.AcceptanceCriteriaVersion,
                    task => task.AcceptanceCriteriaVersion + 1),
                cancellationToken);

    private static Task RollbackAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken) =>
        transaction.RollbackAsync(cancellationToken);

    private static IResult InvalidIdentifierProblem(string field, string message) =>
        EndpointProblems.InvalidIdentifier(field, message);

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

    private static IResult AcceptanceCriteriaCollectionChangedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Acceptance criteria collection changed",
            detail: "The acceptance criteria collection changed while the operation was in progress.");

    private static string CreateEntityTag(AcceptanceCriterion criterion) =>
        HttpEntityTags.CreateResource(
            "acceptance-criterion",
            criterion.Id,
            criterion.Version);

    private static string CreateCollectionEntityTag(
        Specification specification,
        IEnumerable<AcceptanceCriterion> criteria) =>
        CreateCollectionEntityTag(
            specification.Id,
            specification.AcceptanceCriteriaVersion,
            criteria);

    private static string CreateCollectionEntityTag(
        Guid specificationId,
        int collectionVersion,
        IEnumerable<AcceptanceCriterion> criteria) =>
        HttpEntityTags.CreateCollection(
            "acceptance-criteria",
            specificationId,
            collectionVersion,
            criteria.Select(criterion => (criterion.Id, criterion.Version)));

    private static IResult WithEntityTag(
        HttpContext httpContext,
        AcceptanceCriterion criterion,
        IResult result) =>
        HttpEntityTags.WithEntityTag(httpContext, CreateEntityTag(criterion), result);

}
