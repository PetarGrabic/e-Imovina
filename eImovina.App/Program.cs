using MudBlazor.Services;
using eImovina.App.Components;
using eImovina.App.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Typed HttpClient wrapped by ApiClient. This is also where Section 5's auth
// DelegatingHandler slots in later via .AddHttpMessageHandler<...>() on the same registration.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'ApiBaseUrl' is missing.");
    client.BaseAddress = new Uri(baseUrl);
});

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


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
