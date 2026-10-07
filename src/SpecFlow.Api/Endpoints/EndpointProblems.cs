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
}
