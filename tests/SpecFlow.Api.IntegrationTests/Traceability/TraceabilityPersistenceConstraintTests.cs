using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.Traceability;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityPersistenceConstraintTests
{
    [Fact]
    public async Task Save_DuplicateLink_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTaskAcceptanceCriteria.Add(
            CreateLink(context.Task.Id, context.Criterion.Id));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        dbContext.ImplementationTaskAcceptanceCriteria.Add(
            CreateLink(context.Task.Id, context.Criterion.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_LinkWithUnknownTask_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTaskAcceptanceCriteria.Add(
            CreateLink(Guid.NewGuid(), context.Criterion.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_LinkWithUnknownCriterion_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await TraceabilityTestData.CreateContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTaskAcceptanceCriteria.Add(
            CreateLink(context.Task.Id, Guid.NewGuid()));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static ImplementationTaskAcceptanceCriterion CreateLink(
        Guid taskId,
        Guid criterionId) =>
        ImplementationTaskAcceptanceCriterion.Create(taskId, criterionId);
}
