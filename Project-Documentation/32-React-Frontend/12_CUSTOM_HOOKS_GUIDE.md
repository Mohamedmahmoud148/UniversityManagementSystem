---
render_with_liquid: false
---

# 12 — Custom Hooks Guide
## Every Custom Hook — Purpose, Parameters, Return Values, and Internal Logic

---

## What Are Custom Hooks?

Custom hooks are JavaScript functions whose names start with `use`. They can call other hooks (`useState`, `useEffect`, etc.) and encapsulate reusable stateful logic.

**Why use custom hooks?**
- Extract complex logic from components
- Reuse the same logic across multiple components
- Make components simpler and easier to read
- Make logic easier to test independently

---

## 1. `useAuth`

**File:** `src/context/AuthContext.jsx` (exported from context)

**Purpose:** Access global authentication state anywhere in the component tree.

**Parameters:** None

**Returns:**
```javascript
{
  user: FirebaseUser | null,    // Firebase Auth user object
  role: string | null,          // "student" | "professor" | "admin" | "assistant" | "super_admin"
  loading: boolean,             // true while Firebase initializes
  refreshUser: () => Promise<void>  // force-refresh token and update role
}
```

**Internal Logic:**
- Reads from `AuthContext` (which is populated by `AuthProvider`)
- `AuthProvider` uses `onAuthStateChanged` to keep state current
- On each auth state change, force-refreshes token to read latest custom claims

**Usage:**
```javascript
function MyComponent() {
  const { user, role, loading } = useAuth()

  if (loading) return <Spinner />
  if (!user)   return <Navigate to="/signin" />

  return <div>Hello, {user.email} ({role})</div>
}
```

**When to Use:**
- Any component that needs to know who is logged in
- Any component that needs to know the current user's role
- Guards that decide what to render based on auth state

**What Can Break:**
- If `AuthProvider` is not wrapping the component tree, `useContext(AuthContext)` returns `null`
- Always use inside a component that is a descendant of `<AuthProvider>`

---

## 2. `useAuthUser`

**File:** `src/auth/useAuthUser.js`

**Purpose:** Simpler hook that returns just the Firebase user with a loading state. Designed specifically for route guards.

**Parameters:** None

**Returns:**
```javascript
{
  user: FirebaseUser | null,
  authLoading: boolean
}
```

**Internal Logic:**
```javascript
function useAuthUser() {
  const [user, setUser]           = useState(null)
  const [authLoading, setAuthLoading] = useState(true)

  useEffect(() => {
    const unsubscribe = onAuthStateChanged(auth, async (firebaseUser) => {
      if (firebaseUser) {
        await firebaseUser.getIdTokenResult(true)  // force refresh claims
      }
      setUser(firebaseUser)
      setAuthLoading(false)
    })
    return () => unsubscribe()
  }, [])

  return { user, authLoading }
}
```

**Difference from `useAuth`:** 
- `useAuthUser` does not read the role — only returns user + loading
- Used internally by `ProtectedRoute` which resolves the role itself
- `useAuth` returns the role (pre-resolved in AuthContext)

**Usage:**
```javascript
// Inside ProtectedRoute
const { user, authLoading } = useAuthUser()
if (authLoading) return <Spinner />
if (!user) return <Navigate to="/signin" />
```

---

## 3. `useColleges`

**File:** `src/features/colleges/hooks/useColleges.js`

**Purpose:** Full CRUD management of the colleges collection with optimistic updates.

**Parameters:** None

**Returns:**
```javascript
{
  colleges: College[],           // current list (possibly optimistically updated)
  loading: boolean,
  error: string | null,
  reload: () => void,            // trigger a fresh fetch
  addCollege: (data) => Promise<void>,      // optimistic create
  updateCollege: (id, updates) => Promise<void>,  // optimistic update
  deleteCollege: (id) => Promise<void>      // optimistic delete
}
```

**Internal Logic:**

```javascript
function useColleges() {
  const [colleges, setColleges] = useState([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const data = await fetchColleges()  // from collegesApi.js
      setColleges(data)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // OPTIMISTIC CREATE
  const addCollege = async (data) => {
    const tempId = `temp-${Date.now()}`
    setColleges(prev => [...prev, { id: tempId, ...data }])  // instant UI update
    try {
      const realId = await createCollege(data)  // Firestore write
      setColleges(prev => prev.map(c => c.id === tempId ? { ...c, id: realId } : c))
    } catch (e) {
      setColleges(prev => prev.filter(c => c.id !== tempId))  // ROLLBACK
      throw e
    }
  }

  // OPTIMISTIC UPDATE
  const updateCollege = async (id, updates) => {
    const original = colleges.find(c => c.id === id)
    setColleges(prev => prev.map(c => c.id === id ? { ...c, ...updates } : c))
    try {
      await updateCollegeApi(id, updates)
    } catch (e) {
      setColleges(prev => prev.map(c => c.id === id ? original : c))  // ROLLBACK
      throw e
    }
  }

  // OPTIMISTIC DELETE
  const deleteCollege = async (id) => {
    const original = [...colleges]
    setColleges(prev => prev.filter(c => c.id !== id))
    try {
      await deleteCollegeApi(id)
    } catch (e) {
      setColleges(original)  // ROLLBACK
      throw e
    }
  }

  return { colleges, loading, error, reload: load, addCollege, updateCollege, deleteCollege }
}
```

**What "Optimistic Update" Means:**
The UI updates immediately before the Firestore write completes. If the write fails, the change is rolled back. This makes the UI feel instantaneous even when Firestore takes 200-500ms.

**Usage:**
```javascript
function CollegesPage() {
  const { colleges, loading, addCollege } = useColleges()

  const handleCreate = async (formData) => {
    await addCollege(formData)  // UI updates instantly, Firestore write in background
  }
}
```

---

## 4. Custom Hook Pattern — Real-Time Subscription Hooks

Several features could benefit from encapsulating their subscription logic in a hook. The current codebase does this inline in pages, but here is the recommended pattern:

```javascript
// Pattern: useProfessorQuizzes.js (how it should be done)
function useProfessorQuizzes(professorUid) {
  const [quizzes, setQuizzes]   = useState([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState(null)

  useEffect(() => {
    if (!professorUid) return

    const q = query(
      collection(db, 'quizzes'),
      where('createdBy', '==', professorUid),
      orderBy('createdAt', 'desc')
    )

    const unsubscribe = onSnapshot(
      q,
      (snapshot) => {
        setQuizzes(snapshot.docs.map(d => ({ id: d.id, ...d.data() })))
        setLoading(false)
      },
      (err) => {
        setError(err.message)
        setLoading(false)
      }
    )

    return () => unsubscribe()
  }, [professorUid])

  return { quizzes, loading, error }
}
```

The current implementation puts this logic directly in `ProfessorQuizzesPage` instead of extracting it to a hook. This works fine — extraction into a hook is only beneficial when the same subscription is needed by multiple components.

---

## 5. `useOutletContext` (React Router)

Not a custom hook — built into React Router. But used extensively throughout the app.

**Purpose:** Read data passed by the parent `Layout` component through `<Outlet context={...} />`.

**Usage:**
```javascript
// In any page under StudentLayout
const { user, profile, profileLoading } = useOutletContext()
```

**What Goes Wrong Without It:**
If a page uses `useOutletContext()` but is rendered outside of a Layout that provides context, it returns `undefined`. Always ensure the page is nested under the correct layout in `AppRoutes.jsx`.

---

## 6. Missing Hooks — Recommendations

The codebase would benefit from extracting these repeated patterns into custom hooks:

### Recommended: `useQuizzes(createdByUid)`
Currently inline in `ProfessorQuizzesPage` — extract to share with quiz results page.

### Recommended: `useCourseAssignments(professorUid)`
Currently duplicated between `ProfessorCoursesPage` and `ProfessorDashboard`.

### Recommended: `useStudentCourses(profile)`
Currently inline in `StudentCoursesPage` with complex chunking logic.

### Recommended: `useBuildingSchedule(collegeId, buildingId, roomId)`
Currently inline in `RoomSchedulePage`.

### Example Implementation:
```javascript
function useCourseAssignments(professorUid) {
  const [assignments, setAssignments] = useState([])
  const [loading, setLoading]         = useState(true)
  const [error, setError]             = useState(null)

  useEffect(() => {
    if (!professorUid) return

    const q = query(
      collection(db, 'courseAssignments'),
      where('professorIds', 'array-contains', professorUid),
      orderBy('createdAt', 'desc')
    )

    const unsubscribe = onSnapshot(q,
      (snap) => { setAssignments(snap.docs.map(d => ({id:d.id,...d.data()}))); setLoading(false) },
      (err) => { setError(err.message); setLoading(false) }
    )
    return () => unsubscribe()
  }, [professorUid])

  return { assignments, loading, error }
}
```

---

## 7. Hook Rules — Common Pitfalls

### Rule 1: Hooks must be called at the top level
```javascript
// ❌ WRONG — conditional hook call
if (user) {
  const { courses } = useColleges()  // BUG: hooks cannot be inside conditions
}

// ✅ CORRECT — call at top level, use conditionally
const { courses } = useColleges()
if (!user) return null
```

### Rule 2: Hooks must be called from React functions
```javascript
// ❌ WRONG — calling a hook from a regular function
function fetchData() {
  const { user } = useAuth()  // BUG: not inside a component or hook
}

// ✅ CORRECT — call from component or custom hook only
function MyComponent() {
  const { user } = useAuth()  // OK
}
```

### Rule 3: Cleanup subscriptions
```javascript
// ❌ WRONG — no cleanup
useEffect(() => {
  onSnapshot(ref, setData)
}, [])

// ✅ CORRECT — always return cleanup
useEffect(() => {
  const unsub = onSnapshot(ref, setData)
  return () => unsub()
}, [])
```

### Rule 4: Dependency arrays
```javascript
// ❌ WRONG — missing dependency, stale closure
useEffect(() => {
  fetchData(userId)  // userId changes won't re-run this effect
}, [])

// ✅ CORRECT — include all values used inside the effect
useEffect(() => {
  fetchData(userId)
}, [userId])
```
