using System.Net;
using System.Text.Json;
using SpecFlow.Api.IntegrationTests.Infrastructure;

namespace SpecFlow.Api.IntegrationTests.OpenApi;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Document_ContainsOnlyExpectedNamedOperations()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var documentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(documentStream);
        var paths = document.RootElement.GetProperty("paths");
        (string Path, string Method, string OperationId)[] expectedOperations =
        [
            ("/api/projects", "post", "CreateProject"),
            ("/api/projects", "get", "ListProjects"),
            ("/api/projects/{id}", "get", "GetProject"),
            ("/api/projects/{projectId}/proposals", "post", "CreateFeatureProposal"),
            ("/api/projects/{projectId}/proposals", "get", "ListFeatureProposals"),
            ("/api/projects/{projectId}/proposals/{proposalId}", "get", "GetFeatureProposal"),
            ("/api/projects/{projectId}/proposals/{proposalId}/accept", "post", "AcceptFeatureProposal"),
            ("/api/projects/{projectId}/proposals/{proposalId}/reject", "post", "RejectFeatureProposal"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification", "post", "CreateSpecification"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification", "get", "GetSpecification"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification", "put", "UpdateSpecification"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria", "post", "CreateAcceptanceCriterion"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria", "get", "ListAcceptanceCriteria"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order", "put", "ReorderAcceptanceCriteria"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}", "get", "GetAcceptanceCriterion"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}", "put", "UpdateAcceptanceCriterion"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}", "delete", "DeleteAcceptanceCriterion"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks", "post", "CreateImplementationTask"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks", "get", "ListImplementationTasks"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order", "put", "ReorderImplementationTasks"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}", "get", "GetImplementationTask"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}", "put", "UpdateImplementationTask"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}", "delete", "DeleteImplementationTask"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start", "post", "StartImplementationTask"),
            ("/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete", "post", "CompleteImplementationTask")
        ];

        Assert.Equal(
            expectedOperations.Select(operation => operation.Path).Distinct().Count(),
            paths.EnumerateObject().Count());

        foreach (var expected in expectedOperations)
        {
            var operation = paths
                .GetProperty(expected.Path)
                .GetProperty(expected.Method);

            Assert.Equal(
                expected.OperationId,
                operation.GetProperty("operationId").GetString());
        }
    }

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

        var acceptanceCriteriaPath = paths.GetProperty(
            "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria");
        Assert.True(acceptanceCriteriaPath.TryGetProperty("post", out _));
        Assert.True(acceptanceCriteriaPath.TryGetProperty("get", out _));
        Assert.True(
            paths.GetProperty(
                    "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order")
                .TryGetProperty("put", out _));
        var acceptanceCriterionPath = paths.GetProperty(
            "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}");
        Assert.True(acceptanceCriterionPath.TryGetProperty("get", out _));
        Assert.True(acceptanceCriterionPath.TryGetProperty("put", out _));
        Assert.True(acceptanceCriterionPath.TryGetProperty("delete", out _));

        var acceptanceCriterionProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AcceptanceCriterionResponse")
            .GetProperty("properties");
        Assert.True(acceptanceCriterionProperties.TryGetProperty("id", out _));
        Assert.True(acceptanceCriterionProperties.TryGetProperty("specificationId", out _));
        Assert.True(acceptanceCriterionProperties.TryGetProperty("content", out _));
        Assert.True(acceptanceCriterionProperties.TryGetProperty("position", out _));
        Assert.True(acceptanceCriterionProperties.TryGetProperty("createdAtUtc", out _));
        Assert.True(acceptanceCriterionProperties.TryGetProperty("updatedAtUtc", out _));
        Assert.False(acceptanceCriterionProperties.TryGetProperty("contentHash", out _));
        Assert.False(acceptanceCriterionProperties.TryGetProperty("version", out _));

        var implementationTasksPath = paths.GetProperty(
            "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks");
        Assert.True(implementationTasksPath.TryGetProperty("post", out _));
        Assert.True(implementationTasksPath.TryGetProperty("get", out _));
        Assert.True(
            paths.GetProperty(
                    "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order")
                .TryGetProperty("put", out _));
        var implementationTaskPath = paths.GetProperty(
            "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}");
        Assert.True(implementationTaskPath.TryGetProperty("get", out _));
        Assert.True(implementationTaskPath.TryGetProperty("put", out _));
        Assert.True(implementationTaskPath.TryGetProperty("delete", out _));
        Assert.True(
            paths.GetProperty(
                    "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start")
                .TryGetProperty("post", out _));
        Assert.True(
            paths.GetProperty(
                    "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete")
                .TryGetProperty("post", out _));

        var implementationTaskProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ImplementationTaskResponse")
            .GetProperty("properties");
        Assert.True(implementationTaskProperties.TryGetProperty("id", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("specificationId", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("title", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("description", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("status", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("startedAtUtc", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("completedAtUtc", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("position", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("createdAtUtc", out _));
        Assert.True(implementationTaskProperties.TryGetProperty("updatedAtUtc", out _));
        Assert.False(implementationTaskProperties.TryGetProperty("normalizedTitle", out _));
        Assert.False(implementationTaskProperties.TryGetProperty("version", out _));
    }
}
