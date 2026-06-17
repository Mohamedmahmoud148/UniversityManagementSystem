# 07 — Authentication & Authorization
## Complete Security System — Login, Roles, JWT Claims, Guards, and Sessions

---

## 1. Authentication Overview

The application uses **Firebase Authentication** for identity management. Firebase handles:
- Email/password credential validation
- Session persistence (remembers users across page refreshes)
- JWT token generation and signing
- Token refresh (tokens expire after 1 hour; Firebase auto-refreshes)

Authorization (what each user can do) is enforced at two levels:
1. **Client-side route guards** — prevent rendering wrong role's UI
2. **Firestore Security Rules** — prevent unauthorized data access (server-enforced)

---

## 2. Authentication Architecture Diagram

```
User enters email + password
         │
         ▼
signInWithEmailAndPassword(auth, email, password)
         │
   ┌─────┴──────┐
   Error        Success
   │            │
   ▼            ▼
Show error   Firebase returns FirebaseUser
             │
             ▼
     getIdTokenResult(user, forceRefresh=true)
             │
             ▼
     tokenResult.claims.role  ← Custom claim set by Firebase Function
             │
        ┌────┴────┐
        No role   Role found
        │         │
        ▼         ▼
    Error page  Navigate to role home:
                  student    → /student
                  professor  → /prof
                  admin      → /admin/home
                  super_admin → /super_admin/home
                  assistant  → /asst
```

---

## 3. Custom Claims — The Role System

Firebase Authentication tokens (JWTs) can carry custom payload data called **custom claims**. In this application, each user's token contains:

```json
{
  "role": "student"   // or "professor", "admin", "assistant", "super_admin"
}
```

### How Claims Are Set

Custom claims **cannot** be set from the browser. They must be set by a trusted server environment (Firebase Admin SDK). In this project, Firebase Functions set the claims:

```javascript
// functions/index.js — Server-side function
exports.setUserRole = onCall(async (request) => {
  const { userId, role } = request.data

  // Only super_admin can set roles
  if (request.auth.token.role !== 'super_admin') {
    throw new HttpsError('permission-denied', 'Insufficient permissions')
  }

  // Set custom claim
  await admin.auth().setCustomUserClaims(userId, { role })
  return { success: true }
})
```

### When Claims Take Effect

After `setCustomUserClaims()` is called server-side, the change doesn't appear immediately in the user's current token. It takes effect when:
1. The token is force-refreshed (`user.getIdTokenResult(true)`)
2. The user signs in again
3. The current token expires (after ~1 hour) and a new one is issued

This is why all guards use `getIdTokenResult(true)` with force refresh — to ensure they always see the most current role.

### Propagation Delay Example

```
Admin sets student's role from "student" to "professor"
         │
         ▼ (1-2 seconds)
Firebase Admin SDK updates the user's claims in Firebase Auth
         │
         ▼ (requires force refresh)
User must call getIdTokenResult(true) to get new token
         │
         ▼
Old token (1 hour lifetime) is invalidated
New token issued with role: "professor"
```

---

## 4. Login Flow — Sequence Diagram

```
Browser           Firebase Auth       AuthContext
   │                    │                  │
   │ signInWithEmailAndPassword()           │
   │──────────────────>│                  │
   │                    │                  │
   │<── FirebaseUser ──│                  │
   │                    │                  │
   │ getIdTokenResult(forceRefresh=true)   │
   │──────────────────>│                  │
   │                    │                  │
   │<── JWT with claims│                  │
   │                    │                  │
   │ navigate to role home                 │
   │                                       │
   │   onAuthStateChanged fires ──────────>│
   │                                       │ setUser(firebaseUser)
   │                                       │ setRole(claims.role)
   │                                       │ setLoading(false)
```

---

## 5. Session Persistence

Firebase Authentication persists sessions in **browser storage** (IndexedDB by default):

```
User closes browser
Browser storage keeps: encrypted refresh token
User reopens browser
Firebase SDK reads token from storage
Validates with Firebase servers (network request)
onAuthStateChanged fires with existing user
User sees their logged-in state without re-entering credentials
```

**Session Duration:** Sessions persist until:
- The user explicitly signs out (`signOut(auth)`)
- The Firebase project's token is revoked server-side
- Browser storage is cleared (private/incognito mode per session only)

### Customizing Persistence

Firebase supports three persistence modes:
```javascript
import { browserLocalPersistence, browserSessionPersistence, inMemoryPersistence } from 'firebase/auth'

// Default: survives browser close
auth.setPersistence(browserLocalPersistence)

// Session-only: cleared when browser/tab closes
auth.setPersistence(browserSessionPersistence)

// No persistence: lost on page refresh
auth.setPersistence(inMemoryPersistence)
```

This project uses the default `browserLocalPersistence`.

---

## 6. AuthContext — Global Auth State

```javascript
// src/context/AuthContext.jsx
const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser]       = useState(null)
  const [role, setRole]       = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const unsubscribe = onAuthStateChanged(auth, async (firebaseUser) => {
      if (firebaseUser) {
        const tokenResult = await firebaseUser.getIdTokenResult(true)
        setUser(firebaseUser)
        setRole(tokenResult.claims.role || null)
      } else {
        setUser(null)
        setRole(null)
      }
      setLoading(false)
    })
    return () => unsubscribe()   // cleanup listener on unmount
  }, [])

  const refreshUser = async () => {
    if (!user) return
    const tokenResult = await user.getIdTokenResult(true)
    setRole(tokenResult.claims.role || null)
  }

  return (
    <AuthContext.Provider value={{ user, role, loading, refreshUser }}>
      {children}
    </AuthContext.Provider>
  )
}

export const useAuth = () => useContext(AuthContext)
```

**Key Detail:** `onAuthStateChanged` fires:
1. Immediately on mount (with current user if session exists, or null)
2. When user signs in
3. When user signs out
4. When token is refreshed

The `loading: true` initial state prevents rendering before Firebase has checked session status.

---

## 7. Sign Out Flow

```javascript
// From any sidebar (e.g., StudentSidebar.jsx)
import { signOut } from 'firebase/auth'

const handleLogout = async () => {
  await signOut(auth)
  navigate('/signin')
}
```

When `signOut()` is called:
1. Firebase clears the session from browser storage
2. `onAuthStateChanged` fires with `null`
3. `AuthContext` sets `user = null`, `role = null`
4. All route guards now fail → user is redirected to login
5. Any active Firestore `onSnapshot` listeners receive permission-denied errors

**Best Practice:** Always navigate after `signOut()`. Don't rely on the auth listener redirect alone.

---

## 8. Role-Based Access Control (RBAC)

### Frontend RBAC

Routes are guarded by role checks. A student navigating to `/admin/colleges` will:
1. Hit the `RequireRole("admin")` guard
2. Guard fetches token claims: `role === "student"`
3. `"student" !== "admin"` → redirect to `/`

### Data-Level RBAC (Firestore Rules)

Even if a student bypasses the route guard (e.g., directly calling `getDocs()` from browser console), Firestore Security Rules enforce:

```javascript
match /colleges/{collegeId} {
  allow write: if request.auth.token.role == 'admin'
               || request.auth.token.role == 'super_admin';
  allow read: if request.auth != null;  // any authenticated user
}
```

The rule checks `request.auth.token.role` — the claim embedded in the JWT. This cannot be forged by the client.

### Permission Matrix

| Resource | Student | Professor | TA | Admin | SuperAdmin |
|---------|---------|-----------|-----|-------|------------|
| Read colleges | ✅ | ✅ | ✅ | ✅ | ✅ |
| Write colleges | ❌ | ❌ | ❌ | ✅ | ✅ |
| Create quiz | ❌ | ✅ | ❌ | ❌ | ✅ |
| Submit quiz | ✅ | ❌ | ❌ | ❌ | ❌ |
| Read own submission | ✅ | ❌ | ❌ | ✅ | ✅ |
| Read all submissions | ❌ | ✅ (own quizzes) | ❌ | ✅ | ✅ |
| Upload materials | ❌ | ✅ | ✅ | ❌ | ✅ |
| Create admin user | ❌ | ❌ | ❌ | ❌ | ✅ |
| Set user roles | ❌ | ❌ | ❌ | ❌ | ✅ (via CF) |

---

## 9. User Creation Flow

When an admin creates a new user (from `CreateAdminUser.jsx`):

```
Admin fills form: name, email, password, role, college, etc.
                │
                ▼
Secondary Firebase App creates Auth user
(prevents signing out the current admin)
                │
                ▼
createUserWithEmailAndPassword(secondaryAuth, email, password)
                │
                ▼
createUserProfile() writes to Firestore:
  1. users/{uid}                      ← Primary profile
  2. users/roles/{roleCol}/{uid}      ← Role index
  3. {roleCollection}/{uid}           ← e.g., profs/{uid} or students/{uid}
                │
                ▼
Super Admin only: calls setUserRole Cloud Function
  → setCustomUserClaims(uid, { role })
```

**Firestore Collections Written Per Role:**

| Role | Collections Written |
|------|-------------------|
| student | `users/{uid}`, `students/{uid}`, `users/roles/students/{uid}` |
| professor | `users/{uid}`, `profs/{uid}`, `users/roles/profs/{uid}` |
| assistant | `users/{uid}`, `assistants/{uid}`, `users/roles/assistants/{uid}` |
| admin | `users/{uid}`, `Admins/{uid}` (via CF) |

---

## 10. Token Lifecycle

```
User signs in
    │
    ▼
Firebase issues JWT:
  - Expires in 1 hour
  - Contains: uid, email, custom claims (role), iat, exp
    │
    ▼ (after 55 minutes)
Firebase SDK silently refreshes token:
  - Calls Firebase Auth REST API
  - Gets new JWT with fresh 1-hour expiry
  - Stores in IndexedDB
    │
    ▼
All subsequent API calls use the refreshed token
```

**Force Refresh:** Route guards call `getIdTokenResult(true)` which forces a fresh token fetch. This ensures role changes set by admins are immediately reflected.

**Token Contents (decoded):**
```json
{
  "iss": "https://securetoken.google.com/graduation-project-61aa9",
  "aud": "graduation-project-61aa9",
  "auth_time": 1718000000,
  "user_id": "abc123def456",
  "sub": "abc123def456",
  "iat": 1718000000,
  "exp": 1718003600,
  "email": "student@university.edu",
  "firebase": {
    "identities": { "email": ["student@university.edu"] },
    "sign_in_provider": "password"
  },
  "role": "student"   // ← Custom claim
}
```

---

## 11. Security Considerations

### What Is Secure
- Custom claims verified server-side by Firestore Rules
- Firebase Admin SDK only runs in Cloud Functions (server-side)
- Passwords hashed and never stored in Firestore (Firebase Auth manages passwords)
- Storage rules enforce file size limits and path-based ownership
- Token expiry and auto-refresh handled by Firebase SDK

### Potential Vulnerabilities

1. **Client-Side Route Guard Bypass**
   - Risk: A technically savvy user could modify JavaScript to skip the guard
   - Mitigation: Firestore Rules are the real security layer — skipping the guard only shows the UI, data access is still blocked

2. **Token Claims Staleness**
   - Risk: If a user's role is downgraded (e.g., professor removed), they might still see professor UI for up to 1 hour
   - Mitigation: Guards force-refresh the token on every mount

3. **Firestore Rule Gaps**
   - Risk: If rules are too permissive, malicious clients could bypass UI restrictions
   - Mitigation: Test all rules thoroughly using Firebase Emulator Suite

4. **Secondary App Exposure**
   - Risk: The secondary Firebase app config is the same as the primary — both use the same Firebase project
   - Mitigation: This is expected and safe; the config is not a secret

5. **Environment Variable Exposure**
   - Risk: `REACT_APP_*` variables are embedded in the build output (visible in browser)
   - Note: Firebase config values in web apps are intentionally public — security is enforced by Firebase Rules, not by keeping the config secret
