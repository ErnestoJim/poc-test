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
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapPost("/{decisionId}/accept", AcceptTechnicalDecisionAsync)
            .WithName("AcceptTechnicalDecision")
            .Produces<TechnicalDecisionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapPost("/{decisionId}/reject", RejectTechnicalDecisionAsync)
            .WithName("RejectTechnicalDecision")
            .Accepts<RejectTechnicalDecisionRequest>("application/json")
            .Produces<TechnicalDecisionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapPost("/{decisionId}/supersede", SupersedeTechnicalDecisionAsync)
            .WithName("SupersedeTechnicalDecision")
            .Accepts<SupersedeTechnicalDecisionRequest>("application/json")
            .Produces<TechnicalDecisionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapDelete("/{decisionId}", DeleteTechnicalDecisionAsync)
            .WithName("DeleteTechnicalDecision")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

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
            .ToListAsync(cancellationToken);

        return Results.Ok(decisions.Select(TechnicalDecisionResponse.FromDomain));
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

        try
        {
            decision.Update(request.Title, request.Content, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (TechnicalDecisionStateException)
        {
            return TechnicalDecisionNotEditableProblem(decision.Id);
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

    private static Task<IResult> AcceptTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        TransitionTechnicalDecisionAsync(
            projectId,
            decisionId,
            dbContext,
            timeProvider,
            httpContext,
            static (decision, timestamp) => decision.Accept(timestamp),
            cancellationToken);

    private static async Task<IResult> RejectTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        RejectTechnicalDecisionRequest request,
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

        var validationErrors = TechnicalDecision.ValidateRejectionReason(request.Reason);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        return await TransitionTechnicalDecisionAsync(
            projectId,
            decisionId,
            dbContext,
            timeProvider,
            httpContext,
            (decision, timestamp) => decision.Reject(request.Reason, timestamp),
            cancellationToken);
    }

    private static async Task<IResult> SupersedeTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SupersedeTechnicalDecisionRequest request,
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

        if (request.ReplacementDecisionId == Guid.Empty)
        {
            return EndpointProblems.InvalidIdentifier(
                "replacementDecisionId",
                "The replacement technical decision identifier must be a non-empty UUID.");
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

        var replacement = await dbContext.TechnicalDecisions
            .SingleOrDefaultAsync(
                existingDecision =>
                    existingDecision.ProjectId == identifiers.ProjectId &&
                    existingDecision.Id == request.ReplacementDecisionId,
                cancellationToken);

        if (replacement is null)
        {
            return EndpointProblems.ReplacementTechnicalDecisionNotFound(
                request.ReplacementDecisionId);
        }

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(decision));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        try
        {
            decision.SupersedeWith(replacement, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (TechnicalDecisionTransitionException)
        {
            return TechnicalDecisionTransitionConflictProblem(decision.Id);
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

    private static async Task<IResult> DeleteTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SpecFlowDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var decisionResult = await FindTechnicalDecisionAsync(
            projectId,
            decisionId,
            dbContext,
            cancellationToken);
        if (decisionResult.Error is not null)
        {
            return decisionResult.Error;
        }

        var decision = decisionResult.Decision!;
        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(decision));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        try
        {
            decision.EnsureCanDelete();
            dbContext.TechnicalDecisions.Remove(decision);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (TechnicalDecisionStateException)
        {
            return TechnicalDecisionCannotBeDeletedProblem(decision.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointProblems.PreconditionFailed();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> TransitionTechnicalDecisionAsync(
        string projectId,
        string decisionId,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        Action<TechnicalDecision, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var decisionResult = await FindTechnicalDecisionAsync(
            projectId,
            decisionId,
            dbContext,
            cancellationToken);
        if (decisionResult.Error is not null)
        {
            return decisionResult.Error;
        }

        var decision = decisionResult.Decision!;
        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(decision));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        try
        {
            transition(decision, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (TechnicalDecisionTransitionException)
        {
            return TechnicalDecisionTransitionConflictProblem(decision.Id);
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

    private static async Task<(TechnicalDecision? Decision, IResult? Error)>
        FindTechnicalDecisionAsync(
            string projectId,
            string decisionId,
            SpecFlowDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var identifiers = ParseIdentifiers(projectId, decisionId);
        if (identifiers.Error is not null)
        {
            return (null, identifiers.Error);
        }

        return await FindTechnicalDecisionAsync(
            identifiers.ProjectId,
            identifiers.DecisionId,
            dbContext,
            cancellationToken);
    }

    private static async Task<(TechnicalDecision? Decision, IResult? Error)>
        FindTechnicalDecisionAsync(
            Guid projectId,
            Guid decisionId,
            SpecFlowDbContext dbContext,
            CancellationToken cancellationToken)
    {
        if (!await ProjectExistsAsync(projectId, dbContext, cancellationToken))
        {
            return (null, EndpointProblems.ProjectNotFound(projectId));
        }

        var decision = await dbContext.TechnicalDecisions
            .SingleOrDefaultAsync(
                existingDecision =>
                    existingDecision.ProjectId == projectId &&
                    existingDecision.Id == decisionId,
                cancellationToken);

        return decision is null
            ? (null, EndpointProblems.TechnicalDecisionNotFound(decisionId))
            : (decision, null);
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

    private static IResult TechnicalDecisionNotEditableProblem(Guid decisionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Technical decision is not editable",
            detail: $"Technical decision '{decisionId}' is no longer a draft.");

    private static IResult TechnicalDecisionTransitionConflictProblem(Guid decisionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Technical decision transition conflict",
            detail: $"Technical decision '{decisionId}' cannot perform the requested transition.");

    private static IResult TechnicalDecisionCannotBeDeletedProblem(Guid decisionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Technical decision cannot be deleted",
            detail: $"Technical decision '{decisionId}' is no longer a draft.");
}
