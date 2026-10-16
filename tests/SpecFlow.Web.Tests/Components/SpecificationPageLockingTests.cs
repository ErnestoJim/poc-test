using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Web.Components.Pages;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.Components.ScriptedApi;

namespace SpecFlow.Web.Tests.Components;

// Only one edit can be active on the specification page; the rest of the page waits for it.
public sealed class SpecificationPageLockingTests : BunitContext
{
    private static readonly Guid ProjectId = Guid.Parse("4fae5d6c-7a8b-4c9d-8e0f-4a5b6c7d8e9f");
    private static readonly Guid ProposalId = Guid.Parse("5abf6e7d-8b9c-4d0e-9f1a-5b6c7d8e9f0a");
    private static readonly Guid TaskId = Guid.Parse("6bc07f8e-9c0d-4e1f-8a2b-6c7d8e9f0a1b");
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly ScriptedApi api = new();

    public SpecificationPageLockingTests()
    {
        var specificationPath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification";
        api.On(HttpMethod.Get, $"/api/projects/{ProjectId}", Json(new ProjectDto(ProjectId, "Proyecto", null, Now)))
            .On(HttpMethod.Get, $"/api/projects/{ProjectId}/proposals/{ProposalId}",
                Json(new ProposalDto(ProposalId, ProjectId, "Propuesta", null, null, "accepted", Now, null, Now)))
            .On(HttpMethod.Get, specificationPath,
                Json(new SpecificationDto(Guid.NewGuid(), ProposalId, "# Spec", "draft", null, null, null, Now, Now), "\"s1\""))
            .On(HttpMethod.Get, $"{specificationPath}/acceptance-criteria", Json(Array.Empty<AcceptanceCriterionDto>()))
            .On(HttpMethod.Get, $"{specificationPath}/tasks",
                Json(new[] { new ImplementationTaskDto(TaskId, Guid.Empty, "Tarea uno", null, "pending", null, null, 1, Now, Now) }))
            .On(HttpMethod.Get, $"{specificationPath}/tasks/{TaskId}/acceptance-criteria", Json(new TaskAcceptanceCriteriaDto([])));
        Services.AddSingleton(api.Client);
    }

    [Fact]
    public void TextInTheNewCriterionLocksContentTasksAndLifecycleUntilItIsCleared()
    {
        var cut = Render<SpecificationDetail>(parameters => parameters
            .Add(page => page.ProjectId, ProjectId.ToString())
            .Add(page => page.ProposalId, ProposalId.ToString()));
        cut.WaitForAssertion(() => Assert.False(Button(cut, "Aprobar especificación").IsDisabled()));
        Assert.NotNull(Button(cut, "Editar"));

        cut.Find("#criterion-new-content").Input("Un criterio a medio escribir");

        cut.WaitForAssertion(() => Assert.True(Button(cut, "Aprobar especificación").IsDisabled()));
        Assert.Null(Button(cut, "Editar"));
        Assert.True(Button(cut, "Editar tarea 1")?.IsDisabled() ?? true);
        Assert.Contains("Termina la edición en curso para modificar las tareas.", cut.Markup);
        Assert.Contains("Termina la edición en curso para cambiar el estado.", cut.Markup);

        cut.Find("#criterion-new-content").Input("");

        cut.WaitForAssertion(() => Assert.False(Button(cut, "Aprobar especificación").IsDisabled()));
        Assert.NotNull(Button(cut, "Editar"));
        Assert.DoesNotContain("Termina la edición en curso para modificar las tareas.", cut.Markup);
    }

    [Fact]
    public void EditingTheContentLocksCriteriaAndTasks()
    {
        var cut = Render<SpecificationDetail>(parameters => parameters
            .Add(page => page.ProjectId, ProjectId.ToString())
            .Add(page => page.ProposalId, ProposalId.ToString()));
        cut.WaitForAssertion(() => Assert.NotNull(Button(cut, "Editar")));

        Button(cut, "Editar")!.Click();

        cut.WaitForAssertion(() => Assert.Contains("Termina la edición en curso para modificar los criterios.", cut.Markup));
        Assert.Contains("Termina la edición en curso para modificar las tareas.", cut.Markup);
        Assert.Empty(cut.FindAll("#criterion-new-content"));
        Assert.True(Button(cut, "Aprobar especificación")?.IsDisabled() ?? true);
    }

    private static AngleSharp.Dom.IElement? Button(IRenderedComponent<SpecificationDetail> cut, string label) =>
        cut.FindAll("button").FirstOrDefault(button => (button.GetAttribute("aria-label") ?? button.TextContent.Trim()) == label);
}

internal static class ElementExtensions
{
    public static bool IsDisabled(this AngleSharp.Dom.IElement? element) => element?.HasAttribute("disabled") ?? true;
}
