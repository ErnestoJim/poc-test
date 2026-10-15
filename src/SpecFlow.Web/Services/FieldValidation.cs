namespace SpecFlow.Web.Services;

public static class FieldValidation
{
    // Blazor renders a bool `true` as aria-invalid="", which assistive technologies
    // do not treat as invalid. Always emit an explicit "true" or "false".
    public static string AriaInvalid(ApiError? error, string field) =>
        error?.HasField(field) == true ? "true" : "false";

    public static string DescribedBy(ApiError? error, string field, string inputId) =>
        error?.HasField(field) == true
            ? $"{inputId}-hint {inputId}-error"
            : $"{inputId}-hint";
}
