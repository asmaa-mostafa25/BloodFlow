# BloodFlow — Setup Guide

Step by step, from an empty Windows machine to a running API with Swagger. Every command here was used on this project.

---

## 1. Required software

| Software | Version | Why |
|---|---|---|
| **.NET SDK** | **10.0.100 or newer 10.0.x** | The projects target `net10.0`; `global.json` pins SDK 10.0.100 (roll-forward to newer 10.0 feature bands). Check with `dotnet --version`. |
| **Visual Studio 2026** (18.x) with the *ASP.NET and web development* workload | 18.0 or newer | Microsoft supports building `net10.0` only in Visual Studio 18.0+. **Visual Studio 2022 (17.x) is not officially supported for .NET 10** — you can still build and run with the `dotnet` command line (below). VS Code + C# Dev Kit also works. |
| **SQL Server** | Express, Developer or LocalDB (any recent version) | The database. Express is free. |
| **SQL Server Management Studio (SSMS)** | any recent | To look at the database. Also shows your server name. |
| **Git** | any recent | To clone and push. |
| **dotnet-ef tool** | 10.x | To apply migrations. Install once: `dotnet tool install --global dotnet-ef` (or `dotnet tool update --global dotnet-ef`). **Close and reopen the terminal afterwards.** |
| Postman (optional) | any | Alternative to Swagger. |

---

## 2. Get the code

```powershell
cd Desktop
git clone https://github.com/asmaa-mostafa25/BloodFlow.git BloodFlow-Main
cd BloodFlow-Main
```

Open `BloodFlow.slnx` in Visual Studio (File → Open → Project/Solution). The solution has 5 projects: `BloodFlow.Domain`, `BloodFlow.Application`, `BloodFlow.Infrastructure`, `BloodFlow.Api`, `BloodFlow.Tests`. Set **BloodFlow.Api** as the startup project (right-click → *Set as Startup Project*).

Already have the folder? Update it instead:
```powershell
git status          # must be clean, or commit/stash your changes first
git pull origin master
```

---

## 3. Find your SQL Server name

Open SSMS. The **Server name** on the connect screen is what you need, for example:
- `DESKTOP-XXXX\SQLEXPRESS` (SQL Server Express)
- `(localdb)\MSSQLLocalDB` (LocalDB)
- `.` (a default instance on this machine)

No SSMS? Run `services.msc` and look for **SQL Server (NAME)**: `(SQLEXPRESS)` → `.\SQLEXPRESS`; `(MSSQLSERVER)` → `.`. Make sure the service is **Running**.

---

## 4. Secrets (User Secrets)

Secrets are **never** stored in the repository. On your machine they live in User Secrets (outside the project folder). Run from `src\BloodFlow.Api`:

```powershell
cd src\BloodFlow.Api

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=BloodFlowMain;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "PASTE_A_RANDOM_KEY_HERE"
dotnet user-secrets set "AdminSeed:Email" "admin@bloodflow.local"
dotnet user-secrets set "AdminSeed:Password" "Choose-A-Strong-Passw0rd"

# optional, only if you run the AI service locally
dotnet user-secrets set "BloodFlowAi:ApiKey" "THE_AI_SERVICE_KEY"

dotnet user-secrets list      # check the keys (do not share the output: it shows the values)
cd ..\..
```

Generate a strong JWT key in PowerShell:
```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

| Key | Required | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | yes | The startup seeder queries the database |
| `Jwt:Key` | yes | Startup throws if missing. At least 32 characters. |
| `AdminSeed:Email`, `AdminSeed:Password` | yes | Creates the first System Admin. Password: ≥ 8 chars, upper, lower, digit. |
| `BloodFlowAi:ApiKey` | no | AI endpoints only |

Note: the command is `user-secrets` (with **s**). `dotnet user-secret` fails with "does not exist".

---

## 5. Create the database

From the repository root (`BloodFlow-Main`):
```powershell
dotnet ef database update --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api
```
The last line should be `Done.` This creates the database `BloodFlowMain` with the `Arabic_100_CI_AI` collation, all 20 tables, and the 27 governorates and 8 blood groups.
Check in SSMS: Databases → `BloodFlowMain` → Tables. `SELECT * FROM __EFMigrationsHistory` shows 6 rows.

---

## 6. Run the API

**Visual Studio:** choose the `https` profile and press **F5**. The browser opens Swagger.

**Command line:**
```powershell
dotnet run --project src/BloodFlow.Api
```
Then open **https://localhost:7244/swagger** (`https` profile) or **http://localhost:5034/swagger** (`http` profile, the default for `dotnet run`). Ports come from `src/BloodFlow.Api/Properties/launchSettings.json`.

On first start the API creates the roles, the admin account and the system settings automatically.

**Run the tests:**
```powershell
dotnet test
```
Expected: 83 tests passed.

---

## 7. Test the endpoints

### Swagger
1. Expand `POST /api/auth/login` → *Try it out* → your `AdminSeed` email and password → *Execute*.
2. Copy the `token` from the response.
3. Click **Authorize** (top right) → paste the token → *Authorize*.
4. Now every endpoint sends the token. Follow the scenario in `API_DOCUMENTATION.md` §8 to create organizations, staff, stock and a full blood request.

### Visual Studio `.http` file
Open `src/BloodFlow.Api/BloodFlow.Api.http`. `@host` is already `https://localhost:7244`; paste a token into `@token`. Click *Send request* above each request. The file contains `YOUR_PASSWORD` placeholders — type real values only locally and never commit them.

### Postman
- New request → method + `https://localhost:<port>/api/...`
- Login first, then in **Authorization** choose *Bearer Token* and paste the token.
- Body → *raw* → *JSON*.
- If Postman complains about the local certificate, run `dotnet dev-certs https --trust` once, or turn off SSL verification in Postman for localhost only.

---

## 8. Troubleshooting (real errors met in this project)

| You see | Meaning | Fix |
|---|---|---|
| `Could not execute because ... dotnet-ef does not exist` | The EF tool is not installed | `dotnet tool install --global dotnet-ef`, reopen the terminal |
| `dotnet-user-secret does not exist` | Typo | `dotnet user-secrets` |
| `SqlException ... server was not found or was not accessible` | Wrong server name, or SQL Server service stopped | Use the exact name from SSMS (e.g. `DESKTOP-XXXX\SQLEXPRESS`); start the service |
| `SqlException ... Login failed for user` (18456) | Wrong user/password in the connection string | Fix the secret; restart the API |
| `Jwt settings are missing` | `Jwt:Key` not set | Set the user secret |
| `AdminSeed:Email and AdminSeed:Password must be set` | Admin seed missing | Set both user secrets |
| `Failed to create admin user: Passwords must...` | Admin password too weak | Use upper + lower + digit, ≥ 8 chars |
| `401` on every endpoint | No/expired token | Log in again and click Authorize |
| `403 Auth.AccountPending` | Staff account not approved | Approve it as admin |
| Browser shows a CORS error but Swagger/Postman work | The frontend's address is not in `Cors:AllowedOrigins` | Add its exact URL (scheme + host + port) |
| `404` on `/` | Normal: the API has no home page | Use `/swagger` or an `/api/...` route |

**How to read any error:** (1) the exception type (`SqlException` = database, `NullReferenceException` = code), (2) the key words in the message (`not found`, `Login failed`, `does not exist`), (3) ask "where does this value come from?" (which setting/file), (4) if the message is generic (`500.30`, `Build failed`), find the log that has the details.

---

## 9. Deploying (summary)

The API is hosted on **MonsterASP.NET** (free plan). Full steps and troubleshooting are in `README.md` → *Deployment*. Short version:
1. Copy `src/BloodFlow.Api/appsettings.Production.example.json` to `appsettings.Production.json` (git-ignored) and fill in the server values.
2. Create the tables on the server database from your machine with `dotnet ef database update ... --connection "<public server connection string>"`.
3. Visual Studio → right-click `BloodFlow.Api` → **Publish** with the MonsterASP publish profile.
4. Check `https://bloodflow.runasp.net/api/lookups/blood-groups`.
