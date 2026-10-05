using Microsoft.EntityFrameworkCore;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.Projects;

namespace SpecFlow.Infrastructure.Persistence;

public sealed class SpecFlowDbContext(DbContextOptions<SpecFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<FeatureProposal> FeatureProposals => Set<FeatureProposal>();

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpecFlowDbContext).Assembly);
    }
}
