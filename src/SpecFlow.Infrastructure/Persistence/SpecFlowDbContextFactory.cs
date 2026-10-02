using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpecFlow.Infrastructure.Persistence;

public sealed class SpecFlowDbContextFactory : IDesignTimeDbContextFactory<SpecFlowDbContext>
{
    public SpecFlowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SpecFlowDbContext>()
            .UseSqlite("Data Source=specflow.design.db")
            .Options;

        return new SpecFlowDbContext(options);
    }
}
