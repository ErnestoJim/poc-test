using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.IntegrationTests.FeatureProposals;

public sealed class FeatureProposalConcurrencyTests
{
    [Fact]
    public async Task CompetingDecisions_OnlyFirstDecisionIsPersisted()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await FeatureProposalTestData.CreateProjectAsync(client);
        var created = await FeatureProposalTestData.CreateProposalAsync(
            client,
            project.Id,
            "Proposal");
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<SpecFlowDbContext>();
        var firstCopy = await firstDbContext.FeatureProposals.SingleAsync(
            proposal => proposal.Id == created.Id);
        var secondCopy = await secondDbContext.FeatureProposals.SingleAsync(
            proposal => proposal.Id == created.Id);
        var firstDecision = new DateTimeOffset(2026, 10, 6, 9, 15, 0, TimeSpan.Zero);

        firstCopy.Accept(firstDecision);
        secondCopy.Reject(
            "Competing rejection",
            firstDecision.AddMilliseconds(1));

        await firstDbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDbContext.SaveChangesAsync());

        secondDbContext.ChangeTracker.Clear();
        var persisted = await secondDbContext.FeatureProposals
            .AsNoTracking()
            .SingleAsync(proposal => proposal.Id == created.Id);
        Assert.Equal(FeatureProposalStatus.Accepted, persisted.Status);
        Assert.Equal(firstDecision, persisted.DecidedAtUtc);
        Assert.Null(persisted.RejectionReason);
    }
}
