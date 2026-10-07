using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace SpecFlow.Api.Endpoints;

internal static class HttpEntityTags
{
    public static string CreateResource(string resourceType, Guid id, int version) =>
        CreateHash($"{resourceType}\n{id:N}\n{version.ToString(CultureInfo.InvariantCulture)}");

    public static string CreateCollection(
        string resourceType,
        Guid ownerId,
        int collectionVersion,
        IEnumerable<(Guid Id, int Version)> resources)
    {
        var value = new StringBuilder()
            .Append(resourceType)
            .Append('\n')
            .Append(ownerId.ToString("N", CultureInfo.InvariantCulture))
            .Append('\n')
            .Append(collectionVersion.ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        foreach (var resource in resources)
        {
            value.Append(resource.Id.ToString("N", CultureInfo.InvariantCulture))
                .Append(':')
                .Append(resource.Version.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return CreateHash(value.ToString());
    }

    public static IResult? ValidateIfMatch(HttpContext httpContext, string currentEntityTag)
    {
        var values = httpContext.Request.Headers.IfMatch;
        if (values.Count == 0)
        {
            return EndpointProblems.PreconditionRequired();
        }

        var value = values.ToString();
        if (!EntityTagHeaderValue.TryParse(value, out var entityTag) ||
            entityTag.IsWeak ||
            string.Equals(entityTag.Tag, "*", StringComparison.Ordinal))
        {
            return EndpointProblems.InvalidIfMatchHeader();
        }

        return string.Equals(entityTag.ToString(), currentEntityTag, StringComparison.Ordinal)
            ? null
            : EndpointProblems.PreconditionFailed();
    }

    public static IResult WithEntityTag(
        HttpContext httpContext,
        string entityTag,
        IResult result)
    {
        httpContext.Response.Headers.ETag = entityTag;
        return result;
    }

    private static string CreateHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"\"{Convert.ToHexStringLower(hash)}\"";
    }
}
