using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.FeatureProposals;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class FeatureProposalEndpoints
{
    public static RouteGroupBuilder MapFeatureProposalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId}/proposals")
            .WithTags("Feature Proposals");

        group.MapPost("/", CreateFeatureProposalAsync)
            .WithName("CreateFeatureProposal")
            .Accepts<CreateFeatureProposalRequest>("application/json")
            .Produces<FeatureProposalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{proposalId}", GetFeatureProposalAsync)
            .WithName("GetFeatureProposal")
            .Produces<FeatureProposalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListFeatureProposalsAsync)
            .WithName("ListFeatureProposals")
            .Produces<IReadOnlyList<FeatureProposalResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{proposalId}/accept", AcceptFeatureProposalAsync)
            .WithName("AcceptFeatureProposal")
            .Produces<FeatureProposalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{proposalId}/reject", RejectFeatureProposalAsync)
            .WithName("RejectFeatureProposal")
            .Accepts<RejectFeatureProposalRequest>("application/json")
            .Produces<FeatureProposalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return group;
    }

    private static async Task<IResult> CreateFeatureProposalAsync(
        string projectId,
        CreateFeatureProposalRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return InvalidIdentifierProblem(
                "projectId",
                "The project identifier must be a valid UUID.");
        }

        var validationErrors = FeatureProposal.Validate(request.Title, request.Description);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == parsedProjectId, cancellationToken);

        if (!projectExists)
        {
            return ProjectNotFoundProblem(parsedProjectId);
        }

        var proposal = FeatureProposal.Create(
            Guid.NewGuid(),
            parsedProjectId,
            request.Title,
            request.Description,
            timeProvider.GetUtcNow());

        dbContext.FeatureProposals.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = FeatureProposalResponse.FromDomain(proposal);
        return Results.CreatedAtRoute(
            "GetFeatureProposal",
            new { projectId = parsedProjectId, proposalId = proposal.Id },
            response);
    }

    private static async Task<IResult> GetFeatureProposalAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return InvalidIdentifierProblem(
                "projectId",
                "The project identifier must be a valid UUID.");
        }

        if (!Guid.TryParse(proposalId, out var parsedProposalId))
        {
            return InvalidIdentifierProblem(
                "proposalId",
                "The feature proposal identifier must be a valid UUID.");
        }

        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == parsedProjectId, cancellationToken);

        if (!projectExists)
        {
            return ProjectNotFoundProblem(parsedProjectId);
        }

        var proposal = await dbContext.FeatureProposals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingProposal =>
                    existingProposal.ProjectId == parsedProjectId &&
                    existingProposal.Id == parsedProposalId,
                cancellationToken);

        return proposal is null
            ? FeatureProposalNotFoundProblem(parsedProposalId)
            : Results.Ok(FeatureProposalResponse.FromDomain(proposal));
    }

    private static async Task<IResult> ListFeatureProposalsAsync(
        string projectId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return InvalidIdentifierProblem(
                "projectId",
                "The project identifier must be a valid UUID.");
        }

        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == parsedProjectId, cancellationToken);

        if (!projectExists)
        {
            return ProjectNotFoundProblem(parsedProjectId);
        }

        var proposals = await dbContext.FeatureProposals
            .AsNoTracking()
            .Where(proposal => proposal.ProjectId == parsedProjectId)
            .OrderByDescending(proposal => proposal.CreatedAtUtc)
            .ThenBy(proposal => proposal.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(proposals.Select(FeatureProposalResponse.FromDomain));
    }

    private static async Task<IResult> AcceptFeatureProposalAsync(
        string projectId,
        string proposalId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var parsedProjectId = identifiers.ProjectId;
        var parsedProposalId = identifiers.ProposalId;
        var proposalResult = await FindProposalForDecisionAsync(
            parsedProjectId,
            parsedProposalId,
            dbContext,
            cancellationToken);

        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        var proposal = proposalResult.Proposal!;

        try
        {
            proposal.Accept(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (FeatureProposalTransitionException)
        {
            return FeatureProposalTransitionConflictProblem(parsedProposalId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FeatureProposalTransitionConflictProblem(parsedProposalId);
        }

        return Results.Ok(FeatureProposalResponse.FromDomain(proposal));
    }

    private static async Task<IResult> RejectFeatureProposalAsync(
        string projectId,
        string proposalId,
        RejectFeatureProposalRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, proposalId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = FeatureProposal.ValidateRejectionReason(request.Reason);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var parsedProjectId = identifiers.ProjectId;
        var parsedProposalId = identifiers.ProposalId;
        var proposalResult = await FindProposalForDecisionAsync(
            parsedProjectId,
            parsedProposalId,
            dbContext,
            cancellationToken);

        if (proposalResult.Error is not null)
        {
            return proposalResult.Error;
        }

        var proposal = proposalResult.Proposal!;

        try
        {
            proposal.Reject(request.Reason, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (FeatureProposalTransitionException)
        {
            return FeatureProposalTransitionConflictProblem(parsedProposalId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FeatureProposalTransitionConflictProblem(parsedProposalId);
        }

        return Results.Ok(FeatureProposalResponse.FromDomain(proposal));
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

    private static async Task<(FeatureProposal? Proposal, IResult? Error)>
        FindProposalForDecisionAsync(
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

    private static IResult FeatureProposalTransitionConflictProblem(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Feature proposal transition conflict",
            detail: $"Feature proposal '{proposalId}' has already been decided.");
}
