using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Endpoints;
using SpecFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("SpecFlow")
    ?? throw new InvalidOperationException("The SpecFlow database connection string is missing.");

builder.Services.AddDbContext<SpecFlowDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapProjectEndpoints();
app.MapFeatureProposalEndpoints();
app.MapSpecificationEndpoints();
app.MapAcceptanceCriterionEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
