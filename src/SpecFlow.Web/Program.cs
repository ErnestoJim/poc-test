using SpecFlow.Web.Components;
using SpecFlow.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
var apiUrl = builder.Configuration["SpecFlowApi:BaseUrl"]
    ?? throw new InvalidOperationException("SpecFlowApi:BaseUrl is required.");
builder.Services.AddHttpClient<SpecFlowApiClient>(client => client.BaseAddress = new Uri(apiUrl));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
