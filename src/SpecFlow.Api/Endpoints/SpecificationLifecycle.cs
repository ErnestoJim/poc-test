using Microsoft.EntityFrameworkCore;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

internal static class SpecificationLifecycle
{
    public static IResult? EnsureEditable(Specification specification)
    {
        try
        {
            specification.EnsureEditable();
            return null;
        }
        catch (SpecificationStateException)
        {
            return EndpointProblems.SpecificationNotEditable();
        }
    }

    public static IResult? EnsureApprovedForImplementation(Specification specification)
    {
        try
        {
            specification.EnsureApprovedForImplementation();
            return null;
        }
        catch (SpecificationStateException)
        {
            return EndpointProblems.SpecificationNotApprovedForImplementation();
        }
    }

    public static async Task<bool> TryAdvanceAggregateVersionAsync(
        Specification specification,
        SpecificationStatus requiredStatus,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.Specifications
            .Where(existingSpecification =>
                existingSpecification.Id == specification.Id &&
                existingSpecification.Version == specification.Version &&
                existingSpecification.Status == requiredStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    existingSpecification => existingSpecification.Version,
                    existingSpecification => existingSpecification.Version + 1),
                cancellationToken);

        return affectedRows == 1;
    }
}
