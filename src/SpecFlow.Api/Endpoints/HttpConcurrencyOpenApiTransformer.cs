using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SpecFlow.Api.Endpoints;

internal sealed class HttpConcurrencyOpenApiTransformer : IOpenApiOperationTransformer
{
    private static readonly HashSet<string> ConditionalOperations =
    [
        "UpdateSpecification",
        "UpdateAcceptanceCriterion",
        "DeleteAcceptanceCriterion",
        "ReorderAcceptanceCriteria",
        "UpdateImplementationTask",
        "DeleteImplementationTask",
        "StartImplementationTask",
        "CompleteImplementationTask",
        "ReorderImplementationTasks"
    ];

    private static readonly HashSet<string> EntityTagResponseOperations =
    [
        "CreateSpecification",
        "GetSpecification",
        "UpdateSpecification",
        "CreateAcceptanceCriterion",
        "GetAcceptanceCriterion",
        "ListAcceptanceCriteria",
        "UpdateAcceptanceCriterion",
        "ReorderAcceptanceCriteria",
        "CreateImplementationTask",
        "GetImplementationTask",
        "ListImplementationTasks",
        "UpdateImplementationTask",
        "StartImplementationTask",
        "CompleteImplementationTask",
        "ReorderImplementationTasks"
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        if (operation.OperationId is not { } operationId)
        {
            return Task.CompletedTask;
        }

        if (ConditionalOperations.Contains(operationId))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = true,
                Description = "The current strong ETag of the resource or collection.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            });
        }

        if (EntityTagResponseOperations.Contains(operationId) &&
            operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Where(response =>
                response.Key.StartsWith('2')))
            {
                if (response.Value is not OpenApiResponse openApiResponse)
                {
                    continue;
                }

                openApiResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                openApiResponse.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Strong ETag for the returned resource or collection.",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    }
                };
            }
        }

        return Task.CompletedTask;
    }
}
