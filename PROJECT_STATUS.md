# BloodFlow — Project Status

**Date:** 27 November 2026 · **Branch:** `master` · **Scope:** backend (ASP.NET Core API + SQL Server)

"Verified" below means one of: checked in the source code, passed in CI, or seen working on the running API. Anything not verified says so.

---

## 1. Summary

| Area | Status |
|---|---|
| Phase 1 business workflows (5 roles) | ✅ Documented (Arabic Word file in the Claude project) |
| Phase 2 database design (36 tables) | ✅ Designed (DBML + ERD); **20 tables implemented** |
| Slice 1 — System Admin | ✅ Implemented |
| Slice 2 — Blood bank / hospital inventory | ✅ Implemented |
| Slice 3 — Hospital blood requests, reservations, emergencies | ✅ Implemented |
| AI service integration (backend gateway) | ✅ Implemented (needs the external AI service running) |
| Deployment | ✅ API running on MonsterASP (`https://bloodflow.runasp.net`) |
| Slice 4 — Donors | ❌ Not started |
| Slice 5 — Recipients (patient requests) | ❌ Not started |
| Slice 6 — Transfers between organizations | ❌ Not started |
| Slice 7 — Notifications | ❌ Not started |

**Build and tests (verified in CI on `master`):** build in Release with **0 warnings**, **83/83 unit tests passing**.
**Runtime (verified):** locally the API starts, creates the database and opens Swagger; on the server `GET /api/lookups/blood-groups` returns the 8 blood groups and `/swagger` opens.

---

## 2. Fully implemented

### Slice 1 — System Admin
- Staff self-registration (Pending) and login with JWT.
- Admin: approve / reject / deactivate / reactivate staff; deactivation blocks the next request immediately (token check on every request).
- Organizations: create, update (with `rowVersion`), deactivate (blocked if it has active reservations or open requests), activate.
- System settings with type validation and approval reference for medical parameters.
- Audit log for every administrative and business action, with filters.
- Lookups (governorates, blood groups, active organizations).
- Lockout after 5 failed logins (15 minutes).

### Slice 2 — Inventory
- Batch intake with default shelf life, adjustments (Damaged / Expired / Correction) with mandatory reason.
- Append-only stock ledger; `Available = OnHand − Reserved`; FEFO ordering.
- Inventory summary (near-expiry, expired); admin read-only report per organization.
- Hourly job marks expired batches.
- Idempotency (`clientRequestId`) and optimistic concurrency on batches.

### Slice 3 — Requests
- Hospital creates normal/emergency requests (auto-verified), with request numbers from a SQL sequence, duplicate protection and idempotency.
- Board for other organizations (same governorate by default, emergencies first, available units shown).
- Approve = reserve all units with FEFO (no partial approvals); one live reservation per request (database-enforced).
- Fulfil (deducts stock, refuses expired units), reallocate after expiry, receive (idempotent), reject, cancel (releases stock).
- Status history, flags (overdue, receipt overdue, emergency escalation), admin monitoring.
- Patient name privacy (visible to destination hospital and creator only, never in audit logs, anonymized after 12 months for closed requests).
- Composite foreign keys and CHECK constraints guarantee same organization and blood group for every reserved batch.

### Platform
- Clean Architecture, Result pattern, ProblemDetails errors with stable codes, global exception handler.
- CORS from configuration, Swagger switchable by configuration, production config template, git-ignored secrets.
- GitHub Actions CI.

---

## 3. Partially implemented

| Item | What exists | What is missing |
|---|---|---|
| Emergency override | Fully coded: reason, `EmergencyOverrides` table, admin review. | **Currently unreachable**: every request is created by hospital staff and is Verified at creation, so an *unverified* emergency cannot exist until patient requests (Slice 5) are added. |
| Supervisor's decision on emergency override | — | The supervisor asked that each **hospital decides by policy** whether it allows the override. **Not implemented.** Today the rule is the same for all hospitals. Planned change: a per-hospital setting checked in `RequestService.FulfilAsync`. |
| `verify` endpoint | Works. | No unverified requests exist yet (see above). |
| Donor / Recipient roles | Roles are seeded; `RequesterCapacity` has Patient/Representative; donor settings exist. | No endpoints or tables for donors/recipients. |
| Escalation and alerts | Flags are computed and filterable (`onlyFlagged`). | Nobody is notified (no email/in-app notifications). |
| AI integration | Backend endpoints, error mapping, API key header. | Depends on the separate FastAPI service. On the server `BloodFlowAi:BaseUrl` is not configured for a hosted AI service, so AI endpoints are **not expected to work there yet** (not tested on the server). |
| Tests | 83 unit tests on pure rules (FEFO, transitions, flags, inventory rules, settings validation, Result, paging). | No integration tests for services, controllers or database constraints. |

---

## 4. Not implemented

- Donors: profile, donation recording/verification, eligibility, points and badges (Phase 1 D1–D4).
- Recipients: patient/representative requests and tracking (R1–R3, H2 for their requests), abuse tracking for rejected unverified emergencies.
- Transfers between organizations, shortage alerts (Phase 1 section 5).
- Notifications (email and in-app).
- Email confirmation, password reset, refresh tokens.
- Rate limiting, health-check endpoint for the API itself, structured logging/monitoring.
- Docker / containerised deployment.
- Per-hospital emergency override policy (supervisor request).

---

## 5. Known problems and risks

| # | Problem | Impact | Action |
|---|---|---|---|
| 1 | **Secrets were pasted in chat**: the server database password and the server JWT key. | Anyone with the chat could access the database or forge tokens. | DB password was changed. **Change the server `Jwt:Key` and the admin password if not done yet**, then publish again. |
| 2 | Swagger is enabled on the public server (`Swagger:Enabled = true`). | Anyone can see the API surface (data still needs a token). | Set it to `false` after the graduation demo. |
| 3 | Old branches on GitHub could not be deleted from the AI session (permission). | Clutter only; all are merged. | Delete them on GitHub: every branch except `master`. |
| 4 | Any `BloodBankStaff` can reject any pending request (as designed from Phase 1 step 3). | One bank could reject a request others could fulfil. | Confirm the rule with the supervisor; possibly restrict. |
| 5 | Emergency override review has no `RowVersion`. | Two admins reviewing at the same moment: last write wins (low impact). | Add a row version if it matters. |
| 6 | Duplicate-request check compares the patient name text. | A different spelling bypasses it. | Acceptable for v1; a patient identifier would fix it. |
| 7 | Free hosting limits (MonsterASP free plan): app sleeps when idle, 1 GB database, one site. | First request after idle is slow. | Open the site a minute before a demo. |
| 8 | The hourly job runs inside the API process. | If the API is scaled to several instances, each runs the job (idempotent, but duplicated work). | Fine on one instance; use a distributed lock or Hangfire if scaled. |
| 9 | The `vulnerable packages` CI step only prints results. | A vulnerable package would not fail CI. | Read the step log, or make it fail on findings. |
| 10 | Visual Studio 2022 is not officially supported for .NET 10. | Builds may warn or fail in VS 2022. | Use Visual Studio 2026 or the `dotnet` CLI. |

**Build/runtime errors:** none known on `master` at the time of writing. Errors met during setup and deployment, and their fixes, are listed in `SETUP_GUIDE.md` §8 and `README.md` → Deployment.

---

## 6. Remaining tasks (prioritized)

**P0 — now (safety and the demo)**
1. Rotate the server `Jwt:Key`; change the admin password; publish again.
2. Delete merged branches on GitHub.
3. Tell the Frontend team their exact local/deployed URLs → add them to `Cors:AllowedOrigins` on the server.
4. Understand Slices 1–3 using `LEARNING_GUIDE.md` (needed for the supervisor and the defense).

**P1 — next features**
5. Per-hospital emergency override policy (supervisor decision) — small, good first independent change.
6. Slice 4 — Donors (write it yourself, following the Slice 2/3 pattern).
7. Integration tests (Testcontainers + SQL Server) for the request lifecycle and constraints.

**P2 — then**
8. Slice 5 — Recipients (unlocks verify + emergency override).
9. Slice 6 — Transfers and shortage alerts.
10. Slice 7 — Notifications.
11. Host the AI service and configure `BloodFlowAi:BaseUrl` / `ApiKey` on the server.

**P3 — polish**
12. Turn Swagger off in production after the demo; rate limiting; password reset; Docker.
