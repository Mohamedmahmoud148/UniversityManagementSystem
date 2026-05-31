# Role-Based System

> **Last refreshed:** 2026-05-31

---

## 1. Roles

| Role | Description |
|------|-------------|
| `Student` | Enrolled students — access limited to own academic data |
| `Doctor` | Instructors — manage their own subject offerings |
| `Admin` | University administrators — manage structure, analytics, complaints |
| `SuperAdmin` | Full system access including system-level operations |

---

## 2. Permission Matrix

### Backend (.NET) — Controller Level

| Feature | Student | Doctor | Admin | SuperAdmin |
|---------|---------|--------|-------|-----------|
| View own grades/GPA | ✅ | — | ✅ | ✅ |
| View own roadmap | ✅ | — | ✅ | ✅ |
| Submit assignments | ✅ | — | — | — |
| Take exams | ✅ | — | — | — |
| Submit complaints | ✅ | — | — | — |
| Download materials | ✅ | ✅ | ✅ | ✅ |
| Upload materials | — | ✅ | ✅ | ✅ |
| Create assignments | — | ✅ | ✅ | ✅ |
| Create/publish exams | — | ✅ | ✅ | ✅ |
| Grade submissions | — | ✅ | ✅ | ✅ |
| Record attendance | — | ✅ | ✅ | ✅ |
| View student list | — | — | ✅ | ✅ |
| Manage university structure | — | — | ✅ | ✅ |
| Upload regulations | — | — | ✅ | ✅ |
| View complaint reports | — | ✅ | ✅ | ✅ |
| Access admin dashboard | — | — | ✅ | ✅ |
| Bulk import students | — | — | ✅ | ✅ |

### AI Service (FastAPI) — Intent Level

| Intent | Student | Doctor | Admin |
|--------|---------|--------|-------|
| study_plan | ✅ | ✅ | ✅ |
| academic_advice | ✅ | ✅ | ✅ |
| material_qa | ✅ | ✅ | ✅ |
| material_explanation | ✅ | ✅ | ✅ |
| regulation | ✅ | ✅ | ✅ |
| result_query | ✅ | ✅ | ✅ |
| generate_exam | ❌ | ✅ | ✅ |
| assignment_query | ✅ | ✅ | ✅ |
| backend_api_query | ✅ | ✅ | ✅ |
| action_execute | ✅ | ✅ | ✅ |
| complaint_submit | ✅ | ❌ | ✅ |
| complaint_summary | ❌ | ✅ | ✅ |
| file_processing | ❌ | ❌ | ✅ |
| cv_analysis | ✅ | ✅ | ✅ |
| summarization | ✅ | ✅ | ✅ |
| general_chat | ✅ | ✅ | ✅ |

---

## 3. Data-Level Scoping (Service Layer)

Role-based access at the **controller** level prevents unauthorized endpoints. Data-level scoping at the **service layer** ensures users can only see their own records even when sharing the same endpoint:

```csharp
// Student can only read own submission
var studentId = _userContext.GetProfileId();
var submission = await _context.AssignmentSubmissions
    .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == studentId);
```

```csharp
// Doctor can only manage own offerings
var doctorId = _userContext.GetProfileId();
var offering = await _context.SubjectOfferings
    .FirstOrDefaultAsync(o => o.Id == offeringId && o.DoctorId == doctorId);
```

---

## 4. JWT Claim Extraction

`IUserContextService` wraps `IHttpContextAccessor` to extract claims:

```csharp
Ulid GetUserId()      // from "sub" or "userId" claim
Ulid GetProfileId()   // from "ProfileId" claim (studentId or doctorId)
string GetRole()      // from "role" claim
```

---

## 5. StudentType Enum

Students have an additional sub-classification:

| Type | Description |
|------|-------------|
| Regular | Standard enrolled student |
| Transfer | Transferred from another institution |
| Repeating | Repeating a year/course |
| External | External/auditing student |

---

## 6. AI Role Enforcement

When the .NET backend calls FastAPI, it includes:
- `role` field in the request body (lowercased: "student", "doctor", "admin")
- JWT forwarded in `Authorization` header for backend calls on the student's behalf

FastAPI's `app/core/rbac.py` checks `is_allowed(intent, role)` before executing any module. Denied requests return a localized denial message and log the blocked attempt.
