using Microsoft.EntityFrameworkCore;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

internal static class SpecificationContextResolver
{
    public static async Task<(Specification? Specification, IResult? Error)> FindAsync(
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
            return (null, EndpointProblems.ProjectNotFound(projectId));
        }

        var proposalExists = await dbContext.FeatureProposals
            .AnyAsync(
                proposal => proposal.Id == proposalId && proposal.ProjectId == projectId,
                cancellationToken);

        if (!proposalExists)
        {
            return (null, EndpointProblems.FeatureProposalNotFound(proposalId));
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
            ? (null, EndpointProblems.SpecificationNotFound(proposalId))
            : (specification, null);
    }
}
