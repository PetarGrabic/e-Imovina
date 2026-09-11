using eImovina.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Consistent ProblemDetails-shaped bodies for unhandled exceptions and bare status codes
// (401/403/404) - [ApiController]'s automatic 400 ValidationProblemDetails already works
// independent of this.
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// SQLite needs the target folder to exist before it will create the database file.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

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

app.UseAuthorization();

// Lightweight connectivity check used by the App shell.
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
