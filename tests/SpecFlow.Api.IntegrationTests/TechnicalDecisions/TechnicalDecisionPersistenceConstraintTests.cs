using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.TechnicalDecisions;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class TechnicalDecisionPersistenceConstraintTests
{
    [Fact]
    public async Task Save_WithUnknownProject_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var decision = TechnicalDecision.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Orphan decision",
            "# Decision",
            factory.TimeProvider.GetUtcNow());
        dbContext.TechnicalDecisions.Add(decision);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
