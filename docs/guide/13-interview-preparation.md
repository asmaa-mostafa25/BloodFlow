# PART 13 — Interview, Discussion and Defense Preparation

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

Every BloodFlow answer below was checked against the code on `master`. Practise by covering the answer, saying yours out loud, then comparing.

> 🇪🇬 غطّي الإجابة، جاوبي بصوت عالي بكلامك، وبعدين قارني. المهم تفهمي الفكرة مش تحفظي الكلام.

---

## 13.1 Fifty technical questions about BloodFlow

### Overview and architecture
**1. What problem does BloodFlow solve?**
Hospitals and blood banks don't see each other's stock, so blood expires in one place while another place is short. BloodFlow gives every organization a live inventory and a shared request board. Any other hospital or blood bank with stock can approve a request, reserve the units (FEFO), send them, and the requester confirms receipt. Everything is audited.

**2. What is implemented today?**
Slice 1 covers admin work: organizations, staff registration and approval, settings, and audit logs. Slice 2 covers inventory: intake, adjustments, batches, summaries, and expiry. Slice 3 covers the hospital request lifecycle, monitoring, and emergency-override review. The project also has the AI gateway, 53 endpoints, 83 unit tests, CI, and a deployment at `https://bloodflow.runasp.net`. Donors, recipients, transfers, and notifications are planned (Slices 4–7).

**3. Describe the architecture.**
It is Clean Architecture with four projects:
- **Domain** holds entities and enums and has no dependencies.
- **Application** holds the interfaces, DTOs, pure rules, and `Result`/`Error`.
- **Infrastructure** holds EF Core, Identity, JWT, the services, the background job, and the AI client.
- **Api** holds the controllers, `Program.cs`, `CurrentUser`, and exception handling.

Dependencies point inward.

**4. Why Clean Architecture for a graduation project?**
Business rules (`RequestRules`, `InventoryRules`) can be tested without a database. Controllers stay thin. The database or the AI service can change without touching the rules. Each layer has one clear job, which also makes the project easy to explain.

**5. Why is there no repository layer?**
`DbContext` is already a unit of work, and each `DbSet` is already a repository. Another layer would only repeat EF's API. Services use `AppDbContext` directly with projections, and one `SaveChangesAsync` per action.

**6. Walk through one HTTP request end to end.**
1. `POST /api/requests/5/approve` arrives.
2. The exception handler, CORS, authentication (JWT validation plus the `OnTokenValidated` status check), and authorization (`Roles.Staff`) run in that order.
3. `RequestsController.Approve` calls `IRequestService.ApproveAsync`.
4. `RequestService` reads the caller's organization from `ICurrentUser`, loads the request, checks the rules, allocates batches with FEFO, writes ledger rows and the audit row, and calls `SaveChangesAsync` once.
5. It returns a `Result`, and `ToActionResult` turns that into 200 or a ProblemDetails error.

**7. What is the `Result` pattern and why use it?**
A method returns either a value or an `Error(Code, Message, Type)` instead of throwing for expected failures. `ResultExtensions` maps the error type to 400/401/403/404/409 ProblemDetails with a stable `code`. Exceptions are reserved for real bugs, which `GlobalExceptionHandler` turns into a 500.

**8. Why does `ICurrentUser` live in Application but `CurrentUser` in Api?**
Services need to know who the caller is, but they must not depend on HTTP. Application defines the contract. Api implements it by reading claims from `HttpContext`. This is dependency inversion.

**9. Which DI lifetimes do you use, and why?**
- `AppDbContext` and the services are **scoped**: one per HTTP request, so the request shares one unit of work.
- `IClock` (`SystemClock`) is a **singleton**, because it is stateless.
- The hourly job is a hosted service. It creates its own scope for each run, because scoped services can't be injected into a singleton.

**10. What is the middleware order and why does it matter?**
`UseExceptionHandler` → Swagger (when enabled) → `UseHttpsRedirection` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`. CORS must run before auth so that browser preflight requests get CORS headers. Authentication must run before authorization because authorization needs the user it creates.

### Authentication and authorization
**11. How does login work?**
1. `AuthService.LoginAsync` finds the user by email, refuses a locked-out account (`IsLockedOutAsync`), and checks the password with `UserManager.CheckPasswordAsync`. A wrong password calls `AccessFailedAsync`, which counts toward lockout; a correct one resets the counter.
2. Only after the password is correct does it check the account status (Pending, Rejected, Deactivated) and the organization status.
3. It then generates a JWT with `JwtTokenGenerator.GenerateToken(user, roles, organizationId)`.

**12. What is inside your JWT?**
The claims are `sub` (user id), `email`, `name`, `role`, and `org_id`, plus `exp`, issuer, and audience. It is signed with HMAC-SHA256 using `Jwt:Key`, and it lasts 60 minutes (`Jwt:ExpiryMinutes`).

**13. What happens if an admin deactivates a user whose token is still valid?**
`OnTokenValidated` runs on every request. It re-loads the user's account status and organization status, and fails authentication if either is no longer active. The old token gets 401 immediately instead of working until it expires.

**14. Why the same error for an unknown email and a wrong password?**
To avoid user enumeration. An attacker can't learn which emails exist. Both return `Auth.InvalidCredentials`.

**15. How is a user prevented from acting for another organization?**
The organization always comes from the token (`ICurrentUser.OrganizationId`), never from the request body. Services filter by it, and records of other organizations return **404**, not 403, so their existence isn't revealed.

**16. What roles exist?**
`SystemAdmin`, `HospitalStaff`, and `BloodBankStaff` are in use. `Donor` and `Recipient` are defined for later slices. `Roles.Staff` is `"HospitalStaff,BloodBankStaff"`, and the comma means "any of these".

**17. What does `[Authorize]` on both a class and a method mean?**
Both must pass (AND). For example, the class requires `Roles.Staff` and the create action additionally requires `Roles.HospitalStaff`.

**18. How are passwords stored?**
ASP.NET Core Identity hashes them with PBKDF2 and a random salt. The password policy is at least 8 characters with upper case, lower case, and a digit. Lockout is 5 failed attempts for 15 minutes.

### Database and EF Core
**19. How many tables, and which are the main ones?**
There are 20 tables, including the Identity tables. The main ones are `Organizations`, `StaffProfiles`, `BloodBatches`, `InventoryTransactions`, `BloodRequests`, `BloodRequestStatusHistory`, `Reservations`, `ReservationLines`, `EmergencyOverrides`, `AuditLogs`, and `SystemSettings`, plus the lookups `Governorates` and `BloodGroups`.

**20. Why are enums stored as strings?**
String values make the data readable in SSMS and safe if enum members are reordered. Each column has a CHECK constraint that allows only valid names. EF does the conversion with `HasConversion<string>()`.

**21. What are composite foreign keys and why use them?**
Example: a reservation line references `(BatchId, BloodGroupId, OrganizationId)` on the batch, using `HasPrincipalKey`. The database itself then makes it impossible to reserve a batch of the wrong blood group or from another organization. A service bug can't break it.

**22. How many CHECK constraints, and give an example.**
There are 38. One example: `ReservedQuantity <= OnHandQuantity` on batches. Another: `[PatientAnonymizedAt] IS NULL OR [PatientFullName] IS NULL` on requests.

**23. What is a filtered unique index? Example?**
It is a unique index that applies only to rows matching a condition. Example: `UX_Reservations_OneLivePerRequest` is unique on `RequestId` with the filter `[Status] IN ('Active', 'Consumed')`. A request can have old *Released* reservations, but only one active or consumed one.

**24. How do you handle concurrency?**
Mutable tables have a `rowversion` column (6 tables). EF adds `WHERE RowVersion = @original` to updates. If someone else changed the row first, EF throws `DbUpdateConcurrencyException`, and the service returns 409 (`Request.Concurrency`, `Inventory.Concurrency`, `Organization.Concurrency`).

**25. Two blood banks approve the same request at the same moment — what happens?**
Both try to save. The request's `RowVersion` and the unique live-reservation index let only the first one succeed. The second gets 409 and nothing is reserved twice.

**26. How are request numbers generated?**
A SQL Server sequence, `BloodRequestNumbers`, is read with `SqlQueryRaw<int>` and formatted as `BR-2026-000123`. A sequence stays unique under concurrent load, unlike `MAX()+1`.

**27. Why one `SaveChangesAsync` per action?**
Everything in the action (the status change, ledger rows, reservation, audit row) is saved in one database transaction, so either all of it is saved or none of it. When is an explicit transaction used? Only when a newly generated Id is needed before the final save. For example, `IntakeAsync` needs the new batch Id for the ledger and audit rows.

**28. How are times stored?**
Times are stored in UTC (`datetime2`). "Today" for business dates such as expiry comes from `IClock.Today` in Cairo time, so a batch doesn't expire at 2 AM Cairo time just because UTC is still on the previous day.

**29. Why `Arabic_100_CI_AI` collation?**
Searches and the duplicate-patient check then ignore case and Arabic diacritics and accents. For example, two spellings of the same name with different tashkeel compare as equal.

**30. How are migrations managed?**
There are 6 migrations in `Persistence/Migrations`. Each schema change becomes a new migration that is read before it is applied. An applied migration is never edited. Lookups are seeded with `HasData`, while the admin user and system settings are seeded at startup.

### Inventory
**31. What is a batch, and what is "available"?**
A batch is one group of units of one blood group at one organization, with collection and expiry dates. Available = `OnHandQuantity − ReservedQuantity`, for Active batches that haven't expired. It is calculated, not stored.

**32. What is the inventory ledger?**
`InventoryTransactions` is an append-only history. Every stock change writes one row: `Intake`, `Adjust`, `Reserve`, `Release`, or `Deduct`. Rows are never updated or deleted, so the stock history can always be explained.

**33. What rules apply to adjustments?**
The rules are:
- A reason is required: `Damaged`, `Expired`, or `Correction`.
- Zero is refused (`Inventory.ZeroAdjustment`).
- On-hand can't drop below reserved (`Inventory.BelowReserved`) or below zero.
- An expired batch can't be increased.
- `Damaged`/`Expired` must decrease (`Inventory.ReasonRequiresDecrease`).

**34. How is a duplicate intake avoided if the client retries?**
The client sends `clientRequestId`, which is stored on the ledger row. If the same id comes again from the same organization for the same operation type, the service returns the existing batch instead of creating a new one. If the id was already used for something else, it returns 409 `Inventory.DuplicateRequestId`. This is idempotency.

**35. What does the hourly background job do?**
`MaintenanceBackgroundService` uses a `PeriodicTimer` that fires every hour:
1. It marks batches past their expiry date as `Expired`, and releases any active reservation lines on them as `Invalidated`.
2. It anonymizes patient names older than `Privacy.PatientNameRetentionMonths` (12).

### Requests
**36. What is the request lifecycle?**
The allowed transitions are:
- `Pending` → `Approved` | `Rejected` | `Cancelled`
- `Approved` → `Fulfilled` | `Cancelled`
- `Fulfilled` → `Received`

Every other transition is refused. The rule is in `RequestRules.CanTransition`, and a database CHECK enforces it too.

**37. Who can create a request?**
Only `HospitalStaff`, and only for their own hospital (the destination). The request starts `Pending`, `Verified`, with requester capacity `HospitalStaff`. Units are limited by `Requests.MaxUnitsPerRequest` (default 10), and `neededBy` can't be in the past.

**38. What is the duplicate-request check?**
Same hospital, blood group, and patient name, still Pending or Approved, created within `Requests.DuplicateWindowHours` (24) → 409 `Request.Duplicate`.

**39. Who can approve?**
Any staff organization except the request's own hospital (`Request.OwnRequest`). That can be a blood bank or another hospital that has the stock. A Normal request must be verified first. An Emergency may be reserved even before verification (`RequestRules.CanReserve`).

**40. What is FEFO and where is it?**
First-Expiring, First-Out: take units from the batch that expires soonest, then the next one. `RequestRules.AllocateFefo` returns `null` if there isn't enough stock, so there are no partial approvals (`Request.InsufficientStock`).

**41. What happens to the stock when a request is approved, fulfilled, or cancelled?**
- **Approve:** `Reserve` ledger rows, and `ReservedQuantity` goes up.
- **Fulfil:** `Deduct` rows; both on-hand and reserved go down, and the reservation becomes `Consumed`.
- **Cancel after approval:** `Release` rows; reserved goes down and the reservation becomes `Released`.

All of these go through `ReservationLedger`.

**42. What if a reserved batch expires before it is fulfilled?**
The job (or `FulfilAsync` itself) marks the line `Invalidated` and releases it. Fulfil saves that change, then returns 409 `Request.ReservationInvalidated`. Staff call `POST /api/requests/{id}/reallocate` to top the reservation up from other batches, then fulfil again. Expired blood is never sent.

**43. What is the emergency override?**
An unverified Emergency can be fulfilled if staff provide `emergencyOverrideReason`. That creates an `EmergencyOverride` row with status `PendingReview`, and an admin later reviews it. **Status:** 🟡 The code exists, but today every request is created by hospital staff as `Verified`, so the override can only happen once patient/representative requests (Unverified) exist in a later slice. The supervisor's decision to make it a per-hospital policy is **not implemented yet**.

**44. Who can reject or cancel?**
- **Reject** (Pending only, reason required): the destination hospital, or any blood bank.
- **Cancel:** the creator, the destination hospital, the executing organization, or an admin. An admin can cancel only **Pending** requests (`Request.AdminCannotCancelApproved`), because cancelling an approved request moves stock and the admin never changes stock.

**45. How does the requester confirm receipt?**
`POST /api/requests/{id}/receive` by the destination hospital (or the creator) moves `Fulfilled` → `Received`. Pressing it twice is harmless: it just returns the details again.

**46. What monitoring flags exist?**
`RequestRules.ComputeFlags` returns:
- `IsOverdue`: Approved and past `neededBy`.
- `IsReceiptOverdue`: Fulfilled with no receipt after `Requests.ReceiptConfirmationHours`.
- `NeedsEscalation`: an Emergency still Pending after `Requests.EmergencyEscalationMinutes`.

**47. How is patient privacy protected?**
- The patient name is shown only to parties of the request.
- It is anonymized after a retention period, and a CHECK enforces that it is cleared when anonymized.
- DTOs never expose entities.

### Quality, AI, deployment
**48. How is the code tested?**
There are 83 xUnit unit tests on the pure rules: RequestRules 35, InventoryRules 23, SettingValueValidator 17, PageQuery 5, Result 3. GitHub Actions runs build and tests on each push/PR. There are no integration tests yet; that is the next quality step.

**49. How does the AI part work?**
BloodFlow is a **gateway**. `AiController` → `BloodFlowAiClient` (named `HttpClient`s from `IHttpClientFactory`, registered in `AiServiceCollectionExtensions`) → an external FastAPI service, with an `x-api-key` header from configuration. A static `SemaphoreSlim` prevents two retrain calls running at the same time. Errors become `AiServiceException`, which the controller maps to an error response.

**50. How is it deployed and where are the secrets?**
It is deployed on MonsterASP (IIS + SQL Server) through Visual Studio Web Deploy. Secrets are in User Secrets locally and in the git-ignored `appsettings.Production.json` on the server. They are never in Git. CORS origins and `Swagger:Enabled` come from configuration.

---

## 13.2 Thirty general .NET backend interview questions

1. **What is ASP.NET Core?** A cross-platform, open-source framework for web apps and APIs on .NET, with built-in DI, configuration, logging, and middleware, hosted on Kestrel (behind IIS in our deployment).
2. **What is middleware?** A component in the request pipeline that can handle a request or pass it to the next one. Its order matters.
3. **`AddScoped` vs `AddTransient` vs `AddSingleton`?** One instance per request, a new one each time it's resolved, or one for the app's lifetime. Never inject a scoped service into a singleton.
4. **What is dependency injection?** Objects receive their dependencies (usually interfaces) through the constructor instead of creating them. This gives loose coupling and makes testing easy.
5. **What does `[ApiController]` do?** It returns an automatic 400 for invalid models, infers binding sources, and requires attribute routing.
6. **`IActionResult` vs `ActionResult<T>`?** `ActionResult<T>` adds the response type for OpenAPI and allows returning either `T` or a result. We use `IActionResult` with `[ProducesResponseType]`.
7. **Authentication vs authorization?** Who you are vs what you're allowed to do.
8. **What is a JWT?** `header.payload.signature`, Base64Url-encoded and signed. It is stateless, and anyone can read it, so never put secrets in it.
9. **How do you secure secrets?** User Secrets in development; environment variables or a key vault in production; never in source control.
10. **What is EF Core?** An ORM that maps classes to tables, translates LINQ to SQL, tracks changes, and manages migrations.
11. **Code-first vs database-first?** You write the classes and generate migrations, or you generate classes from an existing database.
12. **What is change tracking?** EF remembers loaded entities and generates updates for changed properties on `SaveChanges`. `AsNoTracking` skips this for read-only queries.
13. **What is the N+1 problem?** One query for a list, then one more query per item. Fix it with projections (`Select`) or `Include`.
14. **`IQueryable` vs `IEnumerable`?** `IQueryable` builds a query that runs in the database. `IEnumerable` runs in memory after the data has been loaded.
15. **What are migrations?** Versioned C# files that change the schema step by step, recorded in `__EFMigrationsHistory`.
16. **Optimistic vs pessimistic concurrency?** Detect conflicts at save time (`rowversion`) vs lock rows while editing.
17. **What is a transaction, and what does ACID mean?** A group of operations that commits or rolls back together. ACID = Atomicity, Consistency, Isolation, Durability.
18. **What does `async/await` give a web API?** It frees threads during I/O, so the server handles more requests. Avoid `.Result` and `.Wait()`.
19. **What is `CancellationToken` for?** It stops work when the client disconnects or the app shuts down.
20. **What are the REST principles?** Resources identified by URLs, standard methods, statelessness, and proper status codes.
21. **`PUT` vs `PATCH` vs `POST`?** Replace a resource, partially update it, or create / trigger an action.
22. **401 vs 403 vs 404?** Not authenticated, authenticated but not allowed, and not found (also used to hide others' data).
23. **What is ProblemDetails?** The RFC 9457 JSON error format (`type`, `title`, `status`, `detail`, plus extensions).
24. **How do you handle exceptions globally?** `IExceptionHandler` + `UseExceptionHandler`. Log the details and return a generic 500.
25. **What is CORS?** A browser rule that blocks cross-origin calls unless the server allows that origin. It is not a server-side security wall.
26. **What is the Options pattern?** Binding a configuration section to a typed class (`IOptions<T>`).
27. **What is `BackgroundService`?** A base class for long-running hosted tasks, such as our hourly job.
28. **What is `IHttpClientFactory`?** It creates `HttpClient` instances with pooled handlers. This avoids socket exhaustion and supports typed clients.
29. **Unit vs integration tests?** Testing one unit in isolation vs testing components together (real DB, HTTP pipeline via `WebApplicationFactory`).
30. **What is SOLID?** Single responsibility, Open/closed, Liskov substitution, Interface segregation, Dependency inversion (Part 9 §9.1).

> 🇪🇬 الأسئلة العامة دي بتتسأل في أي إنترفيو Junior .NET. جاوبي عليها وانتي بتدي مثال من BloodFlow. ده بيفرق جداً.

---

## 13.3 Twenty questions a supervisor might ask

| # | Question | Suggested answer (short) |
|---|---|---|
| 1 | What exactly did *you* build? | The backend: design of the database (from the DBML), the API, the business rules, auth, deployment, and the team integration. I used AI as an assistant/mentor and I'm studying every part so I can explain and extend it myself. |
| 2 | Why ASP.NET Core and SQL Server? | Mature, fast, strong typing, built-in DI and Identity; SQL Server gives constraints, sequences, rowversion, and Arabic collation. |
| 3 | How do you guarantee stock never goes negative? | Three levels: rules in code, the single ledger path (`ReservationLedger`), and CHECK constraints in the database. |
| 4 | What if two hospitals take the same units at the same time? | `rowversion` plus the unique live-reservation index: one wins, the other gets 409. |
| 5 | How do you avoid sending expired blood? | FEFO, the hourly expiry job, and the fulfil-time check that invalidates expired lines. |
| 6 | What is the difference between reserved and deducted? | Reserved = promised to a request but still physically here. Deducted = sent out. |
| 7 | Why not allow partial approval? | Phase 1 rule: a request is covered fully or not at all, so the requester knows whether to look elsewhere. |
| 8 | How is data of each hospital isolated? | Organization from the token, filtered queries, 404 for others' records. |
| 9 | How do you audit actions? | `AuditLogger.Add` before every save: actor, organization, action, old/new values, time. The admin reads them via `/api/admin/audit-logs`. |
| 10 | How do you handle emergencies? | Emergency type, reserve before verification, escalation flag, override with mandatory reason and admin review. The per-hospital policy you requested is my next task. |
| 11 | How is the system configurable without redeploying? | `SystemSettings` table (max units, windows, retention, …) edited by the admin with validation. |
| 12 | What about patient privacy? | Name visible only to parties; anonymized after 12 months; DTOs only. |
| 13 | How did you test it? | 83 unit tests on rules plus manual Swagger scenarios; CI runs them. Integration tests are planned. |
| 14 | What are the limitations? | No donors/recipients/transfers/notifications yet; no integration tests; no rate limiting; see Part 11. |
| 15 | How does the AI module connect? | Gateway pattern; separate FastAPI service; API key; the backend only forwards and validates. |
| 16 | How would it scale? | Stateless API (JWT) → multiple instances; indexes on hot queries; paging; the job would need a single-runner lock. |
| 17 | What happens if the AI service is down? | The core system keeps working; only AI endpoints return an error. |
| 18 | How did you organize the team? | Backend contract via Swagger and `API_DOCUMENTATION.md`; CORS for frontends; deployed URL for integration. |
| 19 | What did you learn? | Clean Architecture, EF Core constraints and concurrency, JWT, deployment and debugging from logs. |
| 20 | What would you do with more time? | Slices 4–7, integration tests, rate limiting, notifications, per-hospital override policy. |

---

## 13.4 Architecture decisions — how to explain them

| Decision | Why | Trade-off |
|---|---|---|
| Clean Architecture | Testable rules, clear layers | More projects and files |
| No repository layer | `DbContext` is already Unit of Work | Services depend on EF |
| `Result` instead of exceptions | Explicit, cheap, predictable HTTP mapping | More `if (error) return` lines |
| Enums as strings + CHECK | Readable and safe | Slightly larger columns |
| Composite FKs | Integrity enforced by the DB | More complex configuration |
| Append-only ledger | Full stock history, explainable | More rows |
| Single save per action | Atomic, consistent | Explicit transactions where new Ids are needed |
| JWT + status re-check | Stateless but deactivations apply immediately | One small query per request |
| Settings in DB | Admin changes rules without a deploy | Must validate values (`SettingValueValidator`) |
| AI as external service | Different language/tooling (Python), independent deploy | Network dependency |

> 🇪🇬 لما الدكتور يسأل "ليه عملتي كده؟" قولي: السبب + العيب اللي قبلناه. ده بيبين إنك فاهمة مش حافظة.

---

## 13.5 Common mistakes in a graduation defense
1. Claiming features that aren't implemented (donors, notifications). Say "planned" honestly.
2. Reading slides instead of explaining.
3. Not being able to open the code at the exact place being discussed. Know the paths of `RequestService`, `RequestRules`, `ReservationLedger`, and `DependencyInjection.cs`.
4. A live demo with no prepared data, or with an expired token. Prepare the accounts and the token beforehand.
5. Saying "AI wrote it" or, the opposite, pretending nothing was assisted. Be honest and show understanding.
6. Answering a different question when unsure. It is fine to say "I'm not sure; I would check X."
7. Too much theory, no numbers. Use the facts: 53 endpoints, 83 tests, 20 tables, 38 CHECKs, 6 migrations.
8. Showing secrets on screen (`appsettings.Production.json`, User Secrets).

## 13.6 How to present professionally
1. **Problem (1 min):** expiring blood vs shortages, no visibility.
2. **Solution (1 min):** shared inventory and request board, FEFO reservations, audit.
3. **Architecture (2 min):** one diagram (Part 3), the four layers, the request path.
4. **Database (2 min):** the ERD, and why composite FKs and the ledger exist.
5. **Live demo (5 min):** log in as a hospital → create an Emergency request → log in as a blood bank → see it on the board → approve (show the reservation) → fulfil → the hospital receives → the admin sees the audit log.
6. **Quality (1 min):** tests, CI, concurrency, security.
7. **Status and next steps (1 min):** what's done, what's planned, the supervisor's override policy.
8. **Questions:** listen fully, repeat the question, answer briefly, and show the code if useful.

**Demo checklist:** API running (local or `https://bloodflow.runasp.net/swagger`), seeded admin, one approved hospital account and one approved blood bank account, stock already added to the blood bank, Swagger tabs open, no secrets visible.

> 🇪🇬 العرض قصة: مشكلة → حل → إزاي اتبنى → ديمو → إيه الجاي. اتدربي على الديمو 3 مرات قبلها، وجهزي الحسابات والمخزون.
