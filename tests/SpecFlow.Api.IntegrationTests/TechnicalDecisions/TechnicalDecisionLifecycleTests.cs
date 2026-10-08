using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpecFlow.Api.Contracts.TechnicalDecisions;
using SpecFlow.Api.IntegrationTests.Infrastructure;
using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Api.IntegrationTests.TechnicalDecisions;

public sealed class TechnicalDecisionLifecycleTests
{
    [Fact]
    public async Task Accept_Draft_RecordsDecisionTimeAndReturnsNewEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var resourceRoute = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            resourceRoute);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.AcceptRoute(project.Id, decision.Id),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(accepted);
        Assert.Equal("accepted", accepted.Status);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), accepted.DecidedAtUtc);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), accepted.UpdatedAtUtc);
        Assert.Null(accepted.RejectionReason);
        Assert.Null(accepted.SupersededAtUtc);
        Assert.Null(accepted.SupersededByDecisionId);
        Assert.NotEqual(
            originalEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Fact]
    public async Task Reject_Draft_NormalizesReasonAndReturnsNewEntityTag()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var resourceRoute = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            resourceRoute);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.RejectRoute(project.Id, decision.Id),
            new RejectTechnicalDecisionRequest("  Operational cost is too high.  "),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(rejected);
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("Operational cost is too high.", rejected.RejectionReason);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), rejected.DecidedAtUtc);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), rejected.UpdatedAtUtc);
        Assert.NotEqual(
            originalEntityTag,
            HttpPreconditionTestData.GetRequiredEntityTag(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithMissingReason_ReturnsValidationProblem(string? reason)
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.RejectRoute(project.Id, decision.Id),
            new RejectTechnicalDecisionRequest(reason),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("reason", problem.Errors);
        Assert.Equal(decision, await client.GetFromJsonAsync<TechnicalDecisionResponse>(route));
    }

    [Fact]
    public async Task Reject_WithReasonOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.RejectRoute(project.Id, decision.Id),
            new RejectTechnicalDecisionRequest(
                new string('a', TechnicalDecision.MaxRejectionReasonLength + 1)),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("reason", problem.Errors);
    }

    [Fact]
    public async Task Transition_WhenAlreadyDecided_ReturnsConflictAndPreservesState()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var resourceRoute = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        using var acceptResponse = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            resourceRoute,
            TechnicalDecisionTestData.AcceptRoute(project.Id, decision.Id));
        acceptResponse.EnsureSuccessStatusCode();
        var accepted = await acceptResponse.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        var acceptedEntityTag = HttpPreconditionTestData.GetRequiredEntityTag(acceptResponse);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.RejectRoute(project.Id, decision.Id),
            new RejectTechnicalDecisionRequest("Changed our mind"),
            acceptedEntityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision transition conflict", problem.Title);
        Assert.Equal(accepted, await client.GetFromJsonAsync<TechnicalDecisionResponse>(
            resourceRoute));
    }

    [Fact]
    public async Task Update_AcceptedDecision_ReturnsNotEditableConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        using var acceptResponse = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            route,
            TechnicalDecisionTestData.AcceptRoute(project.Id, decision.Id));
        acceptResponse.EnsureSuccessStatusCode();
        var entityTag = HttpPreconditionTestData.GetRequiredEntityTag(acceptResponse);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Put,
            route,
            new SaveTechnicalDecisionRequest("Updated", "# Updated"),
            entityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision is not editable", problem.Title);
    }

    [Fact]
    public async Task Supersede_WithLaterAcceptedDecision_UpdatesOnlyOriginalDecision()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var original = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            project.Id,
            "Original",
            "# Original");
        var originalRoute = TechnicalDecisionTestData.DecisionRoute(project.Id, original.Id);
        using var acceptOriginal = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            originalRoute,
            TechnicalDecisionTestData.AcceptRoute(project.Id, original.Id));
        acceptOriginal.EnsureSuccessStatusCode();
        var acceptedOriginal = await acceptOriginal.Content
            .ReadFromJsonAsync<TechnicalDecisionResponse>();
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var replacement = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            project.Id,
            "Replacement",
            "# Replacement");
        var replacementRoute = TechnicalDecisionTestData.DecisionRoute(
            project.Id,
            replacement.Id);
        using var acceptReplacement = await HttpPreconditionTestData
            .PostWithCurrentEntityTagAsync(
                client,
                replacementRoute,
                TechnicalDecisionTestData.AcceptRoute(project.Id, replacement.Id));
        acceptReplacement.EnsureSuccessStatusCode();
        var acceptedReplacement = await acceptReplacement.Content
            .ReadFromJsonAsync<TechnicalDecisionResponse>();
        var originalEntityTag = await HttpPreconditionTestData.GetEntityTagAsync(
            client,
            originalRoute);
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, original.Id),
            new SupersedeTechnicalDecisionRequest(replacement.Id),
            originalEntityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var superseded = await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>();
        Assert.NotNull(superseded);
        Assert.Equal("superseded", superseded.Status);
        Assert.Equal(acceptedOriginal?.DecidedAtUtc, superseded.DecidedAtUtc);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), superseded.SupersededAtUtc);
        Assert.Equal(replacement.Id, superseded.SupersededByDecisionId);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), superseded.UpdatedAtUtc);
        Assert.Equal(
            acceptedReplacement,
            await client.GetFromJsonAsync<TechnicalDecisionResponse>(replacementRoute));
    }

    [Fact]
    public async Task Supersede_WithReplacementFromAnotherProject_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var firstProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "First");
        var secondProject = await TechnicalDecisionTestData.CreateProjectAsync(client, "Second");
        var original = await CreateAcceptedDecisionAsync(client, firstProject.Id, "Original");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var replacement = await CreateAcceptedDecisionAsync(client, secondProject.Id, "Replacement");
        var route = TechnicalDecisionTestData.DecisionRoute(firstProject.Id, original.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(firstProject.Id, original.Id),
            new SupersedeTechnicalDecisionRequest(replacement.Id),
            entityTag);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Replacement technical decision not found", problem.Title);
    }

    [Fact]
    public async Task Supersede_WithUnknownReplacement_ReturnsNotFound()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var original = await CreateAcceptedDecisionAsync(client, project.Id, "Original");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, original.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, original.Id),
            new SupersedeTechnicalDecisionRequest(Guid.NewGuid()),
            entityTag);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Replacement technical decision not found", problem.Title);
    }

    [Fact]
    public async Task Supersede_WithDraftReplacement_ReturnsTransitionConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var original = await CreateAcceptedDecisionAsync(client, project.Id, "Original");
        var replacement = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            project.Id,
            "Replacement");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, original.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, original.Id),
            new SupersedeTechnicalDecisionRequest(replacement.Id),
            entityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision transition conflict", problem.Title);
    }

    [Fact]
    public async Task Supersede_WithEarlierAcceptedReplacement_ReturnsTransitionConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var earlier = await CreateAcceptedDecisionAsync(client, project.Id, "Earlier");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var later = await CreateAcceptedDecisionAsync(client, project.Id, "Later");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, later.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, later.Id),
            new SupersedeTechnicalDecisionRequest(earlier.Id),
            entityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Supersede_WithSameDecision_ReturnsTransitionConflict()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await CreateAcceptedDecisionAsync(client, project.Id, "Decision");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, decision.Id),
            new SupersedeTechnicalDecisionRequest(decision.Id),
            entityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Supersede_SeveralAcceptedDecisions_CanUseSameReplacement()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var first = await CreateAcceptedDecisionAsync(client, project.Id, "First");
        var second = await CreateAcceptedDecisionAsync(client, project.Id, "Second");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        var replacement = await CreateAcceptedDecisionAsync(client, project.Id, "Replacement");

        foreach (var original in new[] { first, second })
        {
            var route = TechnicalDecisionTestData.DecisionRoute(project.Id, original.Id);
            var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);
            using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
                client,
                HttpMethod.Post,
                TechnicalDecisionTestData.SupersedeRoute(project.Id, original.Id),
                new SupersedeTechnicalDecisionRequest(replacement.Id),
                entityTag);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var superseded = await response.Content
                .ReadFromJsonAsync<TechnicalDecisionResponse>();
            Assert.Equal(replacement.Id, superseded?.SupersededByDecisionId);
        }
    }

    [Fact]
    public async Task Supersede_WithEmptyReplacementIdentifier_ReturnsValidationProblem()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var original = await CreateAcceptedDecisionAsync(client, project.Id, "Original");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, original.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendAsJsonWithEntityTagAsync(
            client,
            HttpMethod.Post,
            TechnicalDecisionTestData.SupersedeRoute(project.Id, original.Id),
            new SupersedeTechnicalDecisionRequest(Guid.Empty),
            entityTag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("replacementDecisionId", problem.Errors);
    }

    [Fact]
    public async Task Delete_Draft_RemovesDecisionPermanently()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(client, project.Id);
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);

        using var response = await HttpPreconditionTestData.DeleteWithCurrentEntityTagAsync(
            client,
            route);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var getResponse = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_AcceptedDecision_ReturnsConflictAndPreservesDecision()
    {
        using var factory = new SpecFlowApiFactory();
        using var client = factory.CreateClient();
        var project = await TechnicalDecisionTestData.CreateProjectAsync(client);
        var decision = await CreateAcceptedDecisionAsync(client, project.Id, "Accepted");
        var route = TechnicalDecisionTestData.DecisionRoute(project.Id, decision.Id);
        var entityTag = await HttpPreconditionTestData.GetEntityTagAsync(client, route);

        using var response = await HttpPreconditionTestData.SendWithEntityTagAsync(
            client,
            HttpMethod.Delete,
            route,
            entityTag);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Technical decision cannot be deleted", problem.Title);
        Assert.NotNull(await client.GetFromJsonAsync<TechnicalDecisionResponse>(route));
    }

    private static async Task<TechnicalDecisionResponse> CreateAcceptedDecisionAsync(
        HttpClient client,
        Guid projectId,
        string title)
    {
        var decision = await TechnicalDecisionTestData.CreateDecisionAsync(
            client,
            projectId,
            title);
        var route = TechnicalDecisionTestData.DecisionRoute(projectId, decision.Id);
        using var response = await HttpPreconditionTestData.PostWithCurrentEntityTagAsync(
            client,
            route,
            TechnicalDecisionTestData.AcceptRoute(projectId, decision.Id));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TechnicalDecisionResponse>()
            ?? throw new InvalidOperationException(
                "The accept response did not contain a technical decision.");
    }
}
