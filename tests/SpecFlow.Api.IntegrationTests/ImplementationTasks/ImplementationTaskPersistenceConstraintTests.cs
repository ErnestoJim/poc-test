using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskPersistenceConstraintTests
{
    [Fact]
    public async Task Save_WithUnknownSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTasks.Add(CreateTask(Guid.NewGuid(), "Orphan", 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_DuplicateNormalizedTitleInSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTasks.AddRange(
            CreateTask(context.Specification.Id, "Duplicate", 1),
            CreateTask(context.Specification.Id, "duplicate", 2));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_DuplicatePositionInSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.ImplementationTasks.AddRange(
            CreateTask(context.Specification.Id, "First", 1),
            CreateTask(context.Specification.Id, "Second", 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_NonPositivePosition_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var implementationTask = CreateTask(context.Specification.Id, "Task", 1);
        dbContext.ImplementationTasks.Add(implementationTask);
        dbContext.Entry(implementationTask).Property(item => item.Position).CurrentValue = 0;

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static ImplementationTask CreateTask(
        Guid specificationId,
        string title,
        int position) =>
        ImplementationTask.Create(
            Guid.NewGuid(),
            specificationId,
            title,
            null,
            position,
            new DateTimeOffset(2026, 10, 8, 8, 30, 0, TimeSpan.Zero));
}
