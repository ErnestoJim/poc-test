using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Traceability;
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

    [Fact]
    public async Task CompetingApprovals_WithSameEntityTag_OnlyOneSucceeds()
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
            var context = await SpecificationTestData
                .CreateSpecificationContextAsync(firstClient);
            var task = await ImplementationTaskTestData.CreateTaskAsync(
                firstClient,
                context.Project.Id,
                context.Proposal.Id,
                "Task");
            var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
                firstClient,
                context.Project.Id,
                context.Proposal.Id,
                "Criterion");
            var linksRoute = TraceabilityTestData.TaskLinksRoute(
                context.Project.Id,
                context.Proposal.Id,
                task.Id);
            using var linksResponse = await TraceabilityTestData.ReplaceLinksAsync(
                firstClient,
                linksRoute,
                await HttpPreconditionTestData.GetEntityTagAsync(firstClient, linksRoute),
                criterion.Id);
            linksResponse.EnsureSuccessStatusCode();
            var specificationRoute = SpecificationTestData.SpecificationRoute(
                context.Project.Id,
                context.Proposal.Id);
            var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(
                firstClient,
                specificationRoute);

            var responses = await Task.WhenAll(
                HttpPreconditionTestData.SendWithEntityTagAsync(
                    firstClient,
                    HttpMethod.Post,
                    $"{specificationRoute}/approve",
                    entityTag),
                HttpPreconditionTestData.SendWithEntityTagAsync(
                    secondClient,
                    HttpMethod.Post,
                    $"{specificationRoute}/approve",
                    entityTag));

            try
            {
                Assert.Single(
                    responses,
                    response => response.StatusCode == HttpStatusCode.OK);
                Assert.Single(
                    responses,
                    response => response.StatusCode == HttpStatusCode.PreconditionFailed);
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
