using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BloodFlow.Api.OpenApi
{
    /// <summary>
    /// Adds the "Authorize" button to Swagger UI: you paste a JWT once and Swagger
    /// sends it as "Authorization: Bearer ..." with every request.
    /// </summary>
    internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token from POST /api/auth/login (without the word 'Bearer')."
            };

            var requirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
            };

            foreach (var pathItem in document.Paths.Values)
            {
                if (pathItem.Operations is null) continue;
                foreach (var operation in pathItem.Operations.Values)
                {
                    operation.Security ??= [];
                    operation.Security.Add(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
