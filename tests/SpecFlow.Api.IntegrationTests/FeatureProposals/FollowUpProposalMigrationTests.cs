using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FollowUpProposalMigrationTests
{
    private const string PreviousMigration = "20261014110330_AddAcceptanceCriterionVerification";

    [Fact]
    public async Task Migration_PreservesExistingProposalAndAddsNullableSource()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var options = new DbContextOptionsBuilder<SpecFlowDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "specflow.db")}")
                .Options;
            await using var dbContext = new SpecFlowDbContext(options);
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            var projectId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var timestamp = new DateTimeOffset(2026, 10, 14, 8, 30, 0,
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
                    ({proposalId}, {projectId}, {"Existing proposal"}, {"Description"},
                     {"Accepted"}, {timestamp}, {null}, {timestamp}, {1})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var proposal = await dbContext.FeatureProposals.AsNoTracking()
                .SingleAsync(item => item.Id == proposalId);
            Assert.Equal(projectId, proposal.ProjectId);
            Assert.Equal("Existing proposal", proposal.Title);
            Assert.Equal("Description", proposal.Description);
            Assert.Equal("Accepted", proposal.Status.ToString());
            Assert.Equal(1, proposal.Version);
            Assert.Null(proposal.SourceProposalId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
