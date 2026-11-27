# PART 9 — Software Engineering Concepts in BloodFlow

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

For each concept: what it is · where it appears in our code · 🇪🇬 in simple Arabic.

---

## 9.1 SOLID

| Principle | Meaning | Where in BloodFlow |
|---|---|---|
| **S** — Single Responsibility | A class has one reason to change | `ReservationLedger` only moves reserved stock; `AuditLogger` only adds audit rows; `JwtTokenGenerator` only creates tokens; `SettingValueValidator` only validates setting values. (Counter-example to watch: `RequestService` is large — it handles the whole lifecycle.) |
| **O** — Open/Closed | Extend without modifying | New business numbers are added as `SystemSettings` rows; new error kinds map through one `switch` in `ResultExtensions`; new configurations are picked up by `ApplyConfigurationsFromAssembly` without touching `AppDbContext`. |
| **L** — Liskov Substitution | A subtype can replace its base | `Result<T>` is a `Result`; anywhere that needs `Result` (e.g. `ToActionResult(Result)`) works with it. `ApplicationUser` can be used wherever Identity expects `IdentityUser<int>`. |
| **I** — Interface Segregation | Small, focused interfaces | `IRequestService` (staff), `IRequestAdminService` (admin), `IRequestMaintenanceService` (job) instead of one huge interface; `IInventoryService` vs `IInventoryReportService` vs `IBatchExpiryService`. |
| **D** — Dependency Inversion | Depend on abstractions | Controllers depend on `IAuthService`, not `AuthService`; Application defines `ICurrentUser` and `IClock`, implemented in Api and Infrastructure. |

> 🇪🇬 SOLID خمس قواعد بتخلي الكود سهل يتغير: كل كلاس شغلانة واحدة، نزوّد من غير مانكسر، والكلاسات بتعتمد على عقود (Interfaces) مش على تفاصيل.

---

## 9.2 Separation of Concerns
Each part does one kind of job: controllers = HTTP, services = workflow, rules = decisions, configurations = database shape, middleware = cross-cutting (errors, auth). Example: the decision "can this request move to Approved?" is in `RequestRules`, the database work is in `RequestService`, the HTTP translation is in `ResultExtensions`.
> 🇪🇬 كل حاجة في مكانها: اللي بيتعامل مع الإنترنت لوحده، واللي بيقرر القواعد لوحده، واللي بيكلّم الداتابيز لوحده.

## 9.3 Encapsulation
Hide internals, expose a safe surface.
- `Result`'s constructor is `protected`; you can only create results through `Success`/`Failure`/implicit conversions. Reading `Value` of a failed result throws.
- `internal sealed class` for Infrastructure services (`RequestService`, `AuditLogger`, `CurrentUser`) — other projects can't use or inherit them; they only see interfaces.
- `ReservationLedger` is the only code that changes `ReservedQuantity` for requests.
> 🇪🇬 الـ Encapsulation إننا نخبّي التفاصيل ونفتح باب واحد آمن للتعامل.

## 9.4 Abstraction
Expose *what*, hide *how*: `IClock.Today` (how the time zone is computed is hidden), `IBloodFlowAiClient` (HTTP details hidden), `ICurrentUser` (claims parsing hidden).

## 9.5 Interfaces
Contracts in Application: `IAuthService`, `IStaffAdminService`, `IOrganizationService`, `ILookupService`, `ISystemSettingService`, `IAuditLogService`, `IInventoryService`, `IInventoryReportService`, `IBatchExpiryService`, `IRequestService`, `IRequestAdminService`, `IRequestMaintenanceService`, `IBloodFlowAiClient`, `ICurrentUser`, `IClock`. Benefits: replaceable implementations, fakeable in tests, compile-time contract.

## 9.6 Dependency Injection
Constructor injection everywhere, e.g.:
```csharp
public RequestService(AppDbContext db, ICurrentUser currentUser, IClock clock,
                      AuditLogger audit, SettingsReader settings, RequestReads reads) { ... }
```
Registered in `DependencyInjection.cs`. Lifetimes in Part 3 §3.8.

## 9.7 Clean Code practices visible in the repo
- Meaningful names (`AllocateFefo`, `InvalidateExpiredLines`, `NeedsReallocation`).
- Small controller actions; guard clauses that return early (`if (request is null) return NotFound(id);`).
- Constants instead of magic strings (`Roles`, `SettingKeys`, `AuditActions`, `AppClaimTypes`).
- Comments explain **why** (e.g. "No LINQ operator after SqlQuery: NEXT VALUE FOR is not allowed in a subquery").
- One way to do things (always `Result`, always ProblemDetails, always one `SaveChanges`).

## 9.8 Async/Await
All I/O is asynchronous: `await _db.SaveChangesAsync(ct)`, `FindByEmailAsync`, `ToListAsync`. While waiting for SQL Server, the thread returns to the pool and serves other requests. `CancellationToken` is passed down so work stops if the client disconnects (`HttpContext.RequestAborted`).
**Rule:** never use `.Result` or `.Wait()` on tasks (can deadlock and blocks threads).
> 🇪🇬 الـ async/await يعني السيرفر مايقعدش مستني الداتابيز، يروح يخدم طلب تاني لحد ما الرد يرجع.

## 9.9 Exception handling
- **Expected failures → `Result`/`Error`** (not exceptions).
- **Known technical exceptions → caught where meaningful:** `DbUpdateConcurrencyException` in services (`Request.Concurrency`, `Inventory.Concurrency`, …); `AiServiceException` in `AiController`; `OperationCanceledException` in the hourly job.
- **Everything else → `GlobalExceptionHandler`** → 500 ProblemDetails + log (no stack trace to the client).
- **Startup misconfiguration → throw on purpose** (`Jwt` missing, `AdminSeed` missing).

## 9.10 Validation (three levels)
1. DTO attributes (automatic 400 via `[ApiController]`).
2. Business rules (`InventoryRules`, `RequestRules`, service checks) → `Error.Validation` / `Error.Conflict`.
3. Database constraints (CHECK, FK, unique) → last line of defence.

## 9.11 DTO vs Entity
| | Entity (`BloodRequest`) | DTO (`RequestSummaryDto`) |
|---|---|---|
| Layer | Domain | Application |
| Shape | Table columns + navigations | What the client needs |
| Contains | `RowVersion` bytes, `PatientFullName` | computed `flags`, names, `availableAtYourOrganization`; patient name only if allowed |
| Mutability | mutable, tracked by EF | immutable record |
Never return entities: risk of leaking fields, cycles in JSON, and coupling the API to the database.

## 9.12 Service layer
Services hold the use-case logic (load → check → change → audit → save). Controllers stay thin; rules stay pure. Example list: `InventoryService`, `RequestService`, `StaffAdminService`, …

## 9.13 Middleware
Pipeline in `Program.cs` (Part 3 §3.9). Our custom pieces plug into it: `GlobalExceptionHandler` (via `UseExceptionHandler`), the JWT `OnTokenValidated` event (via `UseAuthentication`), the CORS policy.

## 9.14 Configuration
`appsettings.json` (non-secret), User Secrets (local secrets), `appsettings.Production.json` (server, git-ignored), Options pattern (`JwtSettings`, `BloodFlowAiOptions`), runtime settings table (`SystemSettings`). Part 2 §2.15.

## 9.15 Security best practices applied
| Practice | Where |
|---|---|
| Hash passwords, lockout | Identity options |
| Validate JWT fully, short clock skew | `TokenValidationParameters` |
| Re-check account/organization each request | `OnTokenValidated` |
| Organization from token, 404 for others' data | services (`TryGetCaller`, `CanSeeAsync`) |
| Same error for unknown email / wrong password | `AuthService` |
| Secrets out of Git | User Secrets, `.gitignore` |
| Parameterized SQL only | EF LINQ; the only raw SQL is a constant |
| Least data exposure | DTOs, patient-name rules, anonymization |
| No stack traces to clients | `GlobalExceptionHandler` |
| CORS allow-list | `Cors:AllowedOrigins` |
| Audit trail | `AuditLogger` |
Gaps are listed in Part 11.

## 9.16 Other patterns you can name in a defense
| Pattern | Where |
|---|---|
| Result pattern | `Result`, `Error` |
| Unit of Work | `AppDbContext` + one `SaveChangesAsync` per action |
| Optimistic concurrency | `rowversion` |
| Idempotency key | `clientRequestId` |
| Append-only ledger / event-like history | `InventoryTransactions`, `BloodRequestStatusHistory` |
| Composition root | `Program.cs` + `AddInfrastructure` |
| Options pattern | `JwtSettings`, `BloodFlowAiOptions` |
| Gateway | `BloodFlowAiClient` + `AiController` |
| Background worker | `MaintenanceBackgroundService` |
