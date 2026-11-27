# BloodFlow — API Documentation

All endpoints below exist in `src/BloodFlow.Api/Controllers/`. Live, clickable documentation is in **Swagger**: `/swagger` (OpenAPI JSON at `/openapi/v1.json`).

- Local: `https://localhost:<port>/swagger` (the port is in `Properties/launchSettings.json`)
- Server: `https://bloodflow.runasp.net/swagger` (only while `Swagger:Enabled` is `true` on the server)

Ready-made requests for Visual Studio: `src/BloodFlow.Api/BloodFlow.Api.http`.

---

## 1. Conventions

| Topic | Rule |
|---|---|
| Format | JSON. Property names are camelCase. |
| Enums | Sent and returned as **text**: `"Emergency"`, `"Pending"`, `"BloodBank"`. |
| Time | All `DateTime` values are **UTC**. Send them with `Z` (e.g. `2026-12-01T18:00:00Z`). A value without a time zone is treated as UTC. Dates (`collectionDate`, `expiryDate`) are `yyyy-MM-dd`. |
| Auth | `Authorization: Bearer <token>` from `POST /api/auth/login`. |
| Paging | List endpoints take `page` (default 1) and `pageSize` (default 20, max 100) and return `{ items, page, pageSize, totalCount, totalPages }`. |
| Concurrency | Admin edits send back the `rowVersion` they read. A conflicting change returns `409`. |
| Idempotency | Stock intake/adjust and request creation accept `clientRequestId` (a new GUID per form submission). Retrying with the same id returns the first result. |
| Errors | ProblemDetails (RFC 9457): `{ type, title, status, code, traceId }`. **Rely on `code`, not on `title`.** Model-validation errors return `400` with an `errors` object per field. |

### HTTP status codes
| Status | Meaning |
|---|---|
| 200 / 201 / 204 | Success (201 = created, 204 = success with no body) |
| 400 | Invalid input (validation) |
| 401 | Not signed in, bad token, or wrong email/password |
| 403 | Signed in but not allowed (wrong role, account pending/deactivated, not your organization's action) |
| 404 | Not found **or** not yours (other organizations' records are hidden) |
| 409 | Conflict: wrong state, duplicate, insufficient stock, or someone changed it at the same time |
| 502 / 503 | AI service problem (AI endpoints only) |

---

## 2. Authentication — `api/auth` (anonymous)

### `POST /api/auth/login`
```json
{ "email": "admin@bloodflow.local", "password": "..." }
```
**200**
```json
{
  "token": "eyJhbGciOi...",
  "expiresAtUtc": "2026-11-27T19:00:00Z",
  "user": { "id": 1, "fullName": "System Administrator", "email": "admin@bloodflow.local", "role": "SystemAdmin", "organizationId": null }
}
```
Errors: `401 Auth.InvalidCredentials` (same message for unknown email and wrong password), `403 Auth.LockedOut` (5 wrong attempts → 15 minutes), `403 Auth.AccountPending | Auth.AccountRejected | Auth.AccountDeactivated | Auth.OrganizationDeactivated | Auth.StaffProfileMissing`.

### `POST /api/auth/register/staff`
```json
{ "fullName": "Mona Ali", "email": "mona@hospital.eg", "password": "Passw0rd1", "organizationId": 3, "jobTitle": "Nurse" }
```
Creates a **Pending** account. The role (HospitalStaff or BloodBankStaff) comes from the organization's type. The person cannot sign in until a System Admin approves.
**201** `{ "userId": 12, "accountStatus": "Pending", "message": "..." }`
Errors: `400 Registration.Invalid | Registration.WeakPassword | Registration.OrganizationNotFound | Registration.OrganizationInactive`, `409 Registration.EmailTaken`.
Password rules: ≥ 8 characters with a digit, a lowercase and an uppercase letter.

---

## 3. Lookups — `api/lookups` (anonymous)

| Method | Route | Returns |
|---|---|---|
| GET | `/api/lookups/governorates` | `[{ id, name }]` (27) |
| GET | `/api/lookups/blood-groups` | `[{ id, code }]` (8) |
| GET | `/api/lookups/organizations?type=Hospital&governorateId=1` | Active organizations `[{ id, name, type, governorateId, cityName }]` (used by the registration form) |

---

## 4. System Admin — `[Authorize(Roles = "SystemAdmin")]`

### 4.1 Staff — `api/admin/staff`
| Method | Route | Body | Notes |
|---|---|---|---|
| GET | `/api/admin/staff?status=Pending&organizationId=&staffKind=&search=&page=&pageSize=` | — | Paged list |
| GET | `/api/admin/staff/{userId}` | — | Details |
| POST | `/api/admin/staff/{userId}/approve` | — | Pending → Active (organization must be active) |
| POST | `/api/admin/staff/{userId}/reject` | `{ "reason": "..." }` | Pending → Rejected |
| POST | `/api/admin/staff/{userId}/deactivate` | `{ "reason": "..." }` | Active → Deactivated (takes effect immediately) |
| POST | `/api/admin/staff/{userId}/reactivate` | — | Deactivated → Active |

Errors: `404 Staff.NotFound`, `409 Staff.InvalidStatus | Staff.OrganizationInactive | Staff.Concurrency`.

### 4.2 Organizations — `api/admin/organizations`
| Method | Route | Body | Notes |
|---|---|---|---|
| GET | `/api/admin/organizations?type=&status=&governorateId=&search=&page=&pageSize=` | — | Paged list with active staff count |
| GET | `/api/admin/organizations/{id}` | — | Includes `rowVersion` |
| POST | `/api/admin/organizations` | `{ "type": "Hospital", "name": "...", "cityName": "...", "governorateId": 1 }` | **201** |
| PUT | `/api/admin/organizations/{id}` | `{ "name", "cityName", "governorateId", "rowVersion" }` | The type cannot change |
| POST | `/api/admin/organizations/{id}/deactivate` | `{ "reason": "..." }` | Refused if it has active reservations or open requests |
| POST | `/api/admin/organizations/{id}/activate` | — | |

Errors: `400 Organization.GovernorateNotFound | Organization.InvalidRowVersion`, `404 Organization.NotFound`, `409 Organization.DuplicateName | Organization.InvalidStatus | Organization.HasOpenWork | Organization.Concurrency`.

### 4.3 Settings — `api/admin/settings`
| Method | Route | Body |
|---|---|---|
| GET | `/api/admin/settings` | — |
| GET | `/api/admin/settings/{key}` | — |
| PUT | `/api/admin/settings/{key}` | `{ "value": "14", "approvalReference": "Memo 12/2026", "rowVersion": "..." }` |

The value is checked against the setting's type (Int/Decimal/Bool/String). **Medical** settings require `approvalReference`. Errors: `400 Settings.InvalidValue | Settings.ApprovalRequired | Settings.InvalidRowVersion`, `404 Settings.NotFound`, `409 Settings.Concurrency`. Keys and defaults: see `DATABASE_DOCUMENTATION.md` §5.

### 4.4 Audit logs — `api/admin/audit-logs`
`GET /api/admin/audit-logs?entityType=&entityId=&actorUserId=&action=&fromUtc=&toUtc=&page=&pageSize=` — paged, newest first.

### 4.5 Inventory report — `api/admin/inventory`
`GET /api/admin/inventory/organizations/{organizationId}/summary` — the same summary staff see, for any organization (read-only; the admin never changes stock).

### 4.6 Requests monitoring — `api/admin`
| Method | Route | Body | Notes |
|---|---|---|---|
| GET | `/api/admin/requests?status=&type=&bloodGroupId=&onlyFlagged=true` | — | All requests. Patient names are never shown to the admin. |
| POST | `/api/admin/requests/{id}/cancel` | `{ "reason": "..." }` | **Pending only** (an approved request holds stock; the admin never changes stock) |
| GET | `/api/admin/emergency-overrides?reviewStatus=PendingReview` | — | Default filter: waiting for review |
| POST | `/api/admin/emergency-overrides/{id}/review` | `{ "notes": "..." }` (optional) | **204**. Errors `404 EmergencyOverride.NotFound`, `409 EmergencyOverride.AlreadyReviewed` |

---

## 5. Inventory — `api/inventory` — `[Authorize(Roles = "HospitalStaff,BloodBankStaff")]`

Staff work only on **their own organization's** stock (from the token).

| Method | Route | Body / query | Notes |
|---|---|---|---|
| GET | `/api/inventory/summary` | — | Per blood group: onHand, reserved, available, nearExpiryUnits, expiredUnits, nextExpiryDate |
| GET | `/api/inventory/batches` | `bloodGroupId, status, expiringWithinDays, hideEmpty (default true), page, pageSize` | FEFO order (earliest expiry first) |
| GET | `/api/inventory/batches/{id}` | — | `404` if not your organization's |
| POST | `/api/inventory/batches` | `{ "bloodGroupId": 7, "quantity": 6, "collectionDate": "2026-11-20", "expiryDate": "2026-12-25", "clientRequestId": "guid" }` | **201**. `expiryDate` optional → collection date + `Inventory.DefaultShelfLifeDays` (35) |
| POST | `/api/inventory/batches/{id}/adjust` | `{ "quantityDelta": -1, "reasonCode": "Damaged", "reason": "Bag torn", "clientRequestId": "guid" }` | `Damaged`/`Expired` decrease only; `Correction` ±. On-hand can never go below reserved. |
| GET | `/api/inventory/transactions` | `batchId, type, fromUtc, toUtc, page, pageSize` | The ledger, newest first |

Errors: `400 Inventory.InvalidQuantity | CollectionInFuture | ExpiryBeforeCollection | AlreadyExpired | BloodGroupNotFound | ZeroAdjustment | ReasonRequiresDecrease | ExpiredBatchIncrease | NegativeStock`, `403 Inventory.NoOrganization`, `404 Inventory.BatchNotFound`, `409 Inventory.BelowReserved | Inventory.DuplicateRequestId | Inventory.Concurrency`.

---

## 6. Blood requests — `api/requests` — `[Authorize(Roles = "HospitalStaff,BloodBankStaff")]`

### Lifecycle
```
Pending ──approve──► Approved ──fulfil──► Fulfilled ──receive──► Received
   ├──reject──► Rejected     └──cancel──► Cancelled  (reserved units released)
   └──cancel──► Cancelled
```

| Method | Route | Who | Body | What happens |
|---|---|---|---|---|
| POST | `/api/requests` | **HospitalStaff only**, for their own hospital | see below | **201**. Pending, Verified at creation, number `BR-yyyy-000001` |
| GET | `/api/requests/hospital` | Staff | `status, type, bloodGroupId, onlyFlagged, page, pageSize` | Requests whose destination is my hospital |
| GET | `/api/requests/board` | Staff | `type, bloodGroupId, allGovernorates, page, pageSize` | Pending (Verified or Emergency) requests of **other** organizations, same governorate by default, emergencies first then earliest `neededBy`. Each row has `availableAtYourOrganization` |
| GET | `/api/requests/executing` | Staff | `status, type, bloodGroupId, onlyFlagged, page, pageSize` | Requests my organization reserved/fulfilled |
| GET | `/api/requests/{id}` | Parties, or any staff while Pending | — | Details + reservation lines. `patientFullName` only for the destination hospital and the creator |
| GET | `/api/requests/{id}/history` | Same as above | — | Status timeline |
| POST | `/api/requests/{id}/verify` | **HospitalStaff** of the destination hospital | — | Unverified → Verified (for future patient-created requests) |
| POST | `/api/requests/{id}/approve` | Staff of **another** organization | — | Reserves **all** units now with FEFO. Not enough usable stock → `409`, nothing reserved |
| POST | `/api/requests/{id}/reallocate` | The organization holding the reservation | — | Reserves only the missing units after a reserved batch expired |
| POST | `/api/requests/{id}/fulfil` | The organization holding the reservation | `{ "emergencyOverrideReason": "..." }` (only for an unverified emergency, 10–500 chars) | Deducts the units from stock. An expired reserved batch → line invalidated, `409`, reallocate first |
| POST | `/api/requests/{id}/receive` | Destination hospital staff or the creator | — | Fulfilled → Received (pressing twice is harmless) |
| POST | `/api/requests/{id}/reject` | Destination hospital staff, or any BloodBankStaff | `{ "reason": "..." }` | Pending only |
| POST | `/api/requests/{id}/cancel` | Pending: creator or destination hospital. Approved: creator, destination hospital or the executing organization | `{ "reason": "..." }` | Cancelling an approved request releases its reserved units |

**Create body**
```json
{
  "requestType": "Emergency",
  "bloodGroupId": 7,
  "unitsRequested": 3,
  "neededBy": "2026-12-01T18:00:00Z",
  "patientFullName": "Patient name",
  "clientRequestId": "1b2c3d4e-5f60-4718-8293-a4b5c6d7e8f9"
}
```
Rules: units 1 … `Requests.MaxUnitsPerRequest` (10); `neededBy` in the future; an open request (Pending/Approved) for the same hospital + blood group + patient name within `Requests.DuplicateWindowHours` (24) → `409 Request.Duplicate`.

**Flags on every request** (computed when read, not stored)
| Flag | True when |
|---|---|
| `isOverdue` | Approved and `neededBy` has passed |
| `isReceiptOverdue` | Fulfilled and not received within `Requests.ReceiptConfirmationHours` (48) |
| `needsEscalation` | Emergency and still Pending after `Requests.EmergencyEscalationMinutes` (30) |

**Errors**
| Code | Status | When |
|---|---|---|
| `Request.NoOrganization` | 403 | Caller is not staff of an organization |
| `Request.OnlyHospitalStaff` | 403 | Non-hospital staff tries to create |
| `Request.NotExecutor` | 403 | Not the organization holding the reservation |
| `Request.CannotReject` / `Request.CannotCancel` | 403 | Not a party to the request |
| `Request.BloodGroupNotFound` / `TooManyUnits` / `NeededByInPast` / `OverrideReasonRequired` | 400 | Invalid input |
| `Request.NotFound` | 404 | Not found or not visible to you |
| `Request.Duplicate` / `DuplicateRequestId` | 409 | Duplicate request |
| `Request.InvalidState` | 409 | Action not allowed from the current status |
| `Request.OwnRequest` | 409 | A hospital approving its own request |
| `Request.NotVerified` | 409 | Normal request not verified |
| `Request.InsufficientStock` | 409 | Not enough usable units (message includes the numbers) |
| `Request.ReservationInvalidated` / `NeedsReallocation` / `NothingToReallocate` | 409 | Reservation problems around expiry |
| `Request.AdminCannotCancelApproved` | 409 | Admin cancelling an approved request |
| `Request.Concurrency` | 409 | Changed by someone else at the same time |

---

## 7. AI gateway — `api/ai`

Class: `[Authorize(Roles = "SystemAdmin,HospitalStaff,BloodBankStaff")]`. The backend forwards these calls to the Python AI service with the `x-api-key` header. **They work only when the AI service is running and reachable at `BloodFlowAi:BaseUrl`.**

| Method | Route | Auth | Body | Returns |
|---|---|---|---|---|
| GET | `/api/ai/health` | anonymous | — | AI status, model "today", data source |
| GET | `/api/ai/stock` | staff/admin | — | Stock per blood type computed by the AI |
| GET | `/api/ai/forecast` | staff/admin | — | 14-day consumption forecast with an 80% range |
| POST | `/api/ai/shortage` | staff/admin | `{ "stocks": { "O+": 12 } }` optional | When each type runs short, alert level, deficit |
| POST | `/api/ai/expiry` | staff/admin | — | Units expected to expire unused, risk level |
| POST | `/api/ai/substitutions` | staff/admin | `{ "stocks": {...} }` optional | Compatible substitutions (suggestions only) |
| POST | `/api/ai/demand` | staff/admin | `{ "bloodType": "O+", "daysAhead": 14, "history": [{ "date": "2026-11-01", "unitsConsumed": 3 }] }` | Forecast from the given history |
| POST | `/api/ai/retrain` | **SystemAdmin** | consumption/donation/batch rows | Retrains the models (one at a time) |

Errors: `400` bad input to the AI, `409` a retrain is already running, `503` AI unavailable or timed out, `502` wrong/missing AI key or unexpected AI failure.

---

## 8. Quick test scenario (Swagger)

1. `POST /api/auth/login` as the admin → copy `token` → **Authorize** button → paste the token.
2. `POST /api/admin/organizations` twice: a Hospital and a BloodBank **in the same governorate**.
3. `POST /api/auth/register/staff` once for each organization (no token needed) → `POST /api/admin/staff/{userId}/approve` for both.
4. Log in as the blood-bank employee → `POST /api/inventory/batches` (e.g. 6 units O+, `bloodGroupId: 7`).
5. Log in as the hospital employee → `POST /api/requests` (3 units O+).
6. Blood bank → `GET /api/requests/board` → `POST /api/requests/{id}/approve` → `GET /api/inventory/summary` (reserved = 3) → `POST /api/requests/{id}/fulfil` with `{}`.
7. Hospital → `POST /api/requests/{id}/receive` → `GET /api/requests/{id}/history` (4 rows).
8. Admin → `GET /api/admin/audit-logs` to see every step.
