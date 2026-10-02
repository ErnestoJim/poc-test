using SpecFlow.Domain.Projects;

namespace SpecFlow.Api.Contracts.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAtUtc)
{
    public static ProjectResponse FromDomain(Project project) =>
        new(project.Id, project.Name, project.Description, project.CreatedAtUtc);
}
