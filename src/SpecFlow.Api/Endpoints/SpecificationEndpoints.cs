using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Domain.FeatureProposals;
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
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return group;
    }

    private static async Task<IResult> CreateSpecificationAsync(
        string projectId,
        string proposalId,
        SaveSpecificationRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
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
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return SpecificationAlreadyExistsProblem(identifiers.ProposalId);
        }

        return Results.CreatedAtRoute(
            "GetSpecification",
            new
            {
                projectId = identifiers.ProjectId,
                proposalId = identifiers.ProposalId
            },
            SpecificationResponse.FromDomain(specification));
    }

    private static async Task<IResult> GetSpecificationAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
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
            ? SpecificationNotFoundProblem(identifiers.ProposalId)
            : Results.Ok(SpecificationResponse.FromDomain(specification));
    }

    private static async Task<IResult> UpdateSpecificationAsync(
        string projectId,
        string proposalId,
        SaveSpecificationRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
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
            return SpecificationNotFoundProblem(identifiers.ProposalId);
        }

        specification.UpdateContent(request.Content, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(SpecificationResponse.FromDomain(specification));
    }

    private static (
        Guid ProjectId,
        Guid ProposalId,
        IResult? Error) ParseIdentifiers(string projectId, string proposalId)
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
            return (null, ProjectNotFoundProblem(projectId));
        }

        var proposal = await dbContext.FeatureProposals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingProposal =>
                    existingProposal.ProjectId == projectId &&
                    existingProposal.Id == proposalId,
                cancellationToken);

        return proposal is null
            ? (null, FeatureProposalNotFoundProblem(proposalId))
            : (proposal, null);
    }

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

    private static IResult SpecificationNotFoundProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Specification not found",
            detail: $"Feature proposal '{proposalId}' does not have a specification.");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
