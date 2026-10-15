using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpecFlow.Web.Services;

public sealed class SpecFlowApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<List<ProjectDto>>> ListProjectsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ProjectDto>>(HttpMethod.Get, "/api/projects", null, cancellationToken);

    public Task<ApiResult<ProjectDto>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectDto>(HttpMethod.Get, $"/api/projects/{projectId}", null, cancellationToken);

    public Task<ApiResult<ProjectDto>> CreateProjectAsync(
        string? name,
        string? description,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProjectDto>(HttpMethod.Post, "/api/projects", new { name, description }, cancellationToken);

    public Task<ApiResult<List<ProposalDto>>> ListProposalsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<ProposalDto>>(
            HttpMethod.Get,
            $"/api/projects/{projectId}/proposals",
            null,
            cancellationToken);

    public Task<ApiResult<ProposalDto>> GetProposalAsync(
        Guid projectId,
        Guid proposalId,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProposalDto>(
            HttpMethod.Get,
            $"/api/projects/{projectId}/proposals/{proposalId}",
            null,
            cancellationToken);

    public Task<ApiResult<ProposalDto>> CreateProposalAsync(
        Guid projectId,
        string? title,
        string? description,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProposalDto>(
            HttpMethod.Post,
            $"/api/projects/{projectId}/proposals",
            new { title, description },
            cancellationToken);

    public Task<ApiResult<ProposalDto>> AcceptProposalAsync(
        Guid projectId,
        Guid proposalId,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProposalDto>(
            HttpMethod.Post,
            $"/api/projects/{projectId}/proposals/{proposalId}/accept",
            null,
            cancellationToken);

    public Task<ApiResult<ProposalDto>> RejectProposalAsync(
        Guid projectId,
        Guid proposalId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProposalDto>(
            HttpMethod.Post,
            $"/api/projects/{projectId}/proposals/{proposalId}/reject",
            new { reason },
            cancellationToken);

    public Task<ApiResult<SpecificationDto>> GetSpecificationAsync(
        Guid projectId,
        Guid proposalId,
        CancellationToken cancellationToken = default) =>
        SendAsync<SpecificationDto>(
            HttpMethod.Get,
            $"/api/projects/{projectId}/proposals/{proposalId}/specification",
            null,
            cancellationToken);

    public Task<ApiResult<SpecificationDto>> CreateSpecificationAsync(
        Guid projectId,
        Guid proposalId,
        string? content,
        CancellationToken cancellationToken = default) =>
        SendAsync<SpecificationDto>(
            HttpMethod.Post,
            $"/api/projects/{projectId}/proposals/{proposalId}/specification",
            new { content },
            cancellationToken);

    public Task<ApiResult<SpecificationDto>> UpdateSpecificationAsync(
        Guid projectId,
        Guid proposalId,
        string? content,
        string entityTag,
        CancellationToken cancellationToken = default) =>
        SendAsync<SpecificationDto>(
            HttpMethod.Put,
            $"/api/projects/{projectId}/proposals/{proposalId}/specification",
            new { content },
            cancellationToken,
            entityTag);

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (ifMatch is not null)
        {
            // The ETag is opaque: it is forwarded exactly as the API returned it.
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return value is null
                    ? new ApiResult<T>(default, new ApiError((int)response.StatusCode, "La API devolvió una respuesta vacía.", new HashSet<string>()))
                    : new ApiResult<T>(value, null, response.Headers.ETag?.ToString());
            }

            return new ApiResult<T>(default, await ReadErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(default, new ApiError(null, "No se puede conectar con la API. Comprueba que esté en ejecución.", new HashSet<string>()));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiResult<T>(default, new ApiError(null, "La API tardó demasiado en responder.", new HashSet<string>()));
        }
        catch (JsonException)
        {
            return new ApiResult<T>(default, new ApiError(null, "La API devolvió una respuesta no válida.", new HashSet<string>()));
        }
    }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    fields.Add(field.Name);
                }
            }
        }
        catch (JsonException)
        {
            // The HTTP status still provides a useful, localized error.
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Revisa los datos introducidos.",
            HttpStatusCode.NotFound => "El recurso solicitado ya no existe o no pertenece a este proyecto.",
            HttpStatusCode.Conflict => "No se pudo guardar por un conflicto con los datos existentes.",
            HttpStatusCode.PreconditionFailed => "Los datos cambiaron desde la última consulta.",
            _ => "No se pudo completar la operación. Inténtalo de nuevo."
        };
        return new ApiError((int)response.StatusCode, message, fields);
    }
}
