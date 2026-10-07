using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationConcurrencyTests
{
    [Fact]
    public async Task CompetingContentUpdates_AreDetectedByInternalVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var firstCopy = await firstDbContext.Specifications.SingleAsync(
            specification => specification.Id == context.Specification.Id);
        var secondCopy = await secondDbContext.Specifications.SingleAsync(
            specification => specification.Id == context.Specification.Id);
        var timestamp = factory.TimeProvider.GetUtcNow().AddHours(1);

        firstCopy.UpdateContent("# First update", timestamp);
        secondCopy.UpdateContent("# Second update", timestamp);
        await firstDbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CompetingCreations_OnlyOneSpecificationIsCreated()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SpecFlow.Api.IntegrationTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var connectionString = $"Data Source={Path.Combine(temporaryDirectory, "specflow.db")}";
            using var factory = new SpecFlowApiFactory(connectionString);
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();
            var (project, proposal) = await SpecificationTestData
                .CreateAcceptedProposalAsync(firstClient);
            var route =
                $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification";

            var responses = await Task.WhenAll(
                firstClient.PostAsJsonAsync(
                    route,
                    new SaveSpecificationRequest("# First")),
                secondClient.PostAsJsonAsync(
                    route,
                    new SaveSpecificationRequest("# Second")));

            try
            {
                Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
                var conflict = Assert.Single(
                    responses,
                    response => response.StatusCode == HttpStatusCode.Conflict);
                var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal("Specification already exists", problem.Title);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
