# PART 2 — Technologies and Tools

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

Every technology below is **actually used** in the repository (checked in the `.csproj` files and the code). At the end there is a list of well-known technologies we **did not** use, and why.

For each one: what it is · why we chose it · what problem it solves · where it is in BloodFlow · how it talks to the others · a real example.

---

## 2.1 C#

**What:** A typed, object-oriented language from Microsoft. Our code uses modern C# features (records, pattern matching, `switch` expressions, nullable reference types, collection expressions `[]`, `required` members, file-scoped `using`).
**Why:** Types catch many mistakes at compile time; excellent tooling; it is the language of .NET.
**Where:** every `.cs` file.
**Example — a `switch` expression with tuple patterns** (`Application/Requests/RequestRules.cs`):
```csharp
public static bool CanTransition(RequestStatus from, RequestStatus to) => (from, to) switch
{
    (RequestStatus.Pending, RequestStatus.Approved) => true,
    (RequestStatus.Fulfilled, RequestStatus.Received) => true,
    // ...
    _ => false
};
```
**Example — a record** (`Application/Auth/AuthResponse.cs`): `public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);` — one line gives a constructor, read-only properties and value equality.

> 🇪🇬 C# هي اللغة اللي كاتبين بيها كل حاجة. الـ record طريقة مختصرة نعمل بيها كلاس للبيانات بس، والـ switch expression طريقة قصيرة نكتب بيها if كتير.

---

## 2.2 .NET 10 and ASP.NET Core Web API

**What:** .NET is the runtime and libraries; ASP.NET Core is the web framework; "Web API" means it serves JSON over HTTP using **controllers**.
**Why:** Fast, cross-platform, built-in dependency injection, configuration, logging, authentication and OpenAPI.
**Problem solved:** receiving HTTP requests, routing them to code, returning JSON.
**Where:** `src/BloodFlow.Api` (`Microsoft.NET.Sdk.Web`), `Program.cs`, `Controllers/`.
**Talks to:** EF Core through services; Identity/JWT through middleware.
**Example** (`Api/Controllers/LookupsController.cs`):
```csharp
[ApiController]
[Route("api/lookups")]
[AllowAnonymous]
public class LookupsController : ControllerBase
{
    [HttpGet("blood-groups")]
    public async Task<ActionResult<IReadOnlyList<BloodGroupDto>>> BloodGroups(CancellationToken cancellationToken) =>
        Ok(await _lookups.GetBloodGroupsAsync(cancellationToken));
}
```
The SDK is pinned in `global.json` (`10.0.100`, `rollForward: latestFeature`).

> 🇪🇬 ASP.NET Core هو الإطار اللي بيستقبل الطلبات على الإنترنت ويوديها للكود الصح ويرجّع JSON.

---

## 2.3 Entity Framework Core 10 (+ SqlServer provider, Tools, Design)

**What:** An **ORM** (Object-Relational Mapper): you work with C# classes and LINQ; EF Core generates SQL.
**Why:** No hand-written SQL for CRUD; migrations keep the schema in Git; change tracking saves many changes in one transaction.
**Where:** `Infrastructure/Persistence/` (DbContext, configurations, migrations). Packages: `Microsoft.EntityFrameworkCore.SqlServer`, `.Tools` (Infrastructure), `.Design` (Api, needed by `dotnet ef`).
**Talks to:** SQL Server via the SqlServer provider (Microsoft.Data.SqlClient); services use it through `AppDbContext`.
**Example — LINQ that becomes one SQL query** (`Infrastructure/Common/SettingsReader.cs`):
```csharp
var value = await _db.SystemSettings.AsNoTracking()
    .Where(s => s.SettingKey == key)
    .Select(s => s.SettingValue)
    .FirstOrDefaultAsync(cancellationToken);
```
→ `SELECT TOP(1) [SettingValue] FROM [SystemSettings] WHERE [SettingKey] = @key`.

> 🇪🇬 EF Core بيخليني أكتب C# بدل SQL. أقوله "هات الإعداد اللي اسمه كذا" وهو يكتب الـ SQL ويجيب النتيجة.

---

## 2.4 Microsoft SQL Server

**What:** A relational database server.
**Why:** First-class EF Core support, strong constraints (CHECK, filtered unique indexes, sequences, `rowversion`), Arabic collations, free Express edition, offered by our host.
**Where:** connection string `ConnectionStrings:DefaultConnection`; `UseSqlServer(...)` in `DependencyInjection.cs`.
**Example — SQL Server features we rely on:** sequence `BloodRequestNumbers` for request numbers (`AppDbContext.OnModelCreating`), `rowversion` for concurrency, `Arabic_100_CI_AI` collation.

> 🇪🇬 SQL Server هو المكان اللي البيانات متخزنة فيه فعلاً، جداول وصفوف.

---

## 2.5 ASP.NET Core Identity

**What:** Ready-made user management: users, password hashing, roles, lockout.
**Why:** Writing password storage yourself is dangerous; Identity does it correctly.
**Where:** `ApplicationUser : IdentityUser<int>` (`Infrastructure/Identity`), `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`, `AddIdentityCore<ApplicationUser>()...AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<AppDbContext>()` in `DependencyInjection.cs`.
**Talks to:** EF Core (its tables `AspNetUsers`, `AspNetRoles`, …), `AuthService` via `UserManager<ApplicationUser>`.
**Example** (`AuthService.LoginAsync`): `await _userManager.CheckPasswordAsync(user, request.Password)`.
We use `AddIdentityCore` (not `AddIdentity`) because we don't need cookie sign-in pages — only users, roles and password checks; JWT handles sign-in.

> 🇪🇬 Identity هو السيستم الجاهز اللي بيخزن المستخدمين والباسوردات متشفرة والأدوار، وبيقفل الحساب لو حد غلط في الباسورد 5 مرات.

---

## 2.6 JWT Authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)

**What:** JSON Web Token: a signed string with the user's claims. The client sends it in `Authorization: Bearer <token>`.
**Why:** Stateless (no server sessions), works for web and mobile frontends.
**Where:** creation in `Infrastructure/Authentication/JwtTokenGenerator.cs`; validation configured in `DependencyInjection.cs` (`AddAuthentication(...).AddJwtBearer(...)`); `app.UseAuthentication()` in `Program.cs`.
**Example — validation parameters:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = jwtSettings.Issuer, ValidAudience = jwtSettings.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
    RoleClaimType = AppClaimTypes.Role, NameClaimType = AppClaimTypes.Name,
    ClockSkew = TimeSpan.FromSeconds(30)
};
```
Full explanation: Part 5.

> 🇪🇬 الـ JWT كارنيه رقمي موقّع. بعد الـ Login السيرفر بيدّيه للمستخدم، والمستخدم يبعته مع كل طلب.

---

## 2.7 Role-Based Authorization

**What:** Allowing an endpoint only for certain roles: `[Authorize(Roles = "...")]`.
**Where:** every controller. Role names are constants in `Application/Common/Roles.cs`:
```csharp
public const string SystemAdmin = "SystemAdmin";
public const string HospitalStaff = "HospitalStaff";
public const string BloodBankStaff = "BloodBankStaff";
public const string Staff = HospitalStaff + "," + BloodBankStaff;   // "either role"
```
Example: `[Authorize(Roles = Roles.SystemAdmin)]` on `Admin/StaffController`. Roles are not enough on their own: services also check **ownership** (organization from the token). Part 5 explains both.

---

## 2.8 Dependency Injection (built into ASP.NET Core)

**What:** Classes receive their dependencies in the constructor; the DI container creates and passes them.
**Where:** `Infrastructure/DependencyInjection.cs` (`AddInfrastructure`), `Infrastructure/Ai/AiServiceCollectionExtensions.cs`, and `Program.cs`.
**Example:** `services.AddScoped<IRequestService, RequestService>();` — any controller asking for `IRequestService` gets a `RequestService`, created once per HTTP request. Part 3 §3.8 explains lifetimes.

> 🇪🇬 بدل ما كل كلاس يعمل new للحاجات اللي محتاجها، الإطار بيعملها ويحطهاله في الـ Constructor.

---

## 2.9 Clean Architecture
A way to organize the solution in layers so business rules don't depend on technical details. Our 4 projects: Domain, Application, Infrastructure, Api. Part 3 is dedicated to it.

---

## 2.10 REST APIs
Resources have URLs (`/api/requests/15`), HTTP methods express the action (GET read, POST create/act, PUT replace), status codes express the result. We use **action sub-resources** for state changes: `POST /api/requests/{id}/approve`, because "approve" is a business action, not a field update. Part 7.

---

## 2.11 Swagger / OpenAPI (`Microsoft.AspNetCore.OpenApi` + `Swashbuckle.AspNetCore.SwaggerUI`)

**What:** OpenAPI is a JSON description of the API; Swagger UI is a web page generated from it where you can call endpoints.
**Why:** Living documentation for the frontend team; manual testing.
**Where:** `builder.Services.AddOpenApi(...)` generates `/openapi/v1.json`; `app.UseSwaggerUI(...)` serves `/swagger`. `Api/OpenApi/BearerSecuritySchemeTransformer.cs` adds the **Authorize** button. Enabled in Development or when `Swagger:Enabled` is true.
Note: we use Microsoft's own OpenAPI generator and **only the UI part** of Swashbuckle (not Swashbuckle's generator).

> 🇪🇬 Swagger صفحة فيها كل الـ endpoints، تقدري تجربي منها وتشوفي شكل الطلب والرد.

---

## 2.12 Data Transfer Objects (DTOs)
**What:** Records describing what the API receives and returns, separate from entities.
**Where:** `Application/<Feature>/*Dtos.cs`, `LoginRequest.cs`, `RegisterStaffRequest.cs`.
**Example** (`Application/Requests/RequestDtos.cs`):
```csharp
public sealed record CreateBloodRequestRequest
{
    [Required, EnumDataType(typeof(RequestType))] public RequestType? RequestType { get; init; }
    [Range(1, 8)] public byte BloodGroupId { get; init; }
    [Range(1, 100)] public int UnitsRequested { get; init; }
    [Required] public DateTime? NeededBy { get; init; }
    [Required, StringLength(200, MinimumLength = 2)] public string PatientFullName { get; init; } = string.Empty;
    public Guid? ClientRequestId { get; init; }
}
```
Why `RequestType?` nullable + `[Required]`? If the client omits it, a non-nullable enum would silently default to the first value (`Normal`). Nullable + Required forces the client to send it.

---

## 2.13 LINQ
**What:** Language-Integrated Query: `Where`, `Select`, `OrderBy`, `Any`, `Sum`, `GroupBy` in C#.
**Where:** everywhere in services. With EF Core it becomes SQL; on lists it runs in memory.
**Example — in SQL** (`RequestService.AvailableByBloodGroupAsync`):
```csharp
return await _db.BloodBatches.AsNoTracking()
    .Where(b => b.OrganizationId == orgId && b.Status == BatchStatus.Active && !(b.ExpiryDate < today))
    .GroupBy(b => b.BloodGroupId)
    .Select(g => new { g.Key, Available = g.Sum(b => b.OnHandQuantity - b.ReservedQuantity) })
    .ToDictionaryAsync(x => x.Key, x => x.Available, cancellationToken);
```
**Example — in memory** (`RequestRules.AllocateFefo`): `candidates.Where(c => c.Available > 0).OrderBy(c => c.ExpiryDate).ThenBy(c => c.BatchId)`.

---

## 2.14 Middleware
**What:** Components every HTTP request passes through, in order.
**Where:** `Program.cs`: `UseExceptionHandler` → Swagger → `UseHttpsRedirection` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`. Part 3 §3.9.

---

## 2.15 Configuration and the Options Pattern
**What:** ASP.NET Core merges settings from `appsettings.json`, `appsettings.{Environment}.json`, User Secrets (Development), environment variables. The **Options pattern** binds a section to a class.
**Where:**
- `services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));` then `JwtTokenGenerator` receives `IOptions<JwtSettings>`.
- Direct reads: `configuration.GetConnectionString("DefaultConnection")`, `configuration["AdminSeed:Email"]`, `app.Configuration.GetValue<bool>("Swagger:Enabled")`, `GetSection("Cors:AllowedOrigins").Get<string[]>()`.
- `BloodFlowAiOptions` is bound with `GetSection(...).Get<BloodFlowAiOptions>()`.
- Runtime business settings live in the **database** table `SystemSettings` (read by `SettingsReader`), not in config files.

> 🇪🇬 الإعدادات بتتجمع من أكتر من مكان. الـ Options Pattern بيحوّل جزء من الإعدادات لكلاس C# نستخدمه بسهولة.

---

## 2.16 Git and GitHub, GitHub Actions
- **Git:** version control. **GitHub:** hosting, pull requests.
- Workflow used: branch `feature/...` → push → pull request → CI green → merge into `master`.
- **GitHub Actions** (`.github/workflows/ci.yml`): restore, build (Release), test, list vulnerable packages on every push to `master`/`feature/**` and every PR.
- `.gitignore` excludes `bin/`, `obj/`, `appsettings.Production.json`, `publish/`.

---

## 2.17 Visual Studio (and the .NET 10 note)
You asked about Visual Studio 2022. **Important:** Microsoft officially supports building `net10.0` projects only in **Visual Studio 2026 (18.x)**. VS 2022 (17.14) can load the .NET 10 SDK but targeting .NET 10 isn't supported there. Options: use Visual Studio 2026, or build/run with the `dotnet` CLI and use any editor. The solution file is the new XML format `BloodFlow.slnx`.

---

## 2.18 Postman and the `.http` file
Postman is **not** part of the repository; it's an optional external tool to send HTTP requests. Inside the repo we use **`src/BloodFlow.Api/BloodFlow.Api.http`** (Visual Studio's built-in HTTP client) with variables `@host` and `@token`. Both do the same job as Swagger's "Try it out". Part 7 §7.13 shows Postman usage.

---

## 2.19 xUnit (+ Test SDK, runner, coverlet)
**What:** Unit-testing framework. `[Fact]` = one test, `[Theory]` + `[InlineData]` = the same test with several inputs.
**Where:** `tests/BloodFlow.Tests` — 83 tests for `RequestRules`, `InventoryRules`, `SettingValueValidator`, `Result`, `PageQuery`.
**Example** (`Requests/RequestRulesTests.cs`):
```csharp
[Fact]
public void Fefo_NotEnoughStock_ReturnsNull_NoPartialApproval()
{
    var candidates = new[] { new BatchCandidate(1, Today.AddDays(1), 2), new BatchCandidate(2, Today.AddDays(2), 1) };
    Assert.Null(RequestRules.AllocateFefo(candidates, 4));
}
```
`coverlet.collector` can measure code coverage (`dotnet test --collect:"XPlat Code Coverage"`); it is referenced but no coverage report is configured in CI.

---

## 2.20 Other technologies actually in the code

| Technology | Where | Why |
|---|---|---|
| `System.Text.Json` + `JsonStringEnumConverter` | `Program.cs`, `AuditLogger` | Enums as text in JSON and in audit values |
| ProblemDetails (`AddProblemDetails`, `IExceptionHandler`) | `Program.cs`, `GlobalExceptionHandler` | Standard error format (RFC 9457) |
| `IHttpContextAccessor` | `CurrentUser` | Read the token's claims outside controllers |
| `IHttpClientFactory` (named clients) | `AiServiceCollectionExtensions` | Correct `HttpClient` reuse for the AI service |
| `BackgroundService` + `PeriodicTimer` | `MaintenanceBackgroundService` | Hourly maintenance |
| `TimeZoneInfo` | `SystemClock` | "Today" in `Africa/Cairo` |
| Data Annotations (`[Required]`, `[Range]`, …) | DTOs | Input validation |
| `System.Security.Cryptography.Pkcs` | Infrastructure `.csproj` | Added by you in the first JWT commit (`b671c54`). No code calls it directly; it is usually added to pin a secure version of a package that another package depends on. Check whether it is still needed: remove it on a branch and run `dotnet build` + `dotnet list package --vulnerable --include-transitive`. |
| MonsterASP.NET (IIS + Web Deploy) | deployment | Free ASP.NET + SQL Server hosting |
| BloodFlow AI (Python FastAPI) | external, called by `BloodFlowAiClient` | Forecasts; built by the AI team, not in this repo |

---

## 2.21 Technologies we did NOT use (and why)

These are common in .NET projects; supervisors and interviewers often ask "why not X?".

| Not used | What it is | Why not (in BloodFlow) |
|---|---|---|
| **MediatR / CQRS** | Commands/queries sent through a mediator | Adds many files and indirection; our services are already small and testable. Could be added if reads/writes need to scale separately. |
| **Repository pattern / Unit of Work classes** | Wrappers around data access | `DbContext` already is a unit of work and `DbSet` a repository; an extra layer would hide EF features like projections. |
| **AutoMapper** | Automatic object mapping | Explicit `new Dto(...)` / `Select(...)` is clearer, compile-time checked and lets EF generate efficient SQL. |
| **FluentValidation** | Validation library | Data Annotations + pure rule classes cover our needs. |
| **Serilog / NLog** | Structured logging | Built-in `ILogger` is enough so far; recommended later. |
| **Hangfire / Quartz** | Job schedulers | One hourly job → a `BackgroundService` is simpler. Needed if we scale to several servers. |
| **Docker** | Containers | Hosting is IIS on MonsterASP; Docker is planned. |
| **Testcontainers / integration tests** | Real DB in tests | Planned next. |
| **Cookie authentication / `AddIdentity`** | Server-side sessions | The frontend is a separate app; JWT is stateless and simpler for APIs. |
| **Refresh tokens** | Long sessions | Not needed for v1 (60-minute tokens). |
| **Microservices** | Many small services | One team, one backend; Clean Architecture keeps a split possible. The AI service *is* separate because it's Python. |
| **Minimal APIs** | `app.MapGet(...)` style endpoints | Controllers group many related endpoints and attributes nicely for a project this size. |
| **Postman collections in the repo** | Shared request collections | We keep `BloodFlow.Api.http` instead. |
