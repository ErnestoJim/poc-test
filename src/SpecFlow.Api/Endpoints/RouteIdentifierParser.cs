namespace SpecFlow.Api.Endpoints;

internal static class RouteIdentifierParser
{
    public static (Guid Identifier, IResult? Error) ParseProject(
        string value,
        string field = "projectId") =>
        Parse(value, field, "The project identifier must be a valid UUID.");

    public static (Guid Identifier, IResult? Error) ParseFeatureProposal(string value) =>
        Parse(
            value,
            "proposalId",
            "The feature proposal identifier must be a valid UUID.");

    public static (Guid Identifier, IResult? Error) ParseAcceptanceCriterion(string value) =>
        Parse(
            value,
            "criterionId",
            "The acceptance criterion identifier must be a valid UUID.");

    public static (Guid Identifier, IResult? Error) ParseImplementationTask(string value) =>
        Parse(
            value,
            "taskId",
            "The implementation task identifier must be a valid UUID.");

    private static (Guid Identifier, IResult? Error) Parse(
        string value,
        string field,
        string errorMessage)
    {
        if (Guid.TryParse(value, out var identifier))
        {
            return (identifier, null);
        }

        return (Guid.Empty, EndpointProblems.InvalidIdentifier(field, errorMessage));
    }
}
