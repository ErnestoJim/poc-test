using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SpecFlow.Web.Components.Specifications;
using SpecFlow.Web.Services;
using static SpecFlow.Web.Tests.Components.ScriptedApi;

namespace SpecFlow.Web.Tests.Components;

// Recovery paths of the plan sections that cannot be reproduced reliably in a browser.
public sealed class PlanSectionRecoveryTests : BunitContext
{
    private static readonly Guid ProjectId = Guid.Parse("0b6a1f2e-3c4d-4e5f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid ProposalId = Guid.Parse("1c7b2a3f-4d5e-4f6a-9b0c-1d2e3f4a5b6c");
    private static readonly Guid CriterionId = Guid.Parse("2d8c3b4a-5e6f-4a7b-8c9d-2e3f4a5b6c7d");
    private static readonly Guid TaskId = Guid.Parse("3e9d4c5b-6f7a-4b8c-9d0e-3f4a5b6c7d8e");
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly string BasePath = $"/api/projects/{ProjectId}/proposals/{ProposalId}/specification";

    private readonly ScriptedApi api = new();

    public PlanSectionRecoveryTests()
    {
        Services.AddSingleton(api.Client);
    }

    [Fact]
    public void TasksStayVisibleAndCoverageCanBeRetriedWhenLinksCannotBeRead()
    {
        api.On(HttpMethod.Get, $"{BasePath}/tasks", Json(new[] { Task("pending") }))
            .On(HttpMethod.Get, $"{BasePath}/acceptance-criteria", Json(new[] { Criterion() }))
            .On(HttpMethod.Get, $"{BasePath}/tasks/{TaskId}/acceptance-criteria", Offline, Json(new TaskAcceptanceCriteriaDto([CriterionId])));

        var cut = RenderTasks("draft");

        cut.WaitForAssertion(() => Assert.Contains("No se pudo consultar qué criterios cubre cada tarea", cut.Markup));
        Assert.Contains("Tarea uno", cut.Markup);
        Assert.DoesNotContain("Cubre el criterio", cut.Markup);

        Button(cut, "Reintentar").Click();

        cut.WaitForAssertion(() => Assert.Contains("Cubre el criterio 1.", cut.Markup));
        Assert.DoesNotContain("No se pudo consultar qué criterios", cut.Markup);
    }

    [Fact]
    public void CriteriaStayVisibleAndVerificationsCanBeRetriedWhenTheyCannotBeRead()
    {
        api.On(HttpMethod.Get, $"{BasePath}/acceptance-criteria", Json(new[] { Criterion() }))
            .On(HttpMethod.Get, $"{BasePath}/acceptance-criteria/{CriterionId}/verification",
                Offline, Json(new AcceptanceCriterionVerificationDto("passed", "Probado", Now)));

        var cut = Render<AcceptanceCriteriaSection>(parameters => Section<AcceptanceCriteriaSection, CriteriaOperation>(parameters, "implemented"));

        cut.WaitForAssertion(() => Assert.Contains("No se pudo consultar la verificación de los criterios", cut.Markup));
        Assert.Contains("Criterio uno", cut.Markup);

        Button(cut, "Reintentar").Click();

        cut.WaitForAssertion(() => Assert.Contains("Cumple", cut.Markup));
        Assert.Contains("Todos los criterios cumplen.", cut.Markup);
    }

    [Theory]
    [InlineData("in_progress", "El cambio de estado se aplicó correctamente.")]
    [InlineData("pending", "El cambio de estado no se aplicó. Revisa el estado actual de la tarea.")]
    public void UncertainTaskStartIsNotResentAndIsResolvedByCheckingTheState(string savedStatus, string expected)
    {
        api.On(HttpMethod.Get, $"{BasePath}/tasks", Json(new[] { Task("pending") }), Json(new[] { Task(savedStatus) }))
            .On(HttpMethod.Get, $"{BasePath}/acceptance-criteria", Json(Array.Empty<AcceptanceCriterionDto>()))
            .On(HttpMethod.Get, $"{BasePath}/tasks/{TaskId}/acceptance-criteria", Json(new TaskAcceptanceCriteriaDto([])))
            .On(HttpMethod.Get, $"{BasePath}/tasks/{TaskId}", Json(Task("pending"), "\"t1\""))
            .On(HttpMethod.Post, $"{BasePath}/tasks/{TaskId}/start", Offline);

        var cut = RenderTasks("approved");
        cut.WaitForAssertion(() => Assert.Contains("Tarea uno", cut.Markup));

        Button(cut, "Iniciar tarea 1").Click();
        Button(cut, "Confirmar inicio").Click();

        cut.WaitForAssertion(() => Assert.Contains("No se sabe si el cambio se aplicó", cut.Markup));
        Assert.Empty(cut.FindAll("button[aria-label='Iniciar tarea 1']"));

        Button(cut, "Consultar estado actual").Click();

        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Markup));
        Assert.Single(api.Requests, request => request == $"POST {BasePath}/tasks/{TaskId}/start");
    }

    [Fact]
    public void TaskChangedBeforeStartingIsNotRetriedAndShowsItsCurrentState()
    {
        api.On(HttpMethod.Get, $"{BasePath}/tasks", Json(new[] { Task("pending") }), Json(new[] { Task("in_progress") }))
            .On(HttpMethod.Get, $"{BasePath}/acceptance-criteria", Json(Array.Empty<AcceptanceCriterionDto>()))
            .On(HttpMethod.Get, $"{BasePath}/tasks/{TaskId}/acceptance-criteria", Json(new TaskAcceptanceCriteriaDto([])))
            .On(HttpMethod.Get, $"{BasePath}/tasks/{TaskId}", Json(Task("pending"), "\"t1\""))
            .On(HttpMethod.Post, $"{BasePath}/tasks/{TaskId}/start", Status(HttpStatusCode.PreconditionFailed));

        var cut = RenderTasks("approved");
        cut.WaitForAssertion(() => Assert.Contains("Tarea uno", cut.Markup));

        Button(cut, "Iniciar tarea 1").Click();
        Button(cut, "Confirmar inicio").Click();

        cut.WaitForAssertion(() => Assert.Contains("La tarea cambió antes de aplicar el cambio. Se muestra su estado actual.", cut.Markup));
        Assert.Contains("En curso", cut.Markup);
        Assert.Single(api.Requests, request => request == $"POST {BasePath}/tasks/{TaskId}/start");
    }

    private IRenderedComponent<ImplementationTasksSection> RenderTasks(string status) =>
        Render<ImplementationTasksSection>(parameters => Section<ImplementationTasksSection, TaskOperation>(parameters, status));

    private static void Section<TComponent, TOperation>(ComponentParameterCollectionBuilder<TComponent> parameters, string status)
        where TComponent : PlanSectionBase<TOperation>
        where TOperation : struct, Enum
    {
        var specification = new SpecificationDto(Guid.NewGuid(), ProposalId, "# Spec", status, null, null, null, Now, Now);
        parameters
            .Add(section => section.ProjectId, ProjectId)
            .Add(section => section.ProposalId, ProposalId)
            .Add(section => section.Specification, specification)
            .Add(section => section.SpecificationEntityTag, "\"s1\"")
            .Add(section => section.ReloadSpecification,
                () => System.Threading.Tasks.Task.FromResult(new ApiResult<SpecificationDto>(specification, null, "\"s1\"")));
    }

    private static AcceptanceCriterionDto Criterion() => new(CriterionId, Guid.Empty, "Criterio uno", 1, Now, Now);

    private static ImplementationTaskDto Task(string status) => new(
        TaskId, Guid.Empty, "Tarea uno", null, status,
        status == "pending" ? null : Now, status == "completed" ? Now : null, 1, Now, Now);

    private static AngleSharp.Dom.IElement Button<TComponent>(IRenderedComponent<TComponent> cut, string label)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("button").First(button => (button.GetAttribute("aria-label") ?? button.TextContent.Trim()) == label);
}
