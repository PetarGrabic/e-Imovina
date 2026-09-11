using MudBlazor.Services;
using eImovina.App.Auth;
using eImovina.App.Components;
using eImovina.App.Services;
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
        // redirect from LoginPath (401/Challenge, anonymous) - both append "?ReturnUrl=..." by
        // default via BuildRedirectUri, so pointing them at the same literal path makes the two
        // cases indistinguishable to the client. Reuse "/login" (no separate route) but mark the
        // 403 case explicitly so Login.razor can render an access-denied message instead of the
        // login form.
        options.AccessDeniedPath = "/login";
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/login?forbidden=1");
            return Task.CompletedTask;
        };
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });
// No endpoint-level FallbackPolicy here - it would also gate static assets and Blazor's own
// framework endpoints (_blazor/initializers etc.), requiring per-endpoint AllowAnonymous
// whack-a-mole. Instead, "every page requires auth by default" is enforced at the component
// level via a blanket [Authorize] in Components/_Imports.razor, which AuthorizeRouteView reads;
// Login.razor opts out via its own [AllowAnonymous].
builder.Services.AddAuthorization();
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
