using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

public sealed class AcceptanceCriteriaClientTests
{
    [Fact]
    public async Task AcceptanceCriterionOperationsUseSpecificationRoutesAndEntityTags()
    {
        const string text = "  Dado un criterio\r\nentonces se conserva  ";
        var first = new AcceptanceCriterionDto(Guid.NewGuid(), Guid.NewGuid(), text, 1, CreatedAt, CreatedAt);
        var second = first with { Id = Guid.NewGuid(), Content = "Otro", Position = 2 };
        var basePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/acceptance-criteria";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { first, second }) }, "\"list-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(first) }, "\"item-2\""),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.NoContent), "\"list-2\""));
        var client = CreateClient(handler);

        var listed = await client.ListAcceptanceCriteriaAsync(ProjectId, ProposalId);
        var created = await client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, text);
        var fetched = await client.GetAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id);
        var updated = await client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id, text, "\"item-1\"");
        var deleted = await client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, first.Id, "\"item-2\"");
        var reordered = await client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [second.Id, first.Id], "\"list-1\"");

        Assert.Equal([first.Id, second.Id], listed.Value!.Select(criterion => criterion.Id));
        Assert.Equal("\"list-1\"", listed.EntityTag);
        Assert.Equal(text, created.Value?.Content);
        Assert.Equal("\"item-1\"", fetched.EntityTag);
        Assert.Equal("\"item-2\"", updated.EntityTag);
        Assert.True(deleted.IsSuccess);
        Assert.True(deleted.Value);
        Assert.True(reordered.Value);
        Assert.Equal("\"list-2\"", reordered.EntityTag);
        Assert.Equal(
            [
                $"GET {basePath}",
                $"POST {basePath}",
                $"GET {basePath}/{first.Id}",
                $"PUT {basePath}/{first.Id}",
                $"DELETE {basePath}/{first.Id}",
                $"PUT {basePath}/order"
            ],
            handler.Requests.Select(r => r.Route));
        Assert.Equal([null, null, null, "\"item-1\"", "\"item-2\"", "\"list-1\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Equal(text, handler.Requests[1].Body!.Value.GetProperty("content").GetString());
        Assert.Equal(text, handler.Requests[3].Body!.Value.GetProperty("content").GetString());
        Assert.Null(handler.Requests[4].Body);
        Assert.Equal(
            [second.Id.ToString(), first.Id.ToString()],
            handler.Requests[5].Body!.Value.GetProperty("criterionIds").EnumerateArray().Select(id => id.GetString()));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task AcceptanceCriterionModificationErrorsKeepStatusAndField(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error(string field) => hasField ? Problem(status, field) : Problem(status);
        var handler = new RecordingHandler(Error("content"), Error("content"), Error("content"), Error("criterionIds"));
        var client = CreateClient(handler);
        var criterionId = Guid.NewGuid();

        var created = await client.CreateAcceptanceCriterionAsync(ProjectId, ProposalId, "");
        var updated = await client.UpdateAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "", "\"v1\"");
        var deleted = await client.DeleteAcceptanceCriterionAsync(ProjectId, ProposalId, criterionId, "\"v1\"");
        var reordered = await client.ReorderAcceptanceCriteriaAsync(ProjectId, ProposalId, [criterionId], "\"v1\"");

        Assert.All([created.Error, updated.Error, deleted.Error, reordered.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, created.Error!.HasField("content"));
        Assert.Equal(hasField, updated.Error!.HasField("content"));
        Assert.Equal(hasField, reordered.Error!.HasField("criterionIds"));
        Assert.False(deleted.Value);
        Assert.Null(reordered.EntityTag);
    }

    [Fact]
    public async Task CriterionVerificationIsReadAndSavedWithItsOwnEntityTag()
    {
        var criterionId = Guid.NewGuid();
        const string evidence = "  Probado en staging\r\n  ";
        var path = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification/acceptance-criteria/{criterionId}/verification";
        var unverified = new AcceptanceCriterionVerificationDto("unverified", null, null);
        var passed = new AcceptanceCriterionVerificationDto("passed", evidence, CreatedAt);
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unverified) }, "\"ver-1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(passed) }, "\"ver-2\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unverified) }, "\"ver-3\""));
        var client = CreateClient(handler);

        var read = await client.GetAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, criterionId);
        var saved = await client.SaveAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, criterionId, "passed", evidence, "\"ver-1\"");
        var cleared = await client.SaveAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, criterionId, "unverified", null, "\"ver-2\"");

        Assert.Equal("unverified", read.Value?.Status);
        Assert.Null(read.Value?.CheckedAtUtc);
        Assert.Equal("\"ver-1\"", read.EntityTag);
        Assert.Equal(evidence, saved.Value?.Evidence);
        Assert.Equal(CreatedAt, saved.Value?.CheckedAtUtc);
        Assert.Equal("\"ver-2\"", saved.EntityTag);
        Assert.Equal("\"ver-3\"", cleared.EntityTag);
        Assert.Equal([$"GET {path}", $"PUT {path}", $"PUT {path}"], handler.Requests.Select(r => r.Route));
        Assert.Equal([null, "\"ver-1\"", "\"ver-2\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Null(handler.Requests[0].Body);
        Assert.Equal("passed", handler.Requests[1].Body!.Value.GetProperty("status").GetString());
        Assert.Equal(evidence, handler.Requests[1].Body!.Value.GetProperty("evidence").GetString());
        Assert.Equal("unverified", handler.Requests[2].Body!.Value.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, handler.Requests[2].Body!.Value.GetProperty("evidence").ValueKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task VerificationErrorsKeepStatusAndField(HttpStatusCode status, int expectedStatus, bool hasField)
    {
        HttpResponseMessage Error() => hasField ? Problem(status, "evidence") : Problem(status);
        var client = CreateClient(new RecordingHandler(Error(), Error(), Error()));
        var criterionId = Guid.NewGuid();

        var read = await client.GetAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, criterionId);
        var saved = await client.SaveAcceptanceCriterionVerificationAsync(ProjectId, ProposalId, criterionId, "passed", " ", "\"v1\"");
        var verify = await client.VerifySpecificationAsync(ProjectId, ProposalId, "\"v1\"");

        Assert.All([read.Error, saved.Error, verify.Error], error =>
        {
            Assert.Equal(expectedStatus, error?.StatusCode);
            Assert.NotEmpty(error!.Message);
        });
        Assert.Equal(hasField, saved.Error!.HasField("evidence"));
        Assert.Null(saved.Value);
        Assert.Null(verify.EntityTag);
    }
}
