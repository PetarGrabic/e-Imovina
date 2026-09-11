using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace eImovina.Api.Auth;

/// <summary>
/// Adds an HTTP Bearer security scheme + a global security requirement to the AddOpenApi()-generated
/// document, so Swagger UI's "Authorize" button works without switching the doc generator to
/// Swashbuckle SwaggerGen. Targets Microsoft.OpenApi 2.x's reference-typed API (OpenApiSecuritySchemeReference,
/// not the old OpenApiReference indirection).
/// </summary>
public class OpenApiBearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document, null)] = new List<string>(),
        };
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(requirement);

        return Task.CompletedTask;
    }
}
