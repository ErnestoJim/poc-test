using System.Net.Http.Json;

namespace SpecFlow.Api.IntegrationTests.Infrastructure;

internal static class HttpPreconditionTestData
{
    public static async Task<string> GetEntityTagAsync(HttpClient client, string resourceRoute)
    {
        using var response = await client.GetAsync(resourceRoute);
        response.EnsureSuccessStatusCode();
        return GetRequiredEntityTag(response);
    }

    public static string GetRequiredEntityTag(HttpResponseMessage response) =>
        response.Headers.ETag?.ToString()
        ?? throw new InvalidOperationException("The response does not contain an ETag.");

    public static async Task<HttpResponseMessage> PutAsJsonWithCurrentEntityTagAsync<T>(
        HttpClient client,
        string resourceRoute,
        string requestRoute,
        T value)
    {
        var entityTag = await GetEntityTagAsync(client, resourceRoute);
        return await SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            requestRoute,
            value,
            entityTag);
    }

    public static async Task<HttpResponseMessage> PostWithCurrentEntityTagAsync(
        HttpClient client,
        string resourceRoute,
        string requestRoute)
    {
        var entityTag = await GetEntityTagAsync(client, resourceRoute);
        return await SendWithEntityTagAsync(client, HttpMethod.Post, requestRoute, entityTag);
    }

    public static async Task<HttpResponseMessage> DeleteWithCurrentEntityTagAsync(
        HttpClient client,
        string resourceRoute)
    {
        var entityTag = await GetEntityTagAsync(client, resourceRoute);
        return await SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            resourceRoute,
            entityTag);
    }

    public static Task<HttpResponseMessage> SendAsJsonWithEntityTagAsync<T>(
        HttpClient client,
        HttpMethod method,
        string requestRoute,
        T value,
        string entityTag) =>
        SendWithEntityTagAsync(
            client,
            method,
            requestRoute,
            entityTag,
            JsonContent.Create(value));

    public static async Task<HttpResponseMessage> SendWithEntityTagAsync(
        HttpClient client,
        HttpMethod method,
        string requestRoute,
        string entityTag,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, requestRoute)
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("If-Match", entityTag);
        return await client.SendAsync(request);
    }
}
