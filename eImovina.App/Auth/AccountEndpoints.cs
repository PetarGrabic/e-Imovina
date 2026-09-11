using System.Net.Http.Json;
using System.Security.Claims;
using eImovina.Shared.Auth;
using eImovina.Shared.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace eImovina.App.Auth;

/// <summary>
/// POST /account/login and /account/logout - minimal API endpoints, not Razor components, because
/// SignInAsync/SignOutAsync need a real request/response HttpContext to write Set-Cookie, which an
/// Interactive Server circuit's persistent SignalR connection doesn't give a component mid-render.
/// </summary>
public static class AccountEndpoints
{
    public const string TokenClaimType = "api_token";
    public const string TokenProtectorPurpose = "eImovina.App.AuthToken";

    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/login", (Delegate)HandleLoginAsync).AllowAnonymous();
        endpoints.MapPost("/account/logout", (Delegate)HandleLogoutAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleLoginAsync(
        HttpContext http,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider)
    {
        var form = await http.Request.ReadFormAsync();
        var userName = form["username"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();
        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/'))
        {
            returnUrl = "/";
        }

        var apiBaseUrl = configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'ApiBaseUrl' is missing.");

        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(apiBaseUrl);

        LoginResponse? loginResponse = null;
        try
        {
            var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(userName, password));
            if (response.IsSuccessStatusCode)
            {
                loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            }
        }
        catch (HttpRequestException)
        {
            // Api unreachable - fall through to the same "invalid credentials" redirect below.
        }

        if (loginResponse is null)
        {
            return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var protector = dataProtectionProvider.CreateProtector(TokenProtectorPurpose);
        var protectedToken = protector.Protect(loginResponse.Token);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, loginResponse.DisplayName),
            new(TokenClaimType, protectedToken),
        };
        claims.AddRange(loginResponse.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (loginResponse.EmployeeId is { } employeeId)
        {
            claims.Add(new Claim(AuthClaimTypes.EmployeeId, employeeId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            ExpiresUtc = loginResponse.ExpiresAtUtc,
            IsPersistent = true,
        });

        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> HandleLogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }
}
