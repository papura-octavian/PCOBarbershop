using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PCOBarbershop.Server.Services;

// Adauga in documentul OpenAPI schema "Bearer" (JWT), ca in Swagger UI sa apara
// butonul "Authorize" si sa poti testa rutele protejate cu tokenul primit la login.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Lipeste aici tokenul primit de la /api/auth/login (fara prefixul Bearer)."
            }
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
