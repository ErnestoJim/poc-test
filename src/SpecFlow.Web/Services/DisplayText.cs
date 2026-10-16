using System.Globalization;

namespace SpecFlow.Web.Services;

// Spanish text shown for API values, shared by every page so labels stay consistent.
public static class DisplayText
{
    public static string DateTime(DateTimeOffset date) => date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Date(DateTimeOffset date) => date.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // "1", "1 y 2", "1, 2 y 3".
    public static string NumberList(IReadOnlyList<int> numbers) => numbers.Count == 1
        ? numbers[0].ToString(CultureInfo.InvariantCulture)
        : $"{string.Join(", ", numbers.Take(numbers.Count - 1))} y {numbers[^1]}";

    public static string ProposalStatus(string status) => status switch
    {
        "pending" => "Pendiente",
        "accepted" => "Aceptada",
        "rejected" => "Rechazada",
        _ => Unknown
    };

    public static string SpecificationStatus(string status) => status switch
    {
        "draft" => "Borrador",
        "approved" => "Aprobada",
        "implemented" => "Implementada",
        "verified" => "Verificada",
        _ => Unknown
    };

    public static string SpecificationStatusPhrase(string status) => status switch
    {
        "draft" => "en borrador",
        "approved" => "aprobada",
        "implemented" => "implementada",
        "verified" => "verificada",
        _ => UnknownPhrase
    };

    public static string TaskStatus(string status) => status switch
    {
        "pending" => "Pendiente",
        "in_progress" => "En curso",
        "completed" => "Completada",
        _ => Unknown
    };

    public static string VerificationStatus(string status) => status switch
    {
        "unverified" => "Sin verificar",
        "passed" => "Cumple",
        "failed" => "No cumple",
        _ => Unknown
    };

    public static string DecisionStatus(string status) => status switch
    {
        "draft" => "Borrador",
        "accepted" => "Aceptada",
        "rejected" => "Rechazada",
        "superseded" => "Sustituida",
        _ => Unknown
    };

    public static string DecisionStatusPhrase(string status) => status switch
    {
        "draft" => "en borrador",
        "accepted" => "aceptada",
        "rejected" => "rechazada",
        "superseded" => "sustituida",
        _ => UnknownPhrase
    };

    private const string Unknown = "Estado desconocido";
    private const string UnknownPhrase = "en un estado desconocido";
}
