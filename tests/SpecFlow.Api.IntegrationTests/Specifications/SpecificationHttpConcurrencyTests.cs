using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.Contracts.ImplementationTasks;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.ImplementationTasks;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class SpecificationHttpConcurrencyTests
{
    [Fact]
    public async Task CreateAndGet_ReturnSameStrongEntityTagWithoutExposingVersion()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        var route = Route(project.Id, proposal.Id);

        using var createResponse = await client.PostAsJsonAsync(
            route,
            new SaveSpecificationRequest("# Specification"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(createResponse);
        Assert.Matches("^\"[0-9a-f]{64}\"$", createdEntityTag);
        await using var jsonStream = await createResponse.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(jsonStream);
        Assert.False(document.RootElement.TryGetProperty("version", out _));

        using var getResponse = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(
            createdEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
    }

    [Fact]
    public async Task Update_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var route = Route(context.Project.Id, context.Proposal.Id);

        using var response = await client.PutAsJsonAsync(
            route,
            new SaveSpecificationRequest("# Updated"));

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Precondition required", problem.Title);
    }

    [Theory]
    [InlineData("W/\"weak\"")]
    [InlineData("*")]
    [InlineData("\"first\", \"second\"")]
    [InlineData("not-an-etag")]
    public async Task Update_WithUnsupportedIfMatch_ReturnsBadRequest(string entityTag)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var route = Route(context.Project.Id, context.Proposal.Id);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveSpecificationRequest("# Updated"),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid If-Match header", problem.Title);
    }

    [Fact]
    public async Task Update_WithStaleEntityTag_ReturnsPreconditionFailedAndPreservesLatestContent()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var route = Route(context.Project.Id, context.Proposal.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var firstResponse = await HttpPreconditionTestData
            .SendAsJsonWithEntityTagAsync(
                client,
                HttpMethod.Put,
                route,
                new SaveSpecificationRequest("# First update"),
                originalEntityTag);
        using var staleResponse = await HttpPreconditionTestData
            .SendAsJsonWithEntityTagAsync(
                client,
                HttpMethod.Put,
                route,
                new SaveSpecificationRequest("# Stale update"),
                originalEntityTag);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotEqual(
            originalEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(firstResponse));
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        var problem = await staleResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Precondition failed", problem.Title);
        var persisted = await client.GetFromJsonAsync<SpecificationResponse>(route);
        Assert.Equal("# First update", persisted?.Content);
    }

    [Fact]
    public async Task IdenticalUpdate_PreservesEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var route = Route(context.Project.Id, context.Proposal.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveSpecificationRequest(context.Specification.Content),
            entityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task CollectionChanges_DoNotInvalidateSpecificationEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var specificationRoute = Route(context.Project.Id, context.Proposal.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            specificationRoute);

        await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Criterion");
        await ImplementationTaskTestData.CreateTaskAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Task");

        var currentEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            specificationRoute);
        Assert.Equal(originalEntityTag, currentEntityTag);
    }

    private static string Route(Guid projectId, Guid proposalId) =>
        $"/api/projects/{projectId}/proposals/{proposalId}/specification";
}
