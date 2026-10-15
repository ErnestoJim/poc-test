using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SpecFlow.Api.Endpoints;

internal sealed class InvalidRequestBodyExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            return false;
        }

        // Endpoint parameters are bound from strings and HttpContext, so a 400 at
        // binding time can only come from the JSON body.
        var problemDetails = badRequestException.StatusCode == StatusCodes.Status400BadRequest
            ? new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request body",
                Detail = "The request body must be a JSON document compatible with the operation contract."
            }
            : new ProblemDetails { Status = badRequestException.StatusCode };

        httpContext.Response.StatusCode = badRequestException.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }
}
