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

On startup, the **five application roles** are seeded automatically.

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
- [ ] First System Admin account
- [ ] StaffProfile, SystemSetting, AuditLog
- [ ] JWT authentication (login endpoint)
- [ ] Organization management & staff approval endpoints

**Next slices**
- [ ] Slice 2 — Blood Bank Staff (batches & inventory ledger)
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
| Frontend | 4 members |
| UI/UX | 3 members |
| AI | 1 member |