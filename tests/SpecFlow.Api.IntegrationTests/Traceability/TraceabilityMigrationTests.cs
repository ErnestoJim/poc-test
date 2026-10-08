using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Traceability;

public sealed class TraceabilityMigrationTests
{
    private const string PreviousMigration =
        "20261008101149_AddTechnicalDecisionLifecycle";

    [Fact]
    public async Task Migration_AddsEmptyTraceabilityCollectionToExistingTask()
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
            await migrator.MigrateAsync(PreviousMigration);
            var projectId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var specificationId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var timestamp = new DateTimeOffset(
                2026,
                10,
                8,
                8,
                30,
                0,
                TimeSpan.Zero).ToUnixTimeMilliseconds();

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Projects
                    (Id, Name, NormalizedName, Description, CreatedAtUtc)
                VALUES
                    ({projectId}, {"Project"}, {"PROJECT"}, {null}, {timestamp})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO FeatureProposals
                    (Id, ProjectId, Title, Description, Status, DecidedAtUtc,
                     RejectionReason, CreatedAtUtc, Version)
                VALUES
                    ({proposalId}, {projectId}, {"Proposal"}, {null}, {"Accepted"},
                     {timestamp}, {null}, {timestamp}, {1})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Specifications
                    (Id, FeatureProposalId, Content, CreatedAtUtc, UpdatedAtUtc,
                     AcceptanceCriteriaVersion, ImplementationTasksVersion, Version)
                VALUES
                    ({specificationId}, {proposalId}, {"# Specification"},
                     {timestamp}, {timestamp}, {0}, {1}, {0})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ImplementationTasks
                    (Id, SpecificationId, Title, NormalizedTitle, Description,
                     Status, StartedAtUtc, CompletedAtUtc, Position, CreatedAtUtc,
                     UpdatedAtUtc, Version)
                VALUES
                    ({taskId}, {specificationId}, {"Existing task"},
                     {"EXISTING TASK"}, {null}, {"Pending"}, {null}, {null}, {1},
                     {timestamp}, {timestamp}, {0})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var task = await dbContext.ImplementationTasks
                .AsNoTracking()
                .SingleAsync(item => item.Id == taskId);
            Assert.Equal(0, task.AcceptanceCriteriaVersion);
            Assert.Empty(await dbContext.ImplementationTaskAcceptanceCriteria.ToListAsync());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
