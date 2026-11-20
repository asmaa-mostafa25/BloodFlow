# 🩸 BloodFlow

**Smart Blood Inventory & Redistribution System — Backend API**

BloodFlow connects hospitals, blood banks, donors and recipients across Egypt's 27 governorates. It tracks blood stock in real time, manages normal and emergency blood requests, reserves units safely, and redistributes blood between organizations before it expires.

> 🎓 Graduation project — this repository contains the **ASP.NET Core Web API (backend)**.

---

## ✨ Key Features (planned)

- **Five roles:** System Admin, Hospital Staff, Blood Bank Staff, Blood Donor, Blood Recipient
- **Inventory management** with an append-only transaction ledger and FEFO allocation (First Expired, First Out)
- **Blood requests** (normal & emergency) with verification, reservation, fulfilment and receipt
- **Transfers** between organizations with discrepancy tracking
- **Donations** with a two-step verification workflow
- **Alerts & notifications** for emergencies, shortages and near-expiry units
- **Concurrency-safe** stock updates (optimistic concurrency with `rowversion`)

---

## 🛠️ Tech Stack

| Area | Technology |
|---|---|
| Framework | .NET 10, ASP.NET Core Web API |
| ORM | Entity Framework Core 10 (Code-First, Migrations) |
| Database | SQL Server (collation `Arabic_100_CI_AI`) |
| Authentication | ASP.NET Core Identity (`int` keys) + JWT *(in progress)* |
| API Docs | OpenAPI / Swagger |
| Testing | xUnit |

---

## 🏗️ Architecture

The solution follows a pragmatic **Clean Architecture**:

```
src/
├── BloodFlow.Domain          → Entities & enums. Depends on nothing.
├── BloodFlow.Application     → Use cases, business rules, shared constants.
├── BloodFlow.Infrastructure  → EF Core, Identity, persistence, external services.
└── BloodFlow.Api             → Controllers, Program.cs (composition root).
tests/
└── BloodFlow.Tests
```

**Dependency rule:** `Api → Application + Infrastructure`, `Infrastructure → Application`, `Application → Domain`.

- Each layer registers its own services (e.g. `AddInfrastructure()`), so `Program.cs` stays minimal.
- Entity configuration uses the **Fluent API** (`IEntityTypeConfiguration<T>`) inside Infrastructure, keeping the Domain free of framework attributes.

---

## 🚀 Getting Started

### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express, Developer or LocalDB)
- EF Core CLI tool:

```bash
dotnet tool install --global dotnet-ef
```

### 2. Clone the repository

```bash
git clone https://github.com/asmaa-mostafa25/BloodFlow.git
cd BloodFlow
```

### 3. Configure the connection string (User Secrets)

The connection string is **not** stored in the repository. Each developer sets it locally with User Secrets:

```bash
cd src/BloodFlow.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=BloodFlowDb;Trusted_Connection=True;TrustServerCertificate=True"
cd ../..
```

> Replace `.\SQLEXPRESS` with your own SQL Server instance name (e.g. `(localdb)\MSSQLLocalDB`).
> ⚠️ Never commit connection strings, passwords or keys to the repository.

Then set the JWT signing key and the first System Admin account (still inside `src/BloodFlow.Api`):

```bash
dotnet user-secrets set "Jwt:Key" "<a long random value, at least 32 characters>"
dotnet user-secrets set "AdminSeed:Email" "admin@bloodflow.local"
dotnet user-secrets set "AdminSeed:Password" "<a strong password: 8+ chars, upper, lower, digit>"
```

The API refuses to start if any of these are missing, and the error message says which one.

### 4. Create the database

From the solution root:

```bash
dotnet ef database update --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api
```

This creates the database and seeds the **27 governorates** and **8 blood groups**.

> Using Visual Studio's **Package Manager Console** instead? Set `BloodFlow.Api` as the **Startup Project**, choose `BloodFlow.Infrastructure` as the **Default project**, then run `Update-Database`.

### 5. Run the API

```bash
dotnet run --project src/BloodFlow.Api
```

On startup the API seeds the **five roles**, the **first System Admin** and the **default system settings** (only what is missing; nothing is overwritten).

In Development, open **Swagger UI** at `https://localhost:7244/swagger`: call `POST /api/auth/login`, copy the `token`, click **Authorize** and paste it.

---

## 🔌 API (Slice 1)

All errors use the standard **ProblemDetails** JSON with an extra `code` field (e.g. `Staff.InvalidStatus`). Enums are sent as text (`"Hospital"`, `"Pending"`).

| Method | Route | Who | What |
|---|---|---|---|
| POST | `/api/auth/login` | Anyone | Sign in; only **Active** accounts get a token |
| POST | `/api/auth/register/staff` | Anyone | Staff self-registration → account is **Pending** |
| GET | `/api/lookups/governorates` · `/blood-groups` · `/organizations` | Anyone | Dropdown data (active organizations only) |
| GET | `/api/admin/staff?status=Pending` | System Admin | Approval queue (oldest first) |
| GET | `/api/admin/staff/{userId}` | System Admin | Staff details |
| POST | `/api/admin/staff/{userId}/approve` · `/reject` · `/deactivate` · `/reactivate` | System Admin | Change account status (reject/deactivate need a `reason`) |
| GET · POST | `/api/admin/organizations` | System Admin | List / create hospitals and blood banks |
| GET · PUT | `/api/admin/organizations/{id}` | System Admin | Details / update (send back `rowVersion`) |
| POST | `/api/admin/organizations/{id}/deactivate` · `/activate` | System Admin | Change organization status |
| GET · PUT | `/api/admin/settings` · `/api/admin/settings/{key}` | System Admin | System parameters (medical ones need `approvalReference`) |
| GET | `/api/admin/audit-logs` | System Admin | Read-only audit trail |

### Slice 2 — Inventory (Hospital & Blood Bank staff)

Staff always work on **their own organization** (taken from `org_id` in the token).

| Method | Route | What |
|---|---|---|
| GET | `/api/inventory/summary` | Per blood group: on hand, reserved, available, near expiry, expired |
| GET | `/api/inventory/batches?bloodGroupId=&status=&expiringWithinDays=` | Batches in FEFO order (earliest expiry first) |
| GET | `/api/inventory/batches/{id}` | One batch |
| POST | `/api/inventory/batches` | Manual intake (expiry defaults to collection + shelf-life setting) |
| POST | `/api/inventory/batches/{id}/adjust` | `Damaged` / `Expired` (decrease) or `Correction` (±), reason required |
| GET | `/api/inventory/transactions` | Stock ledger (append-only) |
| GET | `/api/admin/inventory/organizations/{id}/summary` | System Admin, read-only |

Send a new `clientRequestId` (GUID) with each intake/adjust form submission: repeating the same request never changes stock twice.
A background job marks batches as `Expired` the day after their expiry date (Cairo time).

**Token claims:** `sub` (user id), `email`, `name`, `role`, and `org_id` for staff.

---

## 📚 Reference Data

### Blood Groups

| Id | Code | | Id | Code |
|---|---|---|---|---|
| 1 | A+ | | 5 | AB+ |
| 2 | A- | | 6 | AB- |
| 3 | B+ | | 7 | O+ |
| 4 | B- | | 8 | O- |

### Governorates

| Id | Name | | Id | Name | | Id | Name |
|---|---|---|---|---|---|---|---|
| 1 | القاهرة | | 10 | الجيزة | | 19 | المنيا |
| 2 | الإسكندرية | | 11 | المنوفية | | 20 | سوهاج |
| 3 | بورسعيد | | 12 | القليوبية | | 21 | قنا |
| 4 | السويس | | 13 | البحيرة | | 22 | كفر الشيخ |
| 5 | البحر الأحمر | | 14 | الغربية | | 23 | مطروح |
| 6 | دمياط | | 15 | الأقصر | | 24 | أسوان |
| 7 | الدقهلية | | 16 | أسيوط | | 25 | شمال سيناء |
| 8 | الشرقية | | 17 | بني سويف | | 26 | جنوب سيناء |
| 9 | الإسماعيلية | | 18 | الفيوم | | 27 | الوادي الجديد |

> These ids are fixed and will not change; frontend dropdowns can rely on them.

---

## 🗺️ Roadmap

**Foundation**
- [x] Solution structure (Clean Architecture)
- [x] Governorate, BloodGroup and Organization entities, configurations and migrations
- [x] Reference data seeding (governorates & blood groups)
- [x] ASP.NET Core Identity with `int` keys
- [x] Role seeding on startup

**Slice 1 — System Admin**
- [x] First System Admin account
- [x] JWT authentication (login endpoint)
- [x] StaffProfile, SystemSetting, AuditLog
- [x] Staff self-registration + admin approval / rejection / deactivation
- [x] Organization management
- [x] Swagger UI with JWT, ProblemDetails errors
- [ ] Notifications to staff on approval (moves to Slice 7)

**Slice 2 — Blood Bank Staff**
- [x] BloodBatch + append-only InventoryTransaction ledger
- [x] Manual intake and adjustments (idempotent, concurrency-safe)
- [x] Stock summary, FEFO batch list, near-expiry filter
- [x] Background expiry job
- [ ] Invalidate reservations of expired batches (needs Slice 3)

**Next slices**
- [ ] Slice 3 — Hospital Staff (requests, reservations, emergency handling)
- [ ] Slice 4 — Donors & donations
- [ ] Slice 5 — Recipients
- [ ] Slice 6 — Transfers between organizations
- [ ] Slice 7 — Notifications & alerts

---

## 👥 Team

| Role | Members |
|---|---|
| Backend & Team Lead | Asmaa Mostafa |
| Frontend | Fadaa Mohamed · Farah Mahmoud · Habiba Atef · Mennatullah Saeed |
| UI/UX | Maryam Helmy · Shahd Sherif · Eman Mahmoud |
| AI | Mennatullah Shaker |
| Supervisor | Dr. Ahmed Salam |