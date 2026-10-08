using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Domain.TechnicalDecisions;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class TechnicalDecisionLifecycleMigrationTests
{
    private const string AddTechnicalDecisionsMigration =
        "20261008071620_AddTechnicalDecisions";

    [Fact]
    public async Task LifecycleMigration_PreservesExistingDecisionAsDraftAndAdvancesVersion()
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
            await migrator.MigrateAsync(AddTechnicalDecisionsMigration);
            var projectId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var createdAtUtc = new DateTimeOffset(
                2026,
                10,
                8,
                8,
                30,
                0,
                TimeSpan.Zero).ToUnixTimeMilliseconds();
            var updatedAtUtc = createdAtUtc + 3_600_000;

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Projects
                    (Id, Name, NormalizedName, Description, CreatedAtUtc)
                VALUES
                    ({projectId}, {"Project"}, {"PROJECT"}, {null}, {createdAtUtc})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO TechnicalDecisions
                    (Id, ProjectId, Title, Content, CreatedAtUtc, UpdatedAtUtc, Version)
                VALUES
                    ({decisionId}, {projectId}, {"Existing decision"}, {"# Decision"},
                     {createdAtUtc}, {updatedAtUtc}, {7})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var decision = await dbContext.TechnicalDecisions
                .AsNoTracking()
                .SingleAsync(item => item.Id == decisionId);
            Assert.Equal(projectId, decision.ProjectId);
            Assert.Equal("Existing decision", decision.Title);
            Assert.Equal("# Decision", decision.Content);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(createdAtUtc), decision.CreatedAtUtc);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(updatedAtUtc), decision.UpdatedAtUtc);
            Assert.Equal(TechnicalDecisionStatus.Draft, decision.Status);
            Assert.Null(decision.DecidedAtUtc);
            Assert.Null(decision.RejectionReason);
            Assert.Null(decision.SupersededAtUtc);
            Assert.Null(decision.SupersededByDecisionId);
            Assert.Equal(8, decision.Version);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
