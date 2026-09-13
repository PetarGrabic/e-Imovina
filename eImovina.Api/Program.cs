using System.Security.Claims;
using System.Text;
using eImovina.Api.Auth;
using eImovina.Api.Data;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<OpenApiBearerSecuritySchemeTransformer>());

// Consistent ProblemDetails-shaped bodies for unhandled exceptions and bare status codes
// (401/403/404) - [ApiController]'s automatic 400 ValidationProblemDetails already works
// independent of this.
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// SQLite needs the target folder to exist before it will create the database file.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // A JWT's baked-in role/employee claims would otherwise be trusted for the token's whole
        // lifetime with no DB re-check, meaning Section 15's deactivate/role-change/employee-link
        // actions would only take effect on the user's NEXT login. This re-fetches the caller's
        // AppUser on every authenticated request instead (Section 15, user-requested fix) - a
        // small extra DB round-trip per request, an acceptable tradeoff at this app's scale.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Neispravan token.");
                    return;
                }

                var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
                if (user is null || !user.IsActive)
                {
                    context.Fail("Korisnički račun je deaktiviran.");
                    return;
                }

                var roles = await (
                    from ur in db.AppUserRoles
                    join r in db.AppRoles on ur.AppRoleId equals r.Id
                    where ur.AppUserId == userId
                    select r.Name).ToListAsync();

                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                foreach (var roleClaim in identity.FindAll(ClaimTypes.Role).ToList())
                {
                    identity.RemoveClaim(roleClaim);
                }
                foreach (var role in roles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }

                var employeeClaim = identity.FindFirst(AuthClaimTypes.EmployeeId);
                if (employeeClaim is not null)
                {
                    identity.RemoveClaim(employeeClaim);
                }
                if (user.EmployeeId is int employeeId)
                {
                    identity.AddClaim(new Claim(AuthClaimTypes.EmployeeId, employeeId.ToString()));
                }
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy("InventoryManagement", policy => policy.RequireRole(RoleNames.Admin, RoleNames.InventoryManager));
    options.AddPolicy("LocationWork", policy => policy.RequireRole(RoleNames.Admin, RoleNames.InventoryManager, RoleNames.LocationResponsible));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DatabaseInitializer.MigrateAndSeedAsync(app.Services);

    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "eImovina API v1"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Lightweight connectivity check used by the App shell.
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
