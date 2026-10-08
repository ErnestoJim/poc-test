using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationLifecycleMigrationTests
{
    private const string PreviousMigration =
        "20261008111209_AddAcceptanceCriterionTaskTraceability";

    [Fact]
    public async Task LifecycleMigration_PreservesExistingSpecificationAsDraftAndAdvancesVersion()
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
                     Version, AcceptanceCriteriaVersion, ImplementationTasksVersion)
                VALUES
                    ({specificationId}, {proposalId}, {"# Existing"},
                     {createdAtUtc}, {updatedAtUtc}, {7}, {2}, {3})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var specification = await dbContext.Specifications
                .AsNoTracking()
                .SingleAsync(item => item.Id == specificationId);
            Assert.Equal(proposalId, specification.FeatureProposalId);
            Assert.Equal("# Existing", specification.Content);
            Assert.Equal(SpecificationStatus.Draft, specification.Status);
            Assert.Null(specification.ApprovedAtUtc);
            Assert.Null(specification.ImplementedAtUtc);
            Assert.Null(specification.VerifiedAtUtc);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(createdAtUtc), specification.CreatedAtUtc);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(updatedAtUtc), specification.UpdatedAtUtc);
            Assert.Equal(8, specification.Version);
            Assert.Equal(2, specification.AcceptanceCriteriaVersion);
            Assert.Equal(3, specification.ImplementationTasksVersion);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
