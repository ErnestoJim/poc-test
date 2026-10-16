using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.ClientTestSupport;

namespace SpecFlow.Web.Tests;

public sealed class SpecificationClientTests
{
    [Fact]
    public async Task SpecificationOperationsUseSingularRouteAndPreserveMarkdown()
    {
        const string markdown = "# Objetivo\r\n\r\n  Texto literal  ";
        var specification = new SpecificationDto(
            Guid.NewGuid(), ProposalId, markdown, "draft", null, null, null, CreatedAt, CreatedAt);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { title = "Specification not found" })
            },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(specification) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(specification) });
        var client = CreateClient(handler);

        var missing = await client.GetSpecificationAsync(ProjectId, ProposalId);
        var created = await client.CreateSpecificationAsync(ProjectId, ProposalId, markdown);
        var fetched = await client.GetSpecificationAsync(ProjectId, ProposalId);

        Assert.Equal(404, missing.Error?.StatusCode);
        Assert.Equal(markdown, created.Value?.Content);
        Assert.Equal("draft", created.Value?.Status);
        Assert.Equal(CreatedAt, fetched.Value?.UpdatedAtUtc);
        Assert.Equal(
            [$"GET /api/projects/{ProjectId}/proposals/{ProposalId}/specification",
             $"POST /api/projects/{ProjectId}/proposals/{ProposalId}/specification",
             $"GET /api/projects/{ProjectId}/proposals/{ProposalId}/specification"],
            handler.Requests.Select(r => r.Route));
        Assert.Equal(markdown, handler.Requests[1].Body!.Value.GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    public async Task SpecificationCreationErrorsKeepStatusAndContentField(HttpStatusCode status, int expectedStatus, bool hasContent)
    {
        var handler = new RecordingHandler(hasContent ? Problem(status, "content") : Problem(status));

        var result = await CreateClient(handler).CreateSpecificationAsync(ProjectId, ProposalId, "");

        Assert.Null(result.Value);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasContent, result.Error?.HasField("content"));
    }

    [Fact]
    public async Task SpecificationReadAndCreationExposeEntityTag()
    {
        var specification = new SpecificationDto(
            Guid.NewGuid(), ProposalId, "# Documento", "draft", null, null, null, CreatedAt, CreatedAt);
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(specification) }, "\"v1\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(specification) }, "\"v2\""));
        var client = CreateClient(handler);

        var created = await client.CreateSpecificationAsync(ProjectId, ProposalId, "# Documento");
        var fetched = await client.GetSpecificationAsync(ProjectId, ProposalId);

        Assert.Equal("\"v1\"", created.EntityTag);
        Assert.Equal("\"v2\"", fetched.EntityTag);
        Assert.All(handler.Requests, request => Assert.Null(request.IfMatch));
    }

    [Fact]
    public async Task UpdateSpecificationSendsExactContentAndIfMatchAndReadsNewEntityTag()
    {
        const string markdown = "# Objetivo\r\n\r\n  Texto editado  ";
        const string entityTag = "\"c3BlYy0xOjQ=\"";
        var updated = new SpecificationDto(
            Guid.NewGuid(), ProposalId, markdown, "draft", null, null, null, CreatedAt, CreatedAt.AddMinutes(5));
        var handler = new RecordingHandler(WithEntityTag(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(updated) },
            "\"c3BlYy0xOjU=\""));

        var result = await CreateClient(handler).UpdateSpecificationAsync(ProjectId, ProposalId, markdown, entityTag);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"PUT /api/projects/{ProjectId}/proposals/{ProposalId}/specification", request.Route);
        Assert.Equal(entityTag, request.IfMatch);
        Assert.Equal(markdown, request.Body!.Value.GetProperty("content").GetString());
        Assert.Equal(markdown, result.Value?.Content);
        Assert.Equal(CreatedAt.AddMinutes(5), result.Value?.UpdatedAtUtc);
        Assert.Equal("\"c3BlYy0xOjU=\"", result.EntityTag);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, true)]
    [InlineData(HttpStatusCode.NotFound, 404, false)]
    [InlineData(HttpStatusCode.Conflict, 409, false)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412, false)]
    [InlineData((HttpStatusCode)428, 428, false)]
    public async Task SpecificationUpdateErrorsKeepStatusAndContentField(HttpStatusCode status, int expectedStatus, bool hasContent)
    {
        var handler = new RecordingHandler(hasContent ? Problem(status, "content") : Problem(status));

        var result = await CreateClient(handler).UpdateSpecificationAsync(ProjectId, ProposalId, "", "\"v1\"");

        Assert.Null(result.Value);
        Assert.Null(result.EntityTag);
        Assert.Equal(expectedStatus, result.Error?.StatusCode);
        Assert.Equal(hasContent, result.Error?.HasField("content"));
        Assert.NotEmpty(result.Error!.Message);
    }

    [Fact]
    public async Task SpecificationTransitionsSendIfMatchWithoutBodyAndReadNewState()
    {
        var approved = new SpecificationDto(
            Guid.NewGuid(), ProposalId, "# Spec", "approved", CreatedAt, null, null, CreatedAt, CreatedAt);
        var implemented = approved with { Status = "implemented", ImplementedAtUtc = CreatedAt.AddDays(1) };
        var basePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification";
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(approved) }, "\"s-2\""),
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(implemented) }, "\"s-3\""));
        var client = CreateClient(handler);

        var approve = await client.ApproveSpecificationAsync(ProjectId, ProposalId, "\"s-1\"");
        var markImplemented = await client.MarkSpecificationImplementedAsync(ProjectId, ProposalId, "\"s-2\"");

        Assert.Equal("approved", approve.Value?.Status);
        Assert.Equal(CreatedAt, approve.Value?.ApprovedAtUtc);
        Assert.Equal("\"s-2\"", approve.EntityTag);
        Assert.Equal("implemented", markImplemented.Value?.Status);
        Assert.Equal(CreatedAt.AddDays(1), markImplemented.Value?.ImplementedAtUtc);
        Assert.Equal("\"s-3\"", markImplemented.EntityTag);
        Assert.Equal([$"POST {basePath}/approve", $"POST {basePath}/mark-implemented"], handler.Requests.Select(r => r.Route));
        Assert.Equal(["\"s-1\"", "\"s-2\""], handler.Requests.Select(r => r.IfMatch));
        Assert.All(handler.Requests, request => Assert.Null(request.Body));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    [InlineData(HttpStatusCode.PreconditionFailed, 412)]
    [InlineData((HttpStatusCode)428, 428)]
    public async Task SpecificationTransitionErrorsKeepStatus(HttpStatusCode status, int expectedStatus)
    {
        HttpResponseMessage Error() => Problem(status);
        var client = CreateClient(new RecordingHandler(Error(), Error()));

        var approve = await client.ApproveSpecificationAsync(ProjectId, ProposalId, "\"v1\"");
        var markImplemented = await client.MarkSpecificationImplementedAsync(ProjectId, ProposalId, "\"v1\"");

        Assert.All([approve, markImplemented], result =>
        {
            Assert.Null(result.Value);
            Assert.Null(result.EntityTag);
            Assert.Equal(expectedStatus, result.Error?.StatusCode);
            Assert.NotEmpty(result.Error!.Message);
        });
    }

    [Fact]
    public async Task VerifySpecificationSendsIfMatchWithoutBodyAndReadsNewState()
    {
        var verified = new SpecificationDto(
            Guid.NewGuid(), ProposalId, "# Spec", "verified", CreatedAt, CreatedAt, CreatedAt.AddDays(2), CreatedAt, CreatedAt);
        var handler = new RecordingHandler(
            WithEntityTag(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(verified) }, "\"s-5\""));
        var client = CreateClient(handler);

        var result = await client.VerifySpecificationAsync(ProjectId, ProposalId, "\"s-4\"");

        Assert.Equal("verified", result.Value?.Status);
        Assert.Equal(CreatedAt.AddDays(2), result.Value?.VerifiedAtUtc);
        Assert.Equal("\"s-5\"", result.EntityTag);
        Assert.Equal([$"POST /api/projects/{ProjectId}/proposals/{ProposalId}/specification/verify"], handler.Requests.Select(r => r.Route));
        Assert.Equal(["\"s-4\""], handler.Requests.Select(r => r.IfMatch));
        Assert.Null(handler.Requests[0].Body);
    }
}
