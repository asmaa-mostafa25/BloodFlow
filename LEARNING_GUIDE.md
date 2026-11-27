# BloodFlow — Learning Guide

> For Asmaa, the backend developer of BloodFlow. The goal is that you can explain, change and extend every part of this code **without AI**.
> Simple language, real file names, and real code from this repository.

---

## 0. How to use this guide

**Do not try to read the whole project at once.** It has more than 100 files, but they all follow **one pattern**. Learn the pattern once (section 1), then every feature becomes "the same thing with different names".

**Suggested plan (about 2 weeks, 1–2 hours a day):**

| Day | Read | Then do |
|---|---|---|
| 1 | §1 The pattern (Login) | Put breakpoints in `AuthController.Login` and `AuthService.LoginAsync`, log in from Swagger, step through with F10/F11 |
| 2 | §2 Building blocks | Find each building block in the code; explain it out loud in 2 sentences |
| 3–4 | §3.1–3.2 Auth + Admin | Exercise 1 and 2 (§6) |
| 5–6 | §3.3 Inventory | Exercise 3 |
| 7–9 | §3.4 Requests | Exercises 4 and 5 |
| 10 | §4 Decisions + §5 Questions | Ask someone to quiz you from §5 |
| 11+ | §6 | Exercise 6 (per-hospital emergency policy), then Slice 4 Donors by yourself |

**The debugger is your best teacher.** Run the API in Visual Studio (F5), put a breakpoint (F9) on the first line of a controller method, call it from Swagger, then press F11 to step *into* each method. Watch the variables. Do this for every feature you study.

The Arabic PDF reports in `docs/reports/` explain each slice in Egyptian Arabic, including every decision. Use them together with this guide.

---

## 1. The pattern — follow ONE request: Login

When someone types their email and password and presses Login, the request passes 5 stations:

```
Swagger / Frontend
   │  POST /api/auth/login  { email, password }
   ▼
① AuthController.Login            (BloodFlow.Api)            the door
   ▼
② IAuthService                    (BloodFlow.Application)    the contract
   ▼
③ AuthService.LoginAsync          (BloodFlow.Infrastructure) the brain: rules + database
   ▼
④ Result<AuthResponse>            (BloodFlow.Application)    success OR an error with a code
   ▼
⑤ ResultExtensions.ToActionResult (BloodFlow.Api)            200 JSON, or 401/403 ProblemDetails
```

### ① The controller — `src/BloodFlow.Api/Controllers/AuthController.cs`
```csharp
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
{
    var result = await _authService.LoginAsync(request, cancellationToken);
    return result.ToActionResult(this);
}
```
- `[HttpPost("login")]` + the class's `[Route("api/auth")]` = `POST /api/auth/login`.
- `LoginRequest` is filled from the JSON body. Its attributes (`[Required, EmailAddress]`) are checked **before** this method runs; if they fail, ASP.NET returns `400` automatically.
- The controller **does not think**. Two lines: call the service, convert the result. Every controller in the project is like this.

### ② The interface — `src/BloodFlow.Application/Auth/IAuthService.cs`
The controller asks for `IAuthService`, not `AuthService`. This is **Dependency Inversion**:
- The Api project does not need to know *how* login works (Identity? database?). It only knows *that* it can call `LoginAsync`.
- `DependencyInjection.cs` connects them: `services.AddScoped<IAuthService, AuthService>();`.
- Benefit: you could replace `AuthService` (or fake it in a test) without touching the controller.

### ③ The service — `src/BloodFlow.Infrastructure/Authentication/AuthService.cs`
In order:
1. `FindByEmailAsync` → not found → `InvalidCredentials`.
2. `IsLockedOutAsync` → locked → `Auth.LockedOut`.
3. `CheckPasswordAsync` → wrong → `AccessFailedAsync` (counts the failure) → `InvalidCredentials`.
4. Check `AccountStatus` (Pending / Rejected / Deactivated → 403 with a clear message).
5. For staff: load the `StaffProfile` and make sure the organization is Active.
6. `JwtTokenGenerator.GenerateToken(user, roles, organizationId)` → the token and its expiry time.

**Why the same error for "email not found" and "wrong password"?** If the API said "this email does not exist", an attacker could test thousands of emails and learn which ones have accounts. One message for both cases hides that.

### ④ The result — `src/BloodFlow.Application/Common/Result.cs`
```csharp
public static implicit operator Result<T>(T value) => Success(value);
public static implicit operator Result<T>(Error error) => Failure(error);
```
Because of these two lines, a service can simply write `return user;` or `return Error.Forbidden("Auth.AccountPending", "...");` and C# turns it into a `Result<T>`.
**Why not throw exceptions?** "Wrong password" is not exceptional — it happens all the time. Exceptions are slow and hide the flow. A `Result` makes it obvious that a method can fail and *how*.

### ⑤ The HTTP response — `src/BloodFlow.Api/Common/ResultExtensions.cs`
Maps `ErrorType` → status: Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409. The body is **ProblemDetails** with `code` (e.g. `Auth.AccountPending`) so the frontend can react to the code, not to the English text.

> **This is the whole project.** Inventory, requests, admin: Controller → Interface → Service (+ pure Rules + DbContext) → Result → HTTP.

---

## 2. The building blocks (used by every feature)

| Block | File | What it does | Why |
|---|---|---|---|
| **Entities** | `Domain/Entities/*.cs` | Plain classes = tables | Domain has no dependencies, so it is easy to read and test |
| **Entity configuration** | `Infrastructure/Persistence/Configurations/*.cs` | Column types, keys, indexes, CHECK constraints, relationships (`IEntityTypeConfiguration<T>`) | Keeps the database details out of the entities; one file per table |
| **AppDbContext** | `Infrastructure/Persistence/AppDbContext.cs` | `DbSet`s, applies all configurations, collation, sequence | EF Core's gateway to SQL Server. Inherits `IdentityDbContext` for the user tables |
| **Migrations** | `Infrastructure/Persistence/Migrations/` | Versioned changes to the database | The database shape is in Git, reviewable and repeatable |
| **DTOs** | `Application/<Feature>/*Dtos.cs` | Records for requests and responses, with validation attributes | Never expose entities directly: you choose exactly what goes in and out |
| **Pure rules** | `InventoryRules`, `RequestRules`, `SettingValueValidator` | Business decisions with no database | Easy to unit-test (all 83 tests test these) |
| **Result / Error** | `Application/Common/Result.cs`, `Error.cs` | Success or a typed error | See §1 ④ |
| **ICurrentUser** | `Application/Common/ICurrentUser.cs` → `Api/Common/CurrentUser.cs` | User id, role, `org_id`, IP from the token | Services never trust ids sent in the body |
| **IClock** | `Application/Common/IClock.cs` → `Infrastructure/Common/SystemClock.cs` | `UtcNow` and `Today` (in Cairo time) | Testable time; expiry dates follow the Egyptian calendar day |
| **AuditLogger** | `Infrastructure/Common/AuditLogger.cs` | Adds an `AuditLog` row to the DbContext (does not save) | The change and its audit row are saved together in one `SaveChanges` |
| **SettingsReader** | `Infrastructure/Common/SettingsReader.cs` | Reads an int setting with a fallback | Business numbers can change without redeploying |
| **DI registration** | `Infrastructure/DependencyInjection.cs` | Registers DbContext, Identity, services, JWT, job, AI | One place to see every dependency |
| **Pipeline** | `Api/Program.cs` | Middleware order | Order matters: authentication before authorization, CORS before both |
| **Global exception handler** | `Api/Common/GlobalExceptionHandler.cs` | Unexpected exception → ProblemDetails (409 for concurrency/duplicates, 500 otherwise) | The client never sees a stack trace |

### How one table is made (example: `BloodBatch`)
1. **Entity** `Domain/Entities/BloodBatch.cs` — properties like `OnHandQuantity`, `ReservedQuantity`, `ExpiryDate`.
2. **Configuration** `BloodBatchConfiguration.cs` — `HasConversion<string>()` for enums, `IsRowVersion()`, indexes, CHECKs like `[OnHandQuantity] >= [ReservedQuantity]`.
3. **DbSet** in `AppDbContext`: `public DbSet<BloodBatch> BloodBatches`.
4. **Migration**: `dotnet ef migrations add AddInventory ...` → read the generated file → `dotnet ef database update`.

---

## 3. Feature by feature

### 3.1 Authentication, Identity and JWT

| Class | Job |
|---|---|
| `ApplicationUser` (Infrastructure/Identity) | Identity user + `FullName`, `AccountStatus`, `CreatedAt` |
| `AuthService` | Login and staff registration |
| `JwtTokenGenerator` | Builds the signed token |
| `JwtSettings` | Issuer, Audience, Key, ExpiryMinutes from configuration |
| `IdentitySeeder` | Creates roles + first admin at startup |
| `DependencyInjection` (JWT part) | Validates tokens on every request |

**The token** contains short claims: `sub` (user id), `email`, `name`, `role`, `org_id` (staff only), `jti` (unique token id). It is signed with HMAC-SHA256 using `Jwt:Key`. Anyone can *read* a JWT (paste one into jwt.io), but nobody can *change* it without the key — that is why the key is secret.

**Validation on every request** (`DependencyInjection.cs`): signature, issuer, audience and expiry are checked, then `OnTokenValidated` runs one small query: is the account still Active, and is the staff member's organization still Active? Without this, a deactivated employee could keep working until the token expires (60 minutes).

**Registration** (`AuthService.RegisterStaffAsync`): checks the organization exists and is active, creates the user as **Pending**, gives the role based on the organization type (Hospital → HospitalStaff, BloodBank → BloodBankStaff), creates the `StaffProfile`, writes an audit row — all in one transaction.

**Authorization**: `[Authorize(Roles = Roles.SystemAdmin)]` on a controller. Note: if both the class and the method have `[Authorize]`, **both** must pass (AND, not OR). That is why the admin's cancel endpoint lives in the admin controller.

### 3.2 System Admin (Slice 1)

- **`StaffAdminService`**: approve/reject/deactivate/reactivate. Each method loads the user, checks the **current status** (e.g. only Pending can be approved), changes it, adds an audit row, saves once. A concurrency conflict → `Staff.Concurrency` 409.
- **`OrganizationService`**: create/update/deactivate/activate. Update requires the `rowVersion` the client read (optimistic concurrency). Deactivate is refused when the organization has active reservations or open requests.
- **`SystemSettingService` + `SettingValueValidator`**: the value must match the type (`Int`, `Decimal`, `Bool`, `String`); medical settings need `approvalReference`.
- **`AuditLogService`**: filters and pages the audit log.
- **`LookupService`**: governorates, blood groups, active organizations (anonymous).

### 3.3 Inventory (Slice 2)

**The model:** stock is kept in **batches** (one collection date, one expiry date, one organization, one blood group).
- `OnHandQuantity` — physically in the fridge.
- `ReservedQuantity` — held for approved requests.
- **Available = OnHand − Reserved** (computed, never stored, so it can never be out of date).

**The ledger** (`InventoryTransactions`): every change writes one row with the deltas and the values *after* the change. Rows are never edited or deleted — like a bank statement. If something is wrong you add a *correction*, you don't erase history.

| Class | Job |
|---|---|
| `InventoryRules` (Application) | Pure checks: quantity > 0, collection not in the future, expiry after collection and not already expired, adjustment rules (Damaged/Expired only decrease, never below reserved) |
| `InventoryService` | Intake, adjust, batches list (FEFO order), ledger, summary for **my** organization |
| `InventorySummaryBuilder` / `InventoryReportService` | Per-blood-group totals; admin read-only version |
| `BatchExpiryService` | Marks expired batches; invalidates their active reservation lines |

**Intake flow:** controller → `InventoryService.IntakeAsync` → `ICurrentUser.OrganizationId` (from the token) → if `clientRequestId` was seen before, return that batch → `expiryDate` empty? use collection + `Inventory.DefaultShelfLifeDays` → `InventoryRules.ValidateIntake` → create batch + `Intake` ledger row + audit → one save.

**`IsExpired(expiry, today)` is `expiry < today`:** a unit that expires today can still be used today. "Today" is the Cairo date from `IClock`.

### 3.4 Blood requests (Slice 3) — the heart of the project

**Two separate ideas:**
- **`BloodRequest`** = the *need* ("3 units of O+ for hospital X before 6 pm").
- **`Reservation`** + **`ReservationLine`s** = the *stock held* for it ("2 from batch 14, 1 from batch 17 at blood bank Y").
Keeping them separate means a reservation can be released and recreated without losing the request's history.

**`RequestRules` (pure, fully tested):**
```csharp
public static bool CanTransition(RequestStatus from, RequestStatus to) => (from, to) switch
{
    (Pending,   Approved)  => true,
    (Pending,   Rejected)  => true,
    (Pending,   Cancelled) => true,
    (Approved,  Fulfilled) => true,
    (Approved,  Cancelled) => true,
    (Fulfilled, Received)  => true,
    _ => false
};
```
```csharp
// FEFO = First Expiring, First Out
foreach (var batch in candidates.Where(c => c.Available > 0)
                                .OrderBy(c => c.ExpiryDate).ThenBy(c => c.BatchId))
{
    var take = Math.Min(batch.Available, remaining);
    result.Add(new Allocation(batch.BatchId, take));
    remaining -= take;
    if (remaining == 0) return result;
}
return null;   // not enough stock → no partial approval
```

**Approve, step by step** (`RequestService.ApproveAsync`):
1. Who am I? `org_id` from the token.
2. Load the request **tracked** (EF will save changes to it).
3. Refuse if it is my own hospital's request, if it is not Pending, or if a normal request is not verified.
4. Load my Active, non-expired batches of that blood group; compute `Available` **in memory**.
5. `AllocateFefo` → null? → `409 InsufficientStock` with the numbers.
6. For each allocation: create a `ReservationLine` and call `ReservationLedger.Reserve(...)` → `ReservedQuantity += q` **and** a `Reserve` ledger row.
7. `ChangeStatus` → status history row; request → Approved.
8. `AuditLogger.Add(...)`.
9. **One `SaveChangesAsync`** → EF writes all of it in a single transaction.
10. Two banks approving at the same second? The request's `RowVersion` or the unique index `UX_Reservations_OneLivePerRequest` stops the second → `409`.

**`ReservationLedger`** (`Infrastructure/Requests/ReservationLedger.cs`, ~60 lines) is the **only** place that moves reserved stock. Each method changes the batch numbers *and* returns the matching ledger row, so the batch and the ledger can never disagree:
- `Reserve`: Reserved +q
- `Release`: Reserved −q (cancel, or expiry → line `Invalidated`)
- `Deduct`: OnHand −q and Reserved −q (the blood leaves)

It sets `ReservationLine = line` (the navigation), not `ReservationLineId = line.Id`, because a brand-new line has no Id yet; EF fills it in during the save.

**Fulfil** (`FulfilAsync`): only the organization holding the reservation; checks `CanFulfil`; if any reserved batch has expired *now*, invalidates that line, **saves that**, and returns `409 ReservationInvalidated` ("never send expired blood"); otherwise `Deduct` for each line, reservation → Consumed, request → Fulfilled. An unverified emergency needs a reason → `EmergencyOverrides` row for admin review.

**Cancel** (`CancelAsync`): Pending → no stock effect. Approved → `Release` every active line in the same save. The admin may cancel only Pending requests (the admin never changes stock).

**Reads** (`RequestReads`): one LINQ projection (`RequestRow.Projection`) turns into one SQL query; flags are computed in C# from the settings; `onlyFlagged` applies the same rules as a SQL filter so paging stays correct. Patient name is shown only to the destination hospital and the creator.

**Database guards** (see `DATABASE_DOCUMENTATION.md`): composite foreign keys make a wrong-blood-group or wrong-bank reservation impossible; CHECKs allow only valid transitions and forbid "Fulfilled but not verified" unless an override was recorded.

### 3.5 The hourly job — `Infrastructure/Jobs/MaintenanceBackgroundService.cs`
A `BackgroundService` with a `PeriodicTimer(1 hour)`. It runs immediately at startup, then every hour:
1. expire batches (+ invalidate reservation lines),
2. anonymize old patient names.
A background service has no HTTP request, so it creates its **own DI scope** (own DbContext) for each step, and each step has its own try/catch.

### 3.6 The AI gateway — `Infrastructure/Ai/` + `Api/Controllers/AiController.cs`
The frontend calls **our** API with its JWT; our API calls the Python AI service with the secret `x-api-key`. The key never reaches the browser. `BloodFlowAiClient` uses `IHttpClientFactory` (correct connection reuse), maps snake_case JSON, and converts failures to `AiServiceException`, which the controller maps to 400/409/502/503.

---

## 4. Important design decisions (and the one-sentence why)

| Decision | Why |
|---|---|
| Clean Architecture | Rules don't depend on technology → testable, replaceable infrastructure |
| Interfaces in Application, implementations in Infrastructure | Dependency Inversion: the Api depends on contracts only |
| Result pattern instead of exceptions | Expected failures are part of the flow; each has a stable `code` |
| Organization always from the token | Nobody can act for another organization by changing a number in the body |
| `404` for other organizations' records | Don't reveal that a record exists |
| Batches + append-only ledger | Every unit is traceable; history is never rewritten |
| Available computed, not stored | Can never be stale |
| FEFO, all-or-nothing reservations | Less waste; a clear yes/no for the hospital |
| Request ≠ Reservation | The need and the stock hold have different lifecycles |
| Composite FKs, CHECKs, filtered unique indexes | The database is the last line of defence against bugs |
| Optimistic concurrency (`rowversion`) | Conflicts are rare; no table locks; the loser gets 409 |
| One `SaveChanges` per action, audit in the same save | All or nothing; no change without its audit row |
| Idempotency with `clientRequestId` | A double click or network retry never creates duplicates |
| UTC storage + Cairo "today" | One time base; expiry follows the local calendar |
| Enums stored as text | Readable data; safe if the C# enum changes order |
| Settings in a table | Business numbers change without a redeploy |
| Token re-checked on every request | Deactivation works immediately |
| Secrets in User Secrets / git-ignored file | Nothing secret ever reaches GitHub |

---

## 5. Questions a supervisor or interviewer may ask

**Architecture**
1. *What is Clean Architecture and why did you use it?* — Layers with dependencies pointing inwards; business rules in the centre don't know about SQL Server or HTTP, so they're testable and the infrastructure is replaceable.
2. *Difference from a classic 3-layer app?* — In 3-layer, business depends on data access. Here, the business layer defines interfaces and Infrastructure implements them (Dependency Inversion).
3. *What is Dependency Injection? Scoped vs Singleton vs Transient?* — The container gives a class what it needs through its constructor. Scoped = one per HTTP request (DbContext), Singleton = one for the app (clock), Transient = new each time.
4. *Why does the background job create a scope?* — It isn't inside an HTTP request, and DbContext is scoped; each run needs its own fresh DbContext.

**EF Core / database**
5. *Code First or Database First?* — Code First with migrations; the design was done first in DBML, then implemented as configurations.
6. *What is a migration? Can you edit one that's applied?* — A versioned schema change. Don't edit an applied one; add a new migration.
7. *What is a composite foreign key? Example?* — A FK on several columns; `ReservationLines(BatchId, OrganizationId, BloodGroupId)` → `BloodBatches(...)` so a wrong blood group is impossible.
8. *How do you prevent two users reserving the same units?* — `rowversion` optimistic concurrency + CHECK `OnHand >= Reserved` + unique live reservation per request.
9. *Optimistic vs pessimistic concurrency?* — Optimistic detects conflicts at save time without locking; good when conflicts are rare.
10. *What is a transaction? Where?* — All-or-nothing; each action uses one `SaveChanges`, which EF wraps in a transaction; explicit transactions where an audit row needs a new Id.
11. *Why no deletes?* — Medical records must be traceable; statuses replace deletes, FKs are Restrict.
12. *What is N+1 and how do you avoid it?* — One query per item in a loop; we use projections (`Select` into DTOs) and `Include` when loading for updates.
13. *What is a filtered unique index?* — Unique only for some rows, e.g. one *live* reservation per request while released ones don't count.

**Security**
14. *How does JWT work?* — Signed token with the user's claims; the server verifies the signature; no server session.
15. *A staff member is deactivated but their token is still valid?* — `OnTokenValidated` checks the account and organization on every request, so access stops immediately.
16. *How are passwords stored?* — ASP.NET Identity hashes them (PBKDF2 with a salt); lockout after 5 failures.
17. *Authentication vs authorization?* — Who are you (token) vs what are you allowed to do (role + ownership).
18. *Why the same message for wrong email and wrong password?* — To avoid revealing which emails have accounts.
19. *Where are the secrets?* — User Secrets locally, a git-ignored `appsettings.Production.json` on the server; never in Git.
20. *Why does the browser need CORS but Postman doesn't?* — CORS is a browser protection for pages calling another origin; Postman isn't a browser.

**Business logic**
21. *What is FEFO and why not FIFO?* — First-Expiring-First-Out; uses the units closest to expiry first, which reduces waste better than "first received".
22. *Why no partial approvals?* — A clear answer for the hospital; partial fulfilment is deferred in Phase 1.
23. *What happens if a reserved bag expires?* — The hourly job (and fulfil) invalidates that line and releases it; the request shows "needs reallocation"; expired blood is never sent.
24. *Emergency without verification?* — Possible only with a mandatory reason, recorded in `EmergencyOverrides` and reviewed by the admin. The supervisor wants each hospital to decide by policy (planned).
25. *How do you protect patient privacy?* — Name only for the destination hospital and the requester, never in audit logs, anonymized after 12 months.

**Quality**
26. *How do you test?* — 83 unit tests on the pure rules, run in GitHub Actions on every push. Next step: integration tests with a real SQL Server (Testcontainers).
27. *What would you improve?* — Integration tests, notifications, transfers, per-hospital emergency policy, rate limiting, Docker.
28. *Did you use AI tools?* — Answer honestly: yes, as an assistant; the analysis, workflows and database design are the team's work; you reviewed, ran and tested the code and can explain every part. Then prove it by explaining one hard part (e.g. composite FKs or FEFO) without notes.

---

## 6. Practise implementing it yourself

Do these **without AI**. Each one touches the same layers as a real feature. Run `dotnet test` and try it in Swagger after each.

| # | Exercise | Files to touch | Hint |
|---|---|---|---|
| 1 | Add a test: approving a `Received` request is not allowed | `tests/.../Requests/RequestRulesTests.cs` | Copy an `[InlineData]` line in `ForbiddenTransitions` |
| 2 | Add a validation: `JobTitle` can't be only digits on staff registration | `Application/Auth/RegisterStaffRequest.cs` or `AuthService` | `[RegularExpression]`, or return `Error.Validation(...)` |
| 3 | New endpoint: `GET /api/inventory/batches/expiring` (batches expiring within N days) | `InventoryController`, `IInventoryService`, `InventoryService` | Reuse `BatchQuery.ExpiringWithinDays` and the existing batch projection |
| 4 | Add a filter `governorateId` to `GET /api/admin/requests` | `RequestQuery`, `RequestReads.ApplyFilters` | `r.DestinationHospital.GovernorateId == ...` |
| 5 | Add an audit row when a request is **viewed** by the admin (then remove it — it's an exercise) | `RequestAdminService` | `_audit.Add(...)` + `SaveChangesAsync` |
| 6 | **Supervisor's request:** each hospital decides whether it allows emergency override | `Organization` entity (new bool), its configuration, a migration, an admin or hospital endpoint to change it, check in `RequestService.FulfilAsync` | Full slice in miniature: Domain → Configuration → Migration → Service rule → Controller → test |
| 7 | Slice 4 — Donors | New entities/configs/services/controllers | Follow Slice 2/3 file by file; design is in the DBML (`DonorProfile`, `Donation`, ...) |

For each exercise, before writing code, write in 3 lines: *which endpoint, which rule, which tables*. That's how real tasks start.

---

## 7. Glossary

| Term | Meaning |
|---|---|
| Entity | A C# class that maps to a table |
| DTO | A record that defines what goes in/out of the API |
| DbContext | EF Core's unit of work: tracks changes and saves them together |
| Migration | A versioned database change |
| Tracked vs `AsNoTracking` | Tracked entities are saved on `SaveChanges`; `AsNoTracking` is faster for read-only queries |
| Projection | `Select(...)` into a DTO → EF fetches only those columns |
| Result pattern | A method returns success or a typed error instead of throwing |
| ProblemDetails | Standard JSON error format (RFC 9457) |
| JWT | Signed token with the user's identity and role |
| Claim | One piece of information inside the token (`sub`, `role`, `org_id`) |
| Optimistic concurrency | Detect conflicting writes at save time using `rowversion` |
| Idempotent | Repeating the call gives the same result without side effects |
| FEFO | First Expiring, First Out |
| Append-only ledger | A table you only add rows to, never edit |
| Composite key | A key made of several columns |
| CHECK constraint | A rule the database enforces on column values |
| Filtered index | An index on only the rows matching a condition |
| CORS | Browser rule controlling which websites may call an API |
| CI | Automatic build and test on every push |
