using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskConcurrencyTests
{
    [Fact]
    public async Task CompetingDuplicateCreations_OnlyOneTaskIsCreated()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            using var factory = new SpecFlowApiFactory(connectionString);
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();
            var context = await ImplementationTaskTestData
                .CreateSpecificationContextAsync(firstClient);
            var route = ImplementationTaskTestData.TasksRoute(
                context.Project.Id,
                context.Proposal.Id);

            var responses = await Task.WhenAll(
                firstClient.PostAsJsonAsync(
                    route,
                    new SaveImplementationTaskRequest("Duplicate", null)),
                secondClient.PostAsJsonAsync(
                    route,
                    new SaveImplementationTaskRequest("  duplicate  ", null)));

            try
            {
                Assert.Contains(
                    responses,
                    response => response.StatusCode == HttpStatusCode.Created);
                var conflict = Assert.Single(
                    responses,
                    response => response.StatusCode == HttpStatusCode.Conflict);
                var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal("Implementation task already exists", problem.Title);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CompetingTaskCollectionChanges_AreDetectedByInternalVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var originalVersion = await firstDbContext.Specifications
            .Where(specification => specification.Id == context.Specification.Id)
            .Select(specification => specification.ImplementationTasksVersion)
            .SingleAsync();

        var firstResult = await AdvanceTaskVersionAsync(firstDbContext, originalVersion);
        var secondResult = await AdvanceTaskVersionAsync(secondDbContext, originalVersion);

        Assert.Equal(1, firstResult);
        Assert.Equal(0, secondResult);

        Task<int> AdvanceTaskVersionAsync(
            SpecFlowDbContext dbContext,
            int expectedVersion) =>
            dbContext.Specifications
                .Where(specification =>
                    specification.Id == context.Specification.Id &&
                    specification.ImplementationTasksVersion == expectedVersion)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        specification => specification.ImplementationTasksVersion,
                        specification => specification.ImplementationTasksVersion + 1));
    }

    [Fact]
    public async Task AcceptanceCriteriaAndTaskCollectionVersions_AreIndependent()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var firstCopy = await firstDbContext.Specifications.SingleAsync(
            specification => specification.Id == context.Specification.Id);
        var secondCopy = await secondDbContext.Specifications.SingleAsync(
            specification => specification.Id == context.Specification.Id);

        firstCopy.MarkAcceptanceCriteriaChanged();
        await firstDbContext.SaveChangesAsync();
        var taskVersionUpdate = await secondDbContext.Specifications
            .Where(specification =>
                specification.Id == context.Specification.Id &&
                specification.ImplementationTasksVersion ==
                secondCopy.ImplementationTasksVersion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    specification => specification.ImplementationTasksVersion,
                    specification => specification.ImplementationTasksVersion + 1));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<SpecFlowDbContext>();
        var persisted = await verificationContext.Specifications
            .AsNoTracking()
            .SingleAsync(specification => specification.Id == context.Specification.Id);
        Assert.Equal(1, persisted.AcceptanceCriteriaVersion);
        Assert.Equal(1, persisted.ImplementationTasksVersion);
        Assert.Equal(1, taskVersionUpdate);
    }

    [Fact]
    public async Task CompetingTaskUpdates_AreDetectedByInternalVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await ImplementationTaskTestData
            .CreateSpecificationContextAsync(client);
        var created = await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var firstCopy = await firstDbContext.ImplementationTasks.SingleAsync(
            implementationTask => implementationTask.Id == created.Id);
        var secondCopy = await secondDbContext.ImplementationTasks.SingleAsync(
            implementationTask => implementationTask.Id == created.Id);
        var timestamp = factory.TimeProvider.GetUtcNow().AddHours(1);

        firstCopy.Update("First update", null, timestamp);
        secondCopy.Update("Second update", null, timestamp);
        await firstDbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDbContext.SaveChangesAsync());
    }
}
