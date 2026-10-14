using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class SpecificationEndpoints
{
    public static RouteGroupBuilder MapSpecificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/projects/{projectId}/proposals/{proposalId}/specification")
            .WithTags("Specifications");

        group.MapPost("/", CreateSpecificationAsync)
            .WithName("CreateSpecification")
            .Accepts<SaveSpecificationRequest>("application/json")
            .Produces<SpecificationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/", GetSpecificationAsync)
            .WithName("GetSpecification")
            .Produces<SpecificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/", UpdateSpecificationAsync)
            .WithName("UpdateSpecification")
            .Accepts<SaveSpecificationRequest>("application/json")
            .Produces<SpecificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapPost("/approve", ApproveSpecificationAsync)
            .WithName("ApproveSpecification")
            .Produces<SpecificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapPost("/mark-implemented", MarkSpecificationImplementedAsync)
            .WithName("MarkSpecificationImplemented")
            .Produces<SpecificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapPost("/verify", VerifySpecificationAsync)
            .WithName("VerifySpecification")
            .Produces<SpecificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return group;
    }

    private static async Task<IResult> CreateSpecificationAsync(
        string projectId,
        string proposalId,
        SaveSpecificationRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = Specification.ValidateContent(request.Content);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var proposalResult = await FindProposalAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);

        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        if (proposalResult.Proposal!.Status != FeatureProposalStatus.Accepted)
        {
            return FeatureProposalNotAcceptedProblem(identifiers.ProposalId);
        }

        var specificationExists = await dbContext.Specifications
            .AnyAsync(
                specification =>
                    specification.FeatureProposalId == identifiers.ProposalId,
                cancellationToken);

        if (specificationExists)
        {
            return SpecificationAlreadyExistsProblem(identifiers.ProposalId);
        }

        var specification = Specification.Create(
            Guid.NewGuid(),
            identifiers.ProposalId,
            request.Content,
            timeProvider.GetUtcNow());
        dbContext.Specifications.Add(specification);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return SpecificationAlreadyExistsProblem(identifiers.ProposalId);
        }

        return WithEntityTag(
            httpContext,
            specification,
            Results.CreatedAtRoute(
                "GetSpecification",
                new
                {
                    projectId = identifiers.ProjectId,
                    proposalId = identifiers.ProposalId
                },
                SpecificationResponse.FromDomain(specification)));
    }

    private static async Task<IResult> GetSpecificationAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var proposalResult = await FindProposalAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);

        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        var specification = await dbContext.Specifications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingSpecification =>
                    existingSpecification.FeatureProposalId == identifiers.ProposalId,
                cancellationToken);

        return specification is null
            ? EndpointProblems.SpecificationNotFound(identifiers.ProposalId)
            : WithEntityTag(
                httpContext,
                specification,
                Results.Ok(SpecificationResponse.FromDomain(specification)));
    }

    private static async Task<IResult> UpdateSpecificationAsync(
        string projectId,
        string proposalId,
        SaveSpecificationRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = Specification.ValidateContent(request.Content);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var proposalResult = await FindProposalAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);

        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        var specification = await dbContext.Specifications
            .SingleOrDefaultAsync(
                existingSpecification =>
                    existingSpecification.FeatureProposalId == identifiers.ProposalId,
                cancellationToken);

        if (specification is null)
        {
            return EndpointProblems.SpecificationNotFound(identifiers.ProposalId);
        }

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(specification));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        try
        {
            specification.UpdateContent(request.Content, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (SpecificationStateException)
        {
            return EndpointProblems.SpecificationNotEditable();
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointProblems.PreconditionFailed();
        }

        return WithEntityTag(
            httpContext,
            specification,
            Results.Ok(SpecificationResponse.FromDomain(specification)));
    }

    private static Task<IResult> ApproveSpecificationAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        TransitionSpecificationAsync(
            projectId,
            proposalId,
            dbContext,
            timeProvider,
            httpContext,
            SpecificationStatus.Approved,
            async (specification, timestamp) =>
            {
                var hasAcceptanceCriteria = await dbContext.AcceptanceCriteria
                    .AnyAsync(
                        criterion => criterion.SpecificationId == specification.Id,
                        cancellationToken);
                var hasImplementationTasks = await dbContext.ImplementationTasks
                    .AnyAsync(
                        task => task.SpecificationId == specification.Id,
                        cancellationToken);
                var allAcceptanceCriteriaCovered = !await dbContext.AcceptanceCriteria
                    .AnyAsync(
                        criterion =>
                            criterion.SpecificationId == specification.Id &&
                            !dbContext.ImplementationTaskAcceptanceCriteria.Any(link =>
                                link.AcceptanceCriterionId == criterion.Id),
                        cancellationToken);

                specification.Approve(
                    hasAcceptanceCriteria,
                    hasImplementationTasks,
                    allAcceptanceCriteriaCovered,
                    timestamp);
            },
            cancellationToken);

    private static Task<IResult> MarkSpecificationImplementedAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        TransitionSpecificationAsync(
            projectId,
            proposalId,
            dbContext,
            timeProvider,
            httpContext,
            SpecificationStatus.Implemented,
            async (specification, timestamp) =>
            {
                var allImplementationTasksCompleted = !await dbContext.ImplementationTasks
                    .AnyAsync(
                        task =>
                            task.SpecificationId == specification.Id &&
                            task.Status != ImplementationTaskStatus.Completed,
                        cancellationToken);
                var allAcceptanceCriteriaCovered = !await dbContext.AcceptanceCriteria
                    .AnyAsync(
                        criterion =>
                            criterion.SpecificationId == specification.Id &&
                            !dbContext.ImplementationTaskAcceptanceCriteria.Any(link =>
                                link.AcceptanceCriterionId == criterion.Id),
                        cancellationToken);

                specification.MarkImplemented(
                    allImplementationTasksCompleted,
                    allAcceptanceCriteriaCovered,
                    timestamp);
            },
            cancellationToken);

    private static Task<IResult> VerifySpecificationAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        TransitionSpecificationAsync(
            projectId,
            proposalId,
            dbContext,
            timeProvider,
            httpContext,
            SpecificationStatus.Verified,
            async (specification, timestamp) =>
            {
                var allAcceptanceCriteriaPassed = !await dbContext.AcceptanceCriteria
                    .AnyAsync(
                        criterion =>
                            criterion.SpecificationId == specification.Id &&
                            criterion.VerificationStatus !=
                            AcceptanceCriterionVerificationStatus.Passed,
                        cancellationToken);
                specification.Verify(allAcceptanceCriteriaPassed, timestamp);
            },
            cancellationToken);

    private static async Task<IResult> TransitionSpecificationAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        SpecificationStatus targetStatus,
        Func<Specification, DateTimeOffset, Task> transition,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var proposalResult = await FindProposalAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            dbContext,
            cancellationToken);
        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        var specification = await dbContext.Specifications
            .SingleOrDefaultAsync(
                item => item.FeatureProposalId == identifiers.ProposalId,
                cancellationToken);
        if (specification is null)
        {
            return EndpointProblems.SpecificationNotFound(identifiers.ProposalId);
        }

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(specification));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        try
        {
            await transition(specification, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (SpecificationReadinessException)
        {
            return SpecificationNotReadyForApprovalProblem();
        }
        catch (SpecificationImplementationIncompleteException)
        {
            return SpecificationImplementationIncompleteProblem();
        }
        catch (SpecificationVerificationIncompleteException)
        {
            return SpecificationVerificationIncompleteProblem();
        }
        catch (SpecificationTransitionException)
        {
            return SpecificationTransitionConflictProblem(targetStatus);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointProblems.PreconditionFailed();
        }

        return WithEntityTag(
            httpContext,
            specification,
            Results.Ok(SpecificationResponse.FromDomain(specification)));
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        IResult? Error) ParseIdentifiers(string projectId, string proposalId)
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

    private static async Task<(FeatureProposal? Proposal, IResult? Error)> FindProposalAsync(
        Guid projectId,
        Guid proposalId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            return (null, EndpointProblems.ProjectNotFound(projectId));
        }

        var proposal = await dbContext.FeatureProposals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingProposal =>
                    existingProposal.ProjectId == projectId &&
                    existingProposal.Id == proposalId,
                cancellationToken);

        return proposal is null
            ? (null, EndpointProblems.FeatureProposalNotFound(proposalId))
            : (proposal, null);
    }

    private static IResult FeatureProposalNotAcceptedProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Feature proposal is not accepted",
            detail: $"Feature proposal '{proposalId}' must be accepted before creating a specification.");

    private static IResult SpecificationAlreadyExistsProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification already exists",
            detail: $"Feature proposal '{proposalId}' already has a specification.");

    private static IResult SpecificationNotReadyForApprovalProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification is not ready for approval",
            detail: "Approval requires at least one criterion, at least one task, and complete criterion coverage.");

    private static IResult SpecificationImplementationIncompleteProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification implementation is incomplete",
            detail: "All implementation tasks must be completed and criterion coverage must remain complete.");

    private static IResult SpecificationVerificationIncompleteProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification verification incomplete",
            detail: "All acceptance criteria must pass before verifying the specification.");

    private static IResult SpecificationTransitionConflictProblem(
        SpecificationStatus targetStatus) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification transition conflict",
            detail: $"The specification cannot transition to '{targetStatus}'.");

    private static string CreateEntityTag(Specification specification) =>
        HttpEntityTags.CreateResource("specification", specification.Id, specification.Version);

    private static IResult WithEntityTag(
        HttpContext httpContext,
        Specification specification,
        IResult result) =>
        HttpEntityTags.WithEntityTag(httpContext, CreateEntityTag(specification), result);

}
