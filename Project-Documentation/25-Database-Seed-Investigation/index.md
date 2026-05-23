---
layout: default
title: "🔍 Database Seed Investigation"
---

# 🔍 Database Seed Investigation — Root Cause Report
> **Date:** 2026-05-18  
> **Severity:** HIGH — Data reappears after manual deletion  
> **Status:** Root cause identified — fix applied

---

## 🎯 Root Cause Summary (One Paragraph)

Every time the application starts — whether via `dotnet run`, a Docker container restart, or a Railway deployment — `Program.cs` unconditionally calls `DbInitializer.SeedAsync()`. This seeder checks `if (!context.Universities.AnyAsync())` and `if (!context.SystemUsers.AnyAsync(u => u.Role == SuperAdmin))`. The moment you manually delete all data from the database, these guards become `true`, and on the very next app start the seeder recreates the entire academic hierarchy (University, College, Department, Batch, 4 Groups, Doctor, Student) plus the SuperAdmin. **The data is never permanently gone — it comes back on every restart while the database is empty.**

---

## 1. COMPLETE STARTUP EXECUTION SEQUENCE

```
App starts (docker run / railway deploy / dotnet run)
    │
    ▼
Program.cs — services configured
    │
    ▼
using (var scope = app.Services.CreateScope())   ← Line 437
{
    │
    ├── [1] db.Database.Migrate()                ← Line 445
    │         Applies any pending EF Core migrations
    │         Runs every startup — safe, idempotent
    │
    ├── [2] Raw SQL patches (Lines 450-576)
    │         ALTER TABLE ... ADD COLUMN IF NOT EXISTS
    │         CREATE TABLE IF NOT EXISTS
    │         INSERT INTO __EFMigrationsHistory ON CONFLICT DO NOTHING
    │         These are also idempotent — safe
    │
    └── [3] await DbInitializer.SeedAsync(services)   ← Line 579  ⚠️ DANGEROUS
              │
              ├── context.Database.MigrateAsync()  ← runs migrations AGAIN
              │
              ├── if (!SystemUsers.Any(SuperAdmin))
              │       → Creates SuperAdmin + Admin profile
              │       → TRIGGERS if ALL users deleted
              │
              ├── if (!Universities.AnyAsync())
              │       → Creates University, College, Department,
              │             Batch, 4 Groups, Doctor, Student
              │       → TRIGGERS if ALL universities deleted
              │
              └── SeedAcademicYearsAsync()
                      → For EACH college: seeds 4 AcademicYear records
                      → TRIGGERS if a college has no AcademicYears
}
    │
    ▼
using (var scope = app.Services.CreateScope())   ← Line 588
{
    └── Hangfire recurring jobs registered:
        ├── daily-complaint-report    (Cron.Daily)
        ├── weekly-complaint-report   (Cron.Weekly)
        ├── monthly-complaint-report  (Cron.Monthly)
        ├── academic-risk-alerts      (Cron.Daily)
        └── exam-reminders            (every 30 min)
}
    │
    ▼
app.Run()
```

---

## 2. DANGEROUS SEEDERS — DETAILED ANALYSIS

### Seeder 1: SuperAdmin Creation
| Property | Value |
|---|---|
| **File** | `UniversityManagementSystem.Infrastructure/Data/DbInitializer.cs` |
| **Method** | `SeedAsync()` → Lines 31–68 |
| **Trigger** | `if (!await context.SystemUsers.AnyAsync(u => u.Role == UserRole.SuperAdmin))` |
| **When fires** | Every app startup when NO SuperAdmin exists |
| **Creates** | `SystemUser` (email: super.admin@university.com) + `Admin` profile |
| **Risk Level** | 🟡 MEDIUM — needed for first-run, but recreates after manual delete |
| **Password** | `BCrypt.HashPassword("SuperSecretPass1!")` — hardcoded |

**The guard logic:**
```csharp
if (!await context.SystemUsers.AnyAsync(u => u.Role == UserRole.SuperAdmin))
```
This fires if:
- Fresh database (first deploy) ✅ intentional
- After `DELETE FROM "SystemUsers"` ⚠️ recreates the admin
- After `TRUNCATE "SystemUsers" CASCADE` ⚠️ recreates the admin

---

### Seeder 2: Full Academic Hierarchy (THE MAIN OFFENDER)
| Property | Value |
|---|---|
| **File** | `UniversityManagementSystem.Infrastructure/Data/DbInitializer.cs` |
| **Method** | `SeedAsync()` → Lines 75–163 |
| **Trigger** | `if (!await context.Universities.AnyAsync())` |
| **When fires** | Every app startup when Universities table is EMPTY |
| **Creates** | University → College → Department → Batch → 4 Groups → Doctor + SystemUser → Student + SystemUser |
| **Risk Level** | 🔴 HIGH — creates demo/test data on every clean start |

**Exact data created (hardcoded):**
```
University:   "Beni Suef National University"
College:      "Faculty of Computers and Information"
Department:   "Artificial Intelligence"
Batch:        "Year 4"
Groups:       Group 1, Group 2, Group 3, Group 4
Doctor:       Dr. Ahmed (ahmed@university.com / Pass123!)
Student:      Student Ali (ali@university.com / Pass123!)
```

**The guard logic:**
```csharp
if (!await context.Universities.AnyAsync())
```
This is the problem. It only checks `Universities`. So:
- Delete students → universities still exist → seeder does NOT run ✅
- Delete all entities → delete universities last → next startup recreates EVERYTHING ⚠️

---

### Seeder 3: AcademicYears Auto-Seeder
| Property | Value |
|---|---|
| **File** | `UniversityManagementSystem.Infrastructure/Data/DbInitializer.cs` |
| **Method** | `SeedAcademicYearsAsync()` → Lines 168–270 |
| **Trigger** | Runs on EVERY startup (no outer guard). Inner check: per-college, per-order |
| **When fires** | Every app startup, always |
| **Creates** | "First Year", "Second Year", "Third Year", "Fourth Year" for each college |
| **Risk Level** | 🟠 MEDIUM — runs always, but has inner duplicate guards |

**This is called unconditionally:**
```csharp
await SeedAcademicYearsAsync(context, logger);  // ← no outer if-guard
```

It only skips if `AnyAsync(y => y.CollegeId == college.Id && y.Order == seedYear.Order)`.
So: if you delete AcademicYears but keep Colleges → they ALL come back on next start.

---

## 3. MIGRATION SEED ANALYSIS

**Verdict: ✅ Migrations are CLEAN — no `InsertData()` or `HasData()` calls.**

Checked all migration files in `UniversityManagementSystem.Infrastructure/Migrations/`:
- No `migrationBuilder.InsertData()`
- No `HasData()` in any entity configuration
- No static seed rows in any migration

The seeding happens ONLY in `DbInitializer.SeedAsync()`, called from `Program.cs`.

---

## 4. RAILWAY DEPLOYMENT BEHAVIOR

### What happens on every Railway deploy:
```
git push main
    │
    ▼ GitHub Actions (.github/workflows/deploy.yml)
railway up --detach
    │
    ▼ Railway builds new Docker container from Dockerfile
    │
    ▼ ENTRYPOINT: dotnet UniversityManagementSystem.Api.dll
    │
    ▼ Program.cs runs FULLY — including DbInitializer.SeedAsync()
    │
    ▼ IF database is empty → ALL demo data recreated
```

### Railway PostgreSQL Persistence
- Railway PostgreSQL is a **persistent external service** — data survives deploys.
- Deleting data via SQL client → data is gone from DB ✅
- BUT: next app restart (deploy, redeploy, container restart) → `SeedAsync` runs → data returns ⚠️

### The Exact Trigger Sequence
```
1. You DELETE from "Universities" via pgAdmin/SQL
2. You push code → Railway deploys new container
3. Container starts → Program.cs runs
4. Migrate() executes (no changes if no new migrations)
5. DbInitializer.SeedAsync() runs
6. !Universities.AnyAsync() == TRUE (you deleted them)
7. Full academic hierarchy is recreated ← ROOT CAUSE
```

---

## 5. HANGFIRE RECURRING JOBS ANALYSIS

The 5 recurring jobs registered in Program.cs (Lines 590–616):

| Job | Schedule | Creates Data? |
|---|---|---|
| `daily-complaint-report` | Daily | ❌ Reads + generates reports only |
| `weekly-complaint-report` | Weekly | ❌ Reads + generates reports only |
| `monthly-complaint-report` | Monthly | ❌ Reads + generates reports only |
| `academic-risk-alerts` | Daily | ❌ Sends notifications — reads only |
| `exam-reminders` | Every 30 min | ❌ Sends notifications — reads only |

**Verdict: ✅ Hangfire jobs do NOT recreate data.** They are read-only/notification jobs.

---

## 6. DevController Analysis

`/api/dev/migrate-debug` endpoint in `DevController.cs`:
- Calls `MigrateAsync()` — applies migrations only
- **Does NOT call `SeedAsync()`** — safe
- Only available in `Development` environment (`IsDevelopment()` guard)
- Not relevant to the data recreation issue

---

## 7. SCENARIO MAP — When Does Data Return?

| Action | Will Data Return? | Why |
|---|---|---|
| Delete a student only | ❌ No | Universities table still has rows |
| Delete all students | ❌ No | Universities table still has rows |
| Delete all groups | ❌ No | Universities table still has rows |
| Delete a doctor | ❌ No | Universities table still has rows |
| Delete all users | ✅ YES | SuperAdmin guard fires → SuperAdmin recreated |
| Delete only universities | ✅ YES | `!Universities.Any()` fires → full hierarchy recreated |
| Delete all data (truncate all) | ✅ YES | Both guards fire → everything recreated |
| Delete academic years | ✅ YES | `SeedAcademicYearsAsync` has NO outer guard → always runs |

---

## 8. THE FIX — Safe Demo Mode

### Option A — Environment Variable Guard (RECOMMENDED)

Add a `DISABLE_SEEDING` environment variable. The seeder only runs when this flag is absent or false.

**Set in Railway:** `DISABLE_SEEDING=true`

### Option B — Seed Only SuperAdmin (Keep structure clean for demo)

Only seed the SuperAdmin (needed for login). Never auto-seed fake demo data.

### Option C — Full Disable (Nuclear — for demo day)

Comment out the entire `DbInitializer.SeedAsync(services)` call. Manual setup only.

---

## 9. RECOMMENDED FIX IMPLEMENTATION

### Files to Modify

| File | Change |
|---|---|
| `UniversityManagementSystem.Infrastructure/Data/DbInitializer.cs` | Add `DISABLE_SEEDING` flag + make academic hierarchy opt-in |
| `UniversityManagementSystem.Api/Program.cs` | Add environment check before calling SeedAsync |

### Risk Level: 🟢 LOW — additive change, does not break existing behavior

---

*Investigation completed: 2026-05-18*
