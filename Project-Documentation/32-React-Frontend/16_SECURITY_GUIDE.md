# 16 — Security Guide
## Authentication Security, Data Protection, XSS Prevention, and Best Practices

---

## 1. Security Model Overview

The application has a **two-layer security model**:

```
Layer 1: Client-Side (Route Guards)
  ├── RequireRole — prevents rendering wrong role's UI
  └── ProtectedRoute — prevents rendering unauthenticated UI
  
  ⚠️ This layer is UI-only — it can be bypassed by determined users

Layer 2: Server-Side (Firebase Rules)
  ├── Firestore Security Rules — enforced by Firebase servers
  ├── Storage Security Rules — enforced for all file operations
  └── Cloud Functions — validate caller's token before executing
  
  ✅ This layer is authoritative — cannot be bypassed
```

Security is only as strong as Layer 2. Layer 1 is for user experience — it prevents legitimate users from accidentally seeing the wrong UI. Layer 2 is for actual data security.

---

## 2. Authentication Security

### Firebase Auth Token Properties

Firebase JWTs have these security properties:
- **Signed** by Firebase with RSA-256 (cannot be forged)
- **Expire** after 1 hour
- **Custom claims** (like `role`) are set server-side only
- **Verified** by Firestore Rules automatically on every request

### Token Security in the Browser

Firebase Auth stores tokens in **IndexedDB** (not cookies, not localStorage by default). This provides:
- **XSS resistance**: IndexedDB is accessible via JavaScript, so XSS can theoretically steal it
- **CSRF resistance**: Not a cookie, so CSRF attacks don't apply

### Role Claim Security

Custom claims (`role: "student"`) can only be set by:
1. Firebase Admin SDK (server-side only)
2. Cloud Functions (which verify the caller is `super_admin` before setting)

A malicious client **cannot** forge or modify custom claims because:
1. The client cannot call Firebase Admin SDK
2. The `setUserRole` Cloud Function checks the caller's role before setting

---

## 3. Firestore Security Rules Analysis

### Strengths

**1. Role-based access using custom claims:**
```javascript
function hasRole(role) {
  return request.auth.token.role == role
}
```
Roles come from JWT custom claims — cannot be faked.

**2. Owner-only writes for quiz submissions:**
```javascript
match /quizSubmissions/{subId} {
  allow create: if hasRole('student')
             && request.resource.data.studentUid == request.auth.uid
}
```
Students can only submit quizzes under their own UID.

**3. Professor-only quiz management:**
```javascript
match /quizzes/{quizId} {
  allow delete: if hasRole('professor')
             && resource.data.createdBy == request.auth.uid
}
```
Professors can only delete their own quizzes.

### Potential Weak Points

**1. Material uploads — any authenticated user can read:**
```javascript
allow read: if request.auth != null
```
This means any logged-in user can download any lecture PDF, even if they're not enrolled in that course. **For a production system, consider restricting reads to students in the same college/year/department.**

**2. User directory readable by all:**
```javascript
match /users/{userId} {
  allow read: if request.auth.uid == userId || isAdmin()
}
```
This is correct — users can only read their own profile, admins read all. **However, verify this rule is not accidentally `allow read: if true`** in the deployed rules.

**3. `courseAssignments` — verify professor cannot modify other professors' assignments:**
```javascript
// Ensure this rule exists:
allow write: if isAdmin()
          || (hasRole('professor') && resource.data.professorIds has request.auth.uid)
```

---

## 4. XSS (Cross-Site Scripting) Prevention

### React's Built-in XSS Prevention

React automatically escapes all dynamic content rendered inside JSX:
```jsx
// This is SAFE — React escapes the content
<div>{userContent}</div>

// The escaped output in DOM:
// <div>&lt;script&gt;alert('xss')&lt;/script&gt;</div>
```

React's JSX transformation calls `React.createElement` — it never sets `innerHTML` with user content.

### `dangerouslySetInnerHTML` — Not Used

The application does not use `dangerouslySetInnerHTML` anywhere. This is the one React API that bypasses XSS protection. Its absence is correct.

### User-Generated Content Safety

Quiz questions and AI chat responses are rendered directly in JSX — they are automatically escaped. No additional sanitization needed for display.

However, if the AI response ever returns HTML that needs to be rendered (e.g., formatted code), `dangerouslySetInnerHTML` with a sanitizer would be needed. **Currently not applicable.**

---

## 5. CSRF (Cross-Site Request Forgery) Prevention

**Firebase Auth is not cookie-based**, so traditional CSRF attacks don't apply:

| Attack Vector | Status |
|--------------|--------|
| Cookie-based CSRF | Not applicable (Firebase uses JWT in headers, not cookies) |
| CORS bypass | Firebase SDK handles CORS correctly |
| Firestore direct API attack | Protected by Security Rules + token signature |
| Cloud Function attack | Protected by token validation + HTTPS |

---

## 6. Sensitive Data Handling

### What Is Stored Where

| Data Type | Storage | Sensitivity | Notes |
|-----------|---------|-------------|-------|
| Password | Firebase Auth only (hashed) | Critical | Never in Firestore |
| Auth token (JWT) | Browser IndexedDB | High | Auto-expires, auto-refreshed |
| User profile | Firestore `users/{uid}` | Medium | Accessible by owner + admins |
| Course materials | Firebase Storage | Low-Medium | Academic content |
| Engagement counters | Firestore | Low | Aggregated, no biometric |
| Quiz answers | Firestore `quizSubmissions` | Low | Academic data |

### Environment Variables

Firebase config values (`REACT_APP_FIREBASE_API_KEY`, etc.) are embedded in the client bundle and are **intentionally public**. Firebase's security model relies on Security Rules — not on keeping the config secret.

**Never put secrets in environment variables for a React (browser) app:**
- ❌ Database passwords
- ❌ API keys to private services without their own auth
- ❌ Private keys
- ✅ Firebase config (public by design)
- ✅ FastAPI endpoint URL (public — but add auth for production)

---

## 7. Input Validation and Injection Prevention

### SQL Injection
Not applicable — the application uses Firestore (NoSQL) not SQL.

### NoSQL Injection
Firestore Security Rules evaluate `request.resource.data` fields individually. There is no "injection" possible because Firestore queries are not string-concatenated.

### File Upload Validation

The application validates file uploads on the client side:
```javascript
if (file.type !== 'application/pdf') throw 'Only PDF files'
if (file.size > 50 * 1024 * 1024) throw 'Max 50MB'
```

Firebase Storage Rules add a server-side size limit:
```javascript
allow write: if request.resource.size < 50 * 1024 * 1024
```

**Recommendation:** Firebase Storage does not natively validate file content type — only size. A malicious user could rename a `.exe` file to `.pdf` and upload it. Consider adding a Cloud Storage trigger that validates file content on upload.

---

## 8. Route Security

### Client-Side Guards

Route guards check the Firebase token before rendering. However, they can be bypassed by:
1. Modifying JavaScript in browser DevTools
2. Using the Firestore API directly without the UI

**Countermeasure:** Firestore Security Rules enforce the same role restrictions at the data layer.

### Direct URL Access

All paths serve `index.html` (from `firebase.json` rewrites). React Router handles routing. If a user manually types `/admin/colleges` while logged in as a student:
1. `RequireRole("admin")` guard fires
2. Checks token: `role === "student"` ≠ `"admin"`
3. Redirects to `/`

---

## 9. Secondary Firebase App Security

The secondary Firebase app (`secondaryAuth`) used for creating users:

```javascript
const secondaryApp = initializeApp(firebaseConfig, 'secondary')
const secondaryAuth = getAuth(secondaryApp)
```

**Security notes:**
- The secondary app uses the same Firebase project and config
- It is only used for `createUserWithEmailAndPassword` to avoid signing out the current admin
- After user creation, the secondary app's session is not saved (the created user is not accessible from the secondary app after the call returns)

---

## 10. API Security

### FastAPI Quiz Generation Endpoint

**Current state:** The `REACT_APP_GENERATE_QUIZ_URL` endpoint is called without any authentication token:
```javascript
fetch(process.env.REACT_APP_GENERATE_QUIZ_URL, {
  method: 'POST',
  body: formData  // No Authorization header
})
```

**Risk:** Anyone who knows the endpoint URL can call it with any PDF file, using your compute resources (and LLM API costs).

**Recommendation for production:**
```javascript
// Add Firebase ID token to the request
const token = await user.getIdTokenResult()
fetch(url, {
  method: 'POST',
  headers: { 'Authorization': `Bearer ${token.token}` },
  body: formData
})

// FastAPI should verify the token:
# In FastAPI
from firebase_admin import auth
def verify_token(authorization: str = Header()):
    token = authorization.replace('Bearer ', '')
    decoded = auth.verify_id_token(token)
    return decoded
```

### Firebase Cloud Functions Security

All Firebase Callable Functions require an authenticated user automatically — the Firebase SDK attaches the JWT to every call. Functions can then check `request.auth.token.role`:

```javascript
// In Cloud Function
exports.setUserRole = onCall((request) => {
  if (!request.auth) throw new HttpsError('unauthenticated', 'Must be logged in')
  if (request.auth.token.role !== 'super_admin')
    throw new HttpsError('permission-denied', 'Super admin only')
  // ... proceed safely
})
```

---

## 11. Security Checklist

| Check | Status |
|-------|--------|
| Passwords never stored in Firestore | ✅ |
| Custom claims set server-side only | ✅ |
| Firestore Rules enforce role access | ✅ |
| Storage Rules enforce file size limits | ✅ |
| No `dangerouslySetInnerHTML` | ✅ |
| React auto-escapes JSX content | ✅ |
| Firebase Functions validate caller role | ✅ |
| FastAPI endpoint authenticated | ⚠️ Needs auth header |
| File content validation (not just type/size) | ⚠️ Not implemented |
| Rate limiting for user creation | ⚠️ Not implemented (Cloud Functions) |
| Audit logging for admin actions | ⚠️ Not implemented |
