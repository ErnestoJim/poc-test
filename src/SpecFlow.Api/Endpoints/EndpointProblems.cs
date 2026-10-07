namespace SpecFlow.Api.Endpoints;

internal static class EndpointProblems
{
    public static IResult InvalidIdentifier(string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [message]
            });

    public static IResult ProjectNotFound(Guid projectId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Project not found",
            detail: $"No project with identifier '{projectId}' was found.");

    public static IResult FeatureProposalNotFound(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Feature proposal not found",
            detail: $"No feature proposal with identifier '{proposalId}' was found in this project.");

    public static IResult SpecificationNotFound(Guid proposalId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Specification not found",
            detail: $"Feature proposal '{proposalId}' does not have a specification.");

    public static IResult PreconditionRequired() =>
        Results.Problem(
            statusCode: StatusCodes.Status428PreconditionRequired,
            title: "Precondition required",
            detail: "The operation requires the current ETag in the If-Match header.");

    public static IResult InvalidIfMatchHeader() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid If-Match header",
            detail: "The If-Match header must contain exactly one strong ETag.");

    public static IResult PreconditionFailed() =>
        Results.Problem(
            statusCode: StatusCodes.Status412PreconditionFailed,
            title: "Precondition failed",
            detail: "The resource changed since it was read.");
}
