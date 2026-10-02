using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpecFlow.Api.Contracts.Projects;
using SpecFlow.Domain.Projects;
using SpecFlow.Infrastructure.Persistence;

namespace SpecFlow.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects")
            .WithTags("Projects");

        group.MapPost("/", CreateProjectAsync)
            .WithName("CreateProject")
            .Accepts<CreateProjectRequest>("application/json")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id}", GetProjectAsync)
            .WithName("GetProject")
            .Produces<ProjectResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListProjectsAsync)
            .WithName("ListProjects")
            .Produces<IReadOnlyList<ProjectResponse>>();

        return group;
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        SpecFlowDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validationErrors = Project.Validate(request.Name, request.Description);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var project = Project.Create(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            timeProvider.GetUtcNow());

        var nameAlreadyExists = await dbContext.Projects
            .AnyAsync(
                existingProject => existingProject.NormalizedName == project.NormalizedName,
                cancellationToken);

        if (nameAlreadyExists)
        {
            return DuplicateNameProblem(project.Name);
        }

        dbContext.Projects.Add(project);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return DuplicateNameProblem(project.Name);
        }

        var response = ProjectResponse.FromDomain(project);
        return Results.CreatedAtRoute("GetProject", new { id = project.Id }, response);
    }

    private static async Task<IResult> GetProjectAsync(
        string id,
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var projectId))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["id"] = ["The project identifier must be a valid UUID."]
                });
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(existingProject => existingProject.Id == projectId, cancellationToken);

        return project is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Project not found",
                detail: $"No project with identifier '{projectId}' was found.")
            : Results.Ok(ProjectResponse.FromDomain(project));
    }

    private static async Task<IResult> ListProjectsAsync(
        SpecFlowDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .OrderByDescending(project => project.CreatedAtUtc)
            .ThenBy(project => project.Id)
            .Select(project => new ProjectResponse(
                project.Id,
                project.Name,
                project.Description,
                project.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(projects);
    }

    private static IResult DuplicateNameProblem(string projectName) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Project name already exists",
            detail: $"A project named '{projectName}' already exists.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["errors"] = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["name"] = ["Project names must be unique."]
                }
            });

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
