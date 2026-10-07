using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class ReorderAcceptanceCriteriaTests
{
    [Fact]
    public async Task Reorder_WithCompleteIdentifierList_ReturnsNoContentAndPersistsOrder()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        var first = await CreateCriterionAsync("First");
        var second = await CreateCriterionAsync("Second");
        var third = await CreateCriterionAsync("Third");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var expectedTimestamp = factory.TimeProvider.GetUtcNow();

        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                CriteriaRoute(),
                $"{CriteriaRoute()}/order",
                new ReorderAcceptanceCriteriaRequest(
                    [third.Id.ToString(), first.Id.ToString(), second.Id.ToString()]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);

        var criteria = await ListCriteriaAsync();
        Assert.Collection(
            criteria,
            criterion =>
            {
                Assert.Equal(third.Id, criterion.Id);
                Assert.Equal(1, criterion.Position);
                Assert.Equal(expectedTimestamp, criterion.UpdatedAtUtc);
            },
            criterion =>
            {
                Assert.Equal(first.Id, criterion.Id);
                Assert.Equal(2, criterion.Position);
                Assert.Equal(expectedTimestamp, criterion.UpdatedAtUtc);
            },
            criterion =>
            {
                Assert.Equal(second.Id, criterion.Id);
                Assert.Equal(3, criterion.Position);
                Assert.Equal(expectedTimestamp, criterion.UpdatedAtUtc);
            });

        async Task<AcceptanceCriterionResponse> CreateCriterionAsync(string content) =>
            await AcceptanceCriterionTestData.CreateCriterionAsync(
                client,
                context.Project.Id,
                context.Proposal.Id,
                content);

        string CriteriaRoute() => AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);

        async Task<List<AcceptanceCriterionResponse>> ListCriteriaAsync() =>
            await client.GetFromJsonAsync<List<AcceptanceCriterionResponse>>(CriteriaRoute())
                ?? throw new InvalidOperationException("The list response was empty.");
    }

    [Fact]
    public async Task Reorder_WithCurrentOrder_PreservesCriterionTimestamps()
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
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        var route = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/order",
                new ReorderAcceptanceCriteriaRequest(
                    [first.Id.ToString(), second.Id.ToString()]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var criteria = await client.GetFromJsonAsync<List<AcceptanceCriterionResponse>>(
            AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id));
        Assert.Equal([first, second], criteria);
    }

    [Fact]
    public async Task Reorder_WithDifferentIdentifierSet_ReturnsConflictWithoutChangingOrder()
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

        var route = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/order",
                new ReorderAcceptanceCriteriaRequest(
                    [first.Id.ToString(), Guid.NewGuid().ToString()]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Acceptance criteria collection changed", problem.Title);
        var criteria = await client.GetFromJsonAsync<List<AcceptanceCriterionResponse>>(
            AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id));
        Assert.Equal([first, second], criteria);
    }

    [Theory]
    [InlineData(null, "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData("", "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData("not-a-uuid", "de305d54-75b4-431b-adb2-eb6b9e546014")]
    [InlineData(
        "de305d54-75b4-431b-adb2-eb6b9e546014",
        "de305d54-75b4-431b-adb2-eb6b9e546014")]
    public async Task Reorder_WithInvalidIdentifierList_ReturnsValidationProblem(
        string? firstId,
        string secondId)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/order",
            new ReorderAcceptanceCriteriaRequest([firstId, secondId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("criterionIds", problem.Errors);
    }

    [Fact]
    public async Task Reorder_EmptyCollection_ReturnsNoContent()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);

        var route = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                $"{route}/order",
                new ReorderAcceptanceCriteriaRequest([]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData
            .CreateSpecificationContextAsync(client);
        using var content = new StringContent(
            """{"criterionIds":[]}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PutAsync(
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/order",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
