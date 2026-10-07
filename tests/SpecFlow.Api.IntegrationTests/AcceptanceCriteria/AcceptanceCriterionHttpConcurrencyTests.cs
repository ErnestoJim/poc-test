using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.AcceptanceCriteria;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.AcceptanceCriteria;

public sealed class AcceptanceCriterionHttpConcurrencyTests
{
    [Fact]
    public async Task Update_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Criterion");
        var route =
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{criterion.Id}";

        using var response = await client.PutAsJsonAsync(
            route,
            new SaveAcceptanceCriterionRequest("Updated"));

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task CreateGetAndList_ReturnStrongEntityTags()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var collectionRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);

        using var createResponse = await client.PostAsJsonAsync(
            collectionRoute,
            new SaveAcceptanceCriterionRequest("Criterion"));
        var created = await createResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        var createdEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(createResponse);
        Assert.Matches("^\"[0-9a-f]{64}\"$", createdEntityTag);

        using var getResponse = await client.GetAsync($"{collectionRoute}/{created.Id}");
        using var listResponse = await client.GetAsync(collectionRoute);

        Assert.Equal(createdEntityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        Assert.Matches(
            "^\"[0-9a-f]{64}\"$",
            HttpPreconditionTestData.GetRequiredEntityTag(listResponse));
    }

    [Fact]
    public async Task CreateAndDelete_ChangeCollectionEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var collectionRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        var emptyEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);

        using var createResponse = await client.PostAsJsonAsync(
            collectionRoute,
            new SaveAcceptanceCriterionRequest("Criterion"));
        var criterion = await createResponse.Content
            .ReadFromJsonAsync<AcceptanceCriterionResponse>();
        Assert.NotNull(criterion);
        var createdCollectionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        using var deleteResponse = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            $"{collectionRoute}/{criterion.Id}",
            HttpPreconditionTestData.GetRequiredEntityTag(createResponse));

        Assert.NotEqual(emptyEntityTag, createdCollectionEntityTag);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotEqual(
            createdCollectionEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, collectionRoute));
    }

    [Fact]
    public async Task Update_ChangesResourceAndCollectionEntityTags()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        var collectionRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        var criterionRoute = $"{collectionRoute}/{criterion.Id}";
        var criterionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criterionRoute);
        var collectionEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            criterionRoute,
            new SaveAcceptanceCriterionRequest("Updated"),
            criterionEntityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(
            criterionEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(response));
        Assert.NotEqual(
            collectionEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, collectionRoute));
    }

    [Fact]
    public async Task Delete_WithStaleEntityTag_ReturnsPreconditionFailedWithoutDeleting()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var criterion = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Original");
        var criterionRoute =
            $"{AcceptanceCriterionTestData.CriteriaRoute(context.Project.Id, context.Proposal.Id)}/{criterion.Id}";
        var staleEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            criterionRoute);
        using var updateResponse = await HttpPreconditionTestData
            .SendAsJsonWithEntityTagAsync(
                client,
                HttpMethod.Put,
                criterionRoute,
                new SaveAcceptanceCriterionRequest("Updated"),
                staleEntityTag);

        using var deleteResponse = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            criterionRoute,
            staleEntityTag);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, deleteResponse.StatusCode);
        using var getResponse = await client.GetAsync(criterionRoute);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Reorder_WithCurrentEntityTag_ReturnsNewCollectionEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
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
        var third = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Third");
        var collectionRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        var firstEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{first.Id}");
        var secondEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{second.Id}");
        var thirdEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            $"{collectionRoute}/{third.Id}");

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            $"{collectionRoute}/order",
            new ReorderAcceptanceCriteriaRequest(
                [second.Id.ToString(), first.Id.ToString(), third.Id.ToString()]),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var newEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(response);
        Assert.NotEqual(originalEntityTag, newEntityTag);
        Assert.Equal(
            newEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(client, collectionRoute));
        Assert.NotEqual(
            firstEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{first.Id}"));
        Assert.NotEqual(
            secondEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{second.Id}"));
        Assert.Equal(
            thirdEntityTag,
            await HttpPreconditionTestData.GetEntityTagAsync(
                client,
                $"{collectionRoute}/{third.Id}"));
    }

    [Fact]
    public async Task Reorder_WithStaleCollectionEntityTag_ReturnsPreconditionFailed()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var context = await AcceptanceCriterionTestData.CreateSpecificationContextAsync(client);
        var first = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "First");
        var collectionRoute = AcceptanceCriterionTestData.CriteriaRoute(
            context.Project.Id,
            context.Proposal.Id);
        var staleEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            collectionRoute);
        var second = await AcceptanceCriterionTestData.CreateCriterionAsync(
            client,
            context.Project.Id,
            context.Proposal.Id,
            "Second");

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            $"{collectionRoute}/order",
            new ReorderAcceptanceCriteriaRequest(
                [second.Id.ToString(), first.Id.ToString()]),
            staleEntityTag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }
}
