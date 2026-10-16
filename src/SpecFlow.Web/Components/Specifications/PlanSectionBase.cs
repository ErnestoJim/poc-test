using Microsoft.AspNetCore.Components;
using SpecFlow.Web.Services;

namespace SpecFlow.Web.Components.Specifications;

// Coordination shared by the sections that edit a specification's plan (criteria and tasks):
// page-wide locking, re-reading the specification after a change and recovering from failures.
// Each section keeps its own rules and texts.
public abstract class PlanSectionBase<TOperation> : ComponentBase where TOperation : struct, Enum
{
    [Inject] protected SpecFlowApiClient Api { get; set; } = default!;

    [Parameter, EditorRequired] public Guid ProjectId { get; set; }
    [Parameter, EditorRequired] public Guid ProposalId { get; set; }
    [Parameter, EditorRequired] public SpecificationDto Specification { get; set; } = default!;

    // Changes whenever anything in the plan changes, so the section can re-read after outside changes.
    [Parameter] public string? SpecificationEntityTag { get; set; }

    // True while another part of the page has an edit, a request or a recovery in progress.
    [Parameter] public bool Locked { get; set; }
    [Parameter] public EventCallback<bool> OnActiveChanged { get; set; }
    [Parameter, EditorRequired] public Func<Task<ApiResult<SpecificationDto>>> ReloadSpecification { get; set; } = default!;
    [Parameter] public EventCallback<ContextLost> OnContextLost { get; set; }

    protected bool Sending { get; set; }
    protected bool Refreshing { get; set; }
    protected TOperation? Pending { get; set; }
    protected string? Notice { get; set; }
    protected ApiError? NoticeError { get; set; }
    private bool reportedActive;

    protected bool Busy => Sending || Refreshing;

    // True while the section has an edit, a confirmation, a request or local text that must lock the rest of the page.
    protected abstract bool Active { get; }

    // Unsaved text handed to the page if the specification no longer exists.
    protected abstract string? ContextText();

    // Appended to a success Notice when the specification could not be re-read afterwards.
    protected abstract string StaleSpecificationHint { get; }

    protected abstract Task LoadListAsync();

    // A 409 with the specification re-read successfully.
    protected abstract Task ClassifyConflictAsync(TOperation operation, SpecificationDto current);

    protected abstract Task HandleMissingAsync(TOperation operation);

    // Resolves a Pending operation once the specification has been re-read.
    protected abstract Task RecoverWithAsync(TOperation operation, SpecificationDto current);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var active = Active;
        if (active != reportedActive)
        {
            reportedActive = active;
            await OnActiveChanged.InvokeAsync(active);
        }
    }

    protected void ClearNotice()
    {
        Notice = null;
        NoticeError = null;
    }

    // The specification ETag covers the whole plan, so it is refreshed after every change.
    protected async Task AfterChangeAsync(string? message)
    {
        var specification = await ReloadSpecification();
        await LoadListAsync();
        Notice = message;
        if (specification.Value is null)
        {
            Notice = $"{message} {StaleSpecificationHint}".TrimStart();
            NoticeError = specification.Error;
        }
    }

    protected async Task HandleFailureAsync(TOperation operation, ApiError? error)
    {
        switch (error?.StatusCode)
        {
            case 409:
                var specification = await ReloadSpecification();
                if (specification.Value is not { } current)
                {
                    await HandleSpecificationErrorAsync(operation, specification.Error);
                    return;
                }

                await ClassifyConflictAsync(operation, current);
                break;
            case 404:
                await HandleMissingAsync(operation);
                break;
            case null or >= 500:
                Pending = operation;
                Notice = "No se sabe si el cambio se aplicó. Consulta el estado actual antes de intentarlo de nuevo.";
                NoticeError = error;
                break;
            default:
                Pending = operation;
                Notice = "No se pudo completar la operación. Consulta el estado actual antes de intentarlo de nuevo.";
                break;
        }
    }

    protected async Task HandleSpecificationErrorAsync(TOperation operation, ApiError? error)
    {
        if (error?.StatusCode == 404)
        {
            await OnContextLost.InvokeAsync(new ContextLost(error, ContextText()));
            return;
        }

        Pending = operation;
        Notice = "No se pudo comprobar el estado actual de la especificación.";
        NoticeError = error;
    }

    // Re-reads the specification after a 404 on an item; null when the failure has been reported.
    protected async Task<SpecificationDto?> ReloadForMissingAsync(string? localText)
    {
        var specification = await ReloadSpecification();
        if (specification.Value is { } current)
        {
            return current;
        }

        if (specification.Error?.StatusCode == 404)
        {
            await OnContextLost.InvokeAsync(new ContextLost(specification.Error, localText ?? ContextText()));
            return null;
        }

        Notice = "No se pudo comprobar el estado actual de la especificación.";
        NoticeError = specification.Error;
        return null;
    }

    protected async Task RecoverAsync()
    {
        if (Refreshing || Pending is not { } operation) return;
        Refreshing = true;
        NoticeError = null;
        try
        {
            var specification = await ReloadSpecification();
            if (specification.Value is not { } current)
            {
                await HandleSpecificationErrorAsync(operation, specification.Error);
                return;
            }

            await RecoverWithAsync(operation, current);
        }
        finally
        {
            Refreshing = false;
        }
    }

    protected async Task ReloadAllAsync()
    {
        await ReloadSpecification();
        await LoadListAsync();
    }
}
