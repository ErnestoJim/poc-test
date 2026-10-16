namespace SpecFlow.Web.Services;

// Route identifiers are validated before calling the API, with the same messages on every page.
public static class RouteIds
{
    public static ApiError InvalidProject { get; } = ApiError.Local(400, "El identificador del proyecto no es válido.");

    public static ApiError InvalidRoute { get; } = ApiError.Local(400, "El identificador de la ruta no es válido.");

    public static bool TryParse(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
}
