# PART 6 — Backend Code Explanation (file by file)

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

Order: entry point → wiring → shared building blocks → features. For each important file: **Purpose · Responsibility · Key members · C# syntax notes · Interacts with · Why this way · If removed/changed · Improvements.** Simple files are grouped in tables.

> 🇪🇬 هنمشي ملف ملف بالترتيب اللي الكود بيشتغل بيه: من أول ما السيرفر يقوم، لحد ما الطلب يوصل للداتابيز.

---

## 6.1 `src/BloodFlow.Api/Program.cs` — the entry point

**Purpose:** build and run the web application. It is the first code that runs.
**Responsibility:** register services, seed data, define the middleware pipeline.
**Key lines:**
```csharp
var builder = WebApplication.CreateBuilder(args);          // loads config, logging, DI container
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddInfrastructure(builder.Configuration);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(FrontendCorsPolicy, p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
await IdentitySeeder.SeedAsync(app.Services);
app.UseExceptionHandler();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled")) { app.MapOpenApi(); app.UseSwaggerUI(...); }
app.UseHttpsRedirection(); app.UseCors(FrontendCorsPolicy); app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.Run();
```
**Syntax notes:** *top-level statements* (no `class Program` / `Main` — the compiler generates them); `await` at top level is allowed; `?? []` = "if null, use an empty array" (`[]` is a C# 12 collection expression); `const string FrontendCorsPolicy = "Frontend";` a local constant.
**Interacts with:** `DependencyInjection.AddInfrastructure`, `IdentitySeeder`, `GlobalExceptionHandler`, `CurrentUser`, `BearerSecuritySchemeTransformer`.
**Why this way:** one readable place for the pipeline; the long registration list is moved to Infrastructure so Program stays short.
**If removed/changed:** removing `AddInfrastructure` → nothing resolves (500 on every call). Moving `UseAuthorization` before `UseAuthentication` → every protected endpoint returns 401. Removing `JsonStringEnumConverter` → enums become numbers and the frontend breaks.
**Improvements:** add `AddHealthChecks()`; rate limiting for `/api/auth/*`; validate options at startup (`ValidateOnStart`).

---

## 6.2 `src/BloodFlow.Infrastructure/DependencyInjection.cs` — the wiring

**Purpose:** `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` registers everything Infrastructure provides.
**Sections:** Database (`AddDbContext` + `UseSqlServer`) · Identity (`AddIdentityCore` + password/lockout options + roles + EF stores) · Application services (scoped) · Inventory · Requests · `AddHostedService<MaintenanceBackgroundService>()` · `AddBloodFlowAi(configuration)` · JWT (`Configure<JwtSettings>`, `AddScoped<JwtTokenGenerator>()`, `AddAuthentication().AddJwtBearer(...)` with `TokenValidationParameters` and the `OnTokenValidated` event).
**Syntax notes:** `this IServiceCollection services` makes it an **extension method**, so Program can write `builder.Services.AddInfrastructure(...)`. The lambda `options => { ... }` configures options objects. `is not null and not OrganizationStatus.Active` is a C# 9 **pattern** combining checks.
**The `OnTokenValidated` event (important):**
```csharp
OnTokenValidated = async context =>
{
    var userIdClaim = context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value;
    if (!int.TryParse(userIdClaim, out var userId)) { context.Fail("Invalid token subject."); return; }
    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
    var state = await db.Users.AsNoTracking().Where(u => u.Id == userId)
        .Select(u => new { u.AccountStatus,
            OrganizationStatus = db.StaffProfiles.Where(s => s.UserId == u.Id)
                .Select(s => (OrganizationStatus?)s.Organization.Status).FirstOrDefault() })
        .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
    if (state is null || state.AccountStatus != AccountStatus.Active) context.Fail("Account is not active.");
    else if (state.OrganizationStatus is not null and not OrganizationStatus.Active) context.Fail("Organization is not active.");
}
```
**If removed:** deactivated users keep access until their token expires.
**Improvements:** validate `Jwt:Key` length (≥ 32 bytes) here; cache the status check briefly if traffic grows.

---

## 6.3 Domain — `src/BloodFlow.Domain/`

**Purpose:** the nouns of the system. No framework references.

| Entity | Key members | Notes |
|---|---|---|
| `Governorate` | `short Id`, `required string Name`, `Organizations` | `required` = must be set when creating the object |
| `BloodGroup` | `byte Id`, `required string Code` | 8 rows |
| `Organization` | `Type`, `Name`, `CityName`, `GovernorateId`, `Status`, `RowVersion` | |
| `StaffProfile` | `UserId`, `OrganizationId`, `StaffKind`, `JobTitle`, approval fields | |
| `SystemSetting` | `SettingKey`, `SettingValue`, `ValueType`, `IsMedicalParameter`, approval/update fields, `RowVersion` | |
| `AuditLog` | actor, action, entity, old/new JSON, reason, IP, correlation | |
| `BloodBatch` | quantities, dates, status, `RowVersion`, `Transactions`, **`AvailableQuantity => Status == BatchStatus.Active ? OnHandQuantity - ReservedQuantity : 0`** | `=>` here is an *expression-bodied property* (computed on read) |
| `InventoryTransaction` | type, deltas, after-values, `ReservationLineId`, reason, actor, ids | |
| `BloodRequest` | number, type, status, verification, group, units, `NeededBy`, destination, patient fields, `RowVersion`, `Reservations`, `StatusHistory` | |
| `BloodRequestStatusHistory` | `FromStatus?` (`null` for the first row), `ToStatus`, who, reason, when, correlation | |
| `Reservation` / `ReservationLine` | org, group, status, `ClosedAt`, `Lines` / batch, quantity, `InvalidationReason` | |
| `EmergencyOverride` | `ReasonText`, `ReviewStatus`, reviewer, notes | |

Enums (`Domain/Enums`): `AccountStatus`, `ActorType`, `AdjustmentReason`, `BatchSourceType`, `BatchStatus`, `ExpiryBasis`, `InventoryTransactionType`, `OrganizationStatus`, `OrganizationType`, `SettingValueType`, and in `RequestEnums.cs`: `RequestType`, `RequestStatus`, `VerificationStatus`, `RequesterCapacity`, `ReservationStatus`, `ReservationLineStatus`, `OverrideReviewStatus`.

**Syntax notes:** `= null!;` on navigation properties tells the compiler "EF will fill this; don't warn me it's null". `ICollection<T> X { get; set; } = new List<T>();` initializes collections so `.Add(...)` works on new entities.
**If you put EF attributes here:** Domain would depend on EF Core — against Clean Architecture.

---

## 6.4 Application — shared building blocks (`Application/Common/`)

### `Result.cs` and `Error.cs`
```csharp
public enum ErrorType { Validation, Unauthorized, Forbidden, NotFound, Conflict }
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    // Validation, Unauthorized, Forbidden, NotFound ...
}

public class Result { public bool IsSuccess { get; } public Error? Error { get; } ...
    public static implicit operator Result(Error error) => Failure(error); }
public sealed class Result<T> : Result { public T Value => IsSuccess ? _value! : throw new InvalidOperationException(...);
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error); }
```
**Syntax:** `implicit operator` lets you `return error;` or `return dto;` where a `Result<T>` is expected. `protected` constructor → only `Success`/`Failure` create results.
**Why:** expected failures are values with a stable code; no exceptions for normal flow.
**Pitfall:** C# can't apply an implicit conversion from an **interface** type, so for `IReadOnlyList<T>` you must write `Result<IReadOnlyList<T>>.Success(list)` (you'll see that in `RequestService.GetHistoryAsync`).

### Other files
| File | What | Why |
|---|---|---|
| `PagedResult.cs` | `PagedResult<T>(Items, Page, PageSize, TotalCount)` + `TotalPages`; abstract `PageQuery` with `SafePage`, `SafePageSize` (max 100), `Skip` | One paging model; protects the DB from `pageSize=100000` |
| `ICurrentUser.cs` | `UserId`, `Role`, `IpAddress`, `OrganizationId` | Services know the caller without knowing HTTP |
| `IClock.cs` | `UtcNow`, `Today` | Testable time; Cairo "today" |
| `Roles.cs` | role constants, `Staff`, `AllRoles` | No magic strings |
| `AppClaimTypes.cs` | `sub`, `email`, `name`, `role`, `org_id` | Same names when writing and reading the token |
| `SettingKeys.cs` | 9 setting keys | No typos in keys |
| `AuditActions.cs` | action names (`Request.Approved`, …) + `AuditEntityTypes` | Consistent audit filtering |
| `ReasonRequest.cs` | `{ reason }` 3–500 chars | Reused by reject/deactivate/cancel |

---

## 6.5 Application — pure rules

### `Inventory/InventoryRules.cs`
- `ValidateIntake(quantity, collectionDate, expiryDate?, today, defaultShelfLifeDays, out expiryDate, out expiryBasis)` — quantity > 0; collection not in the future; if no expiry → collection + shelf life (`ExpiryBasis.DefaultShelfLife`); expiry after collection; not already expired.
- `ValidateAdjustment(delta, reason, status, onHand, reserved)` — delta ≠ 0; Damaged/Expired only decrease; expired batch only decrease; result ≥ 0; result ≥ reserved (`Inventory.BelowReserved`, 409).
- `IsExpired(expiry, today) => expiry < today` (a unit that expires today is still usable today).
**Syntax:** `out` parameters return extra values; `Error?` return = "null means OK".
**Tests:** `InventoryRulesTests.cs`.

### `Requests/RequestRules.cs`
`CanTransition`, `AllocateFefo`, `ComputeFlags`, `CanReserve`, `CanFulfil` and records `BatchCandidate`, `Allocation`, `RequestFlags`. Code and explanation in Part 8 and `LEARNING_GUIDE.md` §3.4. **Tests:** `RequestRulesTests.cs` (35 tests).

### `Settings/SettingValueValidator.cs`
`TryNormalize(type, raw, out normalized, out error)` — trims; Int: whole number > 0; Decimal: number > 0 with `.`; Bool and String handled too. Uses `CultureInfo.InvariantCulture` so "1.5" works on any server language. **Tests:** `SettingValueValidatorTests.cs`.

### DTO files and interfaces
`Auth/*`, `Staff/StaffDtos.cs`, `Organizations/OrganizationDtos.cs`, `Settings/SettingDtos.cs`, `Audit/AuditDtos.cs`, `Lookups/LookupDtos.cs`, `Inventory/InventoryDtos.cs`, `Requests/RequestDtos.cs`, `Ai/AiModels.cs` + interfaces `I*Service`, `IBloodFlowAiClient`, `AiServiceException` (`AiErrorKind`: BadRequest, Busy, Unavailable, Unauthorized, Failed).

---

## 6.6 Infrastructure — common helpers (`Infrastructure/Common/`)

### `AuditLogger.cs`
```csharp
public void Add(string action, string entityType, string? entityId, object? oldValues = null, object? newValues = null,
    string? reason = null, int? organizationId = null, int? actorUserId = null, string? actorRole = null)
{
    var userId = actorUserId ?? _currentUser.UserId;
    _db.AuditLogs.Add(new AuditLog {
        OccurredAt = DateTime.UtcNow,
        ActorType = userId.HasValue ? ActorType.User : ActorType.System,
        ... OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions), ... });
}
```
**Key point:** it only **adds** to the DbContext; the caller's `SaveChangesAsync` writes it with the change. No user (background job) → `ActorType.System`.
**Syntax:** optional parameters with defaults; *named arguments* at call sites (`newValues: new { ... }`); anonymous objects `new { request.Status }` serialized to JSON.
**If removed:** no audit trail. **Improvement:** pass `CorrelationId` (column exists, not filled by `Add`).

### `SettingsReader.cs`
`GetIntAsync(key, fallback)` — reads one setting; invalid or ≤ 0 → fallback. Used for max units, shelf life, windows, retention.

### `SystemClock.cs`
`UtcNow => DateTime.UtcNow`; `Today` converts UTC to `App:TimeZone` (default `Africa/Cairo`) via `TimeZoneInfo.FindSystemTimeZoneById`. Registered as singleton.

### `RowVersionConverter.cs`
`ToBase64(byte[])` and `TryParse(string, out byte[])` (must be 8 bytes).

---

## 6.7 Infrastructure — persistence
`AppDbContext.cs`, `Configurations/` (11 files), `Migrations/`, `Seed/SystemSettingSeeder.cs` — explained in Part 4. Seeder detail: builds the 9 default settings with a local helper `Int(key, value, description, medical: true)` and inserts only keys that don't exist yet.

---

## 6.8 Infrastructure — identity and authentication
`ApplicationUser`, `IdentitySeeder`, `AuthService`, `JwtTokenGenerator`, `JwtSettings` — explained in Part 5. `JwtTokenGenerator.GenerateToken` returns `JwtToken(Value, ExpiresAtUtc)` (an `internal sealed record`).

---

## 6.9 Infrastructure — admin services (`Infrastructure/Services/`)

| Service | Methods | Rules worth knowing |
|---|---|---|
| `StaffAdminService` | `GetStaffAsync`, `GetByIdAsync`, `ApproveAsync`, `RejectAsync`, `DeactivateAsync`, `ReactivateAsync` | Each checks the **current** status (`InvalidTransition` → `Staff.InvalidStatus`); approve/reactivate require an active organization; sets `ApprovedByUserId/At`, `RejectionReason`; audit + one save; concurrency → `Staff.Concurrency` |
| `OrganizationService` | list, get, create, update, deactivate, activate | Unique name per governorate (`Organization.DuplicateName`); update uses `rowVersion` as the original value; deactivate refused if active reservations or open requests (`Organization.HasOpenWork`) |
| `SystemSettingService` | `GetAllAsync`, `GetAsync`, `UpdateAsync` | `SettingValueValidator`; medical → `ApprovalReference` required and approver recorded; `rowVersion` |
| `AuditLogService` | `GetAsync(query)` | Filters + newest first (`OrderByDescending(a => a.Id)`) |
| `LookupService` | governorates, blood groups, active organizations | `AsNoTracking`, ordered |

---

## 6.10 Infrastructure — inventory (`Infrastructure/Inventory/`)

### `InventoryService.cs`
| Method | Does |
|---|---|
| `IntakeAsync` | Shown in Part 4 §4.8: caller from token → replay check → blood group exists → `InventoryRules.ValidateIntake` → batch + `Intake` ledger row via navigation → transaction save → audit → save → commit |
| `AdjustAsync` | Load **my** batch (else 404) → replay check → `ValidateAdjustment` → change `OnHandQuantity` → `Adjust` ledger row with reason → audit → save; `DbUpdateConcurrencyException` → `Inventory.Concurrency` |
| `GetBatchesAsync` | My batches, filters, FEFO order, paging |
| `GetBatchAsync` | One of my batches |
| `GetLedgerAsync` | My ledger rows, filters, newest first |
| `GetSummaryAsync` | Uses `InventorySummaryBuilder` |
`TryGetCaller(out orgId, out userId, out error)` — every method starts with it; non-staff → `Inventory.NoOrganization` 403.

### `InventorySummaryBuilder.cs` (+ `InventoryReportService`)
Per blood group: on hand, reserved, available, near-expiry units (within `Inventory.NearExpiryAlertDays`), expired units, next expiry date. The admin report reuses it for any organization.

### `BatchRow.cs`
A projection record + expression used to build `BatchDto` in one SQL query (computes `DaysToExpiry` and `AvailableQuantity`).

### `BatchExpiryService.cs`
```csharp
var due = await _db.BloodBatches.Where(b => b.Status == BatchStatus.Active && b.ExpiryDate < today).ToListAsync(ct);
foreach (var batch in due) { batch.Status = BatchStatus.Expired; batch.ExpiredAt = now; _audit.Add(... System ...); }
// active reservation lines on those batches → ReservationLedger.Release(..., Invalidated, "BatchExpired", userId: null, ...)
if (due.Count > 0) await _db.SaveChangesAsync(ct);
```
Idempotent: a second run finds nothing to do.

---

## 6.11 Infrastructure — requests (`Infrastructure/Requests/`)

### `RequestService.cs` (implements `IRequestService`)
| Method | Summary |
|---|---|
| `CreateAsync` | Hospital staff only; idempotency; blood group; max units; `NeededBy` future (`ToUtc`); duplicate check (same hospital + group + patient name, open, within window); number from sequence (`NextRequestNumberAsync`); Verified at creation; first history row; transaction: save → audit (no patient name) → save |
| `GetHospitalRequestsAsync` / `GetExecutingAsync` / `GetBoardAsync` | Lists via `RequestReads` (board: Pending + (Verified or Emergency) + not mine + same governorate by default; ordered emergency first then `NeededBy`; adds `availableAtYourOrganization`) |
| `GetAsync` / `GetHistoryAsync` | `CanSeeAsync` visibility (destination, creator, any staff while Pending, executing org) else 404 |
| `VerifyAsync` | Destination hospital; Unverified → Verified |
| `ApproveAsync` | Shown in Part 8 |
| `ReallocateAsync` | Invalidate expired lines, then reserve only the missing units |
| `FulfilAsync` | Executor only; `CanFulfil`; expired lines → invalidate + save + 409; `Deduct` each line; reservation Consumed; override row if unverified emergency |
| `ReceiveAsync` | Destination or creator; idempotent |
| `RejectAsync` / `CancelAsync` | Parties checks; cancel of Approved releases lines; admin only Pending |
| private `AllocateAsync` | Load my usable batches (tracked), compute availability in memory, `AllocateFefo`, create lines + `Reserve` rows |
| private `InvalidateExpiredLines`, `LoadForExecutorAsync`, `ChangeStatus`, `SaveAndReturnAsync`, `DetailsAsync`, `CanSeeAsync`, `AvailableByBloodGroupAsync`, `NextRequestNumberAsync`, `ToUtc`, `TryGetCaller` | helpers |

**Syntax notes in this file:**
- *Tuple return* `Task<(BloodRequest? Request, Reservation? Reservation, Error? Error)>` and *deconstruction* `var (request, reservation, error) = await LoadForExecutorAsync(...)`.
- `request.Status is not (RequestStatus.Pending or RequestStatus.Approved)` — pattern combinators.
- `const string NextNumberSql = "SELECT NEXT VALUE FOR dbo." + BloodRequestConfiguration.NumberSequence + " AS [Value]";` — a constant SQL string (no user input → no SQL injection) used with `SqlQueryRaw<int>`; no LINQ after it because SQL Server doesn't allow `NEXT VALUE FOR` in a subquery.

### `ReservationLedger.cs`
Static class with `Reserve`, `Release`, `Deduct` — the only code that changes `ReservedQuantity` for requests and writes the matching ledger row. Sets `ReservationLine = line` (navigation) so new lines get their Id at save time. `userId == null` → System actor.

### `RequestReads.cs`
`RequestRow` (projection record + `Projection` expression), `FlagContext` (now + thresholds; `ToSummary`, `OnlyFlagged` SQL filter), `RequestReads` (flag settings, filters, paging, details with reservation lines, history). `Expression<Func<BloodRequest, RequestRow>>` is an **expression tree**: EF translates it to SQL instead of running C#.

### `RequestAdminService.cs`, `RequestMaintenanceService.cs`
Admin list/overrides/review; anonymization with `ExecuteUpdateAsync` (one `UPDATE` statement for all due rows, no loading) + one summary audit row, inside a transaction.

---

## 6.12 Infrastructure — background job (`Jobs/MaintenanceBackgroundService.cs`)
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(Interval);              // 1 hour
    do
    {
        await RunStepAsync("Batch expiry", async sp => { ... IBatchExpiryService ... }, stoppingToken);
        await RunStepAsync("Patient name anonymization", async sp => { ... IRequestMaintenanceService ... }, stoppingToken);
    }
    while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
}
```
`RunStepAsync` creates a scope, runs the step, catches `OperationCanceledException` on shutdown and logs any other exception (never crashes the API). `do … while` = run once immediately, then every tick.

---

## 6.13 Infrastructure — AI client (`Infrastructure/Ai/`)
| File | Role |
|---|---|
| `BloodFlowAiOptions` | `BaseUrl`, `ApiKey`, `TimeoutSeconds` (30), `RetrainTimeoutMinutes` (5); section `BloodFlowAi` |
| `AiServiceCollectionExtensions` | Two named `HttpClient`s (normal / retrain timeout) with `x-api-key` header when set; registers `IBloodFlowAiClient` |
| `BloodFlowAiClient` | Calls AI endpoints; `static SemaphoreSlim RetrainLock = new(1, 1)` → only one retrain at a time (`WaitAsync(0)` fails immediately → `Busy` → 409); maps HTTP errors/timeouts to `AiServiceException` kinds |
| `AiJsonNamingPolicy` | snake_case with `_` before digits (`ExpiringIn3Days` → `expiring_in_3_days`) using a source-generated regex (`[GeneratedRegex]`, `partial`) |

---

## 6.14 Api — common (`Api/Common/`)
| File | Purpose | Notes |
|---|---|---|
| `CurrentUser.cs` | Implements `ICurrentUser` from `HttpContext.User` claims and `Connection.RemoteIpAddress` | `internal sealed` = only this project, not inheritable |
| `ResultExtensions.cs` | `ToActionResult` (200 `Ok` / 204 `NoContent`) and `ToProblem` (ErrorType → status + ProblemDetails with `code`, `traceId`) | extension methods on `Result` and `ControllerBase` |
| `GlobalExceptionHandler.cs` | `IExceptionHandler`: `DbUpdateConcurrencyException` → 409 `Concurrency`; SQL 2601/2627 → 409 `Duplicate`; else 500 `ServerError` (logged) | Uses a *property pattern* `DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }` |

---

## 6.15 Api — controllers
All controllers: `[ApiController]` (automatic 400 for invalid models, binding rules), `[Route("api/...")]`, an `[Authorize(...)]` or `[AllowAnonymous]`, `[Produces("application/json")]` on most, `[ProducesResponseType<T>(...)]` for Swagger. They receive an interface in the constructor and each action is 1–3 lines.

| Controller | Route | Auth | Service |
|---|---|---|---|
| `AuthController` | `api/auth` | anonymous | `IAuthService` |
| `LookupsController` | `api/lookups` | anonymous | `ILookupService` |
| `InventoryController` | `api/inventory` | Staff | `IInventoryService` |
| `RequestsController` | `api/requests` | Staff (+Hospital for create/verify) | `IRequestService` |
| `AiController` | `api/ai` | Admin+Staff (health anonymous, retrain admin) | `IBloodFlowAiClient` |
| `Admin/StaffController` | `api/admin/staff` | Admin | `IStaffAdminService` |
| `Admin/OrganizationsController` | `api/admin/organizations` | Admin | `IOrganizationService` |
| `Admin/SettingsController` | `api/admin/settings` | Admin | `ISystemSettingService` |
| `Admin/AuditLogsController` | `api/admin/audit-logs` | Admin | `IAuditLogService` |
| `Admin/InventoryReportsController` | `api/admin/inventory` | Admin | `IInventoryReportService` |
| `Admin/RequestsMonitorController` | `api/admin` | Admin | `IRequestAdminService`, `IRequestService` |

Syntax: `public async Task<IActionResult> Get(int id, CancellationToken ct) => (await _x.GetAsync(id, ct)).ToActionResult(this);` — an expression-bodied async method. `{id:int}` in a route is a **route constraint** (non-numbers → 404 before your code runs). `[FromQuery] RequestQuery query` binds `?status=...&page=...` into a record.

`BearerSecuritySchemeTransformer` (`Api/OpenApi/`) adds the Bearer scheme to the OpenAPI document so Swagger shows **Authorize**.

---

## 6.16 Tests (`tests/BloodFlow.Tests/`)
| File | Tests |
|---|---|
| `Requests/RequestRulesTests.cs` | 35 (transitions, FEFO, flags, CanReserve, CanFulfil) |
| `Inventory/InventoryRulesTests.cs` | 23 |
| `Settings/SettingValueValidatorTests.cs` | 17 |
| `Common/PageQueryTests.cs` | 5 |
| `Common/ResultTests.cs` | 3 |
Run with `dotnet test`. They need no database because they test pure code.

---

## 6.17 Possible improvements across the code
1. Integration tests for services and constraints (Testcontainers + SQL Server).
2. Pass `CorrelationId` into `AuditLogger.Add`.
3. Validate options at startup (`Jwt:Key` length, `AdminSeed`).
4. Rate limiting on `/api/auth/login` and `/register/staff`.
5. Structured logging (Serilog) with the `traceId`.
6. A `RowVersion` on `EmergencyOverride`.
7. Split `RequestService` (largest file) into smaller command services if it keeps growing.
