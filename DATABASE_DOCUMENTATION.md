# BloodFlow — Database Documentation

SQL Server database managed with **EF Core 10 Code First**. Everything below comes from the entity configurations in `src/BloodFlow.Infrastructure/Persistence/Configurations/` and the migrations in `Persistence/Migrations/`. The column tables in section 6 were generated from `AppDbContextModelSnapshot.cs`, which is EF's own record of the model.

**20 tables:** 13 BloodFlow tables + 7 ASP.NET Identity tables. The full design (Phase 2) has 36 tables; the other 16 belong to features not built yet (donors, donations, transfers, notifications, shortage alerts, ...).

---

## 1. Conventions used everywhere

| Convention | How | Why |
|---|---|---|
| Collation | `modelBuilder.UseCollation("Arabic_100_CI_AI")` | Arabic text search ignores case and diacritics ("أحمد" = "احمد"). **Note:** EF applies it only when EF itself creates the database. On a hosted server where the database is created in a control panel, choose `Arabic_100_CI_AI` when creating it. |
| Enums | `HasConversion<string>()` + `varchar(n)` + a CHECK constraint listing the allowed values | Readable data; reordering an enum in C# can never corrupt stored values. |
| Times | `datetime2`, always **UTC** | One unambiguous time base. |
| Dates | `DateOnly` → `date` (collection/expiry dates) | Expiry is a calendar day, not an instant. |
| Concurrency | `rowversion` column `RowVersion` | Optimistic concurrency → `409` on conflicting writes. |
| Deletes | Every BloodFlow foreign key is `ON DELETE RESTRICT` (`NO ACTION`). Only ASP.NET Identity's own link tables (user roles, claims, logins, tokens) keep Identity's default cascade. | Medical records are never cascaded away. Records change status instead of being deleted. |
| Business keys in the DB | CHECK constraints, unique/filtered indexes, composite foreign keys | The database rejects invalid data even if the C# code has a bug. |

---

## 2. Entity relationship overview

```
Governorates 1 ──< Organizations 1 ──< StaffProfiles >── 1 AspNetUsers
                        │ 1                                   │ 1
                        ├──< BloodBatches 1 ──< InventoryTransactions >── 0..1 ReservationLines
                        │          ▲ 1
                        │          └────────< ReservationLines >── 1 Reservations
                        │                                              │
                        ├──< BloodRequests (as destination hospital) 1─┴─< Reservations
                        │         │ 1
                        │         ├──< BloodRequestStatusHistory
                        │         └── 0..1 EmergencyOverrides
BloodGroups 1 ──< BloodBatches, BloodRequests, Reservations, ReservationLines
AspNetUsers 1 ──< AuditLogs (actor), BloodRequests (creator / verifier), ...
SystemSettings (stand-alone key/value table)
```

### Composite foreign keys (the most important design idea)
A normal foreign key says "this id exists". A **composite** foreign key says "this id exists **and has these exact values**", so business rules become impossible to break.

| From | To (alternate key) | Guarantees |
|---|---|---|
| `StaffProfiles(OrganizationId, StaffKind)` | `Organizations(Id, Type)` | A hospital employee belongs to a hospital, a blood-bank employee to a blood bank. |
| `BloodRequests(DestinationHospitalId, DestinationOrgType)` + CHECK `DestinationOrgType = 'Hospital'` | `Organizations(Id, Type)` | A request's destination is always a hospital. |
| `Reservations(RequestId, BloodGroupId)` | `BloodRequests(Id, BloodGroupId)` | A reservation has the same blood group as its request. |
| `ReservationLines(ReservationId, OrganizationId, BloodGroupId)` | `Reservations(Id, OrganizationId, BloodGroupId)` | A line belongs to its reservation's organization and blood group. |
| `ReservationLines(BatchId, OrganizationId, BloodGroupId)` | `BloodBatches(Id, OrganizationId, BloodGroupId)` | The reserved batch is at the same organization and has the same blood group. **A wrong-group or wrong-bank allocation is impossible.** |
| `EmergencyOverrides(RequestId, RequestType)` + CHECK `RequestType = 'Emergency'` | `BloodRequests(Id, RequestType)` | Only emergency requests can have an override. |

The alternate keys these need (`AK_Organizations_Id_Type`, `AK_BloodRequests_Id_BloodGroupId`, `AK_BloodRequests_Id_RequestType`, `AK_Reservations_Id_OrganizationId_BloodGroupId`, `AK_BloodBatches_Id_OrganizationId_BloodGroupId`) are created by the migrations. They cost nothing extra because `Id` alone is already unique.

---

## 3. Tables by feature

### Reference data
- **`Governorates`** — 27 Egyptian governorates (seeded). Unique `Name`.
- **`BloodGroups`** — 8 groups (seeded). Unique `Code`.

### Organizations and people
- **`Organizations`** — hospitals and blood banks. `Type` ∈ {Hospital, BloodBank}, `Status` ∈ {Active, Deactivated}. Unique `(Name, GovernorateId)`. Alternate key `(Id, Type)`.
- **`AspNetUsers`** (Identity, extended by `ApplicationUser`) — adds `FullName`, `AccountStatus` ∈ {Pending, Active, Rejected, Deactivated} (default `Pending`), `CreatedAt`. The email index `EmailIndex` is **unique**.
- **`StaffProfiles`** — one row per hospital/blood-bank employee: organization, `StaffKind`, `JobTitle`, who approved and when, rejection reason. PK = `UserId`.
- **`AspNetRoles`, `AspNetUserRoles`** and the other Identity tables — roles and their assignment.

### Administration
- **`SystemSettings`** — business parameters (key/value with a `ValueType`). Medical parameters need an `ApprovalReference` to change. Seeded at startup (see section 5).
- **`AuditLogs`** — who did what, when, from which IP, old/new values as JSON, reason, correlation id. CHECK: a `User` actor must have a user id; a `System` actor must not.

### Inventory (Slice 2)
- **`BloodBatches`** — a group of blood units with one collection date and one expiry date at one organization. `OnHandQuantity` (physically there), `ReservedQuantity` (held for requests). **Available = OnHand − Reserved** (computed, not stored).
  CHECKs: OnHand ≥ 0, Reserved ≥ 0, **OnHand ≥ Reserved**, Initial > 0, Expiry > Collection, allowed Status/SourceType/ExpiryBasis.
- **`InventoryTransactions`** — the **append-only ledger**: one row per stock movement (`Intake`, `Adjust`, `Reserve`, `Release`, `Deduct`) with the deltas and the quantities after the move. Rows are never updated or deleted. CHECKs: adjustments need a reason; `Reserve/Release/Deduct` must point to a `ReservationLineId`; quantities after the move are consistent; actor rule as in `AuditLogs`. Filtered unique index on `ClientRequestId` (idempotency).

### Requests and reservations (Slice 3)
- **`BloodRequests`** — the **need**: number (`BR-2026-000001`, from the SQL sequence `BloodRequestNumbers`), type, status, verification, blood group, units, `NeededBy`, destination hospital, requester, patient name (nullable after anonymization).
  CHECKs: units > 0; destination is a hospital; allowed enum values; **Fulfilled/Received ⇒ Verified or emergency override used**; anonymized ⇒ no name.
  Indexes: unique `RequestNumber`; filtered unique `ClientRequestId`; `(Status, RequestType, NeededBy)` for the board; `(DestinationHospitalId, Status)`; `(DestinationHospitalId, BloodGroupId, CreatedAt)` for the duplicate check.
- **`BloodRequestStatusHistory`** — one row per status change. Unique `(RequestId, ToStatus)`. CHECK lists the only allowed transitions:
  `NULL→Pending`, `Pending→Approved|Rejected|Cancelled`, `Approved→Fulfilled|Cancelled`, `Fulfilled→Received`.
- **`Reservations`** — the **stock hold** for a request at one organization. Status ∈ {Active, Consumed, Released}. Filtered unique index `UX_Reservations_OneLivePerRequest` on `RequestId` where status is Active or Consumed → **at most one live reservation per request**. CHECK: `ClosedAt` is null only while Active.
- **`ReservationLines`** — "Q units from batch B". Status ∈ {Active, Consumed, Released, Invalidated}; `InvalidationReason` required only when Invalidated (e.g. `BatchExpired`). Index `(BatchId, Status)`.
- **`EmergencyOverrides`** — created when an unverified emergency request is fulfilled with a reason. One per request (unique `RequestId`). `ReviewStatus` ∈ {PendingReview, Reviewed}.

---

## 4. Migrations

Applied in this order (folder `src/BloodFlow.Infrastructure/Persistence/Migrations/`):

| # | Migration | What it does |
|---|---|---|
| 1 | `20261029215507_InitialCreate` | `Governorates`, `BloodGroups`, `Organizations` |
| 2 | `20261031142406_SeedReferenceData` | Inserts the 27 governorates and 8 blood groups |
| 3 | `20261031164252_AddIdentity` | The 7 ASP.NET Identity tables |
| 4 | `20261120150010_AddAdminSlice` | `AccountStatus`, `FullName`, `CreatedAt` on users; unique email index; `AK_Organizations_Id_Type`; `StaffProfiles`, `SystemSettings`, `AuditLogs` |
| 5 | `20261120151138_AddInventory` | `BloodBatches`, `InventoryTransactions` |
| 6 | `20261122180625_AddRequests` | Sequence `BloodRequestNumbers`; `BloodRequests`, `BloodRequestStatusHistory`, `Reservations`, `ReservationLines`, `EmergencyOverrides`; `InventoryTransactions.ReservationLineId`; alternate key on `BloodBatches` |

**Commands** (from the repository root):
```bash
# apply all migrations to the database in your connection string
dotnet ef database update --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api

# create a new migration after changing entities/configurations
dotnet ef migrations add <Name> --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api

# list migrations and see which are applied
dotnet ef migrations list --project src/BloodFlow.Infrastructure --startup-project src/BloodFlow.Api
```
Always open and read a new migration before applying it. Never edit a migration that has already been applied to a shared database; add a new one instead.

---

## 5. Seed data

| What | How | When |
|---|---|---|
| 27 governorates (ids 1–27) | `HasData` in `GovernorateConfiguration` → migration `SeedReferenceData` | `database update` |
| 8 blood groups: 1 A+, 2 A−, 3 B+, 4 B−, 5 AB+, 6 AB−, 7 O+, 8 O− | `HasData` in `BloodGroupConfiguration` | `database update` |
| 5 roles: SystemAdmin, HospitalStaff, BloodBankStaff, Donor, Recipient | `IdentitySeeder.SeedRolesAsync` | Every API startup (creates only missing ones) |
| First System Admin | `IdentitySeeder.SeedAdminAsync` from `AdminSeed:Email` / `AdminSeed:Password` | Every API startup (creates if missing, re-activates if needed) |
| System settings | `SystemSettingSeeder` | Every API startup (inserts only **missing** keys, never overwrites an admin's change) |

**Default system settings**

| Key | Default | Medical? | Used by |
|---|---|---|---|
| `Donation.MinimumDonorAgeYears` | 18 | yes | (donor slice, not built yet) |
| `Donation.IntervalDays` | 56 | yes | (donor slice, not built yet) |
| `Inventory.DefaultShelfLifeDays` | 35 | yes | Intake without an expiry date |
| `Requests.MaxUnitsPerRequest` | 10 | yes | Request creation |
| `Inventory.NearExpiryAlertDays` | 7 | no | Inventory summary |
| `Requests.EmergencyEscalationMinutes` | 30 | no | `needsEscalation` flag |
| `Requests.ReceiptConfirmationHours` | 48 | no | `isReceiptOverdue` flag |
| `Requests.DuplicateWindowHours` | 24 | no | Duplicate request check |
| `Privacy.PatientNameRetentionMonths` | 12 | no | Hourly anonymization |

Medical values are assumptions from Phase 1 and should be confirmed by the supervisor.

---

## 6. Columns of every table (generated from the EF model snapshot)

#### `AuditLogs` — entity `AuditLog`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `bigint` | no | identity |
| `Action` | `varchar(60)` | no |  |
| `ActorRole` | `varchar(30)` | yes |  |
| `ActorType` | `varchar(10)` | no |  |
| `ActorUserId` | `int` | yes |  |
| `CorrelationId` | `uniqueidentifier` | yes |  |
| `EntityId` | `varchar(40)` | yes |  |
| `EntityType` | `varchar(60)` | no |  |
| `IpAddress` | `varchar(45)` | yes |  |
| `NewValues` | `nvarchar(max)` | yes |  |
| `OccurredAt` | `datetime2` | no |  |
| `OldValues` | `nvarchar(max)` | yes |  |
| `OrganizationId` | `int` | yes |  |
| `Reason` | `nvarchar(500)` | yes |  |

#### `BloodBatches` — entity `BloodBatch`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `BloodGroupId` | `tinyint` | no |  |
| `CreatedAt` | `datetime2` | no |  |
| `CreatedByUserId` | `int` | no |  |
| `ExpiredAt` | `datetime2` | yes |  |
| `ExpiryBasis` | `varchar(20)` | no |  |
| `ExpiryDate` | `date` | no |  |
| `InitialQuantity` | `int` | no |  |
| `OnHandQuantity` | `int` | no |  |
| `OrganizationId` | `int` | no |  |
| `OriginalCollectionDate` | `date` | no |  |
| `ReservedQuantity` | `int` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `SourceType` | `varchar(15)` | no |  |
| `Status` | `varchar(10)` | no |  |

#### `BloodGroups` — entity `BloodGroup`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `tinyint` | no |  |
| `Code` | `varchar(3)` | no |  |

#### `BloodRequests` — entity `BloodRequest`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `BloodGroupId` | `tinyint` | no |  |
| `ClientRequestId` | `uniqueidentifier` | yes |  |
| `CreatedAt` | `datetime2` | no |  |
| `CreatedByUserId` | `int` | no |  |
| `DestinationHospitalId` | `int` | no |  |
| `DestinationOrgType` | `varchar(12)` | no |  |
| `EmergencyOverrideUsed` | `bit` | no |  |
| `NeededBy` | `datetime2` | no |  |
| `PatientAnonymizedAt` | `datetime2` | yes |  |
| `PatientFullName` | `nvarchar(200)` | yes |  |
| `RelationshipToPatient` | `nvarchar(100)` | yes |  |
| `RequestNumber` | `varchar(20)` | no |  |
| `RequestType` | `varchar(10)` | no |  |
| `RequesterCapacity` | `varchar(15)` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `Status` | `varchar(10)` | no |  |
| `StatusChangedAt` | `datetime2` | no |  |
| `UnitsRequested` | `int` | no |  |
| `VerificationStatus` | `varchar(10)` | no |  |
| `VerifiedAt` | `datetime2` | yes |  |
| `VerifiedByUserId` | `int` | yes |  |

#### `BloodRequestStatusHistory` — entity `BloodRequestStatusHistory`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `bigint` | no | identity |
| `ChangedAt` | `datetime2` | no |  |
| `ChangedByUserId` | `int` | no |  |
| `CorrelationId` | `uniqueidentifier` | yes |  |
| `FromStatus` | `varchar(10)` | yes |  |
| `Reason` | `nvarchar(500)` | yes |  |
| `RequestId` | `int` | no |  |
| `ToStatus` | `varchar(10)` | no |  |

#### `EmergencyOverrides` — entity `EmergencyOverride`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `OrganizationId` | `int` | no |  |
| `OverriddenAt` | `datetime2` | no |  |
| `OverriddenByUserId` | `int` | no |  |
| `ReasonText` | `nvarchar(500)` | no |  |
| `RequestId` | `int` | no |  |
| `RequestType` | `varchar(10)` | no |  |
| `ReviewNotes` | `nvarchar(500)` | yes |  |
| `ReviewStatus` | `varchar(15)` | no |  |
| `ReviewedAt` | `datetime2` | yes |  |
| `ReviewedByUserId` | `int` | yes |  |

#### `Governorates` — entity `Governorate`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `smallint` | no |  |
| `Name` | `nvarchar(60)` | no |  |

#### `InventoryTransactions` — entity `InventoryTransaction`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `bigint` | no | identity |
| `ActorType` | `varchar(10)` | no |  |
| `BatchId` | `int` | no |  |
| `ClientRequestId` | `uniqueidentifier` | yes |  |
| `CorrelationId` | `uniqueidentifier` | yes |  |
| `CreatedAt` | `datetime2` | no |  |
| `OnHandAfter` | `int` | no |  |
| `OnHandDelta` | `int` | no |  |
| `OrganizationId` | `int` | no |  |
| `PerformedByUserId` | `int` | yes |  |
| `Reason` | `nvarchar(500)` | yes |  |
| `ReasonCode` | `varchar(12)` | yes |  |
| `ReservationLineId` | `int` | yes |  |
| `ReservedAfter` | `int` | no |  |
| `ReservedDelta` | `int` | no |  |
| `TransactionType` | `varchar(10)` | no |  |

#### `Organizations` — entity `Organization`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `CityName` | `nvarchar(100)` | no |  |
| `GovernorateId` | `smallint` | no |  |
| `Name` | `nvarchar(200)` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `Status` | `varchar(12)` | no |  |
| `Type` | `varchar(12)` | no |  |

#### `Reservations` — entity `Reservation`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `BloodGroupId` | `tinyint` | no |  |
| `ClosedAt` | `datetime2` | yes |  |
| `CreatedAt` | `datetime2` | no |  |
| `CreatedByUserId` | `int` | no |  |
| `OrganizationId` | `int` | no |  |
| `RequestId` | `int` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `Status` | `varchar(10)` | no |  |

#### `ReservationLines` — entity `ReservationLine`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `BatchId` | `int` | no |  |
| `BloodGroupId` | `tinyint` | no |  |
| `ClosedAt` | `datetime2` | yes |  |
| `InvalidationReason` | `varchar(30)` | yes |  |
| `OrganizationId` | `int` | no |  |
| `Quantity` | `int` | no |  |
| `ReservationId` | `int` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `Status` | `varchar(12)` | no |  |

#### `StaffProfiles` — entity `StaffProfile`

Primary key: `UserId`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `UserId` | `int` | no |  |
| `ApprovedAt` | `datetime2` | yes |  |
| `ApprovedByUserId` | `int` | yes |  |
| `JobTitle` | `nvarchar(100)` | no |  |
| `OrganizationId` | `int` | no |  |
| `RejectionReason` | `nvarchar(500)` | yes |  |
| `StaffKind` | `varchar(12)` | no |  |

#### `SystemSettings` — entity `SystemSetting`

Primary key: `SettingKey`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `SettingKey` | `varchar(60)` | yes |  |
| `ApprovalReference` | `nvarchar(200)` | yes |  |
| `ApprovedAt` | `datetime2` | yes |  |
| `ApprovedByUserId` | `int` | yes |  |
| `Description` | `nvarchar(300)` | no |  |
| `IsMedicalParameter` | `bit` | no |  |
| `RowVersion` | `rowversion` | no | optimistic concurrency |
| `SettingValue` | `nvarchar(200)` | no |  |
| `UpdatedAt` | `datetime2` | yes |  |
| `UpdatedByUserId` | `int` | yes |  |
| `ValueType` | `varchar(10)` | no |  |

#### `AspNetUsers` — entity `ApplicationUser`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `AccessFailedCount` | `int` | no |  |
| `AccountStatus` | `varchar(12)` | no | default "Pending" |
| `ConcurrencyStamp` | `nvarchar(max)` | yes |  |
| `CreatedAt` | `datetime2` | no | default "SYSUTCDATETIME( |
| `Email` | `nvarchar(256)` | yes |  |
| `EmailConfirmed` | `bit` | no |  |
| `FullName` | `nvarchar(200)` | no |  |
| `LockoutEnabled` | `bit` | no |  |
| `LockoutEnd` | `datetimeoffset` | yes |  |
| `NormalizedEmail` | `nvarchar(256)` | yes |  |
| `NormalizedUserName` | `nvarchar(256)` | yes |  |
| `PasswordHash` | `nvarchar(max)` | yes |  |
| `PhoneNumber` | `nvarchar(max)` | yes |  |
| `PhoneNumberConfirmed` | `bit` | no |  |
| `SecurityStamp` | `nvarchar(max)` | yes |  |
| `TwoFactorEnabled` | `bit` | no |  |
| `UserName` | `nvarchar(256)` | yes |  |

#### `AspNetRoles` — entity `IdentityRole<int>`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `ConcurrencyStamp` | `nvarchar(max)` | yes |  |
| `Name` | `nvarchar(256)` | yes |  |
| `NormalizedName` | `nvarchar(256)` | yes |  |

#### `AspNetRoleClaims` — entity `IdentityRoleClaim<int>`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `ClaimType` | `nvarchar(max)` | yes |  |
| `ClaimValue` | `nvarchar(max)` | yes |  |
| `RoleId` | `int` | no |  |

#### `AspNetUserClaims` — entity `IdentityUserClaim<int>`

Primary key: `Id`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | identity |
| `ClaimType` | `nvarchar(max)` | yes |  |
| `ClaimValue` | `nvarchar(max)` | yes |  |
| `UserId` | `int` | no |  |

#### `AspNetUserLogins` — entity `IdentityUserLogin<int>`

Primary key: `LoginProvider, ProviderKey`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `LoginProvider` | `nvarchar(450)` | yes |  |
| `ProviderKey` | `nvarchar(450)` | yes |  |
| `ProviderDisplayName` | `nvarchar(max)` | yes |  |
| `UserId` | `int` | no |  |

#### `AspNetUserRoles` — entity `IdentityUserRole<int>`

Primary key: `UserId, RoleId`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `UserId` | `int` | no |  |
| `RoleId` | `int` | no |  |

#### `AspNetUserTokens` — entity `IdentityUserToken<int>`

Primary key: `UserId, LoginProvider, Name`

| Column | SQL type | Null | Notes |
|---|---|---|---|
| `UserId` | `int` | no |  |
| `LoginProvider` | `nvarchar(450)` | yes |  |
| `Name` | `nvarchar(450)` | yes |  |
| `Value` | `nvarchar(max)` | yes |  |
