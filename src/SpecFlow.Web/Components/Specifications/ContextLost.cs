using SpecFlow.Web.Services;

namespace SpecFlow.Web.Components.Specifications;

// Raised by a plan section when the specification no longer exists, with any text the person had not saved.
public sealed record ContextLost(ApiError Error, string? LocalText);
