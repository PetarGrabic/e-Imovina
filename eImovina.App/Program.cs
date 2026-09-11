using MudBlazor.Services;
using eImovina.App.Auth;
using eImovina.App.Components;
using eImovina.App.Services;
using eImovina.Shared.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        // AccessDeniedPath (403/Forbid, authenticated-but-wrong-role) is a genuinely separate
        // route from LoginPath (401/Challenge, anonymous), so ASP.NET Core's default
        // "?ReturnUrl=..." on both redirects is no longer ambiguous between the two cases.
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });
// No endpoint-level FallbackPolicy here - it would also gate static assets and Blazor's own
// framework endpoints (_blazor/initializers etc.), requiring per-endpoint AllowAnonymous
// whack-a-mole. Instead, "every page requires auth by default" is enforced at the component
// level via a blanket [Authorize] in Components/_Imports.razor, which AuthorizeRouteView reads;
// Login.razor opts out via its own [AllowAnonymous].
// Mirrors the policy names registered on eImovina.Api's Program.cs - the cookie carries the same
// role claims the API's JWT does, so [Authorize(Policy = "...")] / <AuthorizeView Policy="...">
// here are the same cosmetic (real enforcement stays on the API) checks under the same names.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy("InventoryManagement", policy => policy.RequireRole(RoleNames.Admin, RoleNames.InventoryManager));
    options.AddPolicy("LocationWork", policy => policy.RequireRole(RoleNames.Admin, RoleNames.InventoryManager, RoleNames.LocationResponsible));
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<ITokenAccessor, TokenAccessor>();
builder.Services.AddTransient<AuthHeaderHandler>();

// Typed HttpClient wrapped by ApiClient. AuthHeaderHandler attaches the signed-in user's bearer
// token to every request made through this client.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'ApiBaseUrl' is missing.");
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<AuthHeaderHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();

app.Run();
