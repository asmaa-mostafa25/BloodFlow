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

// CORS: which websites (the Frontend) may call this API from the browser.
// The list lives in configuration (Cors:AllowedOrigins), so a new Frontend URL needs no code change.
// Swagger and Postman are not browsers, so CORS never affects them.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Seeds the five roles and the first System Admin.
await IdentitySeeder.SeedAsync(app.Services);

// ---------- HTTP pipeline (order matters) ----------

app.UseExceptionHandler();

// Swagger: always on the developer's machine; on the server only when Swagger:Enabled is true
// (turn it off after the graduation demo without changing code).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.MapOpenApi();                                   // /openapi/v1.json
    app.UseSwaggerUI(options =>                         // /swagger
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BloodFlow API");
        options.DocumentTitle = "BloodFlow API";
    });
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy); // before authentication, so browser preflight requests get the CORS headers
app.UseAuthentication();   // 1. Who are you? (reads and checks the JWT)
app.UseAuthorization();    // 2. Are you allowed? (checks the role)

app.MapControllers();

app.Run();
