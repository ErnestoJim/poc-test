using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FeatureProposalMigrationTests
{
    private const string AddFeatureProposalsMigration =
        "20261005200256_AddFeatureProposals";

    [Fact]
    public async Task LifecycleMigration_UpdatesExistingProposalToPending()
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
            await migrator.MigrateAsync(AddFeatureProposalsMigration);
            var projectId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var createdAtUtc = new DateTimeOffset(
                2026,
                10,
                5,
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
                    (Id, ProjectId, Title, Description, CreatedAtUtc)
                VALUES
                    ({proposalId}, {projectId}, {"Existing proposal"}, {null}, {createdAtUtc})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var proposal = await dbContext.FeatureProposals
                .AsNoTracking()
                .SingleAsync(item => item.Id == proposalId);
            Assert.Equal(FeatureProposalStatus.Pending, proposal.Status);
            Assert.Null(proposal.DecidedAtUtc);
            Assert.Null(proposal.RejectionReason);
            Assert.Equal(0, proposal.Version);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
