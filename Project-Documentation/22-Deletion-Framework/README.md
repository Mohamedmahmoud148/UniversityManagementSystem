---
layout: default
title: "🗑️ Intelligent Deletion Framework"
---

# 🗑️ Intelligent Deletion Framework — Complete Reference

> **Added:** 2026-05-18  
> **Affects:** All Admin + SuperAdmin delete operations  
> **Protection Level:** Enterprise-grade — dependency-aware, risk-classified, audit-logged

---

## What Is This System?

Before this framework existed, deleting any entity (e.g., a College) would either:
- Silently cascade-delete hundreds of related records, or
- Fail with a cryptic FK constraint error

The Intelligent Deletion Framework adds a **two-phase delete protocol**:

```
Phase 1 — ANALYZE:  Tell me what will happen if I delete X
Phase 2 — EXECUTE:  Actually delete X (after confirmed understanding)
```

No delete can reach Phase 2 without passing through Phase 1 first.

---

## Files Created

```
UniversityManagementSystem.Core/
└── DTOs/
    └── DeletionDtos.cs              ← All request/response shapes

    Interfaces/
    └── IDeletionService.cs          ← Service contract

UniversityManagementSystem.Infrastructure/
└── Services/
    └── Deletion/
        ├── EntityDeletionPolicy.cs  ← Risk + delete-type registry per entity
        ├── DependencyGraph.cs       ← Full child-relationship map + safe order
        └── DeletionService.cs       ← Analysis engine + execution engine

UniversityManagementSystem.Api/
└── Controllers/
    └── DeletionController.cs        ← POST /api/deletion/analyze + /execute
```

Also modified:
- `AppDbContext.cs` — Added missing `DbSet<AcademicYear>` and `DbSet<Semester>`
- `Program.cs` — Registered `IDeletionService`

---

## API Endpoints

Both endpoints require `[Authorize(Roles = "Admin,SuperAdmin")]`.

---

### POST `/api/deletion/analyze`

**Purpose:** Scan the dependency graph for an entity and return a full impact report. Does NOT modify any data.

**Request:**
```json
{
  "entityName": "College",
  "entityId": "01JXXXXXXXXXXXXXXXXXXXXXXXXX"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "entityName": "College",
    "entityId": "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
    "displayName": "College of Engineering",
    "riskLevel": "Catastrophic",
    "riskLevelLabel": "CATASTROPHIC",
    "deleteType": "SoftDelete",
    "deleteTypeLabel": "Soft Delete",
    "canDelete": true,
    "isBlocked": false,
    "summary": {
      "counts": {
        "Department": 4,
        "Student": 120,
        "SubjectOffering": 38,
        "StudentGrade": 240,
        "Exam": 17,
        "Enrollment": 310
      }
    },
    "dependencyTree": [
      {
        "entityName": "Department",
        "friendlyName": "Departments",
        "count": 4,
        "isHistorical": true,
        "isBlocking": true,
        "deleteBehavior": "Restrict",
        "children": [
          {
            "entityName": "Batch",
            "friendlyName": "Batches",
            "count": 12,
            "isHistorical": true,
            "isBlocking": true,
            "deleteBehavior": "Restrict",
            "children": []
          }
        ]
      }
    ],
    "warnings": [
      "This operation will affect historical academic data that cannot be recovered.",
      "⚠️ 240 student grade records will be affected — GPA calculations and transcripts may be impacted.",
      "⚠️ 120 students will lose access to the system and their academic history.",
      "☠️ CATASTROPHIC RISK: This is an irreversible operation with institution-wide impact.",
      "This operation requires second admin approval and cannot be undone."
    ],
    "blockers": [],
    "confirmation": {
      "requiresTypedConfirmation": true,
      "typedConfirmationPhrase": "DELETE COLLEGE: COLLEGE OF ENGINEERING",
      "requiresPasswordConfirmation": true,
      "requiresSecondAdminApproval": true,
      "confirmationSteps": 4
    },
    "deletionOrder": [
      "StudentGrade", "ExamSubmission", "Exam",
      "Enrollment", "SubjectOffering", "Student",
      "Department", "College"
    ]
  }
}
```

**When `isBlocked = true` (example — finalized grades exist):**
```json
{
  "data": {
    "canDelete": false,
    "isBlocked": true,
    "blockers": [
      {
        "reason": "Subject offering has 47 finalized grades",
        "entityName": "StudentGrade",
        "count": 47
      }
    ]
  }
}
```

---

### POST `/api/deletion/execute`

**Purpose:** Execute the delete after the frontend has collected all required confirmations.

**Request:**
```json
{
  "entityName": "College",
  "entityId": "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
  "typedConfirmationPhrase": "DELETE COLLEGE: COLLEGE OF ENGINEERING",
  "adminPassword": "AdminP@ssword123",
  "secondAdminApprovalToken": null
}
```

**Response (200 success):**
```json
{
  "success": true,
  "message": "College has been successfully deactivated (soft deleted).",
  "entityName": "College",
  "entityId": "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
  "deleteTypeApplied": "SoftDelete",
  "affectedCounts": { "College": 1 },
  "executedSteps": ["Soft-deleted College"]
}
```

**Response (409 Conflict — blocked):**
```json
{
  "success": false,
  "message": "Delete blocked: Grade is finalized and immutable"
}
```

**Response (409 Conflict — wrong phrase):**
```json
{
  "success": false,
  "message": "Typed confirmation does not match. Expected: \"DELETE COLLEGE: COLLEGE OF ENGINEERING\""
}
```

---

## Risk Level System

| Level | Color | Examples | Confirmation Required |
|---|---|---|---|
| `Low` | 🟢 Green | Notification, RefreshToken, ChatMessage | Single click |
| `Medium` | 🟡 Amber | Group, AiMemory, ScheduleEntry, Material | Warning modal + confirm |
| `High` | 🟠 Orange | Exam, Batch, Regulation, AttendanceSession | Typed confirmation phrase |
| `Critical` | 🔴 Red | Student, Doctor, Subject, SubjectOffering | Typed phrase + password |
| `Catastrophic` | 🟣 Purple | University, College, Department, Semester | Typed phrase + password + 2nd admin |

---

## Delete Type System

| Type | Meaning | Database Action |
|---|---|---|
| `SoftDelete` | Record deactivated, data preserved | `DeletedAt = DateTime.UtcNow` |
| `HardDelete` | Permanently removed from DB | `context.Remove(entity)` |
| `Restricted` | Cannot delete at all (Enrollment, AuditLog) | Throws exception |
| `ArchiveOnly` | Move to archive table, no delete | Archive job |
| `ImmutableBlocked` | Absolute prohibition (finalized grades, audit logs) | Throws exception always |

---

## Entity Policy Table (Complete)

| Entity | Risk Level | Delete Type | Confirmation Phrase | Historical? |
|---|---|---|---|---|
| University | Catastrophic | SoftDelete | DELETE UNIVERSITY | ✅ |
| College | Catastrophic | SoftDelete | DELETE COLLEGE | ✅ |
| Department | Catastrophic | SoftDelete | DELETE DEPARTMENT | ✅ |
| Semester | Catastrophic | SoftDelete | DELETE SEMESTER | ✅ |
| SubjectOffering | Catastrophic | SoftDelete | DELETE SUBJECT OFFERING | ✅ |
| Student | Critical | SoftDelete | DELETE STUDENT | ✅ |
| SystemUser | Critical | SoftDelete | DELETE USER | ✅ |
| Doctor | Critical | SoftDelete | DELETE DOCTOR | ✅ |
| Subject | Critical | SoftDelete | DELETE SUBJECT | ✅ |
| StudentGrade | Critical | ImmutableBlocked | — | ✅ |
| Enrollment | Critical | Restricted | — | ✅ |
| AuditLog | Critical | ImmutableBlocked | — | ✅ |
| AcademicYear | High | SoftDelete | DELETE ACADEMIC YEAR | ✅ |
| Batch | High | SoftDelete | DELETE BATCH | ✅ |
| TeachingAssistant | High | SoftDelete | — | ✅ |
| Regulation | High | SoftDelete | DELETE REGULATION | ✅ |
| Exam | High | SoftDelete | DELETE EXAM | ✅ |
| ExamSubmission | High | Restricted | — | ✅ |
| AttendanceSession | High | SoftDelete | — | ✅ |
| StudentAttendance | High | SoftDelete | — | ✅ |
| Group | Medium | SoftDelete | — | ❌ |
| Complaint | Medium | SoftDelete | — | ✅ |
| Material | Medium | SoftDelete | — | ❌ |
| UploadedFile | Medium | SoftDelete | — | ❌ |
| AiMemory | Medium | HardDelete | — | ❌ |
| Conversation | Medium | SoftDelete | — | ❌ |
| ScheduleEntry | Medium | HardDelete | — | ❌ |
| EnrollmentUpload | Medium | ArchiveOnly | — | ✅ |
| AppNotification | Low | HardDelete | — | ❌ |
| RefreshToken | Low | HardDelete | — | ❌ |
| ChatMessage | Low | HardDelete | — | ❌ |
| ComplaintAnalysis | Low | HardDelete | — | ❌ |
| ComplaintCluster | Low | HardDelete | — | ❌ |
| ExamQuestion | Low | HardDelete | — | ❌ |
| StudentExamVariant | Low | HardDelete | — | ❌ |
| StudentFile | Low | HardDelete | — | ❌ |
| AiActionLog | Low | HardDelete | — | ❌ |

---

## Immutability Guards (Hard Stops)

These checks run on **every** execute call regardless of what was already confirmed:

| Entity | Condition | Blocker Message |
|---|---|---|
| `StudentGrade` | `IsFinalized == true` | "Grade is finalized and immutable" |
| `Exam` | `Status != Draft` | "Exam is Published/Closed — only Draft exams can be deleted" |
| `Exam` | Any `ExamSubmission` exists | "Exam has N student submissions" |
| `Enrollment` | `IsActive == true` | "Active enrollment cannot be deleted" |
| `AuditLog` | Always | "Audit logs are immutable — they can never be deleted" |
| `Semester` | Active enrollments exist | "Semester has N active enrollments" |
| `SubjectOffering` | Finalized grades exist | "Subject offering has N finalized grades" |
| `Regulation` | Assigned to batches | "Regulation is assigned to N batches" |

---

## Dependency Scan — How It Works

```
AnalyzeAsync("College", id)
    │
    ├── Gets EntityDeletionPolicy for "College"
    │   → RiskLevel: Catastrophic, DeleteType: SoftDelete
    │
    ├── ScanDependenciesAsync("College", id, depth=0)
    │   ├── CountChildrenAsync("College", id, "Department") → 4
    │   ├── CountChildrenAsync("College", id, "AcademicYear") → 2
    │   ├── CountChildrenAsync("College", id, "Subject") → 18
    │   └── CountChildrenAsync("College", id, "Student") → 120
    │
    │   For each child with count > 0:
    │   └── ScanDependenciesAsync("Department", firstDeptId, depth=1)
    │       ├── CountChildrenAsync("Department", id, "Batch") → 3
    │       ├── CountChildrenAsync("Department", id, "Doctor") → 8
    │       └── ScanDependenciesAsync("Batch", firstBatchId, depth=2)
    │           ├── CountChildrenAsync("Batch", id, "Group") → 4
    │           └── CountChildrenAsync("Batch", id, "Student") → 30
    │           → Scaled by dept count → Batch ~9, Student ~90 (estimate)
    │
    ├── CheckImmutabilityAsync("College", id)
    │   → (no College-specific immutability check)
    │
    ├── BuildWarnings(...)
    │   → "Historical data" warning (IsHistorical=true)
    │   → "120 students" warning
    │   → "240 grades" warning
    │   → "CATASTROPHIC" warning
    │
    └── BuildConfirmationRequirements(Catastrophic)
        → requiresTypedConfirmation: true
        → requiresPasswordConfirmation: true
        → requiresSecondAdminApproval: true
        → confirmationSteps: 4
```

> **Note on Estimates:** Grandchild counts beyond depth=2 are estimated by multiplying the sample child count by the parent count. This is intentional — exact recursive counts at depth 5+ would require O(N) database queries. The estimates are clearly labeled in the response as approximations.

---

## Execution Flow

```
ExecuteAsync(request, performedByUserId)
    │
    ├── 1. Validate EntityId format (ULID parse)
    │
    ├── 2. Load EntityDeletionPolicy
    │   → If ImmutableBlocked → throw immediately
    │
    ├── 3. Validate typed confirmation phrase
    │   → For Risk >= High: check request.TypedConfirmationPhrase matches policy phrase
    │
    ├── 4. Re-run CheckImmutabilityAsync
    │   → Second check to catch state changes between analyze and execute
    │
    ├── 5. Capture entity snapshot (JSON) for audit
    │
    ├── 6. Open DB Transaction
    │   ├── SoftDeleteEntityAsync OR HardDeleteEntityAsync
    │   └── context.SaveChangesAsync()
    │
    ├── 7. Commit Transaction
    │   → On failure: RollbackAsync() → re-throw
    │
    └── 8. AuditLog.LogAsync(
            actionType: "Delete",
            entityName: ...,
            oldValues: snapshot JSON,
            newValues: { DeleteType: "SoftDelete" },
            performedByUserId: ...
        )
```

---

## Safe Deletion Order (39 Entities)

When doing a full system reset or development teardown, entities must be deleted in this exact order (leaf-first):

```
1.  ComplaintAnalysis       → no downstream deps
2.  ComplaintCluster        → no downstream deps
3.  AiActionLog             → no downstream deps
4.  AiMemory                → no downstream deps
5.  ChatMessage             → no downstream deps
6.  Conversation            → owns ChatMessages
7.  AuditLog                → no downstream deps
8.  AppNotification         → no downstream deps
9.  RefreshToken            → no downstream deps
10. StudentExamVariant      → no downstream deps
11. ExamSubmission          → no downstream deps
12. ExamQuestion            → no downstream deps
13. Exam                    → owns Questions + Submissions
14. StudentGrade            → no downstream deps
15. StudentAttendance       → no downstream deps
16. AttendanceSession       → owns StudentAttendances
17. Complaint               → owns ComplaintAnalysis
18. StudentFile             → no downstream deps
19. Material                → no downstream deps
20. EnrollmentUpload        → no downstream deps
21. UploadedFile            → referenced by Material, Regulation
22. ScheduleEntry           → no downstream deps
23. Enrollment              → no downstream deps
24. SubjectOffering         → owns Enrollment, Exam, Grade, Material, Schedule
25. SubjectDoctor           → junction
26. SubjectAssistant        → junction
27. RegulationSubject       → junction
28. AcademicYearDepartment  → junction
29. Student                 → owns all student records
30. Doctor                  → owns Offerings, Exams, Materials
31. TeachingAssistant       → owns Sessions
32. Admin                   → owns EnrollmentUploads
33. SystemUser              → owns all auth records
34. Group                   → owns Students
35. Batch                   → owns Groups + Students
36. Subject                 → owns Offerings, Sessions
37. Regulation              → owns RegulationSubjects
38. Semester                → owns SubjectOfferings
39. AcademicYear            → owns Semesters
40. Department              → owns Batches, Doctors, Subjects
41. College                 → owns Departments, AcademicYears
42. University              → owns Colleges
```

---

## Frontend Integration Summary

See full frontend prompt in the main task description. Quick reference:

| Scenario | What Frontend Should Do |
|---|---|
| User clicks Delete | Call `/analyze` first — never skip this |
| `isBlocked = true` | Show blockers, hide delete button entirely |
| `riskLevel = Low` | Simple confirm dialog (1 step) |
| `riskLevel = Medium` | Warning modal (1 step + warnings list) |
| `riskLevel = High` | 2-step: warning → typed phrase input |
| `riskLevel = Critical` | 3-step: warning → typed phrase → password |
| `riskLevel = Catastrophic` | 4-step: warning → impact tree → typed phrase → password + checkbox |
| `deleteTypeLabel = Soft Delete` | Button text: "Deactivate" |
| `deleteTypeLabel = Hard Delete` | Button text: "Permanently Delete" |
| 409 response on execute | Show conflict toast, re-open analyze modal |
| Success | Green toast + refresh parent list |

---

## Architecture Decisions

**Why two separate API calls (analyze + execute)?**
- Frontend can show real data (not mock counts) in the confirmation dialog
- User sees exactly what will be affected before confirming
- The execute call does a second immutability check to prevent TOCTOU race conditions

**Why soft delete by default for most entities?**
- Academic data has legal retention requirements
- Soft deletes allow audit trail reconstruction
- Grade history and transcripts must survive "deleted" student records

**Why estimate grandchild counts instead of exact counts?**
- Exact recursive count for College (depth 8) would require ~50 DB queries
- Estimates are labeled and accurate enough for UX warnings
- The exact counts are irrelevant — the user just needs to understand the scale

**Why re-check immutability in ExecuteAsync?**
- Time gap between analyze and execute (user reads confirmation, types phrase)
- A grade could be finalized or exam submitted during that window
- Defense against TOCTOU (Time-of-check, Time-of-use) race conditions

---

*Last updated: 2026-05-18*
