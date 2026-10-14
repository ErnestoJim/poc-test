using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class AcceptanceCriterionVerificationEndpoints
{
    public static RouteGroupBuilder MapAcceptanceCriterionVerificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification")
            .WithTags("Acceptance Criteria");

        group.MapGet("/", GetVerificationAsync)
            .WithName("GetAcceptanceCriterionVerification")
            .Produces<AcceptanceCriterionVerificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/", SaveVerificationAsync)
            .WithName("SaveAcceptanceCriterionVerification")
            .Accepts<SaveAcceptanceCriterionVerificationRequest>("application/json")
            .Produces<AcceptanceCriterionVerificationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return group;
    }

    private static async Task<IResult> GetVerificationAsync(
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

        var (criterion, error) = await FindCriterionAsync(
            identifiers.ProjectId,
            identifiers.ProposalId,
            identifiers.CriterionId,
            dbContext,
            tracking: false,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return WithEntityTag(
            httpContext,
            criterion!,
            Results.Ok(AcceptanceCriterionVerificationResponse.FromDomain(criterion!)));
    }

    private static async Task<IResult> SaveVerificationAsync(
        string projectId,
        string proposalId,
        string criterionId,
        SaveAcceptanceCriterionVerificationRequest request,
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

        var status = ParseStatus(request.Status);
        if (status is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["status"] = ["The verification status must be unverified, passed, or failed."]
            });
        }

        var validationErrors = AcceptanceCriterion.ValidateVerification(status.Value, request.Evidence);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await SpecificationContextResolver.FindAsync(
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
                item => item.SpecificationId == specification.Id &&
                        item.Id == identifiers.CriterionId,
                cancellationToken);
        if (criterion is null)
        {
            return CriterionNotFound(identifiers.CriterionId);
        }

        var preconditionError = HttpEntityTags.ValidateIfMatch(
            httpContext,
            CreateEntityTag(criterion));
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        if (specification.Status != SpecificationStatus.Implemented)
        {
            return SpecificationNotReady();
        }

        var changed = criterion.SetVerification(
            status.Value,
            request.Evidence,
            timeProvider.GetUtcNow());
        if (!changed)
        {
            return WithEntityTag(
                httpContext,
                criterion,
                Results.Ok(AcceptanceCriterionVerificationResponse.FromDomain(criterion)));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await SpecificationLifecycle.TryAdvanceAggregateVersionAsync(
                    specification,
                    SpecificationStatus.Implemented,
                    dbContext,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return EndpointProblems.PreconditionFailed();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return EndpointProblems.PreconditionFailed();
        }

        return WithEntityTag(
            httpContext,
            criterion,
            Results.Ok(AcceptanceCriterionVerificationResponse.FromDomain(criterion)));
    }

    private static async Task<(AcceptanceCriterion? Criterion, IResult? Error)> FindCriterionAsync(
        Guid projectId,
        Guid proposalId,
        Guid criterionId,
        SpecFlowDbContext dbContext,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var context = await SpecificationContextResolver.FindAsync(
            projectId,
            proposalId,
            dbContext,
            tracking: false,
            cancellationToken);
        if (context.Error is not null)
        {
            return (null, context.Error);
        }

        IQueryable<AcceptanceCriterion> query = dbContext.AcceptanceCriteria;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var criterion = await query.SingleOrDefaultAsync(
            item => item.SpecificationId == context.Specification!.Id && item.Id == criterionId,
            cancellationToken);
        return criterion is null
            ? (null, CriterionNotFound(criterionId))
            : (criterion, null);
    }

    private static (Guid ProjectId, Guid ProposalId, Guid CriterionId, IResult? Error)
        ParseIdentifiers(string projectId, string proposalId, string criterionId)
    {
        var project = RouteIdentifierParser.ParseProject(projectId);
        if (project.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, project.Error);
        }

        var proposal = RouteIdentifierParser.ParseFeatureProposal(proposalId);
        if (proposal.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, proposal.Error);
        }

        var criterion = RouteIdentifierParser.ParseAcceptanceCriterion(criterionId);
        if (criterion.Error is not null)
        {
            return (Guid.Empty, Guid.Empty, Guid.Empty, criterion.Error);
        }

        return (project.Identifier, proposal.Identifier, criterion.Identifier, null);
    }

    private static AcceptanceCriterionVerificationStatus? ParseStatus(string? status) =>
        status switch
        {
            "unverified" => AcceptanceCriterionVerificationStatus.Unverified,
            "passed" => AcceptanceCriterionVerificationStatus.Passed,
            "failed" => AcceptanceCriterionVerificationStatus.Failed,
            _ => null
        };

    private static IResult CriterionNotFound(Guid criterionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Acceptance criterion not found",
            detail: $"No acceptance criterion with identifier '{criterionId}' was found in this specification.");

    private static IResult SpecificationNotReady() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Specification is not ready for criterion verification",
            detail: "Criteria can only be verified while the specification is implemented.");

    private static string CreateEntityTag(AcceptanceCriterion criterion) =>
        HttpEntityTags.CreateResource(
            "acceptance-criterion-verification",
            criterion.Id,
            criterion.VerificationVersion);

    private static IResult WithEntityTag(
        HttpContext httpContext,
        AcceptanceCriterion criterion,
        IResult result) =>
        HttpEntityTags.WithEntityTag(httpContext, CreateEntityTag(criterion), result);
}
