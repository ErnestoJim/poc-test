using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

public sealed class TechnicalDecisionClientTests
{
    [Fact]
    public async Task TechnicalDecisionOperationsUseProjectRoutesBodiesAndEntityTags()
    {
        const string content = "# Decisión\r\n\nUsar SQLite  ";
        var decision = new TechnicalDecisionDto(
            Guid.NewGuid(), ProjectId, "Persistencia", content, "draft", null, null, null, null, CreatedAt, CreatedAt);
        var replacementId = Guid.NewGuid();
        var basePath = $"/api/projects/{ProjectId}/technical-decisions";
        HttpResponseMessage Ok(TechnicalDecisionDto value, string tag) =>
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) }, tag);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { decision }) },
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(decision) }, "\"d-1\""),
            Ok(decision, "\"d-1\""),
            Ok(decision, "\"d-2\""),
            Ok(decision with { Status = "accepted", DecidedAtUtc = CreatedAt }, "\"d-3\""),
            Ok(decision with { Status = "rejected", RejectionReason = "No encaja" }, "\"d-4\""),
            Ok(decision with { Status = "superseded", SupersededByDecisionId = replacementId }, "\"d-5\""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        var listed = await client.ListTechnicalDecisionsAsync(ProjectId);
        var created = await client.CreateTechnicalDecisionAsync(ProjectId, "  Persistencia ", content);
        var fetched = await client.GetTechnicalDecisionAsync(ProjectId, decision.Id);
        var updated = await client.UpdateTechnicalDecisionAsync(ProjectId, decision.Id, "Persistencia", content, "\"d-1\"");
        var accepted = await client.AcceptTechnicalDecisionAsync(ProjectId, decision.Id, "\"d-2\"");
        var rejected = await client.RejectTechnicalDecisionAsync(ProjectId, decision.Id, " No encaja ", "\"d-2\"");
        var superseded = await client.SupersedeTechnicalDecisionAsync(ProjectId, decision.Id, replacementId, "\"d-3\"");
        var deleted = await client.DeleteTechnicalDecisionAsync(ProjectId, decision.Id, "\"d-1\"");

        Assert.Equal([decision.Id], listed.Value!.Select(d => d.Id));
        Assert.Equal("\"d-1\"", created.EntityTag);
        Assert.Equal("\"d-1\"", fetched.EntityTag);
        Assert.Equal("\"d-2\"", updated.EntityTag);
        Assert.Equal("accepted", accepted.Value?.Status);
        Assert.Equal("No encaja", rejected.Value?.RejectionReason);
        Assert.Equal(replacementId, superseded.Value?.SupersededByDecisionId);
        Assert.Equal("\"d-5\"", superseded.EntityTag);
        Assert.True(deleted.Value);
        Assert.Equal(
            [
                $"GET {basePath}",
                $"POST {basePath}",
                $"GET {basePath}/{decision.Id}",
                $"PUT {basePath}/{decision.Id}",
                $"POST {basePath}/{decision.Id}/accept",
                $"POST {basePath}/{decision.Id}/reject",
                $"POST {basePath}/{decision.Id}/supersede",
                $"DELETE {basePath}/{decision.Id}"
            ],
            handler.Requests.Select(r => r.Route));
        Assert.Equal(
            [null, null, null, "\"d-1\"", "\"d-2\"", "\"d-2\"", "\"d-3\"", "\"d-1\""],
            handler.Requests.Select(r => r.IfMatch));
        Assert.Equal("  Persistencia ", handler.Requests[1].Body!.Value.GetProperty("title").GetString());
        Assert.Equal(content, handler.Requests[1].Body!.Value.GetProperty("content").GetString());
        Assert.Equal(content, handler.Requests[3].Body!.Value.GetProperty("content").GetString());
        Assert.Null(handler.Requests[4].Body);
        Assert.Equal(" No encaja ", handler.Requests[5].Body!.Value.GetProperty("reason").GetString());
        Assert.Equal(replacementId.ToString(), handler.Requests[6].Body!.Value.GetProperty("replacementDecisionId").GetString());
        Assert.Null(handler.Requests[7].Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task TechnicalDecisionErrorsKeepStatusAndFields(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error(params string[] fields) => hasField ? Problem(status, fields) : Problem(status);
        var handler = new RecordingHandler(
            Error("title", "content"), Error("content"), Error(), Error("reason"), Error("replacementDecisionId"), Error());
        var client = CreateClient(handler);
        var decisionId = Guid.NewGuid();

        var created = await client.CreateTechnicalDecisionAsync(ProjectId, "", "");
        var updated = await client.UpdateTechnicalDecisionAsync(ProjectId, decisionId, "T", "", "\"v1\"");
        var accepted = await client.AcceptTechnicalDecisionAsync(ProjectId, decisionId, "\"v1\"");
        var rejected = await client.RejectTechnicalDecisionAsync(ProjectId, decisionId, "", "\"v1\"");
        var superseded = await client.SupersedeTechnicalDecisionAsync(ProjectId, decisionId, Guid.NewGuid(), "\"v1\"");
        var deleted = await client.DeleteTechnicalDecisionAsync(ProjectId, decisionId, "\"v1\"");

        Assert.All([created.Error, updated.Error, accepted.Error, rejected.Error, superseded.Error, deleted.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, created.Error!.HasField("title"));
        Assert.Equal(hasField, created.Error.HasField("content"));
        Assert.Equal(hasField, updated.Error!.HasField("content"));
        Assert.Equal(hasField, rejected.Error!.HasField("reason"));
        Assert.Equal(hasField, superseded.Error!.HasField("replacementDecisionId"));
        Assert.Null(accepted.EntityTag);
        Assert.False(deleted.IsSuccess);
    }
}
