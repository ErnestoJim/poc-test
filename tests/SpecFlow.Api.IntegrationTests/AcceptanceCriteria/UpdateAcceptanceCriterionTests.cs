using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class UpdateAcceptanceCriterionTests
{
    [Fact]
    public async Task Update_WithDifferentContent_PreservesIdentityAndUpdatesTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var created = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        const string UpdatedContent = "  Updated\r\n";

        var route =
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{created.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionRequest(UpdatedContent));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.SpecificationId, updated.SpecificationId);
        Assert.Equal(created.Position, updated.Position);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal(UpdatedContent, updated.Content);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_WithIdenticalContent_PreservesUpdatedTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var created = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Criterion");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        var route =
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{created.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionRequest(created.Content));

        var unchanged = await response.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, unchanged);
    }

    [Fact]
    public async Task Update_ToDuplicateContent_ReturnsConflictAndPreservesOriginal()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var first = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        var second = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        var route =
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{second.Id}";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveAcceptanceCriterionRequest(first.Content));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criterion already exists", problem.Title);

        using var getResponse = await client.GetAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{second.Id}");
        var persisted = await getResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.Equal(second, persisted);
    }

    [Fact]
    public async Task Update_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        using var content = new StringContent(
            """{"content":"Updated"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PutAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{criterion.Id}",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
