using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class HttpConcurrencyMigrationTests
{
    private const string PreviousMigration =
        "20261007143557_AddImplementationTaskLifecycle";

    [Fact]
    public async Task Migration_AddsZeroVersionToExistingSpecification()
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
                     {createdAtUtc}, {createdAtUtc}, {0}, {0})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var specification = await dbContext.Specifications
                .AsNoTracking()
                .SingleAsync(item => item.Id == specificationId);
            Assert.Equal(0, specification.Version);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
