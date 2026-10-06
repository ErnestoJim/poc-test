using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class AcceptanceCriterionPersistenceConstraintTests
{
    [Fact]
    public async Task Save_WithUnknownSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var criterion = CreateCriterion(Guid.NewGuid(), "Orphan", 1);
        dbContext.AcceptanceCriteria.Add(criterion);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_DuplicateContentInSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.AcceptanceCriteria.AddRange(
            CreateCriterion(context.Specification.Id, "Duplicate", 1),
            CreateCriterion(context.Specification.Id, "Duplicate", 2));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_DuplicatePositionInSpecification_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        dbContext.AcceptanceCriteria.AddRange(
            CreateCriterion(context.Specification.Id, "First", 1),
            CreateCriterion(context.Specification.Id, "Second", 1));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_NonPositivePosition_IsRejectedByDatabase()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var criterion = CreateCriterion(context.Specification.Id, "Criterion", 1);
        dbContext.AcceptanceCriteria.Add(criterion);
        dbContext.Entry(criterion).Property(item => item.Position).CurrentValue = 0;

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    private static AcceptanceCriterion CreateCriterion(
        Guid specificationId,
        string content,
        int position) =>
        AcceptanceCriterion.Create(
            Guid.NewGuid(),
            specificationId,
            content,
            position,
            new DateTimeOffset(2026, 10, 8, 8, 30, 0, TimeSpan.Zero));
}
