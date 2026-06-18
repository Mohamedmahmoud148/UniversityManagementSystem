# 08 — State Management Guide
## How Data Flows Through the Application

---

## 1. State Management Philosophy

This project uses **local component state + React Context**. There is no Redux, Zustand, MobX, or Jotai. This is intentional and appropriate for the scale of this application.

**Why no Redux?**
- The global state is minimal — only authentication (`user`, `role`, `loading`)
- Most data is per-page (quiz list for one professor, courses for one student)
- Firebase's `onSnapshot` provides real-time reactivity without a store
- Adding Redux would add complexity without solving a real problem

**Rule of thumb used here:**
> If data is needed by more than one component tree → Context
> If data is only needed by one page or component → local `useState`

---

## 2. Global State: AuthContext

The only globally shared state is authentication:

```javascript
// Available everywhere via useAuth() hook
const { user, role, loading, refreshUser } = useAuth()
```

| Property | Type | Description |
|----------|------|-------------|
| `user` | `FirebaseUser \| null` | Firebase Auth user object. Has `uid`, `email`, `displayName`, etc. |
| `role` | `string \| null` | Resolved role from token claims: `"student"`, `"professor"`, etc. |
| `loading` | `boolean` | `true` while Firebase checks session on app start |
| `refreshUser` | `() => Promise<void>` | Force-refreshes token and updates `role` |

### When to Use `useAuth()`

```javascript
// In a page — get the current user's UID for Firestore queries
const { user } = useAuth()
const myQuizzes = await getQuizzesByCreator(user.uid)

// In a guard — check role to decide what to render
const { role, loading } = useAuth()
if (loading) return <Spinner />
if (role !== 'professor') return <Redirect to="/signin" />
```

---

## 3. Layout State: Outlet Context

Layouts load profile data once and pass it to all child pages via React Router's Outlet Context pattern:

{% raw %}
```javascript
// StudentLayout.jsx — fetches once, serves all children
const { user } = useAuth()
const [profile, setProfile] = useState(null)
const [profileLoading, setProfileLoading] = useState(true)

useEffect(() => {
  if (!user) return
  fetchUserProfile(user.uid).then(p => {
    setProfile(p)
    setProfileLoading(false)
  })
}, [user])

return <Outlet context={{ user, profile, profileLoading }} />

// Any student page — receives without fetching
const { user, profile, profileLoading } = useOutletContext()
// profile.collegeId, profile.yearId, profile.departmentId available immediately
```
{% endraw %}

**Profile Shape:**
```javascript
{
  uid: "abc123",
  fullName: "Mohamed Ahmed",
  email: "m.ahmed@uni.edu",
  role: "student",
  collegeId: "coll_01",
  yearId: "year_02",
  departmentId: "dept_cs",
  phone: "01012345678",
  createdAt: Timestamp
}
```

---

## 4. Local State Patterns

### Pattern 1: Data + Loading + Error

The most common pattern. Every page that fetches data follows this structure:

```javascript
const [items, setItems]     = useState([])
const [loading, setLoading] = useState(true)
const [error, setError]     = useState(null)

useEffect(() => {
  fetchItems()
    .then(data => setItems(data))
    .catch(err => setError(err.message))
    .finally(() => setLoading(false))
}, [])

if (loading) return <CircularProgress />
if (error)   return <Alert severity="error">{error}</Alert>
return <ItemList items={items} />
```

### Pattern 2: Real-Time Subscription

For live data (quizzes, attendance, course assignments):

```javascript
const [data, setData] = useState([])

useEffect(() => {
  // Subscribe
  const unsubscribe = onSnapshot(
    query(collection(db, 'quizzes'), where('createdBy', '==', uid)),
    (snapshot) => {
      setData(snapshot.docs.map(d => ({ id: d.id, ...d.data() })))
    },
    (error) => setError(error.message)
  )
  
  // CRITICAL: return cleanup function to prevent memory leaks
  return () => unsubscribe()
}, [uid])  // re-subscribe if uid changes
```

**Why cleanup matters:** If the component unmounts (user navigates away) without calling `unsubscribe()`, the listener continues running in the background. This causes:
- Memory leaks
- State updates on unmounted components (React warning)
- Unnecessary Firebase reads (billing cost)

### Pattern 3: Form State

Forms use controlled components — React state drives every input value:

```javascript
const [formData, setFormData] = useState({
  title: "",
  description: "",
  durationMinutes: 30,
  isPublished: false
})

// Generic field handler
const handleChange = (field) => (event) => {
  setFormData(prev => ({ ...prev, [field]: event.target.value }))
}

// Usage
<TextField
  value={formData.title}
  onChange={handleChange('title')}
/>
```

### Pattern 4: Modal State

```javascript
const [createModalOpen, setCreateModalOpen] = useState(false)
const [editTarget, setEditTarget]           = useState(null)   // null = no item selected
const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
const [deleteTarget, setDeleteTarget]       = useState(null)

// Open edit modal
const handleEdit = (item) => {
  setEditTarget(item)
  setCreateModalOpen(true)
}

// Close and reset
const handleClose = () => {
  setCreateModalOpen(false)
  setEditTarget(null)
}
```

### Pattern 5: Pagination

```javascript
const ITEMS_PER_PAGE = 10
const [page, setPage] = useState(0)

const paginatedItems = items.slice(
  page * ITEMS_PER_PAGE,
  (page + 1) * ITEMS_PER_PAGE
)
```

Note: This is client-side pagination — all data is fetched first, then paginated locally. For large datasets, server-side pagination with Firestore cursors (`startAfter`) would be more efficient.

---

## 5. Quiz State — Complex Example

The quiz-taking page (`StudentQuizTakePage`) is the most complex state machine in the application:

```javascript
// Quiz data
const [quiz, setQuiz]               = useState(null)
const [loading, setLoading]         = useState(true)
const [alreadySubmitted, setAlreadySubmitted] = useState(false)

// Timer state
const [timeRemaining, setTimeRemaining] = useState(null)  // seconds
const [isExpired, setIsExpired]         = useState(false)

// Answer state
const [answers, setAnswers] = useState({})
// Shape: { [questionIndex]: selectedAnswer }

// Navigation
const [currentQuestion, setCurrentQuestion] = useState(0)

// Submission
const [submitting, setSubmitting] = useState(false)
const [submitted, setSubmitted]   = useState(false)
const submitGuardRef = useRef(false)  // prevents double-submit

// Timer countdown
useEffect(() => {
  if (!quiz || isExpired) return
  
  const endTime = quiz.startTime.toMillis() + quiz.durationMinutes * 60 * 1000
  
  const interval = setInterval(() => {
    const remaining = Math.floor((endTime - Date.now()) / 1000)
    
    if (remaining <= 0) {
      setIsExpired(true)
      clearInterval(interval)
      handleAutoSubmit()  // triggered by timer
    } else {
      setTimeRemaining(remaining)
    }
  }, 1000)
  
  return () => clearInterval(interval)
}, [quiz])

// Guard against double submission
const handleAutoSubmit = async () => {
  if (submitGuardRef.current) return  // already submitted
  submitGuardRef.current = true
  await handleSubmit()
}
```

**State Transitions:**

```
quiz = null + loading = true
        │ (Firestore fetch)
        ▼
quiz loaded + loading = false
        │
        ├── alreadySubmitted = true → show result link
        │
        └── alreadySubmitted = false
                │
                ▼
        Timer counting down
        User answers questions
                │
                ├── User clicks Submit → handleSubmit()
                │
                └── Timer expires → handleAutoSubmit()
                        │
                        ▼
                submitGuardRef.current = true (prevents double submit)
                Write to quizSubmissions/{id}
                        │
                        ▼
                submitted = true
                navigate to result page
```

---

## 6. Caching Strategy

The application relies on two caching layers:

### Firebase Offline Cache
Firestore automatically caches data in the browser's IndexedDB. When the network is unavailable:
- `getDoc()` returns cached data
- `onSnapshot()` returns the last cached value

This is transparent to the application — no special code needed.

### In-Memory Cache

`users.service.js` implements a simple in-memory cache for professor profiles:

```javascript
const _cache = new Map()  // { uid: profileData }

export const getProfessorById = async ({ db, professorId }) => {
  if (_cache.has(professorId)) return _cache.get(professorId)
  
  const doc = await getDoc(docRef(db, 'profs', professorId))
  const profile = doc.exists() ? { id: doc.id, ...doc.data() } : null
  _cache.set(professorId, profile)
  return profile
}
```

This prevents redundant Firestore reads when the same professor's profile is needed multiple times during a session.

**Limitation:** Cache is never invalidated — if a professor's profile changes during the session, the cached value is stale until page refresh.

---

## 7. Optimistic Updates

The `useColleges.js` hook implements optimistic updates:

```javascript
const addCollege = async (data) => {
  const tempId = `temp-${Date.now()}`
  const tempCollege = { id: tempId, ...data }
  
  // Optimistic: update UI immediately
  setColleges(prev => [...prev, tempCollege])
  
  try {
    const realId = await createCollege(data)  // Firestore write
    // Replace temp item with real item
    setColleges(prev => prev.map(c => 
      c.id === tempId ? { ...c, id: realId } : c
    ))
  } catch (error) {
    // Rollback: remove the temp item
    setColleges(prev => prev.filter(c => c.id !== tempId))
    throw error
  }
}
```

**Why this matters for UX:** Without optimistic updates, the user clicks "Add College" and waits for the Firestore write to complete before seeing the new item. With optimistic updates, the item appears instantly and is seamlessly replaced with the real item after the write succeeds.

---

## 8. State and Firebase Real-Time — The Connection

Firebase's `onSnapshot` bridges Firebase's real-time data model with React's state model:

```
Firebase Firestore
(holds authoritative data)
       │
       │ onSnapshot listener pushes changes
       ▼
React setState call
       │
       │ React schedules re-render
       ▼
Component re-renders with new data
       │
       ▼
User sees updated UI (automatic, no manual refresh)
```

This pattern means that when Professor A adds a quiz, **all students who have the quiz page open** see it appear automatically — without page refresh, without polling.

---

## 9. Common State Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Not cleaning up subscriptions
```javascript
// WRONG — memory leak
useEffect(() => {
  onSnapshot(colRef, setData)
}, [])

// CORRECT — cleanup on unmount
useEffect(() => {
  const unsub = onSnapshot(colRef, setData)
  return () => unsub()
}, [])
```

### ❌ Anti-Pattern 2: Stale closure in useEffect
```javascript
// WRONG — userId is captured at first render, never updates
useEffect(() => {
  fetchData(userId)  // BUG: uses initial userId even after it changes
}, [])

// CORRECT — re-run when userId changes
useEffect(() => {
  fetchData(userId)
}, [userId])
```

### ❌ Anti-Pattern 3: Redundant profile fetching
```javascript
// WRONG — each page fetches the same profile
function StudentCoursesPage() {
  const [profile, setProfile] = useState(null)
  useEffect(() => { fetchProfile(user.uid).then(setProfile) }, [])
}

// CORRECT — get from layout's outlet context
function StudentCoursesPage() {
  const { profile } = useOutletContext()
}
```

### ❌ Anti-Pattern 4: Double submit without guard
```javascript
// WRONG — user clicks submit twice, creates two submissions
const handleSubmit = async () => {
  setSubmitting(true)
  await writeToFirestore(data)
}

// CORRECT — guard with ref (works even in async context)
const guardRef = useRef(false)
const handleSubmit = async () => {
  if (guardRef.current) return
  guardRef.current = true
  setSubmitting(true)
  await writeToFirestore(data)
}
```
