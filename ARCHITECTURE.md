# BloodFlow — Architecture

This document explains how the backend is organised, what each project is responsible for, and how a request travels through the code. Everything here matches the code on `master`.

---

## 1. The big picture

BloodFlow is an **ASP.NET Core Web API (.NET 10)** with a **SQL Server** database accessed through **Entity Framework Core 10**. It follows **Clean Architecture**: business rules sit in the middle and do not depend on databases, web frameworks or other technical details.

```
          ┌──────────────────────────────────────────────┐
          │  BloodFlow.Api            (the door)          │  Controllers, JWT, Swagger, CORS, errors
          │  ┌────────────────────────────────────────┐  │
          │  │  BloodFlow.Infrastructure  (the tools) │  │  EF Core, SQL Server, Identity, services, jobs, AI client
          │  │  ┌──────────────────────────────────┐  │  │
          │  │  │  BloodFlow.Application (rules)   │  │  │  Interfaces, DTOs, Result, pure business rules
          │  │  │  ┌────────────────────────────┐  │  │  │
          │  │  │  │  BloodFlow.Domain (nouns)  │  │  │  │  Entities and enums, no dependencies
          │  │  │  └────────────────────────────┘  │  │  │
          │  │  └──────────────────────────────────┘  │  │
          │  └────────────────────────────────────────┘  │
          └──────────────────────────────────────────────┘
               Dependencies always point INWARDS.
```

**Project references (from the `.csproj` files):**

| Project | References | NuGet packages |
|---|---|---|
| `BloodFlow.Domain` | nothing | none |
| `BloodFlow.Application` | Domain | none (only the framework) |
| `BloodFlow.Infrastructure` | Application | EF Core SqlServer 10.0.14, EF Core Tools, Identity EF Core, JwtBearer, System.Security.Cryptography.Pkcs |
| `BloodFlow.Api` | Application, Infrastructure | Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore.SwaggerUI, EF Core Design |
| `BloodFlow.Tests` | Application, Domain | xUnit 2.9.3, Microsoft.NET.Test.Sdk, coverlet |

The SDK version is pinned in `global.json` (`10.0.100`, roll forward to the latest feature band).

---

## 2. Folder structure

```
BloodFlow/
├── .github/workflows/ci.yml          CI: restore, build, test, vulnerable-package check
├── BloodFlow.slnx                     Solution file (new XML format)
├── global.json                        .NET SDK pin
├── docs/reports/                      Arabic PDF reports (Slices 1–3) and defense guide
├── src/
│   ├── BloodFlow.Domain/
│   │   ├── Entities/                  13 entity classes (Organization, BloodBatch, BloodRequest, ...)
│   │   └── Enums/                     Status and type enums (stored as text in the database)
│   ├── BloodFlow.Application/
│   │   ├── Common/                    Result, Error, PagedResult, Roles, SettingKeys, AuditActions, IClock, ICurrentUser
│   │   ├── Auth/                      Login/register DTOs + IAuthService
│   │   ├── Staff/ Organizations/ Settings/ Audit/ Lookups/    Admin features (DTOs + interfaces)
│   │   ├── Inventory/                 InventoryRules (pure), DTOs, IInventoryService
│   │   ├── Requests/                  RequestRules (pure), DTOs, IRequestService
│   │   └── Ai/                        AI DTOs + IBloodFlowAiClient
│   ├── BloodFlow.Infrastructure/
│   │   ├── DependencyInjection.cs     AddInfrastructure(): registers everything below
│   │   ├── Persistence/               AppDbContext, Configurations/, Migrations/, Seed/
│   │   ├── Identity/                  ApplicationUser, IdentitySeeder
│   │   ├── Authentication/            AuthService, JwtTokenGenerator, JwtSettings
│   │   ├── Services/                  Staff, Organization, Lookup, Settings, AuditLog services
│   │   ├── Inventory/                 InventoryService, BatchExpiryService, summaries
│   │   ├── Requests/                  RequestService, RequestAdminService, ReservationLedger, ...
│   │   ├── Jobs/                      MaintenanceBackgroundService (hourly)
│   │   ├── Ai/                        HTTP client for the Python AI service
│   │   └── Common/                    AuditLogger, SettingsReader, SystemClock, RowVersionConverter
│   └── BloodFlow.Api/
│       ├── Program.cs                 Startup: services + HTTP pipeline
│       ├── Controllers/               Public + staff controllers
│       ├── Controllers/Admin/         SystemAdmin-only controllers
│       ├── Common/                    CurrentUser, ResultExtensions, GlobalExceptionHandler
│       ├── OpenApi/                   Adds the "Bearer" button to Swagger
│       ├── appsettings*.json          Non-secret configuration (+ production template)
│       └── BloodFlow.Api.http         Ready-made requests for Visual Studio
└── tests/BloodFlow.Tests/             83 unit tests for the pure rules
```

---

## 3. What each project does

### 3.1 `BloodFlow.Domain` — the nouns
Plain C# classes that describe the data: `Organization`, `StaffProfile`, `BloodBatch`, `InventoryTransaction`, `BloodRequest`, `Reservation`, `ReservationLine`, `EmergencyOverride`, `AuditLog`, `SystemSetting`, `Governorate`, `BloodGroup`, `BloodRequestStatusHistory`. No EF attributes, no references to other projects. The database shape is configured elsewhere (Infrastructure).

### 3.2 `BloodFlow.Application` — the rules and the contracts
- **Interfaces** (`IAuthService`, `IInventoryService`, `IRequestService`, ...) say *what* the system can do.
- **DTOs** (records) describe what goes in and out of the API. Validation attributes (`[Required]`, `[Range]`, `[StringLength]`) live on the request DTOs.
- **Pure rule classes** (no database): `InventoryRules`, `RequestRules`, `SettingValueValidator`. They are the most tested code.
- **`Result` / `Result<T>` / `Error`**: every service method returns a result instead of throwing for expected problems.
- **Constants**: `Roles`, `SettingKeys`, `AuditActions`, `AppClaimTypes`.
- **Abstractions**: `IClock` (what time/date is it), `ICurrentUser` (who is calling).

### 3.3 `BloodFlow.Infrastructure` — the tools
- **`AppDbContext`** (inherits `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`), all entity configurations, migrations and seed data.
- **Service implementations** of the Application interfaces.
- **Identity + JWT**: user store, password hashing, lockout, token creation and validation.
- **Background job**: `MaintenanceBackgroundService` (every hour).
- **AI client**: named `HttpClient`s (via `IHttpClientFactory`) that talk to the separate FastAPI AI service.

### 3.4 `BloodFlow.Api` — the door
- **Controllers** receive HTTP requests, call one service method and convert the `Result` into an HTTP response.
- **`Program.cs`** wires everything together and defines the middleware order.
- **`CurrentUser`** reads the JWT claims (`sub`, `role`, `org_id`) and the caller's IP address.
- **`GlobalExceptionHandler`** turns unexpected exceptions into ProblemDetails.

---

## 4. Startup and the HTTP pipeline (`Program.cs`)

**Services registered** (in this order):
1. Controllers + `JsonStringEnumConverter` (enums travel as text, e.g. `"Pending"`).
2. `AddProblemDetails()` + `GlobalExceptionHandler`.
3. `HttpContextAccessor` + `ICurrentUser → CurrentUser`.
4. OpenAPI document with the Bearer security scheme.
5. `AddInfrastructure(configuration)` — database, Identity, all services, the hourly job, the AI client, JWT authentication.
6. CORS policy `"Frontend"` with origins from `Cors:AllowedOrigins`.

**On startup**, before accepting requests: `IdentitySeeder.SeedAsync` creates the 5 roles, creates/repairs the first System Admin from `AdminSeed:Email`/`AdminSeed:Password`, and inserts any missing `SystemSettings` rows. If `AdminSeed` is missing, startup fails on purpose.

**Middleware order** (order matters):
```
UseExceptionHandler      → catches anything thrown later
MapOpenApi + SwaggerUI   → only in Development, or when Swagger:Enabled = true
UseHttpsRedirection
UseCors("Frontend")      → before authentication so browser pre-flight requests get CORS headers
UseAuthentication        → reads and validates the JWT
UseAuthorization         → checks [Authorize(Roles = ...)]
MapControllers
```

---

## 5. Dependency Injection

All registrations live in `Infrastructure/DependencyInjection.cs` (plus `Ai/AiServiceCollectionExtensions.cs`) and a few in `Program.cs`.

| Lifetime | Services | Why |
|---|---|---|
| **Scoped** (one per HTTP request) | `AppDbContext`, `AuditLogger`, all `I...Service` implementations, `SettingsReader`, `RequestReads`, `InventorySummaryBuilder`, `JwtTokenGenerator`, `ICurrentUser` | They use the DbContext, which must not be shared between requests. |
| **Singleton** (one for the whole app) | `IClock → SystemClock` | It holds no per-request state. |
| **Hosted service** | `MaintenanceBackgroundService` | Runs in the background; it creates its **own scope** each hour to get a fresh DbContext. |
| **HttpClientFactory** | two named clients for the AI service (normal and retrain timeouts) | Reuses connections correctly. |

Controllers depend on **interfaces** from Application (e.g. `IRequestService`), never on the concrete classes. This is Dependency Inversion: the Api project does not know how requests are stored.

---

## 6. Configuration

| Source | Used for | Committed to Git? |
|---|---|---|
| `appsettings.json` | Non-secret defaults: JWT issuer/audience/expiry, `App:TimeZone`, `BloodFlowAi:BaseUrl`/timeouts, `Swagger:Enabled=false`, `Cors:AllowedOrigins` | Yes |
| `appsettings.Development.json` | Development logging | Yes |
| **User Secrets** (developer machine) | `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `AdminSeed:Email`, `AdminSeed:Password`, optional `BloodFlowAi:ApiKey` | **No** (stored outside the repo) |
| `appsettings.Production.json` (server only) | The same secrets for the hosted server, `Swagger:Enabled`, `Cors:AllowedOrigins` | **No** (in `.gitignore`). Template: `appsettings.Production.example.json` |
| `SystemSettings` table | Business parameters an admin can change at runtime (max units, near-expiry days, ...) | Seeded by code |

Required at startup: `Jwt:Key` (startup throws if missing), `AdminSeed:Email` and `AdminSeed:Password` (seeder throws if missing), and a working connection string (the seeder queries the database).

---

## 7. Request lifecycle — from Controller to Database

Example: a blood bank approves request 15 (`POST /api/requests/15/approve`).

```
HTTP request + "Authorization: Bearer <token>"
  │
  ├─ UseAuthentication
  │     JwtBearer validates signature, issuer, audience, expiry.
  │     OnTokenValidated: one small query → the account must be Active and its organization Active.
  ├─ UseAuthorization
  │     RequestsController has [Authorize(Roles = "HospitalStaff,BloodBankStaff")].
  │
  ├─ RequestsController.Approve(15)
  │     return (await _requests.ApproveAsync(15)).ToActionResult(this);
  │
  ├─ RequestService.ApproveAsync  (Infrastructure)
  │     • who is calling? → ICurrentUser (user id + org_id from the token)
  │     • load the request (tracked) → checks with RequestRules (status, verification)
  │     • load my usable batches → RequestRules.AllocateFefo(...) → all units or an error
  │     • ReservationLedger.Reserve(...) for each batch: Reserved += q and a ledger row
  │     • ChangeStatus(...) → status history row
  │     • AuditLogger.Add(...) → audit row (not saved yet)
  │     • ONE SaveChangesAsync → EF Core writes everything in one transaction
  │
  ├─ Result<RequestDetailsDto>
  │
  └─ ResultExtensions.ToActionResult
        success → 200 + JSON
        error   → ProblemDetails with status (400/401/403/404/409) and "code"
```

Every feature in the project follows the same shape: **Controller → Interface → Service → (Rules + DbContext) → Result → HTTP response**.

---

## 8. Cross-cutting concerns

### 8.1 Authentication and authorization
- **Identity** (`AddIdentityCore<ApplicationUser>` + roles + EF stores) stores users and hashes passwords. Password policy: at least 8 characters, a digit, a lowercase and an uppercase letter. Lockout: 5 failed attempts → locked for 15 minutes.
- **JWT** (`JwtTokenGenerator`): HMAC-SHA256 signed token with short claims `sub`, `email`, `name`, `role`, `org_id` (staff only) and `jti`. Lifetime from `Jwt:ExpiryMinutes` (60). `MapInboundClaims = false` keeps the short claim names. `ClockSkew` = 30 seconds.
- **Account check on every request**: `OnTokenValidated` rejects the token if the account is no longer `Active` or the staff member's organization is deactivated. So a deactivation takes effect immediately, not after the token expires.
- **Roles**: `SystemAdmin`, `HospitalStaff`, `BloodBankStaff`, `Donor`, `Recipient` (the last two exist but have no endpoints yet).
- **Organization scoping**: the organization always comes from the token (`org_id`), never from the request body. Records of other organizations return `404`.

### 8.2 Errors and validation
1. **Model validation** (attributes on DTOs) → automatic `400` with field errors (`[ApiController]`).
2. **Business errors** → services return `Error.Validation/NotFound/Conflict/Forbidden/Unauthorized` with a stable code like `Request.InsufficientStock`. `ResultExtensions` maps them to 400/404/409/403/401 ProblemDetails with `code` and `traceId`.
3. **Unexpected exceptions** → `GlobalExceptionHandler`:
   - `DbUpdateConcurrencyException` → `409` code `Concurrency`
   - SQL unique violation (2601/2627) → `409` code `Duplicate`
   - anything else → `500` code `ServerError` (logged with the trace id)
4. **Database constraints** (CHECK, unique, foreign keys) are the last line of defence.

### 8.3 Audit logging
`AuditLogger.Add(...)` adds an `AuditLog` row to the **same DbContext** without saving. The service's single `SaveChangesAsync` then saves the change and its audit row together — there is never a change without its audit row. When the audit row needs a new Id (e.g. after creating an entity), the service uses an explicit transaction: save → audit → save → commit.

### 8.4 Concurrency
`RowVersion` (`rowversion` column) on `Organizations`, `SystemSettings`, `BloodBatches`, `BloodRequests`, `Reservations`, `ReservationLines`. If two users change the same row at the same moment, the second save fails and the user gets `409`. Admin edit endpoints (organization, setting) require the client to send back the `rowVersion` it read.

### 8.5 Idempotency
`clientRequestId` (a GUID from the client) on stock intake/adjustment and on request creation. A retry with the same id returns the original result instead of creating a duplicate (filtered unique indexes enforce it).

### 8.6 Time
All timestamps are stored in **UTC**. "Today" (for expiry dates) is computed by `SystemClock` in the business time zone `App:TimeZone` (default `Africa/Cairo`).

### 8.7 Background job
`MaintenanceBackgroundService` runs once at startup and then every hour:
1. `BatchExpiryService.ExpireDueBatchesAsync` — marks expired batches and invalidates their active reservation lines.
2. `RequestMaintenanceService.AnonymizePatientNamesAsync` — removes patient names from closed requests older than `Privacy.PatientNameRetentionMonths`.

Each step has its own scope and try/catch, so one failure never stops the other or crashes the API.

### 8.8 External integration — AI service
A separate Python **FastAPI** service (built by the AI team) provides forecasts. The backend calls it server-to-server through `BloodFlowAiClient`:
- Base URL: `BloodFlowAi:BaseUrl` (default `http://127.0.0.1:8000`).
- Header `x-api-key` from `BloodFlowAi:ApiKey` (secret, optional; added only when set).
- The AI service uses snake_case JSON; `AiJsonNamingPolicy` maps it.
- Failures become `AiServiceException` → `AiController` returns 400/409/503/502 ProblemDetails.
The browser never talks to the AI service directly, so the AI key is never exposed.

### 8.9 CORS and Swagger
- CORS policy `Frontend` allows the origins listed in `Cors:AllowedOrigins` (any header, any method).
- Swagger UI at `/swagger`, OpenAPI JSON at `/openapi/v1.json`. Enabled in Development, or anywhere `Swagger:Enabled` is `true`.

---

## 9. Continuous Integration

`.github/workflows/ci.yml` runs on pushes to `master` and `feature/**`, on pull requests to `master`, and manually:
1. Set up .NET `10.0.x`
2. `dotnet restore BloodFlow.slnx`
3. `dotnet build --configuration Release`
4. `dotnet test` (83 tests)
5. `dotnet list package --vulnerable --include-transitive` — prints vulnerable packages in the log. Note: this command does **not** fail the build by itself; read its output.

Last verified run on `master`: build with 0 warnings, all tests passed.
