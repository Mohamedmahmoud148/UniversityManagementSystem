# Security Design

## 1. Overview

The university management system handles sensitive academic data: grades, personal student information, complaints, and exam content. Security is a first-class concern implemented at multiple independent layers. Even if one layer is bypassed, the others provide defense in depth.

Security is enforced at four levels:

1. **React route guards** — prevent unauthorized UI access.
2. **Firebase Security Rules** — enforce read/write permissions at the database level.
3. **.NET `[Authorize]` attributes** — enforce role access at the API controller/action level.
4. **FastAPI RBAC** — enforce intent-level access in the AI service.

---

## 2. Authentication Architecture

### 2.1 Dual Authentication System

The system operates two authentication systems that serve different subsystems:

| System | Provider | Used For | Token Format |
|--------|---------|---------|-------------|
| Academic Auth | .NET backend (custom JWT) | REST API calls, grade data, enrollments | JWT Bearer |
| Classroom Auth | Firebase Auth | Firestore, Cloud Functions, real-time features | Firebase ID Token |

**Why two systems?** The .NET backend predates Firebase integration and owns the canonical user database. Firebase Auth is used only for the classroom real-time features (quizzes, attendance, engagement). The two systems are linked by a shared user identifier (Firebase UID is stored as a reference in `SystemUsers`).

### 2.2 JWT Security (Academic Auth)

```mermaid
flowchart TD
    LOGIN[POST /api/auth/login] --> VERIFY[BCrypt password verify]
    VERIFY --> GEN[Generate JWT]
    GEN --> STORE[Store hashed\nRefreshToken in DB]
    GEN --> CLIENT[Return AccessToken + RefreshToken]

    CLIENT --> USE[Client uses AccessToken\n(15 min TTL)]
    USE --> EXPIRE{Token expired?}
    EXPIRE -->|No| API[Access granted]
    EXPIRE -->|Yes| REFRESH[POST /api/auth/refresh\nwith RefreshToken]
    REFRESH --> ROTATE[Issue new AccessToken\n+ rotate RefreshToken]
    ROTATE --> CLIENT
```

**JWT security properties:**
- Signed with HMAC-SHA256.
- 256-bit secret key stored in Railway environment variables (never in source code).
- Access token TTL: 15 minutes.
- Refresh token TTL: 7 days.
- Refresh tokens are stored as BCrypt hashes in the database (not plaintext).
- Single-use refresh tokens: each use rotates to a new token, invalidating the old one.
- Logout invalidates the refresh token (marks it revoked in the database).

### 2.3 Firebase Auth Security

Firebase Auth manages classroom identities. Custom claims are set server-side via Admin SDK:

```javascript
// Cloud Function: setCustomClaims
await admin.auth().setCustomUserClaims(uid, {
  role: "Student",
  departmentId: "uuid-...",
  entityId: "uuid-..."
});
```

These claims are embedded in the Firebase ID token and cannot be forged by the client. Firebase rules and the frontend's `RequireRole` guard both read these claims.

---

## 3. Authorization — Four-Layer RBAC

### 3.1 Layer 1: React Route Guards

The frontend prevents unauthorized users from even seeing restricted pages. Two guards compose to enforce access:

- **`ProtectedRoute`**: Blocks unauthenticated access (no Firebase session).
- **`RequireRole`**: Blocks users with the wrong role claim.

This layer provides UX protection and prevents accidental navigation. It is **not** a security boundary on its own — all security is re-enforced at the backend layers.

### 3.2 Layer 2: Firebase Security Rules

Firebase Firestore and Storage rules enforce read/write permissions using the authenticated user's custom claims.

**Firestore rules pattern:**

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Quiz sessions: only the professor who created it can write
    match /quizSessions/{sessionId} {
      allow read: if request.auth != null
                  && request.auth.token.role in ['Student', 'Professor', 'Assistant'];
      allow write: if request.auth != null
                   && request.auth.token.role == 'Professor'
                   && request.auth.uid == resource.data.createdBy;
    }

    // Student quiz responses: only the student themselves can write
    match /quizSessions/{sessionId}/responses/{studentId} {
      allow read: if request.auth != null
                  && (request.auth.token.role == 'Professor'
                      || request.auth.uid == studentId);
      allow write: if request.auth != null
                   && request.auth.uid == studentId;
    }

    // Attendance records: Assistants and Professors write, students read own
    match /attendanceSessions/{sessionId}/records/{studentId} {
      allow read: if request.auth != null
                  && (request.auth.token.role in ['Professor', 'Assistant', 'Admin']
                      || request.auth.uid == studentId);
      allow write: if request.auth != null
                   && request.auth.token.role in ['Professor', 'Assistant'];
    }

    // Engagement scores: professors and admins read, Cloud Functions write
    match /engagementScores/{offeringId}/scores/{date} {
      allow read: if request.auth != null
                  && request.auth.token.role in ['Professor', 'Admin'];
      allow write: if false; // only writable by Cloud Functions (Admin SDK)
    }
  }
}
```

**Firebase Storage rules:**

```javascript
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    // Lecture PDFs: professors upload, all authenticated users read
    match /lectures/{subjectCode}/{fileName} {
      allow read: if request.auth != null;
      allow write: if request.auth != null
                   && request.auth.token.role in ['Professor', 'Assistant'];
    }
  }
}
```

### 3.3 Layer 3: .NET `[Authorize]` Attributes

Every .NET controller and action that handles sensitive data is decorated with `[Authorize]` specifying allowed roles:

```csharp
[ApiController]
[Route("api/grades")]
[Authorize]  // all endpoints require authentication
public class GradesController : ControllerBase
{
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyGrades() { ... }

    [HttpPut("{enrollmentId}")]
    [Authorize(Roles = "Professor")]
    public async Task<IActionResult> UpdateGrade(...) { ... }

    [HttpPost("bulk-publish")]
    [Authorize(Roles = "Professor,Admin")]
    public async Task<IActionResult> BulkPublish(...) { ... }

    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAllGrades() { ... }
}
```

Additionally, service-layer **ownership checks** prevent horizontal privilege escalation (e.g., a student accessing another student's grades):

```csharp
// Student can only access their own data
var studentId = User.GetEntityId(); // from JWT claims
if (enrollment.StudentId != studentId)
    throw new ForbiddenException("Access denied to this resource.");
```

### 3.4 Layer 4: FastAPI RBAC

The AI service enforces role-based intent access at the orchestrator level:

```python
INTENT_ROLES = {
    "academic_advice":    ["Student"],
    "student_overview":   ["Professor", "Admin"],
    "grades_query":       ["Student", "Professor", "Admin"],
    "quiz_generation":    ["Professor"],
    # ...
}

def check_intent_permission(intent: str, user_role: str) -> bool:
    allowed = INTENT_ROLES.get(intent, [])
    return user_role in allowed
```

If a user attempts to access a restricted intent, the orchestrator returns a 403 response before any module logic executes.

---

## 4. Data Protection

### 4.1 Passwords

- All passwords are hashed using BCrypt with a cost factor of 12 before storage.
- Plaintext passwords are never logged or stored.
- Password reset uses a time-limited, single-use token sent via email.

### 4.2 Secrets Management

All secrets (JWT signing key, database connection string, OpenRouter API key, Firebase service account) are stored as environment variables in Railway and Firebase project settings. No secrets appear in the source code or version control.

### 4.3 Sensitive Data in Transit

- All communication over public networks uses HTTPS (TLS 1.2+).
- Railway services communicate over Railway's private internal network for service-to-service calls.
- Firebase SDK uses encrypted channels (HTTPS / gRPC).

### 4.4 Sensitive Data at Rest

- PostgreSQL data is stored on Railway-managed disks with encryption at rest.
- Firebase Firestore data is encrypted at rest by Google Cloud.
- Cloudflare R2 objects are encrypted at rest by Cloudflare.

---

## 5. SQL Injection Prevention

The .NET backend uses EF Core for all database access. EF Core generates parameterized queries exclusively:

```csharp
// Safe: EF Core parameterizes this automatically
var student = await context.Students
    .Where(s => s.StudentCode == studentCode)
    .FirstOrDefaultAsync();
```

Raw SQL is not used in the application. In the rare case where a raw query is required (complex reporting), EF Core's `FromSqlRaw` with explicit parameters is used:

```csharp
// Safe: parameters prevent injection
context.Students.FromSqlRaw("SELECT * FROM \"Students\" WHERE \"Code\" = {0}", code);
```

---

## 6. CORS Configuration

Cross-Origin Resource Sharing is configured explicitly on the .NET backend:

```csharp
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        policy
            .WithOrigins(
                "https://university-app.web.app",   // Firebase Hosting production
                "http://localhost:5173"              // Vite development server
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

All other origins receive `403 Forbidden` at the CORS middleware level. Wildcard origins (`*`) are never used.

---

## 7. Rate Limiting

Rate limiting is applied in .NET using the ASP.NET Core rate limiter:

| Endpoint Group | Limit | Window |
|----------------|-------|--------|
| `POST /api/auth/login` | 5 requests | 1 minute (per IP) |
| `POST /api/auth/refresh` | 10 requests | 1 minute (per IP) |
| All other endpoints | 120 requests | 1 minute (per user) |
| File upload | 10 uploads | 1 minute (per user) |

When the limit is exceeded, the server responds with `429 Too Many Requests` and a `Retry-After` header.

---

## 8. Soft-Delete as a Security Control

The soft-delete pattern serves a secondary security function: it prevents data loss from unauthorized deletion attempts. Even if a malicious admin deletes a student's grade record, the record is not physically destroyed. The `DeletedAt` timestamp is set, and a SuperAdmin can recover the record.

This creates an immutable audit trail of all entity states over time.

---

## 9. Audit Logging

All state-changing operations (POST, PUT, PATCH, DELETE) are logged with:
- Timestamp (UTC)
- User ID + role
- HTTP method + endpoint
- Request body summary (sensitive fields redacted)
- Response status code

Logs are emitted via Serilog to Railway's log aggregation service, which retains them for 30 days.

---

## 10. Security Threat Model Summary

| Threat | Mitigation |
|--------|-----------|
| Credential brute force | Rate limiting on login endpoint |
| JWT forgery | HMAC-SHA256 signature verification on every request |
| Horizontal privilege escalation | Service-layer ownership checks |
| Vertical privilege escalation | `[Authorize(Roles)]` at every controller/action |
| SQL injection | EF Core parameterized queries exclusively |
| XSS | React's built-in JSX escaping; no `dangerouslySetInnerHTML` |
| CSRF | JWT in Authorization header (not cookie) eliminates CSRF risk |
| Unauthorized Firestore access | Security Rules enforced by Firebase server |
| Sensitive data exposure | HTTPS everywhere; secrets in env vars |
| Data loss from accidental deletion | Soft-delete across all entities |
