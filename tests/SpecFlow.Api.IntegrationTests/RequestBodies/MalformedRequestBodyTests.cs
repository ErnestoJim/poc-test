using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Api.Contracts.Specifications;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Api.IntegrationTests.Specifications;

namespace SpecFlow.Api.IntegrationTests.RequestBodies;

public sealed class MalformedRequestBodyTests
{
    private const string InvalidRequestBodyTitle = "Invalid request body";

    private static readonly (string Method, string Path)[] OperationsWithBody =
    [
        ("post", "/api/projects"),
        ("post", "/api/projects/{projectId}/proposals"),
        ("post", "/api/projects/{projectId}/proposals/{proposalId}/reject"),
        ("post", "/api/projects/{projectId}/proposals/{proposalId}/specification"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification"),
        ("post", "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification"),
        ("post", "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}"),
        ("put", "/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria"),
        ("post", "/api/projects/{projectId}/technical-decisions"),
        ("put", "/api/projects/{projectId}/technical-decisions/{decisionId}"),
        ("post", "/api/projects/{projectId}/technical-decisions/{decisionId}/reject"),
        ("post", "/api/projects/{projectId}/technical-decisions/{decisionId}/supersede"),
    ];

    private static readonly string[] InternalDetailMarkers =
    [
        "System.",
        "JsonException",
        "LineNumber",
        "BytePosition",
        "Path: $",
        "SpecFlow.",
        "Exception",
        "   at ",
    ];

    [Theory]
    [InlineData("Development", "{bad")]
    [InlineData("Development", "")]
    [InlineData("Development", "[]")]
    [InlineData("Testing", "{bad")]
    [InlineData("Testing", "")]
    [InlineData("Testing", "[]")]
    [InlineData("Production", "{bad")]
    [InlineData("Production", "")]
    [InlineData("Production", "[]")]
    public async Task MalformedBody_OnEveryOperationWithBody_ReturnsInvalidRequestBody(
        string environment,
        string body)
    {
        using var factory = new SpecFlowApiFactory(environment: environment);
        using var client = factory.CreateClient();

        foreach (var (method, path) in OperationsWithBody)
        {
            // Routes point to resources that do not exist and requests omit If-Match:
            // the unreadable body must be reported before both checks.
            var route = path
                .Replace("{projectId}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
                .Replace("{proposalId}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
                .Replace("{criterionId}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
                .Replace("{taskId}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
                .Replace("{decisionId}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

            using var response = await SendJsonAsync(client, new HttpMethod(method), route, body);

            await AssertInvalidRequestBodyAsync(response, $"{method.ToUpperInvariant()} {path}");
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task IncompatiblePropertyType_ReturnsInvalidRequestBody(string environment)
    {
        using var factory = new SpecFlowApiFactory(environment: environment);
        using var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        using var projectResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/projects",
            """{"name":123}""");
        using var proposalResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/projects/{project.Id}/proposals",
            """{"title":"Proposal","sourceProposalId":false}""");

        await AssertInvalidRequestBodyAsync(projectResponse, "POST /api/projects");
        await AssertInvalidRequestBodyAsync(proposalResponse, "POST /api/projects/{projectId}/proposals");
    }

    [Fact]
    public async Task RejectedBody_DoesNotChangeDataOrEntityTags()
    {
        using var factory = new SpecFlowApiFactory(environment: "Development");
        using var client = factory.CreateClient();
        var (project, proposal, _) = await SpecificationTestData.CreateSpecificationContextAsync(client);
        var specificationRoute = SpecificationTestData.SpecificationRoute(project.Id, proposal.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, specificationRoute);

        using var createResponse = await SendJsonAsync(client, HttpMethod.Post, "/api/projects", "{bad");
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, specificationRoute)
        {
            Content = new StringContent("""{"content":42}""", Encoding.UTF8, "application/json")
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", entityTag);
        using var updateResponse = await client.SendAsync(updateRequest);

        await AssertInvalidRequestBodyAsync(createResponse, "POST /api/projects");
        await AssertInvalidRequestBodyAsync(updateResponse, "PUT specification");
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        Assert.Single(projects!);
        using var getResponse = await client.GetAsync(specificationRoute);
        Assert.Equal(entityTag, HttpPreconditionTestData.GetRequiredEntityTag(getResponse));
        var specification = await getResponse.Content.ReadFromJsonAsync<SpecificationResponse>();
        Assert.Equal("# Specification", specification?.Content);
    }

    [Fact]
    public async Task Development_KeepsValidBodiesValidationProblemsAndUnsupportedMediaType()
    {
        using var factory = new SpecFlowApiFactory(environment: "Development");
        using var client = factory.CreateClient();

        using var validResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("SpecFlow", null));
        using var validationResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("   ", null));
        using var mediaTypeResponse = await client.PostAsync(
            "/api/projects",
            new StringContent("name=SpecFlow", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, validationResponse.StatusCode);
        using var validation = JsonDocument.Parse(await validationResponse.Content.ReadAsStringAsync());
        Assert.True(validation.RootElement.GetProperty("errors").TryGetProperty("name", out _));
        Assert.NotEqual(InvalidRequestBodyTitle, validation.RootElement.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, mediaTypeResponse.StatusCode);
    }

    [Fact]
    public async Task OpenApi_OperationsWithBody_AreAllCovered()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();

        await using var stream = await client.GetStreamAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(stream);
        var documented = document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => operation.Value.TryGetProperty("requestBody", out _))
                .Select(operation => (operation.Name, path.Name)))
            .ToHashSet();

        Assert.Equal(documented, OperationsWithBody.ToHashSet());
    }

    private static Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        string body) =>
        client.SendAsync(new HttpRequestMessage(method, route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Project", null));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProjectResponse>()
            ?? throw new InvalidOperationException("The create response did not contain a project.");
    }

    private static async Task AssertInvalidRequestBodyAsync(HttpResponseMessage response, string operation)
    {
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"{operation} returned {(int)response.StatusCode}: {content}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(content);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(InvalidRequestBodyTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.False(problem.RootElement.TryGetProperty("errors", out _), operation);
        foreach (var marker in InternalDetailMarkers)
        {
            Assert.False(content.Contains(marker, StringComparison.Ordinal), $"{operation} exposes '{marker}': {content}");
        }
    }
}
