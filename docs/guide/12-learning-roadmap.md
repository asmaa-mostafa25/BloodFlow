# PART 12 — Your Personal Backend Learning Roadmap

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md) · Companion: [`LEARNING_GUIDE.md`](../../LEARNING_GUIDE.md)

**Goal:** understand, explain, debug and extend BloodFlow without AI-generated code.
**Method for every topic:** read the concept → find it in the code → do the exercise **without AI** → answer the questions out loud → move on only when you can do the "independently" item.

> 🇪🇬 الخطة دي مبنية على كود BloodFlow نفسه. كل موضوع: تفهميه، تلاقيه في الكود، تعملي تمرين بإيدك، وتجاوبي أسئلة. ماتنتقليش للي بعده غير لما تقدري تعمليه لوحدك.

---

## 12.0 What you must understand first (week 1)
1. **The request pattern:** Controller → Interface → Service → Rules/DbContext → `Result` → HTTP (Part 3 §3.7, `LEARNING_GUIDE.md` §1).
2. **The debugger:** F9 breakpoint in `AuthController.Login`, F5, call login in Swagger, F10/F11 through `AuthService.LoginAsync`. Repeat for `InventoryService.IntakeAsync` and `RequestService.ApproveAsync`.
3. **The data model:** draw the ERD (Part 4 §4.2) on paper from memory.

---

## 12.1 C# concepts to revise

| Concept | Where in BloodFlow |
|---|---|
| Classes, records, `init`, `required` | DTO records (`RequestDtos.cs`), entities with `required string Name` |
| Interfaces and implementation | `IRequestService` / `RequestService` |
| Access modifiers (`public`, `internal`, `sealed`, `protected`) | `internal sealed class RequestService`, `protected Result(...)` |
| Nullable types (`int?`, `string?`, `!`, `null!`) | `ICurrentUser.OrganizationId`, navigations `= null!` |
| Enums | `Domain/Enums` |
| `switch` expressions, pattern matching (`is not (A or B)`, property patterns) | `RequestRules.CanTransition`, `GlobalExceptionHandler` |
| LINQ (`Where`, `Select`, `OrderBy`, `GroupBy`, `Any`, `Sum`) | services, `AllocateFefo` |
| `async`/`await`, `Task<T>`, `CancellationToken` | every service method |
| Extension methods | `AddInfrastructure`, `ToActionResult` |
| `out` parameters, tuples and deconstruction | `TryGetCaller(out ...)`, `LoadForExecutorAsync` |
| Implicit operators, generics | `Result<T>` |
| Expression trees | `RequestRow.Projection` |
| Static classes | `RequestRules`, `ReservationLedger` |

**Exercise:** write a console app with a record `Batch(int Id, DateOnly Expiry, int Available)` and implement FEFO allocation yourself, then compare with `RequestRules.AllocateFefo`.
**Questions:** What's the difference between a `record` and a `class`? Why `RequestType?` + `[Required]`? What does `= null!` tell the compiler? Why can't `Result<T>` implicitly convert from an interface type?
**Independently:** write `Result`-returning methods with guard clauses; write LINQ queries with projections.

---

## 12.2 ASP.NET Core concepts to master

| Concept | Where |
|---|---|
| Hosting & `Program.cs` | `Program.cs` |
| Dependency Injection & lifetimes | `DependencyInjection.cs` |
| Middleware order | `Program.cs` pipeline |
| Controllers, routing, route constraints | `Controllers/*`, `{id:int}` |
| Model binding (`[FromQuery]`, body) | `Board([FromQuery] BoardQuery query)` |
| `[ApiController]` automatic 400 | all controllers |
| Configuration & Options | `JwtSettings`, `Swagger:Enabled`, `Cors:AllowedOrigins` |
| ProblemDetails & exception handling | `ResultExtensions`, `GlobalExceptionHandler` |
| Authentication/Authorization | JWT setup, `[Authorize(Roles=...)]` |
| CORS | `AddCors`/`UseCors` |
| Background services | `MaintenanceBackgroundService` |
| HttpClientFactory | AI client |
| OpenAPI/Swagger | `AddOpenApi`, `BearerSecuritySchemeTransformer` |

**Exercise:** add `GET /api/lookups/request-types` that returns the names of `RequestType` (anonymous). Then add `app.MapHealthChecks("/health")` on a branch.
**Questions:** Why is `UseCors` before `UseAuthentication`? What does `[ApiController]` do? Scoped vs Singleton — give one BloodFlow example of each. What happens if two `[Authorize]` attributes are on a class and a method?
**Independently:** create a new controller with 3 endpoints, correct roles and Swagger annotations.

---

## 12.3 EF Core concepts to practise

| Concept | Where |
|---|---|
| DbContext, DbSet, change tracking | `AppDbContext`, services |
| Fluent API configurations | `Persistence/Configurations/*` |
| Relationships, composite keys, `HasPrincipalKey` | `ReservationConfiguration` |
| Value conversions (enum → string) | every configuration |
| Indexes, filtered indexes, CHECK constraints | configurations |
| Migrations & snapshot | `Persistence/Migrations` |
| Seeding (`HasData` vs startup seeder) | Governorate/BloodGroup configs, `IdentitySeeder`, `SystemSettingSeeder` |
| `AsNoTracking`, projections, `Include` | reads vs writes in `RequestService` |
| Concurrency (`IsRowVersion`, original value) | `OrganizationService.UpdateAsync` |
| Transactions | `IntakeAsync`, `CreateAsync`, `RegisterStaffAsync` |
| `ExecuteUpdateAsync`, raw SQL | `RequestMaintenanceService`, `NextRequestNumberAsync` |

**Exercise:** on a branch, add a nullable `Notes` column (`nvarchar(300)`) to `Organizations`: entity → configuration → migration (read it) → update DTO/service/controller → test → then `dotnet ef migrations remove` and discard the branch.
**Questions:** What does `SaveChangesAsync` do with tracked entities? Why add the ledger row via `batch.Transactions.Add(...)`? When do we need an explicit transaction? Why is the settings seeder not `HasData`? What SQL does a `rowversion` update generate?
**Independently:** design an entity with relationships, write its configuration and migration, and query it with projections.

---

## 12.4 SQL concepts you need

| Concept | Where to see it |
|---|---|
| `SELECT`, `WHERE`, `JOIN`, `GROUP BY`, `ORDER BY`, `TOP` | Turn on EF logging or read queries in SSMS |
| PK, FK, unique, CHECK | SSMS → table → Keys/Constraints |
| Indexes and execution plans | SSMS "Include Actual Execution Plan" on the board query |
| Transactions (`BEGIN/COMMIT/ROLLBACK`) | `IntakeAsync` |
| Sequences | `BloodRequestNumbers` |
| Collation | `Arabic_100_CI_AI` |
| Data types (`date`, `datetime2`, `varchar` vs `nvarchar`, `rowversion`) | `DATABASE_DOCUMENTATION.md` §6 |

**Exercise (SSMS):** write SQL to list, per organization and blood group, `SUM(OnHandQuantity - ReservedQuantity)` for Active, non-expired batches — the same as `GET /api/inventory/summary`'s `available`. Then try to `UPDATE BloodBatches SET ReservedQuantity = OnHandQuantity + 1` on one row and read the CHECK error.
**Questions:** Why `varchar` for enums but `nvarchar` for names? What does a filtered unique index allow that a normal one doesn't? Why is `Available` not a column?
**Independently:** read and explain any migration file and any EF-generated query.

---

## 12.5 Authentication concepts you need

| Concept | Where |
|---|---|
| Hashing vs encryption, salts | Identity `PasswordHasher` |
| Identity: `UserManager`, roles, lockout | `AuthService`, `IdentitySeeder` |
| JWT structure, claims, signing, expiry | `JwtTokenGenerator`, jwt.io |
| Token validation parameters | `DependencyInjection.cs` |
| Role-based + resource-based authorization | `[Authorize]` + `TryGetCaller`, `CanSeeAsync` |
| Secrets management | User Secrets, `appsettings.Production.json` |

**Exercise:** log in, paste the token into jwt.io, find `sub`, `role`, `org_id`, `exp`. Change one character of the payload and call an endpoint — explain the 401. Deactivate a staff user as admin and call an endpoint with their old token — explain the 401.
**Questions:** Why can't a user change `org_id` in their token? Why the same message for unknown email and wrong password? What is `ClockSkew`? Why is `org_id` read from the token and not from the request body?
**Independently:** add a new role-protected endpoint and explain every line of the JWT setup.

---

## 12.6 Architecture concepts you need

| Concept | Where |
|---|---|
| Clean Architecture, dependency rule | Part 3 |
| SOLID, separation of concerns | Part 9 |
| Result pattern | `Result`, `Error` |
| DTO vs entity | Part 9 §9.11 |
| Idempotency, optimistic concurrency, append-only ledger | inventory and requests |
| Composition root | `Program.cs`, `AddInfrastructure` |

**Exercise:** explain in writing (one page) why `ICurrentUser` lives in Application but `CurrentUser` in Api. Then draw the request lifecycle sequence for `cancel` of an Approved request from memory and check it against Part 8.
**Questions:** Where would a new business rule go? What breaks if Application references Infrastructure? Why no repository layer?
**Independently:** place any new piece of code in the right layer without hesitation.

---

## 12.7 Practice projects inside BloodFlow (ordered)

| # | Task | Skills |
|---|---|---|
| 1 | Add a test: `(Received → Approved)` is forbidden | xUnit, rules |
| 2 | `[StringLength(100)]` on `Search` query properties + test in Swagger | validation |
| 3 | Validate `Jwt:Key` length at startup | configuration, security |
| 4 | `governorateId` filter on `GET /api/admin/requests` | LINQ, DTOs |
| 5 | Pass `CorrelationId` into `AuditLogger.Add` and use it in approve/fulfil | refactoring across layers |
| 6 | **Per-hospital emergency override policy** (supervisor) | full slice: entity → migration → service rule → endpoint → test |
| 7 | One integration test with `WebApplicationFactory` for `POST /api/auth/login` | testing |
| 8 | **Slice 4 — Donors** from the DBML | everything |

**Definition of "done" for each task:** builds with 0 warnings, `dotnet test` green, tried in Swagger (success + each error), committed on a branch, PR merged, and you can explain every changed line.

---

## 12.8 Suggested schedule
| Weeks | Focus |
|---|---|
| 1 | 12.0 + 12.1 + tasks 1–2 |
| 2 | 12.2 + 12.5 + task 3 |
| 3 | 12.3 + 12.4 + task 4 |
| 4 | 12.6 + tasks 5–6 |
| 5–6 | task 7 + Slice 4 design |
| 7–9 | Slice 4 implementation |

**Free, reliable resources:** Microsoft Learn (ASP.NET Core fundamentals, EF Core docs, "Enable CORS", "Authentication and authorization in ASP.NET Core", "Use Identity"), the official C# guide, and SQL Server docs. Search by the concept name used in this guide.
