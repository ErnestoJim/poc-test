using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.Specifications;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class AcceptanceCriterionVerificationMigrationTests
{
    private const string PreviousMigration = "20261008114751_AddSpecificationLifecycle";

    [Fact]
    public async Task Migration_PreservesVerifiedSpecificationWithoutInventingResults()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var options = new DbContextOptionsBuilder<SpecFlowDbContext>()
                .UseSqlite($"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}")
                .Options;
            await using var dbContext = new SpecFlowDbContext(options);
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            var projectId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var specificationId = Guid.NewGuid();
            var criterionId = Guid.NewGuid();
            var timestamp = new DateTimeOffset(2026, 10, 8, 8, 30, 0, TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            const string Content = "Existing criterion";

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
                    (Id, FeatureProposalId, Content, Status, ApprovedAtUtc,
                     ImplementedAtUtc, VerifiedAtUtc, CreatedAtUtc, UpdatedAtUtc,
                     Version, AcceptanceCriteriaVersion, ImplementationTasksVersion)
                VALUES
                    ({specificationId}, {proposalId}, {"# Existing"}, {"Verified"},
                     {timestamp}, {timestamp}, {timestamp}, {timestamp},
                     {timestamp}, {3}, {1}, {1})
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO AcceptanceCriteria
                    (Id, SpecificationId, Content, ContentHash, Position,
                     CreatedAtUtc, UpdatedAtUtc, Version)
                VALUES
                    ({criterionId}, {specificationId}, {Content},
                     {AcceptanceCriterion.CalculateContentHash(Content)}, {1},
                     {timestamp}, {timestamp}, {0})
                """);

            await migrator.MigrateAsync();
            dbContext.ChangeTracker.Clear();

            var specification = await dbContext.Specifications
                .AsNoTracking()
                .SingleAsync(item => item.Id == specificationId);
            var criterion = await dbContext.AcceptanceCriteria
                .AsNoTracking()
                .SingleAsync(item => item.Id == criterionId);
            Assert.Equal(SpecificationStatus.Verified, specification.Status);
            Assert.NotNull(specification.VerifiedAtUtc);
            Assert.Equal(3, specification.Version);
            Assert.Equal(AcceptanceCriterionVerificationStatus.Unverified,
                criterion.VerificationStatus);
            Assert.Null(criterion.VerificationEvidence);
            Assert.Null(criterion.CheckedAtUtc);
            Assert.Equal(0, criterion.VerificationVersion);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
