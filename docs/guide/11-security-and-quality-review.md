# PART 11 — Security and Code Quality Review

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

A review of the code on `master`. Each finding has **evidence** (file/behaviour), a **severity** (High / Medium / Low / Info) and a **practical recommendation**. Nothing here was changed in code — these are tasks for you.

> 🇪🇬 دي مراجعة للكود: إيه الكويس، وإيه المشاكل الحقيقية، وتصلحيها إزاي. مفيش تعديل اتعمل في الكود، دي مهام ليكي.

---

## 11.1 Summary

| Area | Verdict |
|---|---|
| Authentication | Good base (Identity hashing, lockout, full JWT validation, status re-check). Gaps: no rate limiting, key length not validated, registration reveals existing emails. |
| Authorization | Good: roles + organization ownership + 404 for others' data. One rule possibly too broad (reject). |
| Secrets | None in Git (history scanned). Two server secrets were exposed in chat → rotate. |
| SQL injection | No risk found: EF parameterizes everything; one raw SQL is a constant. |
| Input validation | Good (attributes + rules + DB constraints). Minor gaps (search length). |
| Data integrity | Strong (constraints, composite FKs, concurrency, single save). Small gaps (override review concurrency). |
| Error handling | Consistent ProblemDetails; no stack traces to clients. |
| Performance | Fine for the expected load; one extra query per authenticated request. |
| Maintainability | Clear layering and conventions; `RequestService` is large. |
| Tests | 83 unit tests on rules; **no** integration/API tests. |

---

## 11.2 Authentication

| # | Finding | Evidence | Severity | Recommendation |
|---|---|---|---|---|
| A1 | **No rate limiting** on login/registration | No `AddRateLimiter` in the code | Medium | Add ASP.NET Core rate limiting (fixed window per IP) on `/api/auth/*`. |
| A2 | `Jwt:Key` length not validated | `DependencyInjection.cs` only checks `IsNullOrWhiteSpace`; a short key makes HMAC-SHA256 signing fail at login | Medium | At startup: `if (Encoding.UTF8.GetByteCount(key) < 32) throw ...`. |
| A3 | **Email enumeration via registration** | `RegisterStaffAsync` returns `409 Registration.EmailTaken`, while login deliberately hides whether an email exists | Low | Accept for v1 (staff accounts), or return a neutral message and handle duplicates by email. |
| A4 | Account lockout can be abused to lock someone out | Identity lockout 5 / 15 min | Low (known trade-off) | Combine with rate limiting by IP (A1). |
| A5 | No refresh tokens / revocation list | 60-min access tokens only | Info | Fine for v1; status re-check already blocks deactivated users. |
| A6 | Password policy has no special-character rule | `RequireNonAlphanumeric = false` | Info | Acceptable; length ≥ 8 + mixed case + digit. Consider 10+. |
| A7 | **Good:** same error for unknown email / wrong password; status revealed only after a correct password; `ClockSkew` 30 s; `OnTokenValidated` re-check | `AuthService`, `DependencyInjection` | — | Keep. |

## 11.3 Authorization

| # | Finding | Evidence | Severity | Recommendation |
|---|---|---|---|---|
| Z1 | Any `BloodBankStaff` can reject **any** pending request | `RequestService.RejectAsync`: `allowed = DestinationHospitalId == orgId \|\| Role == BloodBankStaff` | Medium (business) | Confirm with the supervisor; option: only the destination hospital rejects, banks simply don't approve. |
| Z2 | Any staff member can open any **Pending** request's details (needed for the board) | `CanSeeAsync` | Low | Acceptable; patient name is already hidden from non-parties. |
| Z3 | **Good:** organization always from the token; 404 for other organizations' batches/requests; admin can't change stock or cancel approved requests | services | — | Keep. |
| Z4 | Class + method `[Authorize]` are ANDed — documented and handled | `RequestsMonitorController` cancel route | — | Remember this when adding endpoints. |

## 11.4 Exposed secrets

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| S1 | No secrets in the repository or its history (scanned for passwords, keys, connection strings); `.http` uses placeholders; `appsettings.Production.json` and `publish/` git-ignored | — | Keep checking `git status` before commits. |
| S2 | The **server JWT key** and **database password** were pasted in a chat | **High** | DB password: changed. **Rotate `Jwt:Key` and the admin password on the server, then publish.** |
| S3 | The production secrets live in a plain file on the host | Low | Normal for this host; restrict who has panel access. |

## 11.5 SQL injection

- All queries are LINQ → EF Core sends **parameters**, never concatenated values. Search filters (`StaffAdminService`, `OrganizationService`) use `Contains(term)` → parameterized `LIKE`.
- The only raw SQL: `SqlQueryRaw<int>(NextNumberSql)` where `NextNumberSql` is a **compile-time constant** (`"SELECT NEXT VALUE FOR dbo." + BloodRequestConfiguration.NumberSequence + " AS [Value]"`) with no user input.
**Verdict:** no SQL injection risk found. **Rule for the future:** never build SQL strings from input; if you need raw SQL, use `FromSqlInterpolated`/parameters.

> 🇪🇬 الـ SQL Injection إن حد يكتب كود SQL في خانة إدخال. احنا بنستخدم EF وهو بيبعت القيم كـ Parameters، فمفيش خطر.

## 11.6 Input validation

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| V1 | `Search` query strings have no maximum length | Low | Add `[StringLength(100)]` to `StaffQuery.Search` and `OrganizationQuery.Search`. |
| V2 | `neededBy` has no upper bound (e.g. year 2099 accepted) | Low | Optional rule: within 30 days. |
| V3 | Duplicate-request check depends on patient name spelling | Low | Later: a patient identifier. |
| V4 | **Good:** DTO attributes, nullable+`[Required]` enums, rule classes, CHECK constraints, idempotency keys | — | Keep. |

## 11.7 Data integrity

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| D1 | `EmergencyOverride` has no `RowVersion` → two admins reviewing at once: last wins | Low | Add a row version (new migration). |
| D2 | `AuditLog.CorrelationId` column exists but `AuditLogger.Add` never sets it | Low | Add an optional `correlationId` parameter so audit rows link to ledger rows. |
| D3 | Fulfil saves the invalidation **then** returns 409 | — (intended) | Documented behaviour from Phase 1 ("prevent and mark invalid"). |
| D4 | **Good:** composite FKs, 38 CHECKs, filtered unique indexes, `rowversion`, one `SaveChanges`, append-only ledger, transitions enforced in DB | — | Keep. |

## 11.8 Error handling

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| E1 | **Good:** expected errors are `Result`; unexpected → `GlobalExceptionHandler` (500, logged with traceId, no stack trace); concurrency/duplicates → 409 | — | Keep. |
| E2 | Startup failures (missing config, DB unreachable) only show as IIS 500.30 | Info | Add a startup log line and use the stdout log procedure; later structured logging. |
| E3 | `IdentitySeeder` error text says "user secrets" even on the server | Info | Reword to "configuration". |

## 11.9 Performance

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| P1 | `OnTokenValidated` runs one small primary-key query per authenticated request | Low | Fine; if traffic grows, cache status for ~30 s. |
| P2 | Lists use projections and paging (max 100) and have matching indexes | — | Keep. |
| P3 | Board query + an extra grouped query for availability | Low | Fine at this scale. |
| P4 | Hourly job loads all due batches into memory | Low | Fine (few per hour). |

## 11.10 Maintainability

| # | Finding | Recommendation |
|---|---|---|
| M1 | `RequestService` is the largest file (whole lifecycle) | If it grows, split into `RequestCommands` (create/approve/fulfil/…) and keep reads in `RequestReads`. |
| M2 | Conventions are consistent and written down (`CLAUDE.md`, `ARCHITECTURE.md`) | Keep following them. |
| M3 | `System.Security.Cryptography.Pkcs` referenced without a comment | Verify if still needed; add a comment why. |
| M4 | Swagger enabled on the public server | Set `Swagger:Enabled=false` after the demo. |
| M5 | No HSTS (`UseHsts`) | Add `app.UseHsts()` in non-Development once HTTPS is stable. |
| M6 | `AllowedHosts: "*"` | Set to `bloodflow.runasp.net;localhost` in production. |

## 11.11 Missing tests

| What | Why it matters | Suggested tool |
|---|---|---|
| Service tests (approve, fulfil, cancel, intake, adjust) | They contain most of the workflow | xUnit + Testcontainers (real SQL Server) |
| Database constraint tests (composite FKs, CHECKs, unique live reservation) | Prove the "impossible by design" claims | Testcontainers |
| Concurrency tests (two approvals at once) | Prove 409 behaviour | Testcontainers, two DbContexts |
| API tests (status codes, auth) | Contract for the frontend | `WebApplicationFactory<Program>` |
| Auth tests (lockout, deactivated user blocked) | Security | `WebApplicationFactory` |
| CI step that fails on vulnerable packages | Today it only prints | add `--vulnerable` output check |

---

## 11.12 Prioritized fixes
1. **Rotate** server `Jwt:Key` and admin password (S2).
2. Validate `Jwt:Key` length at startup (A2).
3. Rate limiting on auth endpoints (A1).
4. Decide the reject rule with the supervisor (Z1).
5. Integration tests for the request lifecycle (11.11).
6. Small hardening: search length, override row version, correlation id in audit, HSTS, AllowedHosts, Swagger off after demo.
