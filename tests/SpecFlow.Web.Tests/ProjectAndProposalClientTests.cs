using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

public sealed class ProjectAndProposalClientTests
{
    [Fact]
    public async Task ProjectOperationsUsePublicRoutesAndDeserializeResponses()
    {
        var project = new ProjectDto(ProjectId, "Equipo", "Trabajo", CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { project }) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(project) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(project) });
        var client = CreateClient(handler);

        var listed = await client.ListProjectsAsync();
        var fetched = await client.GetProjectAsync(ProjectId);
        var created = await client.CreateProjectAsync("Equipo", "Trabajo");

        Assert.Equal(ProjectId, Assert.Single(listed.Value!).Id);
        Assert.Equal("Equipo", fetched.Value?.Name);
        Assert.Equal(ProjectId, created.Value?.Id);
        Assert.Equal(["GET /api/projects", $"GET /api/projects/{ProjectId}", "POST /api/projects"], handler.Requests.Select(r => r.Route));
        Assert.Equal("Equipo", handler.Requests[2].Body!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ProposalOperationsStayInProjectAndCreationOmitsSource()
    {
        var proposal = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "pending", null, null, CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { proposal }) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(proposal) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(proposal) });
        var client = CreateClient(handler);

        var listed = await client.ListProposalsAsync(ProjectId);
        var fetched = await client.GetProposalAsync(ProjectId, ProposalId);
        var created = await client.CreateProposalAsync(ProjectId, "Idea", null);

        Assert.Equal(ProposalId, Assert.Single(listed.Value!).Id);
        Assert.Equal("pending", fetched.Value?.Status);
        Assert.Equal(ProposalId, created.Value?.Id);
        Assert.Equal(
            [$"GET /api/projects/{ProjectId}/proposals", $"GET /api/projects/{ProjectId}/proposals/{ProposalId}", $"POST /api/projects/{ProjectId}/proposals"],
            handler.Requests.Select(r => r.Route));
        Assert.False(handler.Requests[2].Body!.Value.TryGetProperty("sourceProposalId", out _));
    }

    [Fact]
    public async Task AcceptProposalUsesProjectRouteWithoutBodyAndReadsDecision()
    {
        var decidedAt = CreatedAt.AddMinutes(3);
        var accepted = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "accepted", decidedAt, null, CreatedAt);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(accepted)
        });

        var result = await CreateClient(handler).AcceptProposalAsync(ProjectId, ProposalId);

        Assert.Equal("accepted", result.Value?.Status);
        Assert.Equal(decidedAt, result.Value?.DecidedAtUtc);
        Assert.Equal($"POST /api/projects/{ProjectId}/proposals/{ProposalId}/accept", Assert.Single(handler.Requests).Route);
        Assert.Null(handler.Requests[0].Body);
    }

    [Fact]
    public async Task RejectProposalSendsUnchangedReasonAndReadsDecision()
    {
        const string reason = "  Falta valor  ";
        var decidedAt = CreatedAt.AddMinutes(4);
        var rejected = new ProposalDto(ProposalId, ProjectId, "Idea", null, null, "rejected", decidedAt, "Falta valor", CreatedAt);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rejected)
        });

        var result = await CreateClient(handler).RejectProposalAsync(ProjectId, ProposalId, reason);

        Assert.Equal("rejected", result.Value?.Status);
        Assert.Equal("Falta valor", result.Value?.RejectionReason);
        Assert.Equal(decidedAt, result.Value?.DecidedAtUtc);
        Assert.Equal($"POST /api/projects/{ProjectId}/proposals/{ProposalId}/reject", Assert.Single(handler.Requests).Route);
        Assert.Equal(reason, handler.Requests[0].Body!.Value.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    public async Task DecisionErrorsKeepTheirStatusAndValidationField(HttpStatusCode status, int expectedStatus, bool hasReason)
    {
        var handler = new RecordingHandler(hasReason ? Problem(status, "reason") : Problem(status));

        var result = await CreateClient(handler).RejectProposalAsync(ProjectId, ProposalId, "");

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasReason, result.Error?.HasField("reason"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "name", 400)]
    [InlineData(HttpStatusCode.Conflict, "name", 409)]
    public async Task ProblemDetailsFieldErrorsRemainAssociatedWithTheirField(HttpStatusCode status, string field, int expectedStatus)
    {
        var handler = new RecordingHandler(Problem(status, field));
        var result = await CreateClient(handler).CreateProjectAsync("", null);

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.True(result.Error?.HasField(field));
    }

    [Fact]
    public async Task MissingResourceAndNetworkFailureReturnPresentableErrors()
    {
        var missingHandler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent.Create(new { title = "Project not found" })
        });
        var missing = await CreateClient(missingHandler).GetProjectAsync(ProjectId);
        var offline = await CreateClient(new FailingHandler()).ListProjectsAsync();

        Assert.Equal(404, missing.Error?.StatusCode);
        Assert.Null(offline.Error?.StatusCode);
        Assert.NotEmpty(offline.Error!.Message);
    }
}
