using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.ImplementationTasks;

public sealed class ImplementationTaskLifecycleMigrationTests
{
    private const string AddImplementationTasksMigration =
        "20261007101153_AddImplementationTasks";

    [Fact]
    public async Task LifecycleMigration_UpdatesExistingTaskToPending()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var databasePath = Path.Combine(temporaryDirectory, "specflow.db");
            var options = new DbContextOptionsBuilder<SpecFlowDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var dbContext = new SpecFlowDbContext(options);
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(AddImplementationTasksMigration);
            var projectId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var specificationId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var createdAtUtc = new DateTimeOffset(
                2026,
                10,
                7,
                10,
                30,
                0,
                TimeSpan.Zero).ToUnixTimeMilliseconds();

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Projects
                    (Id, Name, NormalizedName, Description, CreatedAtUtc)
                VALUES
                    ({projectId}, {"Project"}, {"PROJECT"}, {null}, {createdAtUtc})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO FeatureProposals
                    (Id, ProjectId, Title, Description, Status, DecidedAtUtc,
                     RejectionReason, CreatedAtUtc, Version)
                VALUES
                    ({proposalId}, {projectId}, {"Proposal"}, {null}, {"Accepted"},
                     {createdAtUtc}, {null}, {createdAtUtc}, {1})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Specifications
                    (Id, FeatureProposalId, Content, CreatedAtUtc, UpdatedAtUtc,
                     AcceptanceCriteriaVersion, ImplementationTasksVersion)
                VALUES
                    ({specificationId}, {proposalId}, {"# Specification"},
                     {createdAtUtc}, {createdAtUtc}, {0}, {1})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ImplementationTasks
                    (Id, SpecificationId, Title, NormalizedTitle, Description,
                     Position, CreatedAtUtc, UpdatedAtUtc, Version)
                VALUES
                    ({taskId}, {specificationId}, {"Existing task"},
                     {"EXISTING TASK"}, {null}, {1}, {createdAtUtc},
                     {createdAtUtc}, {0})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var implementationTask = await dbContext.ImplementationTasks
                .AsNoTracking()
                .SingleAsync(item => item.Id == taskId);
            Assert.Equal(ImplementationTaskStatus.Pending, implementationTask.Status);
            Assert.Null(implementationTask.StartedAtUtc);
            Assert.Null(implementationTask.CompletedAtUtc);
            Assert.Equal(0, implementationTask.Version);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
