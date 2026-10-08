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

    [Fact]
    public async Task Delete_ReferencedReplacement_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var timestamp = factory.TimeProvider.GetUtcNow();
        var original = TechnicalDecision.Create(
            Guid.NewGuid(),
            project.Id,
            "Original",
            "# Original",
            timestamp);
        var replacement = TechnicalDecision.Create(
            Guid.NewGuid(),
            project.Id,
            "Replacement",
            "# Replacement",
            timestamp);
        original.Accept(timestamp.AddHours(1));
        replacement.Accept(timestamp.AddHours(2));
        original.SupersedeWith(replacement, timestamp.AddHours(3));
        dbContext.TechnicalDecisions.AddRange(original, replacement);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var persistedReplacement = await dbContext.TechnicalDecisions.SingleAsync(
            decision => decision.Id == replacement.Id);
        dbContext.TechnicalDecisions.Remove(persistedReplacement);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
