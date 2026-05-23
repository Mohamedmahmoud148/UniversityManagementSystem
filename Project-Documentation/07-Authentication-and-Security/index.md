---
layout: default
title: "🔐 Authentication & Security"
---

# 🔐 Authentication & Security — Complete Guide

## Authentication Architecture

This system uses **JWT (JSON Web Token) + Refresh Token** authentication — the industry standard for stateless, scalable auth.

### Why JWT?
- **Stateless:** Server doesn't need to store session — JWT is self-contained
- **Scalable:** Any server instance can validate any token
- **Role-embedded:** Role/claims in token — no DB lookup per request
- **Expirable:** Short-lived access tokens + long-lived refresh tokens

---

## JWT Token Structure

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9  ← Header (algorithm)
.
eyJuYW1laWQiOiIwMUhYWVoiLCJQcm9maWxlSWQiOiIwMUhBQkMiLCJyb2xlIjoiU3R1ZGVudCJ9  ← Payload
.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c  ← Signature
```

### JWT Claims (Payload)

| Claim | Value | Purpose |
|-------|-------|---------|
| `nameid` | SystemUser.Id (ULID) | The user's system account ID |
| `ProfileId` | Student/Doctor/Admin.Id | The role-specific profile ID |
| `role` | "Student"\|"Doctor"\|"Admin"\|"SuperAdmin" | Role for authorization |
| `email` | User's email | Display/identification |
| `exp` | Unix timestamp | Token expiry |
| `iat` | Unix timestamp | Token issued at |

### How ProfileId Is Used

```csharp
// In controllers — getting the current student's ID:
var profileIdClaim = User.FindFirst("ProfileId")?.Value;
Ulid.TryParse(profileIdClaim, out var studentId);

// This means controllers never need to hit the DB
// to figure out who is making the request
```

---

## Token Lifetime

| Token | Lifetime | Purpose |
|-------|---------|---------|
| Access JWT | 60 minutes | API access |
| Refresh Token | 7 days | Silent renewal |

### Refresh Flow
```
Access Token expires
        │
        ▼
Frontend detects 401 Unauthorized
        │
        ▼
POST /api/auth/refresh { refreshToken: "..." }
        │
        ▼
Server validates refresh token
  ├── Valid → Issue new access + refresh token
  └── Invalid/Expired → Force logout → redirect to login
```

---

## Account Lockout System

```csharp
// SystemUser entity:
public int AccessFailedCount { get; set; }
public DateTime? LockoutEnd { get; set; }

// AuthService login logic:
if (user.AccessFailedCount >= 5 && user.LockoutEnd > DateTime.UtcNow)
    throw new Exception("Account locked. Try again after " + user.LockoutEnd);

if (passwordMatch)
{
    user.AccessFailedCount = 0;  // Reset on success
    user.LockoutEnd = null;
}
else
{
    user.AccessFailedCount++;
    if (user.AccessFailedCount >= 5)
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
}
```

---

## Role-Based Authorization (RBAC)

### Roles Hierarchy
```
SuperAdmin
    ├── Can do everything Admin can do
    ├── Can create other Admins
    └── Full system access

Admin
    ├── Manage university structure
    ├── Manage all users
    ├── View analytics
    └── Handle complaints

Doctor
    ├── Manage own offerings
    ├── Create exams
    ├── Grade students
    └── Post materials

Student
    ├── View own data only
    ├── Enroll in courses
    ├── Submit complaints
    └── Use AI chat
```

### Authorization in Controllers
```csharp
[Authorize]                          // Any authenticated user
[Authorize(Roles = "Admin")]         // Admin only
[Authorize(Roles = "Admin,SuperAdmin")]  // Either Admin or SuperAdmin
[Authorize(Roles = "Doctor")]        // Doctor only
[Authorize(Roles = "Student")]       // Student only
```

### Resource-Level Authorization (Row Security)
Beyond role checks, controllers verify ownership:

```csharp
// Doctor can only see students in THEIR offerings
[HttpGet("by-offering/{offeringId}")]
[Authorize(Roles = "Doctor,Admin")]
public async Task<IActionResult> StudentsByOffering(string offeringId)
{
    var doctorId = User.FindFirst("ProfileId")?.Value;
    
    // Verify this doctor owns the offering
    var offering = await _context.SubjectOfferings
        .FirstOrDefaultAsync(o => o.Id == oid && o.DoctorId == Ulid.Parse(doctorId));
    
    if (offering == null && !User.IsInRole("Admin"))
        return Forbid();  // Doctor doesn't own this — blocked
}
```

---

## Password Security

### Hashing
- Algorithm: **BCrypt** (industry standard, automatically salted)
- Work factor: 10 rounds (2^10 = 1024 iterations — slow enough to deter brute force)

```csharp
// Storing: 
string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 10);

// Verifying:
bool valid = BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
```

### First-Login Policy
```csharp
// When admin creates a student account:
user.MustChangePassword = true;

// On login response:
{ "mustChangePassword": true }

// Frontend must:
if (response.mustChangePassword) {
  redirect("/change-password")  // Block access to app until changed
}
```

### Password Validation Rules
- Minimum 8 characters
- Must contain at least one letter and one number (enforced at DTO level)

---

## National ID Validation
```csharp
// SystemUser is created with NationalId
// Used for identity verification and username generation
// Egyptian NID: 14 digits
// First digit indicates century (2=1900s, 3=2000s)
// Digits 2-7: birth date YYMMDD
```

---

## Egyptian Phone Validation
```csharp
// Enforced in Student and Doctor entities via domain logic:
if (!Regex.IsMatch(value, @"^01[0125][0-9]{8}$"))
    throw new DomainException("Invalid Egyptian phone number.");

// Valid prefixes: 010, 011, 012, 015
// (Vodafone, Etisalat, Mobinil/Orange, WE)
```

---

## Rate Limiting

```csharp
// Program.cs — rate limiter configuration
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", opt =>
    {
        opt.PermitLimit = 100;        // Max 100 requests
        opt.Window = TimeSpan.FromMinutes(1);  // Per minute
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

app.UseRateLimiter();
```

**Effect:** Each IP/user limited to 100 requests/minute. Protects against:
- DDoS attacks
- Brute force login attempts
- Scraping

---

## CORS Policy

```csharp
// Configured to allow the frontend domain
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});
```

---

## Audit Trail

Every significant action is logged to `AuditLogs` table:

```csharp
// AuditService logs:
await _auditService.LogAsync(
    action: "Create",           // Create | Update | Delete | Login
    entityType: "Student",      // Table name
    entityId: student.Id.ToString(),
    oldValues: null,            // JSON of before-state
    newValues: JsonSerializer.Serialize(newStudent),
    performedBy: currentUserId
);
```

**Logged Actions:**
- User login/logout
- Student/Doctor/Admin creation
- Grade finalization
- Complaint resolution
- Any Admin-level change

**Access:** Only SuperAdmin can read AuditLogs via `GET /api/auditlogs`

---

## Middleware Security Chain

```
Incoming Request
        │
        ▼
[ExceptionMiddleware] — catches all errors
        │
        ▼
[CorrelationIdMiddleware] — adds X-Correlation-Id header
        │
        ▼
[SerilogRequestLogging] — logs all requests
        │
        ▼
[CORS] — validates origin
        │
        ▼
[HTTPS Redirect] — forces HTTPS in production
        │
        ▼
[Authentication] — validates JWT
        │
        ▼
[Authorization] — checks role/policy
        │
        ▼
[RateLimiter] — enforces request limits
        │
        ▼
[Controller] — handles request
```

---

## Hangfire Dashboard Security

The Hangfire background job dashboard is at `/hangfire`.  
Access is protected by a custom `HangfireAuthorizationFilter`:

```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Only allow Admin/SuperAdmin
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin") || 
               httpContext.User.IsInRole("SuperAdmin");
    }
}
```

---

## AI Security Boundaries

| What AI Can Do | What AI Cannot Do |
|---------------|------------------|
| GET any data user has permission to see | DELETE any record |
| POST to exam creation endpoints | PUT/PATCH any record |
| POST to complaint submission | Access /api/auth |
| POST to auto-enroll | Access /api/dev |
| POST to grade calculation | Access another user's private data |
| Query analytics (if admin) | Directly modify the database |

This is enforced in `api_discovery.py` before the AI even sees the available endpoints.
