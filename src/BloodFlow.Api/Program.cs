using System.Text.Json.Serialization;
using BloodFlow.Api.Common;
using BloodFlow.Api.OpenApi;
using BloodFlow.Application.Common;
using BloodFlow.Infrastructure;
using BloodFlow.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as text ("Hospital", "Pending") — the same values stored in the database.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Standard error format (ProblemDetails) + the safety net for unexpected exceptions.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Who is calling? (read from the JWT; used for audit logs and "approved by").
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Seeds the five roles and the first System Admin.
await IdentitySeeder.SeedAsync(app.Services);

// ---------- HTTP pipeline (order matters) ----------

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                   // /openapi/v1.json
    app.UseSwaggerUI(options =>                         // /swagger
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BloodFlow API");
        options.DocumentTitle = "BloodFlow API";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();   // 1. Who are you? (reads and checks the JWT)
app.UseAuthorization();    // 2. Are you allowed? (checks the role)

app.MapControllers();

app.Run();
