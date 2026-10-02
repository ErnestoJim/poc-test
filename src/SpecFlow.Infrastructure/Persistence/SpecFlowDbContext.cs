using Microsoft.EntityFrameworkCore;
using SpecFlow.Domain.Projects;

namespace SpecFlow.Infrastructure.Persistence;

public sealed class SpecFlowDbContext(DbContextOptions<SpecFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpecFlowDbContext).Assembly);
    }
}
