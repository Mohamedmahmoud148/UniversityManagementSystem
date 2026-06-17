---
render_with_liquid: false
---

# 06 — Routing System
## Complete Route Architecture, Guards, Navigation Flows, and Diagrams

---

## 1. Routing Library: React Router DOM v7

The application uses React Router v7 with the `BrowserRouter` + `Routes` + `Route` declarative API. All route definitions live in one file: `src/routes/AppRoutes.jsx`.

---

## 2. Complete Route Tree

```
/                           → SignIn (redirect root to login)
/signin                     → SignIn
/signup                     → SignUp
/unauthorized               → Unauthorized page

/admin/                     [RequireRole("admin")]
  home                      → AdminHome
  create-admin              → CreateAdminUser
  colleges                  → CollegesPage
  colleges/:collegeId/
    years                   → YearsPage
    years/:yearId/
      departments           → DepartmentsPage
      departments/:deptId/
        courses             → DepartmentCoursesPage
        courses/:courseId   → AdminCourseDetailsPage
  assignments               → AssignmentsPage
  assignments/new           → CreateCourseAssignment
  bulk-import-users         → BulkImportUsersPage
  campus-buildings          → BuildingsList
  campus-buildings/:buildingId → BuildingDetails
  campus-buildings/:buildingId/floors/:floorId/rooms/:roomId → RoomSchedulePage

/super_admin/               [RequireRole("super_admin")]
  home                      → SuperAdminHome
  create-admin              → CreateAdminUser (shared component)
  bulk-import-users         → BulkImportUsersPage (shared component)

/prof/                      [ProtectedRoute("professor")] ← Modern tree
  (index)                   → ProfessorHome
  dashboard                 → ProfessorDashboard
  courses                   → ProfessorCoursesPage
  courses/:courseDocId      → ProfessorCourseDetailsPage
  quizzes                   → ProfessorQuizzesPage
  quizzes/:quizId/results   → ProfessorQuizResultsPage
  materials                 → <Navigate to="/prof/courses" />

/professor/                 [RequireRole("professor")] ← Legacy tree
  home                      → ProfessorHome (same component)
  dashboard                 → ProfessorDashboard (same component)
  courses                   → ProfessorCoursesPage (same component)
  courses/:courseDocId      → ProfessorCourseDetailsPage (same component)
  materials                 → <Navigate to="/professor/courses" />

/asst/                      [ProtectedRoute("assistant")]
  (index)                   → AssistantHome
  courses                   → AssistantCoursesPage

/student/                   [ProtectedRoute("student")]
  (index)                   → StudentHome
  courses                   → StudentCoursesPage
  quizzes                   → StudentQuizzesPage
  quizzes/:quizId           → StudentQuizTakePage
  quizzes/:quizId/result    → StudentQuizResultPage

/buildings/                 [RequireRole("admin")] ← Legacy buildings tree
  (index)                   → BuildingsPage
  :buildingId/rooms         → RoomsPage
  :buildingId/rooms/:roomId → LegacyRoomSchedulePage
```

---

## 3. Navigation Flow Diagram

```
User lands on any URL
         │
         ▼
     Route matched?
    ┌────┴────┐
    No        Yes
    │         │
    ▼         ▼
redirect   Has guard?
to /signin ┌──┴──┐
           No    Yes
           │     │
           ▼     ▼
        render  Guard checks auth
        page    (see Auth Guard Flow)
```

---

## 4. Route Guards

### 4a. `RequireRole` (Legacy Guard)

```javascript
// src/auth/RequireRole.js
function RequireRole({ role, children }) {
  const [loading, setLoading] = useState(true)
  const [ok, setOk] = useState(false)

  useEffect(() => {
    const unsubscribe = onAuthStateChanged(auth, async (user) => {
      if (!user) { setOk(false); setLoading(false); return }
      const tokenResult = await user.getIdTokenResult(true)  // force refresh
      setOk(tokenResult.claims.role === role)
      setLoading(false)
    })
    return () => unsubscribe()
  }, [role])

  if (loading) return <LoadingSpinner />
  if (!ok) return <Navigate to="/" />
  return children
}
```

**Used by:** `/admin/*`, `/professor/*`, `/super_admin/*`

**Behavior:**
- Shows spinner while checking auth
- No user → redirect to `/` (sign in page)
- Wrong role → redirect to `/` (also sign in page, not `/unauthorized`)
- Correct role → render children

**Note:** This guard redirects to `/` even on wrong role — it doesn't distinguish between "not logged in" and "wrong role". The modern `ProtectedRoute` redirects to `/unauthorized` for wrong role.

---

### 4b. `ProtectedRoute` (Modern Guard)

```javascript
// src/components/common/ProtectedRoute.jsx
function ProtectedRoute({ children, requiredRole, redirectTo = '/signin' }) {
  const { user, authLoading } = useAuthUser()
  const [role, setRole]           = useState(null)
  const [roleLoading, setRoleLoading] = useState(true)
  const [error, setError]         = useState(null)

  useEffect(() => {
    if (!user) { setRoleLoading(false); return }

    const resolveRole = async () => {
      // Step 1: Try token claims
      const tokenResult = await user.getIdTokenResult()
      if (tokenResult.claims.role) {
        setRole(tokenResult.claims.role)
        setRoleLoading(false)
        return
      }
      // Step 2: Fall back to Firestore
      const userDoc = await getDoc(doc(db, 'users', user.uid))
      if (userDoc.exists()) {
        setRole(userDoc.data().role)
      }
      setRoleLoading(false)
    }
    resolveRole().catch(err => { setError(err.message); setRoleLoading(false) })
  }, [user])

  if (authLoading || roleLoading) return <LoadingSpinner />
  if (!user) return <Navigate to={redirectTo} replace />
  if (error) return <ErrorDisplay message={error} />
  if (requiredRole && role !== requiredRole) return <UnauthorizedMessage />
  return children
}
```

**Used by:** `/prof/*`, `/asst/*`, `/student/*`

**Key Differences from RequireRole:**

| Feature | RequireRole | ProtectedRoute |
|---------|-------------|----------------|
| Role source | Token claims only | Claims + Firestore fallback |
| Wrong role behavior | Redirect to `/` | Show inline unauthorized message |
| Error handling | None | Renders error message |
| Force token refresh | Yes | No (uses cached token) |

---

### 4c. `RequireSuperAdmin`

```javascript
// src/auth/RequireSuperAdmin.js
function RequireSuperAdmin({ children }) {
  const [ok, setOk] = useState(null)

  useEffect(() => {
    auth.currentUser?.getIdTokenResult(true).then(result => {
      setOk(result.claims.role === 'super_admin')
    })
  }, [])

  if (ok === null) return <LoadingSpinner />
  if (!ok) return <Navigate to="/" />
  return children
}
```

**Note:** Uses `auth.currentUser` synchronously — doesn't set up a listener. This means it only checks the role once when the component mounts. If the user's session has expired, `auth.currentUser` could be stale. This is acceptable for super_admin routes which are rarely accessed.

---

## 5. Layout-Based Route Nesting

Routes are organized in a nested pattern where the layout component wraps all child pages:

```javascript
// In AppRoutes.jsx
<Route path="/student" element={
  <ProtectedRoute requiredRole="student">
    <StudentLayout />          // ← Renders sidebar + topbar + <Outlet />
  </ProtectedRoute>
}>
  <Route index element={<StudentHome />} />          // /student
  <Route path="courses" element={<StudentCoursesPage />} />  // /student/courses
  <Route path="quizzes" element={<StudentQuizzesPage />} />  // /student/quizzes
  <Route path="quizzes/:quizId" element={<StudentQuizTakePage />} />
  <Route path="quizzes/:quizId/result" element={<StudentQuizResultPage />} />
</Route>
```

**How Outlet Context Works:**
```javascript
// StudentLayout.jsx
const { user } = useAuthUser()
const [profile, setProfile] = useState(null)

useEffect(() => {
  if (user) fetchUserProfile(user.uid).then(setProfile)
}, [user])

return (
  <div className="flex">
    <StudentSidebar />
    <main>
      <Outlet context={{ user, profile, profileLoading }} />
    </main>
  </div>
)

// StudentCoursesPage.jsx — receives context from layout
const { user, profile, profileLoading } = useOutletContext()
// Now has user and profile without fetching them again
```

**Why This Pattern?**  
- Profile is fetched **once** per layout, not once per page
- All pages within the layout automatically receive fresh user data
- Adding a new page under StudentLayout automatically gets access to profile

---

## 6. URL Parameters

### Dynamic Route Parameters

```javascript
// Route definition
<Route path="courses/:courseDocId" element={<ProfessorCourseDetailsPage />} />
<Route path="quizzes/:quizId" element={<StudentQuizTakePage />} />
<Route path="campus-buildings/:buildingId/floors/:floorId/rooms/:roomId"
       element={<RoomSchedulePage />} />

// Usage in page component
const { courseDocId } = useParams()
const { quizId } = useParams()
const { buildingId, floorId, roomId } = useParams()
```

### How Parameters Map to Firestore

| URL Parameter | Firestore Usage |
|--------------|----------------|
| `courseDocId` | `prof_courses/{profId}/courses/{courseDocId}` |
| `quizId` | `quizzes/{quizId}` |
| `buildingId` | `colleges/{collegeId}/buildings/{buildingId}` |
| `floorId` | Used as label for display (floors are not a separate collection) |
| `roomId` | `colleges/{collegeId}/buildings/{buildingId}/rooms/{roomId}` |
| `collegeId` | `colleges/{collegeId}` + all subcollections |
| `yearId` | `colleges/{collegeId}/years/{yearId}` |
| `deptId` | `colleges/{collegeId}/years/{yearId}/departments/{deptId}` |
| `courseId` | `colleges/{collegeId}/years/{yearId}/departments/{deptId}/courses/{courseId}` |

---

## 7. Redirects

### Explicit Redirects

```javascript
// Redirect /prof/materials → /prof/courses
<Route path="materials" element={<Navigate to="/prof/courses" replace />} />
```

The `replace` prop replaces the current history entry instead of pushing a new one, so pressing Back skips the redirected route.

### Programmatic Navigation

```javascript
const navigate = useNavigate()

// After successful login
navigate('/student', { replace: true })  // replace prevents Back going to login

// Navigate with state (pass data without URL params)
navigate('/quiz/result', { state: { score: 85, total: 100 } })

// Back navigation
navigate(-1)
```

---

## 8. The 404 Case

There is no explicit 404 page defined. When a user navigates to an unrecognized path:

1. If they are not in any defined route tree → they see a blank page (React Router renders nothing)
2. Firebase Hosting rewrites ALL paths to `index.html` (from `firebase.json`)
3. React Router then handles the path and renders nothing for unknown routes

**Improvement Recommendation:** Add a catch-all route:
```javascript
<Route path="*" element={<Navigate to="/signin" replace />} />
```

---

## 9. Navigation Between Roles

Users don't navigate between role areas — the guard enforces that a student can't access `/admin` routes and vice versa. However, the login page redirects users to their role-specific home after authentication:

```javascript
// SignIn.jsx — after successful login
const tokenResult = await user.getIdTokenResult(true)
const role = tokenResult.claims.role

const roleRoutes = {
  student:    '/student',
  professor:  '/prof',
  assistant:  '/asst',
  admin:      '/admin/home',
  super_admin: '/super_admin/home'
}

navigate(roleRoutes[role] || '/', { replace: true })
```

---

## 10. Lazy Loading (Current Status)

**Not implemented.** All routes are eagerly imported at bundle load time:

```javascript
// Current approach (eager)
import ProfessorQuizzesPage from '../pages/professor/ProfessorQuizzesPage'

// Recommended (lazy) — reduces initial bundle size
const ProfessorQuizzesPage = lazy(() => import('../pages/professor/ProfessorQuizzesPage'))
// Wrap routes in <Suspense fallback={<Loading />}>
```

**Impact:** The entire application (all pages for all roles) is loaded when any user first visits the site, even if they only use student features. Lazy loading would split the bundle and load each role's code on demand.

**Improvement Recommendation:** Wrap the role-specific route trees with `React.lazy()` + `<Suspense>`.

---

## 11. Route Protection Summary

| Route Prefix | Guard | Authorized Roles |
|-------------|-------|-----------------|
| `/admin/*` | RequireRole("admin") | admin |
| `/super_admin/*` | RequireRole("super_admin") | super_admin |
| `/professor/*` | RequireRole("professor") | professor |
| `/prof/*` | ProtectedRoute("professor") | professor |
| `/asst/*` | ProtectedRoute("assistant") | assistant |
| `/student/*` | ProtectedRoute("student") | student |
| `/signin`, `/signup` | None | Anyone (unauthenticated) |
| `/unauthorized` | None | Anyone |

**Security Note:** Route guards are client-side checks only — they prevent the UI from rendering. They do NOT prevent data access. The actual security for data is in Firestore Security Rules (server-side). Both layers are needed.
