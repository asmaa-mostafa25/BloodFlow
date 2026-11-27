# 🩸 BloodFlow — Backend API

**Smart Blood Inventory & Redistribution System** — graduation project.

[![CI](https://github.com/asmaa-mostafa25/BloodFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/asmaa-mostafa25/BloodFlow/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![EF Core 10](https://img.shields.io/badge/EF%20Core-10-512BD4)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927)

BloodFlow connects **hospitals and blood banks** across Egypt's 27 governorates. It tracks blood stock per batch, handles normal and emergency blood requests, reserves units safely (first-expiring-first-out), and keeps a full audit trail. Donors, recipients, transfers and notifications are designed and planned next.

This repository is the **ASP.NET Core Web API**. The frontend, UI/UX and AI service are built by their teams.

- **Live API:** https://bloodflow.runasp.net (Swagger: `/swagger`)
- **Health check of the data:** https://bloodflow.runasp.net/api/lookups/blood-groups

---

## 📚 Documentation

| File | What's inside |
|---|---|
| [SETUP_GUIDE.md](SETUP_GUIDE.md) | Install, configure secrets, create the database, run, test with Swagger/Postman, troubleshooting |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layers, folders, every project's job, DI, configuration, request lifecycle, auth, errors, CI |
| [DATABASE_DOCUMENTATION.md](DATABASE_DOCUMENTATION.md) | All 20 tables and columns, relationships, constraints, indexes, migrations, seed data |
| [API_DOCUMENTATION.md](API_DOCUMENTATION.md) | Every endpoint with roles, bodies, rules and error codes |
| [PROJECT_STATUS.md](PROJECT_STATUS.md) | What's done, partial, missing; known problems; prioritized tasks |
| [LEARNING_GUIDE.md](LEARNING_GUIDE.md) | Beginner-friendly walkthrough of the code, design decisions, interview questions, practice exercises |
| [CLAUDE.md](CLAUDE.md) | Instructions for AI assistants working on this repository |
| [`docs/reports/`](docs/reports) | Arabic PDF reports for Slices 1–3 and the graduation defense guide |

---

## 📍 Status

| Part | Scope | State |
|---|---|---|
| Foundation | Clean Architecture, reference data, Identity, JWT login | ✅ Done |
| **Slice 1 · System Admin** | Staff registration & approval, organizations, settings, audit log | ✅ Done |
| **Slice 2 · Inventory** | Batches, stock ledger, intake, adjustments, expiry job, summaries | ✅ Done |
| **Slice 3 · Hospital requests** | Requests, FEFO reservations, fulfilment, receipt, emergencies, admin monitoring | ✅ Done |
| **AI integration** | Backend gateway to the AI service (forecast, shortage, expiry, substitutions, retrain) | ✅ Done (needs the AI service running) |
| **Deployment** | MonsterASP.NET, CORS, configurable Swagger | ✅ Done |
| Slice 4 · Donors | Donor profile, donations & verification, points & badges | ⏳ Next |
| Slice 5 · Recipients | Patient / representative requests | ⏳ |
| Slice 6 · Transfers | Moving stock between organizations, shortage alerts | ⏳ |
| Slice 7 · Notifications | E-mail and in-app alerts | ⏳ |

Details and known issues: [PROJECT_STATUS.md](PROJECT_STATUS.md).

---

## 🛠️ Tech stack

ASP.NET Core Web API (.NET 10) · Entity Framework Core 10 · SQL Server · ASP.NET Core Identity · JWT Bearer · OpenAPI + Swagger UI · xUnit · GitHub Actions.

```
src/
  BloodFlow.Domain          entities and enums (no dependencies)
  BloodFlow.Application     interfaces, DTOs, Result, pure business rules
  BloodFlow.Infrastructure  EF Core, Identity, JWT, services, background job, AI client
  BloodFlow.Api             controllers, Program.cs, Swagger, CORS, error handling
tests/
  BloodFlow.Tests           83 unit tests
```

---

## 🚀 Quick start

Requirements: .NET SDK 10.0.100+, SQL Server, `dotnet-ef` 10, Visual Studio 2026 (or the `dotnet` CLI). Full guide: [SETUP_GUIDE.md](SETUP_GUIDE.md).

```powershell
git clone https://github.com/asmaa-mostafa25/BloodFlow.git
cd BloodFlow

cd src\BloodFlow.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=BloodFlowMain;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "A_RANDOM_KEY_OF_AT_LEAST_32_CHARACTERS"
dotnet user-secrets set "AdminSeed:Email" "admin@bloodflow.local"
dotnet user-secrets set "AdminSeed:Password" "Choose-A-Strong-Passw0rd"
cd ..\..

dotnet tool install --global dotnet-ef        # once per machine
dotnet ef database update --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api
dotnet run --project src/BloodFlow.Api        # then open http://localhost:5034/swagger
dotnet test                                    # 83 tests
```

---

## ⚙️ Configuration

| Key | Where | Default | Notes |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | User Secrets / server file | — | Required |
| `Jwt:Key` | User Secrets / server file | — | Required, ≥ 32 characters |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | `BloodFlow.Api`, `BloodFlow.Client`, `60` | |
| `AdminSeed:Email` / `AdminSeed:Password` | User Secrets / server file | — | Required, creates the first admin |
| `App:TimeZone` | `appsettings.json` | `Africa/Cairo` | Defines "today" for expiry |
| `Swagger:Enabled` | `appsettings.json` / server file | `false` | Swagger is always on in Development |
| `Cors:AllowedOrigins` | `appsettings.json` / server file | `localhost:3000`, `4200`, `5173` | Frontend URLs allowed to call the API from a browser |
| `BloodFlowAi:BaseUrl` | `appsettings.json` | `http://127.0.0.1:8000` | AI service address |
| `BloodFlowAi:ApiKey` | User Secrets / server file | — | Optional; AI calls fail with 502 if the AI requires it |

Business parameters (max units per request, near-expiry days, ...) are in the `SystemSettings` table and editable by the admin: see [DATABASE_DOCUMENTATION.md](DATABASE_DOCUMENTATION.md#5-seed-data).

**Never commit secrets.** `appsettings.Production.json` and `publish/` are git-ignored; use [`appsettings.Production.example.json`](src/BloodFlow.Api/appsettings.Production.example.json) as the template.

---

## 🌍 Deployment

The API runs on **MonsterASP.NET** (free plan): **https://bloodflow.runasp.net** · Swagger: `/swagger`.

**Server settings** live in `src/BloodFlow.Api/appsettings.Production.json`, which is **git-ignored** and published with the app. Copy [`appsettings.Production.example.json`](src/BloodFlow.Api/appsettings.Production.example.json) and fill in the real values. Never commit it.

| | From your machine | From the website |
|

---

## 🔀 Git workflow

- `master` always builds and passes CI.
- Work on a branch: `feature/<name>` → push → Pull Request → CI green → merge.
- Before every commit: `git status` and make sure no secret file is listed.

---

## 👥 Team

| Role | Members |
|---|---|
| Backend & Team Lead | Asmaa Mostafa |
| Frontend | Fadaa Mohamed · Farah Mahmoud · Habiba Atef · Mennatullah Saeed |
| UI/UX | Maryam Helmy · Shahd Sherif · Eman Mahmoud |
| AI | Mennatullah Shaker |
| Supervisor | Dr. Ahmed Salam |
