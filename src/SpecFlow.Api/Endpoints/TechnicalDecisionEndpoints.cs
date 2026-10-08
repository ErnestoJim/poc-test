using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Domain.TechnicalDecisions;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class TechnicalDecisionEndpoints
{
    public static RouteGroupBuilder MapTechnicalDecisionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId}/technical-decisions")
            .WithTags("Technical Decisions");

        group.MapPost("/", CreateTechnicalDecisionAsync)
            .WithName("CreateTechnicalDecision")
            .Accepts<SaveTechnicalDecisionRequest>("application/json")
            .Produces<TechnicalDecisionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{decisionId}", GetTechnicalDecisionAsync)
            .WithName("GetTechnicalDecision")
            .Produces<TechnicalDecisionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListTechnicalDecisionsAsync)
            .WithName("ListTechnicalDecisions")
            .Produces<IReadOnlyList<TechnicalDecisionResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{decisionId}", UpdateTechnicalDecisionAsync)
            .WithName("UpdateTechnicalDecision")
            .Accepts<SaveTechnicalDecisionRequest>("application/json")
            .Produces<TechnicalDecisionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return group;
    }

    private static async Task<IResult> CreateTechnicalDecisionAsync(
        string projectId,
        SaveTechnicalDecisionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var projectIdentifier = RouteIdentifierParser.ParseProject(projectId);
        if (projectIdentifier.Error is not null)
        {
            return projectIdentifier.Error;
        }

        var validationErrors = TechnicalDecision.Validate(request.Title, request.Content);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (!await ProjectExistsAsync(
                projectIdentifier.Identifier,
                dbContext,
                cancellationToken))
        {
            return EndpointProblems.ProjectNotFound(projectIdentifier.Identifier);
        }

        var decision = TechnicalDecision.Create(
            Guid.NewGuid(),
            projectIdentifier.Identifier,
            request.Title,
            request.Content,
            timeProvider.GetUtcNow());
        dbContext.TechnicalDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);

        return WithEntityTag(
            httpContext,
            decision,
            Results.CreatedAtRoute(
                "GetTechnicalDecision",
                new
                {
                    projectId = projectIdentifier.Identifier,
                    decisionId = decision.Id
                },
                TechnicalDecisionResponse.FromDomain(decision)));
    }

    private static async Task<IResult> GetTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, decisionId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        if (!await ProjectExistsAsync(
                identifiers.ProjectId,
                dbContext,
                cancellationToken))
        {
            return EndpointProblems.ProjectNotFound(identifiers.ProjectId);
        }

        var decision = await dbContext.TechnicalDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingDecision =>
                    existingDecision.ProjectId == identifiers.ProjectId &&
                    existingDecision.Id == identifiers.DecisionId,
                cancellationToken);

        return decision is null
            ? EndpointProblems.TechnicalDecisionNotFound(identifiers.DecisionId)
            : WithEntityTag(
                httpContext,
                decision,
                Results.Ok(TechnicalDecisionResponse.FromDomain(decision)));
    }

    private static async Task<IResult> ListTechnicalDecisionsAsync(
        string projectId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var projectIdentifier = RouteIdentifierParser.ParseProject(projectId);
        if (projectIdentifier.Error is not null)
        {
            return projectIdentifier.Error;
        }

        if (!await ProjectExistsAsync(
                projectIdentifier.Identifier,
                dbContext,
                cancellationToken))
        {
            return EndpointProblems.ProjectNotFound(projectIdentifier.Identifier);
        }

        var decisions = await dbContext.TechnicalDecisions
            .AsNoTracking()
            .Where(decision => decision.ProjectId == projectIdentifier.Identifier)
            .OrderBy(decision => decision.CreatedAtUtc)
            .ThenBy(decision => decision.Id)
            .Select(decision => new TechnicalDecisionResponse(
                decision.Id,
                decision.ProjectId,
                decision.Title,
                decision.Content,
                decision.CreatedAtUtc,
                decision.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(decisions);
    }

    private static async Task<IResult> UpdateTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SaveTechnicalDecisionRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, decisionId);
        if (identifiers.Error is not null)
        {
            return identifiers.Error;
        }

        var validationErrors = TechnicalDecision.Validate(request.Title, request.Content);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (!await ProjectExistsAsync(
                identifiers.ProjectId,
                dbContext,
                cancellationToken))
        {
            return EndpointProblems.ProjectNotFound(identifiers.ProjectId);
        }

        var decision = await dbContext.TechnicalDecisions
            .SingleOrDefaultAsync(
                existingDecision =>
                    existingDecision.ProjectId == identifiers.ProjectId &&
                    existingDecision.Id == identifiers.DecisionId,
                cancellationToken);

        if (decision is null)
        {
            return EndpointProblems.TechnicalDecisionNotFound(identifiers.DecisionId);
        }

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(decision));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        decision.Update(request.Title, request.Content, timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointProblems.PreconditionFailed();
        }

        return WithEntityTag(
            httpContext,
            decision,
            Results.Ok(TechnicalDecisionResponse.FromDomain(decision)));
    }

    private static (Guid ProjectId, Guid DecisionId, IResult? Error) ParseIdentifiers(
        string projectId,
        string decisionId)
    {
        var projectIdentifier = RouteIdentifierParser.ParseProject(projectId);
        if (projectIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, projectIdentifier.Error);
        }

        var decisionIdentifier = RouteIdentifierParser.ParseTechnicalDecision(decisionId);
        if (decisionIdentifier.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, decisionIdentifier.Error);
        }

        return (projectIdentifier.Identifier, decisionIdentifier.Identifier, null);
    }

    private static Task<bool> ProjectExistsAsync(
        Guid projectId,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId, cancellationToken);

    private static string CreateEntityTag(TechnicalDecision decision) =>
        HttpEntityTags.CreateResource(
            "technical-decision",
            decision.Id,
            decision.Version);

    private static IResult WithEntityTag(
        HttpContext httpContext,
        TechnicalDecision decision,
        IResult result) =>
        HttpEntityTags.WithEntityTag(httpContext, CreateEntityTag(decision), result);
}
