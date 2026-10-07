using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.FeatureProposals;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.Specifications;

public sealed class UpdateSpecificationTests
{
    [Fact]
    public async Task Update_WithDifferentContent_ReplacesContentAndUpdatesTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        var created = await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id,
            "# Original");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        const string UpdatedContent = "  # Updated\r\n\r\nNew content  \n";

        var route = $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveSpecificationRequest(UpdatedContent));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.FeatureProposalId, updated.FeatureProposalId);
        Assert.Equal(UpdatedContent, updated.Content);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), updated.UpdatedAtUtc);

        using var getResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification");
        var persisted = await getResponse.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.Equal(updated, persisted);
    }

    [Fact]
    public async Task Update_WithIdenticalContent_PreservesUpdatedTimestamp()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        var created = await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id,
            "# Specification");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        var route = $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification";
        using var response = await HttpPreconditionTestData
            .PutAsJsonWithCurrentEntityTagAsync(
                client,
                route,
                route,
                new SaveSpecificationRequest(created.Content));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unchanged = await response.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.Equal(created, unchanged);
    }

    [Fact]
    public async Task Update_WhenSpecificationDoesNotExist_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Updated"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Specification not found", problem.Title);
    }

    [Fact]
    public async Task Update_UsingDifferentProject_ReturnsFeatureProposalNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (firstProject, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client, "First");
        var secondProject = await FeatureProposalTestData.CreateProjectAsync(client, "Second");
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            firstProject.Id,
            proposal.Id);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{secondProject.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest("# Updated"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Feature proposal not found", problem.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_WithMissingContent_ReturnsValidationProblem(string? content)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            new SaveSpecificationRequest(content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("content", problem.Errors);
    }

    [Fact]
    public async Task Update_WithUnsupportedMediaType_ReturnsProblemDetails()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var (project, proposal) = await SpecificationTestData
            .CreateAcceptedProposalAsync(client);
        await SpecificationTestData.CreateSpecificationAsync(
            client,
            project.Id,
            proposal.Id);
        using var content = new StringContent(
            """{"content":"# Updated"}""",
            Encoding.UTF8,
            "text/plain");

        using var response = await client.PutAsync(
            $"/api/projects/{project.Id}/proposals/{proposal.Id}/specification",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.Status);
    }
}
