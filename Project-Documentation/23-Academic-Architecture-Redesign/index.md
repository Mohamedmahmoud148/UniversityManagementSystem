---
layout: default
title: "🏛️ Enterprise Academic Architecture Redesign"
---

# 🏛️ Enterprise Academic Architecture Redesign
> **Date:** 2026-05-18  
> **Status:** Analysis Complete — Implementation Roadmap Active  
> **Scope:** Full system redesign from CRUD to Enterprise Academic OS

---

## Executive Summary

The current system is a functional but simple academic CRUD platform.
This document defines the path to a **production-grade University Academic Operating System**.

---

## Critical Bugs (Fix Immediately)

| # | Bug | File | Line | Fix |
|---|---|---|---|---|
| 1 | `ImportStudentsAsync` never saves to DB | `ExcelImportService.cs` | 122 | Add `AddRange` + `SaveChangesAsync` |
| 2 | `UniversityStudentId = nationalId` | `EnrollmentUploadService.cs` | 183 | Generate proper student ID |
| 3 | `StudentImportDto` missing `GroupCode` | `ImportDtos.cs` + `ExcelService.cs` | — | Add GroupCode field |
| 4 | Finalized grades can be overwritten | `ExcelImportService.cs` | 572 | Add `IsFinalized` guard |
| 5 | GPA never persisted on student | `GradeService.cs` | — | Hook into StudentAcademicStatus |
| 6 | No capacity check on enrollment | `EnrollmentService.cs` | 78 | Add count vs MaxCapacity |
| 7 | Transaction per row in BulkUploadJob | `BulkUploadJob.cs` | 70 | Single bulk transaction |

---

## New Entities Required

### 1. `StudentAcademicStatus` — Academic State + GPA Persistence
**Why:** GPA is currently recalculated on every request and never stored.
Filtering students by GPA, tracking standing changes, and applying GPA-based
credit hour limits all require a persisted academic status record.

```
StudentAcademicStatus
├── GPA                  (persisted — updated after each grade finalization)
├── CGPA
├── LastSemesterGPA
├── EarnedCreditHours
├── RegisteredCreditHours
├── RemainingCreditHours
├── TotalRequiredHours
├── AcademicStanding     (Honors/Good/Satisfactory/Warning/Probation)
├── State                (Active/Warning/Probation/Suspended/Graduated/Expelled)
├── WarningCount
└── CurrentLevel
```

**Backfill:** Run `BackfillAcademicStatusJob` after migration to populate for all existing students.

---

### 2. `AcademicPolicy` — Configurable GPA Rules
**Why:** Credit hour limits and GPA thresholds are currently hardcoded
in business logic. Different regulations or departments may have different rules.

```
AcademicPolicy
├── DefaultMaxCreditHours    (18)
├── HonorMaxCreditHours      (21 — for GPA >= 3.5)
├── WarningMaxCreditHours    (12 — for GPA < 2.0)
├── ProbationMaxCreditHours  (9  — for GPA < 1.5)
├── ProbationGpaThreshold    (1.5)
├── WarningGpaThreshold      (2.0)
├── GraduationMinGpa         (2.0)
├── MaxSubjectRetakes        (3)
└── MustRetakeFailedRequired (true)
```

**Seed:** One global default policy (RegulationId = null) applies to all unless overridden.

---

### 3. `SubjectPrerequisite` — Prerequisite Graph
**Why:** The system currently has no concept of prerequisites.
A student can register CS501 (Advanced AI) without ever completing CS101.

```
SubjectPrerequisite
├── SubjectId              (the subject being registered)
├── PrerequisiteSubjectId  (must be completed first)
├── Type                   (MustPass / MustComplete / Concurrent / Recommended)
└── MinimumGrade           (optional — e.g., minimum 70 required)
```

---

### 4. `GradeScale` — Configurable Grade Scale
**Why:** The A/B/C/D/F scale is hardcoded. Different regulations
(e.g., medical school vs engineering) may use different grade boundaries.

```
GradeScale
├── RegulationId  (null = global default)
├── Letter        (A+, A, A-, B+, B, ...)
├── MinScore      (97, 93, 90, 87, ...)
├── MaxScore
├── GradePoints   (4.0, 4.0, 3.7, 3.3, ...)
└── IsPassing     (D- and above = true)
```

**Seed:** Standard Egyptian university scale (A+ through F, 13 levels).

---

### 5. `SubjectOfferingWaitlist` — Capacity Management
**Why:** When an offering reaches MaxCapacity, students have no recourse.
A waitlist allows automatic notification when a spot opens.

```
SubjectOfferingWaitlist
├── StudentId
├── SubjectOfferingId
├── Position      (queue order)
├── AddedAt
└── IsNotified    (has student been notified of available spot?)
```

---

### 6. `ImportHistory` — Audit Trail for All Imports
**Why:** Currently, only `EnrollmentUpload` tracks import history.
Student imports, grade imports, and doctor imports leave no trace.

```
ImportHistory
├── ImportType         (Students / Grades / Doctors / Enrollment)
├── PerformedByUserId
├── FileName
├── StorageKey         (original file in R2)
├── ErrorReportKey     (generated error Excel in R2)
├── TotalRows / SuccessCount / FailedCount / SkippedCount
├── Status             (Pending / Processing / Completed / Failed)
├── ErrorSummaryJson   (structured error list)
└── ProcessingDuration
```

---

## Registration Engine — Architecture

The `EnrollmentService` currently checks only 3 conditions (Department, Batch, Group).
A real registration engine must run a **Policy Pipeline**:

```
RegistrationPipeline — Ordered Policies
═══════════════════════════════════════════════════════
Priority  1  | AcademicStatusPolicy
             | → Blocks: Suspended, Expelled, Graduated, Withdrawn

Priority  5  | AlreadyPassedPolicy
             | → Blocks if student already passed this subject

Priority  10 | PrerequisitePolicy
             | → Blocks if required prerequisites not completed

Priority  15 | DepartmentBatchGroupPolicy (existing logic)
             | → Blocks wrong dept/batch/group

Priority  20 | DuplicateEnrollmentPolicy
             | → Blocks if already enrolled this semester

Priority  25 | CreditHoursLimitPolicy
             | → Blocks if adding this subject exceeds max allowed hours
             | → Max hours depends on GPA (from AcademicPolicy)

Priority  30 | CapacityPolicy
             | → Blocks if offering is full (redirects to waitlist)

Priority  35 | SemesterAvailabilityPolicy
             | → Blocks if offering registration window is closed
═══════════════════════════════════════════════════════
```

**API Change Required:**
```
GET /api/registration/eligible-offerings?semesterId={id}
→ Returns all offerings the student CAN and CANNOT register for
→ Each offering includes: IsEligible, Blockers[], Warnings[]
→ Frontend shows eligible in green, blocked in red with reason
```

---

## GPA Engine — Lifecycle

```
Grade Finalization Event
         │
         ▼
GradeService.CalculateGradesForOfferingAsync()
         │
         ▼ (NEW — currently missing)
AcademicStatusService.RecalculateAndPersistGpaAsync(studentId)
         │
         ├─ Recalculate CGPA from all finalized grades
         ├─ Recalculate Semester GPA from current semester grades
         ├─ Update StudentAcademicStatus.GPA + CGPA + LastSemesterGPA
         ├─ RecalculateAcademicStanding()
         │       GPA >= 3.7 → Honors
         │       GPA >= 3.0 → Good
         │       GPA >= 2.0 → Satisfactory
         │       GPA >= 1.5 → Warning
         │       GPA <  1.5 → Probation
         └─ ApplyAcademicPolicyTransitions()
                 GPA < Warning threshold → IncrementWarning()
                 2+ warnings → State = Probation
                 GPA improves → State = Active
```

---

## Academic Status State Machine

```
Active ──────→ Warning (GPA < 2.0, 1 semester)
  │              │
  │              ├──→ Active (GPA recovers)
  │              └──→ Probation (GPA < 2.0, 2nd consecutive)
  │                       │
  │                       ├──→ Active (GPA recovers)
  │                       └──→ Suspended (policy or admin)
  │                               │
  │                               ├──→ Active (admin reinstatement)
  │                               └──→ Expelled (terminal)
  │
  ├──→ Withdrawn (student/admin request) → Active (readmission)
  └──→ Graduated (TERMINAL — all credits + min GPA)

Registration restrictions:
  Active     → Full (hours by GPA tier)
  Warning    → Max 12 hours
  Probation  → Max 9 hours
  Suspended  → BLOCKED
  Expelled   → BLOCKED
  Graduated  → BLOCKED
```

---

## Excel Import — Critical Fixes

### Fix 1: ImportStudentsAsync — Missing SaveChanges
```csharp
// BEFORE (broken): method ends without saving
return result;

// AFTER (fixed): add before return
if (newStudents.Count > 0)
{
    _context.SystemUsers.AddRange(newUsers);
    _context.Students.AddRange(newStudents);
    await _context.SaveChangesAsync();
}
return result;
```

### Fix 2: UniversityStudentId ≠ NationalId
```csharp
// BEFORE (wrong)
UniversityStudentId = nationalId,

// AFTER (correct)
UniversityStudentId = $"STU{DateTime.UtcNow.Year}{autoIdCounter++:D4}",
```

### Fix 3: BulkUploadJob — Single Transaction
```csharp
// BEFORE: 500 students = 500 transactions + 1000 SaveChanges calls
foreach (var dto in students) {
    using var tx = await _context.Database.BeginTransactionAsync(); // PER ROW!
}

// AFTER: all students in one transaction
var allUsers    = BuildAllUsers(students);
var allStudents = BuildAllStudents(students);
using var tx = await _context.Database.BeginTransactionAsync();
_context.SystemUsers.AddRange(allUsers);
_context.Students.AddRange(allStudents);
await _context.SaveChangesAsync();  // ONCE
await tx.CommitAsync();
```

### Fix 4: Dry-Run Mode
All import endpoints must support `?dryRun=true`:
```
POST /api/students/import-excel?dryRun=true
→ Runs full validation pipeline
→ Returns: WillSucceed, WillSkip, Errors[], preview of first 10 rows
→ Does NOT save anything to DB
```

---

## Subject Domain — Additions Required

```csharp
// Subject needs:
public SubjectCategory Category { get; set; }    // Core/Elective/Lab/GraduationProject
public int  MinimumLevel        { get; set; }    // Level 1-6 restriction
public bool CanBeRepeated       { get; set; }    // Can student retake after passing?
public int  MaxRetakes          { get; set; }    // Max allowed retakes

// Subject.Prerequisites navigation (via SubjectPrerequisite table):
public ICollection<SubjectPrerequisite> Prerequisites { get; set; }

// SubjectOffering needs:
public OfferingStatus Status               { get; set; }  // Scheduled/Open/Closed
public int    CurrentEnrollmentCount       { get; set; }  // Cached count — updated on enroll/unenroll
public DateTime? RegistrationStartDate     { get; set; }
public DateTime? RegistrationEndDate       { get; set; }

// SubjectOffering computed:
public bool IsRegistrationOpen => Status == Open && DateTime.UtcNow in window
public bool HasCapacity        => CurrentEnrollmentCount < MaxCapacity
```

---

## Student Domain Expansion — Phase 3

The current `Student` table should eventually split into:

| Table | Data | When Populated |
|---|---|---|
| `Students` | Academic placement + identity | At registration |
| `StudentProfiles` | Personal info + contact | After account creation |
| `StudentAddresses` | Home address | Optional |
| `StudentDocuments` | File references + verification | During enrollment |
| `StudentGuardians` | Guardian info | At registration |
| `StudentAcademicStatuses` | GPA + standing + hours | Auto-maintained |
| `StudentMedicalInfos` | Health data (encrypted) | Optional |

**Migration approach:** Additive — create new tables, backfill from existing Student data,
then gradually remove redundant columns from Student table over 3 deploys.

---

## Performance Indexes — Add These

```sql
CREATE INDEX idx_enrollments_student_offering
    ON "Enrollments"("StudentId", "SubjectOfferingId")
    WHERE "DeletedAt" IS NULL;

CREATE INDEX idx_grades_student_finalized
    ON "StudentGrades"("StudentId", "IsFinalized")
    WHERE "DeletedAt" IS NULL;

CREATE INDEX idx_offering_semester_batch
    ON "SubjectOfferings"("SemesterId", "BatchId", "DepartmentId")
    WHERE "DeletedAt" IS NULL;

CREATE INDEX idx_academic_status_gpa
    ON "StudentAcademicStatuses"("GPA", "State")
    WHERE "DeletedAt" IS NULL;

CREATE INDEX idx_prereq_subject
    ON "SubjectPrerequisites"("SubjectId");
```

---

## Refactoring Roadmap

### Phase 0 — This Week (Critical Bugs)
- [ ] Fix ImportStudentsAsync missing SaveChanges
- [ ] Fix UniversityStudentId = NationalId bug
- [ ] Add GroupCode to StudentImportDto + ExcelService
- [ ] Add IsFinalized guard in grades import
- [ ] Add capacity check to EnrollmentService
- [ ] Fix BulkUploadJob to single transaction

### Phase 1 — Week 2 (Performance + GPA)
- [ ] Add GradeScale table + seed + hook into GradeService
- [ ] Add AcademicPolicy table + seed
- [ ] Add SubjectPrerequisite table
- [ ] Hook GradeService → AcademicStatusService after finalization
- [ ] Add StudentAcademicStatus table + BackfillJob

### Phase 2 — Month 1 (Registration Engine)
- [ ] Build RegistrationPipeline with all 8 policies
- [ ] New API: GET /api/registration/eligible-offerings
- [ ] Add ImportHistory tracking to all import flows
- [ ] Add SubjectOfferingWaitlist
- [ ] Add Dry-Run mode to all imports

### Phase 3 — Month 2-3 (Student Domain Expansion)
- [ ] Add StudentProfile table (migrate personal data from Student)
- [ ] Add StudentAddress table
- [ ] Add StudentDocuments table + verification workflow
- [ ] Add StudentGuardian table
- [ ] Add StudentMedicalInfo table (with encryption)

### Phase 4 — Month 3-4 (Import Redesign)
- [ ] Build unified ImportOrchestrator<T>
- [ ] Structured errors (ImportRowError with ErrorCode)
- [ ] Template download endpoint
- [ ] Error report Excel export
- [ ] Background processing for large files (> 500 rows)

---

*Last updated: 2026-05-18*
