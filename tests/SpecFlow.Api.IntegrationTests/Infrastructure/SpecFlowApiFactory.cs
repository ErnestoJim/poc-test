using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Infrastructure;

internal sealed class SpecFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string? _connectionString;
    private SqliteConnection? _connection;

    public SpecFlowApiFactory(string? connectionString = null)
    {
        _connectionString = connectionString;
    }

    public MutableTimeProvider TimeProvider { get; } = new(
        new DateTimeOffset(2026, 10, 2, 8, 30, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SpecFlowDbContext>>();
            services.RemoveAll<SpecFlowDbContext>();
            services.RemoveAll<TimeProvider>();

            if (_connectionString is null)
            {
                _connection = new SqliteConnection("Data Source=:memory:");
                _connection.Open();

                services.AddDbContext<SpecFlowDbContext>(options =>
                    options.UseSqlite(_connection));
            }
            else
            {
                services.AddDbContext<SpecFlowDbContext>(options =>
                    options.UseSqlite(_connectionString));
            }

            services.AddSingleton<TimeProvider>(TimeProvider);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
