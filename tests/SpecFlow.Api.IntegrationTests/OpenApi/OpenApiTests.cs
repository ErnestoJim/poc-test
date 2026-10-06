using System.Net;
using System.Text.Json;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.OpenApi;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Document_ContainsExpectedOperations()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var documentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(documentStream);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/projects").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/projects").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/projects/{id}").TryGetProperty("get", out _));
        Assert.True(
            paths.GetProperty("/api/projects/{projectId}/proposals")
                .TryGetProperty("post", out _));
        Assert.True(
            paths.GetProperty("/api/projects/{projectId}/proposals")
                .TryGetProperty("get", out _));
        Assert.True(
            paths.GetProperty("/api/projects/{projectId}/proposals/{proposalId}")
                .TryGetProperty("get", out _));
        Assert.True(
            paths.GetProperty("/api/projects/{projectId}/proposals/{proposalId}/accept")
                .TryGetProperty("post", out _));
        Assert.True(
            paths.GetProperty("/api/projects/{projectId}/proposals/{proposalId}/reject")
                .TryGetProperty("post", out _));
        var specificationPath = paths.GetProperty(
            "/api/projects/{projectId}/proposals/{proposalId}/specification");
        Assert.True(specificationPath.TryGetProperty("post", out _));
        Assert.True(specificationPath.TryGetProperty("get", out _));
        Assert.True(specificationPath.TryGetProperty("put", out _));

        var responseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("FeatureProposalResponse")
            .GetProperty("properties");
        Assert.True(responseProperties.TryGetProperty("status", out _));
        Assert.True(responseProperties.TryGetProperty("decidedAtUtc", out _));
        Assert.True(responseProperties.TryGetProperty("rejectionReason", out _));

        var specificationProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("SpecificationResponse")
            .GetProperty("properties");
        Assert.True(specificationProperties.TryGetProperty("id", out _));
        Assert.True(specificationProperties.TryGetProperty("featureProposalId", out _));
        Assert.True(specificationProperties.TryGetProperty("content", out _));
        Assert.True(specificationProperties.TryGetProperty("createdAtUtc", out _));
        Assert.True(specificationProperties.TryGetProperty("updatedAtUtc", out _));
    }
}
