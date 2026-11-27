# PART 7 — API and HTTP Fundamentals + every BloodFlow endpoint

[← Master guide](../../BLOODFLOW_COMPLETE_MASTER_GUIDE.md)

---

## 7.1 What is an API?
An **Application Programming Interface** is a set of operations one program offers to others. BloodFlow's API is a list of URLs; the frontend calls them to read and change data.
> 🇪🇬 الـ API هو الباب اللي الفرونت بيكلّم بيه الباك إند: لينكات، كل لينك بيعمل حاجة.

## 7.2 What is REST?
An architectural style for web APIs:
- **Resources** have URLs: `/api/requests`, `/api/requests/15`.
- **HTTP methods** say what to do: GET read, POST create/act, PUT replace, PATCH partial update, DELETE remove.
- **Stateless:** every request carries what's needed (our JWT); the server keeps no session.
- **Status codes** describe the result.
BloodFlow follows REST and uses **action endpoints** for business transitions (`POST /api/requests/15/approve`) because approving is more than changing a field — it reserves stock, writes history and audit.

## 7.3 What is HTTP?
The protocol between client and server. A **request** = method + URL + headers + optional body. A **response** = status code + headers + optional body.

```
POST /api/auth/login HTTP/1.1
Host: localhost:7244
Content-Type: application/json

{"email":"admin@bloodflow.local","password":"..."}
```
```
HTTP/1.1 200 OK
Content-Type: application/json

{"token":"eyJ...","expiresAtUtc":"2026-11-27T19:00:00Z","user":{...}}
```

## 7.4 Methods
| Method | Meaning | Used in BloodFlow |
|---|---|---|
| GET | Read; no side effects | lists, details, summaries, lookups |
| POST | Create, or perform an action | create organization/batch/request, login, approve, fulfil, cancel, … |
| PUT | Replace a resource's editable fields | update organization, update setting |
| PATCH | Partial update | not used |
| DELETE | Remove | not used — records change **status** instead (medical traceability) |

## 7.5 Status codes
| Code | Name | BloodFlow meaning |
|---|---|---|
| 200 | OK | Success with a body |
| 201 | Created | Organization, batch, request, staff registration created |
| 204 | No Content | Success without a body (e.g. approve staff, review override) |
| 400 | Bad Request | Invalid input (validation or business validation) |
| 401 | Unauthorized | No/invalid/expired token, wrong credentials, account no longer active |
| 403 | Forbidden | Valid user but not allowed (role, status, not your action) |
| 404 | Not Found | Doesn't exist **or** belongs to another organization |
| 409 | Conflict | Wrong state, duplicate, insufficient stock, concurrent change |
| 500 | Server Error | Unexpected bug (logged; `code: ServerError`) |
| 502/503 | Bad Gateway / Unavailable | AI service problems |

## 7.6 Request and response, JSON, headers and body
- **JSON** = text format `{ "key": value }`. Our property names are camelCase; enums are text.
- **Headers** we use: `Content-Type: application/json`, `Authorization: Bearer <token>`.
- **Body** carries the DTO for POST/PUT. GET parameters go in the **query string** (`?page=2&pageSize=20`) or the **route** (`/api/requests/15`).

## 7.7 Model binding
ASP.NET Core fills action parameters automatically:
| Source | Example |
|---|---|
| Route | `Get(int id)` ← `/api/requests/{id:int}` |
| Query | `Board([FromQuery] BoardQuery query)` ← `?type=Emergency&allGovernorates=true` |
| Body | `Create(CreateBloodRequestRequest request)` ← JSON body (complex types default to body with `[ApiController]`) |
| Services | `CancellationToken cancellationToken` ← cancelled if the client disconnects |

## 7.8 Validation
1. **Attributes on DTOs** — `[Required]`, `[Range(1, 8)]`, `[StringLength(500, MinimumLength = 3)]`, `[EmailAddress]`, `[EnumDataType]`. With `[ApiController]`, invalid input returns **400** automatically with an `errors` dictionary, before your code runs.
2. **Business validation** in services/rules → `Error.Validation(...)` → 400 with a `code`.
3. **Database constraints** as the last line of defence.

## 7.9 DTOs
Request/response records in Application. Never return entities directly: entities contain internal fields (`RowVersion` bytes, `PasswordHash`), and DTOs let us shape responses (e.g. hide `PatientFullName` from non-authorized callers).

## 7.10 Error format (ProblemDetails)
```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Not enough usable units: 5 needed, 3 available. Nothing was reserved.",
  "status": 409,
  "code": "Request.InsufficientStock",
  "traceId": "0HN8..."
}
```
Frontend rule: switch on `code`, show `title`.

## 7.11 Paging
`?page=1&pageSize=20` (max 100) → `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 57, "totalPages": 3 }`.

## 7.12 Swagger
`/swagger` (Development, or `Swagger:Enabled=true`). Click **Authorize**, paste the token (without "Bearer"), then *Try it out* on any endpoint.

## 7.13 Testing with Postman
1. New request → `POST https://localhost:7244/api/auth/login` → Body → raw → JSON → send.
2. Copy `token`. Create an environment variable `token`.
3. For other requests: **Authorization** tab → *Bearer Token* → `{{token}}`.
4. Local HTTPS certificate issues: run `dotnet dev-certs https --trust` once, or disable SSL verification for localhost only.
The repo alternative is `src/BloodFlow.Api/BloodFlow.Api.http` in Visual Studio.

> 🇪🇬 Postman برنامج بنبعت بيه طلبات للـ API ونشوف الرد. أول حاجة نعمل Login وناخد الـ token، وبعدين نحطه في Authorization.

---

# 7.14 Every implemented endpoint (53)

Legend — **Auth**: 🌐 anonymous · 🔑 any listed role. **Codes**: success first, then possible errors. All errors are ProblemDetails. 400 for model validation is possible on every endpoint with a body/query and is not repeated.

## Auth — `AuthController` (🌐)

| # | Method & URL | Purpose | Input | Success response | Errors |
|---|---|---|---|---|---|
| 1 | `POST /api/auth/login` | Sign in | body `{ email, password }` | 200 `{ token, expiresAtUtc, user: { id, fullName, email, role, organizationId } }` | 401 `Auth.InvalidCredentials`; 403 `Auth.LockedOut`, `AccountPending`, `AccountRejected`, `AccountDeactivated`, `OrganizationDeactivated`, `StaffProfileMissing` |
| 2 | `POST /api/auth/register/staff` | Staff self-registration (Pending) | body `{ fullName (3–200), email, password (8–128), organizationId, jobTitle (2–100) }` | 201 `{ userId, accountStatus: "Pending", message }` | 400 `Registration.Invalid`, `WeakPassword`, `OrganizationNotFound`, `OrganizationInactive`; 409 `Registration.EmailTaken` |

## Lookups — `LookupsController` (🌐)
| # | Method & URL | Purpose | Input | Success |
|---|---|---|---|---|
| 3 | `GET /api/lookups/governorates` | Governorate list | — | 200 `[{ id, name }]` |
| 4 | `GET /api/lookups/blood-groups` | Blood groups | — | 200 `[{ id, code }]` |
| 5 | `GET /api/lookups/organizations` | Active organizations (for registration) | query `type?`, `governorateId?` | 200 `[{ id, name, type, governorateId, cityName }]` |

## Admin — staff — `Admin/StaffController` (🔑 SystemAdmin)
| # | Method & URL | Purpose | Input | Success | Errors |
|---|---|---|---|---|---|
| 6 | `GET /api/admin/staff` | List staff | query `status, organizationId, staffKind, search, page, pageSize` | 200 paged `StaffListItemDto` (`userId, fullName, email, jobTitle, staffKind, organizationId, organizationName, governorateName, accountStatus, createdAt`) | — |
| 7 | `GET /api/admin/staff/{userId}` | Details | route | 200 `StaffDetailsDto` (+ organization status, approval fields, rejection reason) | 404 `Staff.NotFound` |
| 8 | `POST /api/admin/staff/{userId}/approve` | Pending → Active | — | 204 | 404; 409 `Staff.InvalidStatus`, `Staff.OrganizationInactive`, `Staff.Concurrency` |
| 9 | `POST /api/admin/staff/{userId}/reject` | Pending → Rejected | `{ reason }` | 204 | 404; 409 |
| 10 | `POST /api/admin/staff/{userId}/deactivate` | Active → Deactivated | `{ reason }` | 204 | 404; 409 |
| 11 | `POST /api/admin/staff/{userId}/reactivate` | Deactivated → Active | — | 204 | 404; 409 |

## Admin — organizations — `Admin/OrganizationsController` (🔑 SystemAdmin)
| # | Method & URL | Purpose | Input | Success | Errors |
|---|---|---|---|---|---|
| 12 | `GET /api/admin/organizations` | List | query `type, status, governorateId, search, page, pageSize` | 200 paged `OrganizationDto` (`id, type, name, cityName, governorateId, governorateName, status, activeStaffCount, rowVersion`) | — |
| 13 | `GET /api/admin/organizations/{id}` | Details | route | 200 `OrganizationDto` | 404 `Organization.NotFound` |
| 14 | `POST /api/admin/organizations` | Create | `{ type, name (3–200), cityName (2–100), governorateId }` | 201 `OrganizationDto` | 400 `Organization.GovernorateNotFound`; 409 `Organization.DuplicateName` |
| 15 | `PUT /api/admin/organizations/{id}` | Update | `{ name, cityName, governorateId, rowVersion }` | 200 `OrganizationDto` | 400 `InvalidRowVersion`, `GovernorateNotFound`; 404; 409 `DuplicateName`, `Concurrency` |
| 16 | `POST /api/admin/organizations/{id}/deactivate` | Deactivate | `{ reason }` | 204 | 404; 409 `InvalidStatus`, `HasOpenWork`, `Concurrency` |
| 17 | `POST /api/admin/organizations/{id}/activate` | Activate | — | 204 | 404; 409 `InvalidStatus` |

## Admin — settings — `Admin/SettingsController` (🔑 SystemAdmin)
| # | Method & URL | Purpose | Input | Success | Errors |
|---|---|---|---|---|---|
| 18 | `GET /api/admin/settings` | All settings | — | 200 `[SystemSettingDto]` (`key, value, valueType, description, isMedicalParameter, approvalReference, approvedByUserId, approvedAt, updatedByUserId, updatedAt, rowVersion`) | — |
| 19 | `GET /api/admin/settings/{key}` | One setting | route `key` (e.g. `Requests.MaxUnitsPerRequest`) | 200 `SystemSettingDto` | 404 `Settings.NotFound` |
| 20 | `PUT /api/admin/settings/{key}` | Change value | `{ value, approvalReference?, rowVersion }` | 200 `SystemSettingDto` | 400 `Settings.InvalidValue`, `ApprovalRequired`, `InvalidRowVersion`; 404; 409 `Settings.Concurrency` |

## Admin — audit and reports (🔑 SystemAdmin)
| # | Method & URL | Purpose | Input | Success |
|---|---|---|---|---|
| 21 | `GET /api/admin/audit-logs` | Audit log | query `entityType, entityId, actorUserId, action, fromUtc, toUtc, page, pageSize` | 200 paged `AuditLogDto` (`id, occurredAt, actorType, actorUserId, actorRole, organizationId, action, entityType, entityId, oldValues, newValues, reason, ipAddress`) |
| 22 | `GET /api/admin/inventory/organizations/{organizationId}/summary` | Any organization's stock summary (read-only) | route | 200 `InventorySummaryDto` (see #23); 404 if not found |

## Inventory — `InventoryController` (🔑 HospitalStaff, BloodBankStaff — own organization)
| # | Method & URL | Purpose | Input | Success | Errors |
|---|---|---|---|---|---|
| 23 | `GET /api/inventory/summary` | Stock per blood group | — | 200 `{ organizationId, organizationName, today, nearExpiryDays, bloodGroups: [{ bloodGroupId, bloodGroupCode, onHand, reserved, available, nearExpiryUnits, expiredUnits, nextExpiryDate }] }` | 403 `Inventory.NoOrganization` |
| 24 | `GET /api/inventory/batches` | Batches (FEFO order) | query `bloodGroupId, status, expiringWithinDays, hideEmpty (true), page, pageSize` | 200 paged `BatchDto` (`id, bloodGroupId, bloodGroupCode, sourceType, collectionDate, expiryDate, expiryBasis, daysToExpiry, initialQuantity, onHandQuantity, reservedQuantity, availableQuantity, status, createdAt`) | 403 |
| 25 | `GET /api/inventory/batches/{id}` | One batch | route | 200 `BatchDto` | 404 `Inventory.BatchNotFound` |
| 26 | `POST /api/inventory/batches` | Intake | `{ bloodGroupId (1–8), quantity (1–1000), collectionDate, expiryDate?, clientRequestId? }` | 201 `BatchDto` | 400 `InvalidQuantity`, `CollectionInFuture`, `ExpiryBeforeCollection`, `AlreadyExpired`, `BloodGroupNotFound`; 409 `DuplicateRequestId` |
| 27 | `POST /api/inventory/batches/{id}/adjust` | Adjust | `{ quantityDelta (−1000…1000), reasonCode (Damaged/Expired/Correction), reason (3–500), clientRequestId? }` | 200 `BatchDto` | 400 `ZeroAdjustment`, `ReasonRequiresDecrease`, `ExpiredBatchIncrease`, `NegativeStock`; 404; 409 `BelowReserved`, `Concurrency`, `DuplicateRequestId` |
| 28 | `GET /api/inventory/transactions` | Ledger | query `batchId, type, fromUtc, toUtc, page, pageSize` | 200 paged `InventoryTransactionDto` (`id, batchId, bloodGroupCode, type, onHandDelta, reservedDelta, onHandAfter, reservedAfter, reasonCode, reason, actorType, performedByUserId, createdAt`) | 403 |

## Requests — `RequestsController` (🔑 HospitalStaff, BloodBankStaff)
Response types: **Summary** = `RequestSummaryDto { id, requestNumber, requestType, status, verificationStatus, bloodGroupId, bloodGroupCode, unitsRequested, neededBy, destinationHospitalId, destinationHospitalName, governorateName, executingOrganizationId, executingOrganizationName, createdAt, statusChangedAt, flags: { isOverdue, isReceiptOverdue, needsEscalation }, availableAtYourOrganization }`. **Details** = `RequestDetailsDto { summary, requesterCapacity, createdByUserId, patientFullName, patientAnonymized, verifiedByUserId, verifiedAt, emergencyOverrideUsed, reservation: { id, organizationId, organizationName, status, activeUnits, needsReallocation, createdAt, closedAt, lines: [{ id, batchId, batchExpiryDate, quantity, status, invalidationReason }] } }`.

| # | Method & URL | Purpose / who | Input | Success | Errors |
|---|---|---|---|---|---|
| 29 | `POST /api/requests` | Create (🔑 **HospitalStaff only**) | `{ requestType, bloodGroupId, unitsRequested, neededBy, patientFullName, clientRequestId? }` | 201 Details | 400 `BloodGroupNotFound`, `TooManyUnits`, `NeededByInPast`; 403 `OnlyHospitalStaff`, `NoOrganization`; 409 `Duplicate`, `DuplicateRequestId` |
| 30 | `GET /api/requests/hospital` | My hospital's requests | query `status, type, bloodGroupId, onlyFlagged, page, pageSize` | 200 paged Summary | 403 |
| 31 | `GET /api/requests/board` | Open requests I could fulfil | query `type, bloodGroupId, allGovernorates, page, pageSize` | 200 paged Summary (+ `availableAtYourOrganization`) | 403 |
| 32 | `GET /api/requests/executing` | Requests my org reserved | query as #30 | 200 paged Summary | 403 |
| 33 | `GET /api/requests/{id}` | Details | route | 200 Details | 404 `Request.NotFound` |
| 34 | `GET /api/requests/{id}/history` | Timeline | route | 200 `[{ fromStatus, toStatus, changedByUserId, reason, changedAt }]` | 404 |
| 35 | `POST /api/requests/{id}/verify` | Verify (🔑 HospitalStaff, destination) | — | 200 Details | 404; 409 `InvalidState` |
| 36 | `POST /api/requests/{id}/approve` | Reserve all units FEFO (other org) | — | 200 Details | 404; 409 `OwnRequest`, `InvalidState`, `NotVerified`, `InsufficientStock`, `Concurrency` |
| 37 | `POST /api/requests/{id}/reallocate` | Reserve missing units (executor) | — | 200 Details | 403 `NotExecutor`; 404; 409 `InvalidState`, `NothingToReallocate`, `InsufficientStock` |
| 38 | `POST /api/requests/{id}/fulfil` | Deduct stock (executor) | `{ emergencyOverrideReason? (10–500) }` | 200 Details | 400 `OverrideReasonRequired`; 403 `NotExecutor`; 404; 409 `InvalidState`, `NotVerified`, `ReservationInvalidated`, `NeedsReallocation`, `Concurrency` |
| 39 | `POST /api/requests/{id}/receive` | Confirm arrival (destination or creator) | — | 200 Details | 404; 409 `InvalidState` |
| 40 | `POST /api/requests/{id}/reject` | Reject pending (destination or any BloodBankStaff) | `{ reason }` | 200 Details | 403 `CannotReject`; 404; 409 `InvalidState` |
| 41 | `POST /api/requests/{id}/cancel` | Cancel (parties) | `{ reason }` | 200 Details | 403 `CannotCancel`; 404; 409 `InvalidState` |

## Admin — requests — `Admin/RequestsMonitorController` (🔑 SystemAdmin)
| # | Method & URL | Purpose | Input | Success | Errors |
|---|---|---|---|---|---|
| 42 | `GET /api/admin/requests` | All requests (no patient names) | query `status, type, bloodGroupId, onlyFlagged, page, pageSize` | 200 paged Summary | — |
| 43 | `POST /api/admin/requests/{id}/cancel` | Cancel a **Pending** request | `{ reason }` | 200 Details | 404; 409 `AdminCannotCancelApproved`, `InvalidState` |
| 44 | `GET /api/admin/emergency-overrides` | Overrides (default PendingReview) | query `reviewStatus, page, pageSize` | 200 paged `{ id, requestId, requestNumber, organizationId, organizationName, overriddenByUserId, reasonText, overriddenAt, reviewStatus, reviewedByUserId, reviewedAt, reviewNotes }` | — |
| 45 | `POST /api/admin/emergency-overrides/{id}/review` | Mark reviewed | `{ notes? }` | 204 | 404 `EmergencyOverride.NotFound`; 409 `AlreadyReviewed` |

## AI gateway — `AiController` (🔑 SystemAdmin, HospitalStaff, BloodBankStaff)
Works only when the AI service is reachable (`BloodFlowAi:BaseUrl`). Errors for all: 400 (AI rejected input), 409 (retrain busy), 502 (AI key wrong/missing or AI failure), 503 (AI down / timeout).

| # | Method & URL | Purpose | Input | Success |
|---|---|---|---|---|
| 46 | `GET /api/ai/health` (🌐) | AI status | — | 200 `{ status, today, dataSource, consumptionRows, dateRange, methods }` |
| 47 | `GET /api/ai/stock` | Stock per type (AI view) | — | 200 `{ today, stock: { "O+": 12, ... } }` |
| 48 | `GET /api/ai/forecast` | 14-day forecast per type | — | 200 forecast points `{ date, predicted, lower, upper }` per type |
| 49 | `POST /api/ai/shortage` | Shortage prediction | `{ stocks? }` | 200 `[{ bloodType, currentStock, safetyThreshold, daysOfSupply, predictedConsumption14d, shortageInDays, worstCaseShortageInDays, expectedDeficitUnits, alertLevel, forecastMethod, daily }]` |
| 50 | `POST /api/ai/expiry` | Waste risk | — | 200 `[{ bloodType, availableUnits, expiringIn3Days, expiringIn7Days, expectedWasteUnits, wasteIfLowDemandUnits, wasteRiskPercent, riskLevel }]` |
| 51 | `POST /api/ai/substitutions` | Compatible substitutions | `{ stocks? }` | 200 `{ substitutions: [...], nearExpiry: [...] }` |
| 52 | `POST /api/ai/demand` | Forecast from given history | `{ bloodType, daysAhead, history: [{ date, unitsConsumed }] }` | 200 `{ bloodType, historyDaysUsed, method, forecast }` |
| 53 | `POST /api/ai/retrain` | Retrain models (🔑 **SystemAdmin**) | consumption/donation/batch rows | 200 result from the AI |

Exact AI response shapes are defined in `src/BloodFlow.Application/Ai/AiModels.cs`; the AI service itself is not in this repository.
