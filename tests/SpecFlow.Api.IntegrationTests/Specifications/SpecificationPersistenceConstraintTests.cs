using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationPersistenceConstraintTests
{
    [Fact]
    public async Task Save_WithUnknownProposal_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var specification = Specification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "# Orphan specification",
            factory.TimeProvider.GetUtcNow());
        dbContext.Specifications.Add(specification);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_SecondSpecificationForProposal_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var duplicate = Specification.Create(
            Guid.NewGuid(),
            proposal.Id,
            "# Duplicate",
            factory.TimeProvider.GetUtcNow());
        dbContext.Specifications.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }
}
