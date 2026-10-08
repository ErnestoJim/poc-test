using Microsoft.EntityFrameworkCore;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Projects;
using SpecFlow.Domain.Specifications;
using SpecFlow.Domain.TechnicalDecisions;
using SpecFlow.Domain.Traceability;

namespace SpecFlow.Infrastructure.Persistence;

public sealed class SpecFlowDbContext(DbContextOptions<SpecFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();

    public DbSet<FeatureProposal> FeatureProposals => Set<FeatureProposal>();

    public DbSet<ImplementationTask> ImplementationTasks => Set<ImplementationTask>();

    public DbSet<ImplementationTaskAcceptanceCriterion>
        ImplementationTaskAcceptanceCriteria => Set<ImplementationTaskAcceptanceCriterion>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Specification> Specifications => Set<Specification>();

    public DbSet<TechnicalDecision> TechnicalDecisions => Set<TechnicalDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpecFlowDbContext).Assembly);
    }
}
