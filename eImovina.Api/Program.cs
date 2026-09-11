var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "eImovina API v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Lightweight connectivity check used by the App shell.
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
