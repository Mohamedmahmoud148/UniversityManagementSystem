# 👥 Role-Based System — Complete Guide

## Overview

The system has **4 roles** with strictly separated capabilities. Every API endpoint is locked to specific roles. A student cannot see analytics, a doctor cannot delete users, and the AI enforces the same rules.

---

## Role Definitions

```
SuperAdmin
    │   • Full system access
    │   • Manage Admins (create/update/delete)
    │   • Read AuditLogs (only role that can)
    │   • All Admin permissions
    │
    └── Admin
            │   • Manage university structure (University, College, Dept, Batch, Group)
            │   • Manage Students and Doctors (CRUD, bulk upload)
            │   • Assign Regulations to batches/students
            │   • View and resolve Complaints
            │   • View Analytics dashboards
            │   • Send notifications to any user
            │   • View Hangfire dashboard (/hangfire)
            │   • Manage Semesters and Academic Years
            │
            ├── Doctor
            │       • Manage own SubjectOfferings only
            │       • Create/Edit/Publish Exams (for their offerings)
            │       • View and grade students enrolled in their offerings
            │       • Record attendance sessions
            │       • Upload course Materials
            │       • Send notifications to their students
            │       • Use AI chat (scoped to their data)
            │       • Generate AI exam questions
            │
            └── Student
                    • View own profile, grades, GPA
                    • View own academic roadmap
                    • Enroll in courses (manual or AI auto-enroll)
                    • View own schedule
                    • Submit and view own complaints
                    • Take exams (their enrolled offerings only)
                    • View attendance for their sessions
                    • Download course materials (enrolled offerings)
                    • Use AI chat (scoped to their own data)
                    • Receive notifications
```

---

## How Roles Are Stored

```csharp
// UserRole enum in Core/Entities/UserRole.cs
public enum UserRole
{
    SuperAdmin = 0,
    Admin      = 1,
    Doctor     = 2,
    Student    = 3
}

// SystemUser.Role stores the enum value as an integer in DB
public UserRole Role { get; set; }
```

---

## How Roles Are Encoded in JWT

When a user logs in, their role is embedded in the JWT:

```json
// JWT payload (decoded)
{
  "nameid":    "01HXYZ...",      // SystemUser.Id
  "ProfileId": "01HABC...",      // Student/Doctor/Admin profile Id
  "role":      "Student",        // String role name
  "email":     "user@uni.edu",
  "exp":       1748000000
}
```

Every request carries this token → no DB lookup needed to know who is calling.

---

## Role Enforcement Layers

### Layer 1: Controller Attribute
```csharp
// Everyone who is authenticated:
[Authorize]

// Specific role(s):
[Authorize(Roles = "Admin,SuperAdmin")]
[Authorize(Roles = "Doctor")]
[Authorize(Roles = "Student")]

// Public endpoint (no auth):
[AllowAnonymous]
```

### Layer 2: Resource-Level Ownership Check
Beyond role, the code checks **ownership**:

```csharp
// Doctor can only grade students in THEIR offerings:
[HttpPost("calculate/{offeringId}")]
[Authorize(Roles = "Doctor")]
public async Task<IActionResult> Calculate(string offeringId)
{
    var doctorProfileId = User.FindFirst("ProfileId")?.Value;

    // Verify this doctor owns the offering
    var offering = await _context.SubjectOfferings
        .FirstOrDefaultAsync(o => o.Id == uid && o.DoctorId == Ulid.Parse(doctorProfileId));

    if (offering == null) return Forbid();  // Right role, wrong resource
}
```

```csharp
// Student can only see THEIR grades:
[HttpGet("my-grades")]
[Authorize(Roles = "Student")]
public async Task<IActionResult> MyGrades()
{
    var studentId = User.FindFirst("ProfileId")?.Value;
    var grades = await _gradeService.GetStudentGradesAsync(Ulid.Parse(studentId));
    // Only returns grades for THIS student — no student can see another's
}
```

### Layer 3: AI RBAC (api_discovery.py)
The AI layer applies the same role rules when calling the backend. The student's JWT is forwarded — so even if an AI were manipulated, the backend would reject unauthorized requests.

---

## Role-to-Profile Mapping

Each role has a **profile record** linked via 1:1 relationship to SystemUser:

| Role | Profile Table | Profile Id Claim |
|------|-------------|-----------------|
| Student | `Students` | `ProfileId` in JWT |
| Doctor | `Doctors` | `ProfileId` in JWT |
| Admin / SuperAdmin | `Admins` | `ProfileId` in JWT |

```csharp
// Getting the Student profile from JWT in a controller:
var profileId = User.FindFirst("ProfileId")?.Value;
if (!Ulid.TryParse(profileId, out var studentId)) return BadRequest();

var student = await _context.Students.FindAsync(studentId);
```

---

## Permission Matrix — All Endpoints

### Students Controller
| Endpoint | Student | Doctor | Admin | SuperAdmin |
|---------|---------|--------|-------|------------|
| GET /api/students | ❌ | ❌ | ✅ | ✅ |
| GET /api/students/me | ✅ | ❌ | ❌ | ❌ |
| GET /api/students/{id} | ❌ | ✅* | ✅ | ✅ |
| GET /api/students/by-offering | ❌ | ✅* | ✅ | ✅ |
| GET /api/students/struggling | ❌ | ❌ | ✅ | ✅ |
| POST /api/students | ❌ | ❌ | ✅ | ✅ |
| PUT /api/students/{id} | ❌ | ❌ | ✅ | ✅ |
| DELETE /api/students/{id} | ❌ | ❌ | ✅ | ✅ |

*Doctor: only students in their own offerings

### Grades Controller
| Endpoint | Student | Doctor | Admin |
|---------|---------|--------|-------|
| GET /api/grades/my-grades | ✅ | ❌ | ❌ |
| GET /api/grades/offering/{id} | ❌ | ✅* | ✅ |
| POST /api/grades/calculate | ❌ | ✅* | ✅ |
| POST /api/grades/submit | ❌ | ✅* | ✅ |

*Doctor: only their own offerings

### Analytics Controller
| Endpoint | Student | Doctor | Admin |
|---------|---------|--------|-------|
| All /api/analytics/* | ❌ | ❌ | ✅ |

### Notifications
| Endpoint | Student | Doctor | Admin |
|---------|---------|--------|-------|
| GET /api/notification | ✅ | ✅ | ✅ |
| PUT /api/notification/{id}/read | ✅ | ✅ | ✅ |
| POST /api/notification | ❌ | ❌ | ✅ |
| POST /api/notification/send-to-my-students | ❌ | ✅ | ❌ |
| DELETE /api/notification/{id} | ❌ | ❌ | ✅ |

---

## First-Login Password Policy

When Admin creates any user account:

```csharp
// SystemUser creation:
var user = new SystemUser
{
    Email = email,
    PasswordHash = BCrypt.HashPassword(defaultPassword),
    MustChangePassword = true,  // ← Force change on first login
    Role = UserRole.Student
};
```

Login response includes:
```json
{ "mustChangePassword": true }
```

**Frontend must:** Block all navigation and redirect to `/change-password` until this is cleared.

After changing:
```csharp
user.MustChangePassword = false;
await _context.SaveChangesAsync();
```

---

## SuperAdmin vs Admin Difference

| Feature | Admin | SuperAdmin |
|---------|-------|------------|
| Create/Edit/Delete Admins | ❌ | ✅ |
| View AuditLogs | ❌ | ✅ |
| Everything else | ✅ | ✅ |

```csharp
[HttpGet]
[Authorize(Roles = "SuperAdmin")]   // SuperAdmin ONLY
public async Task<IActionResult> GetAuditLogs() { }

[HttpPost]
[Authorize(Roles = "Admin,SuperAdmin")]  // Both
public async Task<IActionResult> SendNotification() { }
```
