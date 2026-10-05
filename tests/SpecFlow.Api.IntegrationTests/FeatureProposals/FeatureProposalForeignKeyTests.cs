using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FeatureProposalForeignKeyTests
{
    [Fact]
    public async Task Save_WithUnknownProject_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var proposal = FeatureProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Orphan proposal",
            null,
            factory.TimeProvider.GetUtcNow());
        dbContext.FeatureProposals.Add(proposal);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }
}
