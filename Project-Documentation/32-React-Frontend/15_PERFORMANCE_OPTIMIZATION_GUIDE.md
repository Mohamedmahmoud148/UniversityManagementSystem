---
render_with_liquid: false
---

# 15 — Performance Optimization Guide
## Current State, Opportunities, and Recommendations

---

## 1. Performance Overview

The application is built with Create React App on Firebase Hosting (CDN-backed). Current performance characteristics:

| Metric | Expected | Status |
|--------|---------|--------|
| First Contentful Paint | ~1.5s | Acceptable |
| Time to Interactive | ~2.5s | Needs improvement |
| Bundle size | ~2.5MB gzipped | High (opportunity) |
| Firebase cold start | ~500ms | Normal |
| Firestore first query | ~300ms | Normal |

The largest performance opportunity is **code splitting and lazy loading**.

---

## 2. Current Optimizations (Already Done)

### 2.1 Firebase Offline Cache
Firestore automatically caches all read data in IndexedDB. On repeat visits or network interruptions, cached data is served instantly — no waiting for Firebase.

### 2.2 Real-Time Subscriptions (vs. Polling)
Using `onSnapshot` means the UI only updates when data actually changes — not every N seconds. This is both faster and more efficient than polling.

### 2.3 In-Memory Profile Cache
`users.service.js` caches professor profiles in a `Map`. The first read hits Firestore; subsequent reads return the cached value instantly.

```javascript
const _cache = new Map()

export const getProfessorById = async ({ db, professorId }) => {
  if (_cache.has(professorId)) return _cache.get(professorId)  // instant
  const profile = await getFromFirestore(professorId)
  _cache.set(professorId, profile)
  return profile
}
```

### 2.4 Layout-Level Profile Loading
Layouts load the user profile once and pass it to all children via Outlet Context. Child pages don't re-fetch the same profile — they receive it from the layout.

### 2.5 Chunked Firestore Queries
The `'in'` operator in Firestore is limited to 30 values. Long arrays are chunked:
```javascript
// chunks array into groups of 10, fires parallel queries
const chunks = chunkArray(userIds, 10)
const results = await Promise.all(chunks.map(chunk => queryChunk(chunk)))
```

---

## 3. Performance Issues (Current)

### 3.1 No Code Splitting ⚠️

**Problem:** All page components for all roles (student, professor, admin, TA) are bundled into a single JavaScript file. A student loads all admin code on every visit.

**Impact:** ~2x larger initial bundle than necessary for any given user.

**Solution:**
```javascript
// Before (eager):
import ProfessorQuizzesPage from '../pages/professor/ProfessorQuizzesPage'

// After (lazy):
const ProfessorQuizzesPage = React.lazy(() =>
  import('../pages/professor/ProfessorQuizzesPage')
)

// Wrap route tree in Suspense:
<Suspense fallback={<LinearProgress />}>
  <Route path="/prof/quizzes" element={<ProfessorQuizzesPage />} />
</Suspense>
```

**Estimated improvement:** 40-50% reduction in initial load time for students (who don't need admin code).

---

### 3.2 No React.memo for Expensive Components ⚠️

**Problem:** Components like `QuizCard` and `CourseCard` re-render even when their props haven't changed (because parent component re-renders).

**Impact:** Unnecessary DOM reconciliation when list data updates.

**Solution:**
```javascript
// Wrap expensive list-item components
const QuizCard = React.memo(function QuizCard({ quiz, onEdit, onDelete }) {
  return (
    <Card>
      {/* ... */}
    </Card>
  )
}, (prevProps, nextProps) => {
  // Return true if component should NOT re-render
  return prevProps.quiz.id === nextProps.quiz.id
      && prevProps.quiz.updatedAt === nextProps.quiz.updatedAt
})
```

---

### 3.3 MediaPipe Model Loading ⚠️

**Problem:** `EngagementTracker` loads two MediaPipe models from CDN (~30MB combined) on every session start. This takes 2-5 seconds on first load.

**Current behavior:** Models are loaded fresh every time the component mounts.

**Solution 1:** Cache the model instances in a module-level variable (outside the component):
```javascript
// module-level (persistent across renders)
let cachedLandmarker = null
let cachedDetector = null

async function getLandmarker() {
  if (!cachedLandmarker) {
    cachedLandmarker = await FaceLandmarker.createFromOptions(vision, config)
  }
  return cachedLandmarker
}
```

**Solution 2:** Preload models in the background when the session page loads, before the user starts the tracker.

---

### 3.4 No Pagination for Large Firestore Collections ⚠️

**Problem:** Queries like `fetchAssignments()` fetch ALL documents with no limit. As the database grows, this becomes slow.

**Current:** `getDocs(query(collection(db, 'courseAssignments')))` — no limit

**Solution for real-time subscriptions:**
```javascript
// Limit + load more pattern
const PAGE_SIZE = 20
const [lastDoc, setLastDoc] = useState(null)

const loadMore = () => {
  const q = query(
    collection(db, 'courseAssignments'),
    orderBy('createdAt', 'desc'),
    startAfter(lastDoc),
    limit(PAGE_SIZE)
  )
  // ...
}
```

**Solution for admin tables:**
Use MUI DataGrid's server-side pagination with Firestore cursors.

---

### 3.5 Synchronous Student Course Filtering ⚠️

**Problem:** `StudentCoursesPage` fetches ALL courses in a college, then filters by year and department client-side. As course count grows, this wastes bandwidth.

**Current:**
```javascript
// Gets all courses for the college (could be hundreds)
where('collegeId', '==', profile.collegeId)
// Then filters client-side by yearId and departmentId
```

**Solution:**
```javascript
// Filter server-side with compound where clauses (requires composite index)
query(
  collection(db, 'allCourses'),
  where('collegeId', '==', profile.collegeId),
  where('yearId', '==', profile.yearId),
  where('departmentId', '==', profile.departmentId)
)
```

**Note:** This requires adding a composite index to `firestore.indexes.json`.

---

## 4. Bundle Optimization

### Current Bundle Analysis

Without running webpack-bundle-analyzer, estimated contributions:

| Library | Estimated Size |
|---------|---------------|
| Firebase SDK (all modules) | ~400KB gzipped |
| MUI Material + DataGrid | ~350KB gzipped |
| React + ReactDOM | ~130KB gzipped |
| MediaPipe | ~50KB gzipped (runtime loads from CDN) |
| ApexCharts | ~80KB gzipped |
| xlsx | ~100KB gzipped |
| jsPDF | ~120KB gzipped |
| Other | ~100KB gzipped |
| **Total** | **~1.33MB** |

**Note:** MediaPipe model files (~30MB) load from CDN at runtime — not in the bundle.

### Optimization Opportunities

**1. Import only used Firebase modules:**
```javascript
// Bad (imports everything)
import firebase from 'firebase/app'

// Good (tree-shakeable, CRA handles this correctly with v9 modular imports)
import { getFirestore, collection, query } from 'firebase/firestore'
import { getAuth } from 'firebase/auth'
```
Firebase v9 modular API is already used — this is correctly done.

**2. Import only used MUI components:**
```javascript
// Bad
import * as MUI from '@mui/material'

// Good (current practice - correctly done)
import { Button, TextField, Dialog } from '@mui/material'
```

**3. Lazy-load xlsx and jsPDF:**
These libraries are only needed in specific pages. Load them on demand:
```javascript
// In BulkImportUsersPage.jsx
const handleFileUpload = async (file) => {
  const XLSX = await import('xlsx')  // loaded only when needed
  const workbook = XLSX.read(...)
}
```

---

## 5. Firestore Read Optimization

### Read Counts and Costs

Firestore charges per document read. Current read patterns:

| Operation | Reads | Frequency |
|-----------|-------|-----------|
| Course list (student) | N courses per query | Once per session |
| Schedule per course | M schedule docs per course × N courses | Once per session |
| Quiz list (student) | All published quizzes for class | Per page visit |
| Professor assignments | All assignments per professor | Real-time |
| User directory (admin) | All users | Per admin session |

### Recommendations

1. **Denormalize frequently-joined data** — instead of loading schedules separately for each course, include schedule summary in the course document.

2. **Use Firestore counters** instead of counting documents client-side:
```javascript
// Bad: count all quiz submissions to calculate stats
const submissions = await getDocs(query(collection(db, 'quizSubmissions'), where('quizId', '==', id)))
const count = submissions.size  // reads ALL submissions

// Good: maintain a counter in the quiz document
const quiz = await getDoc(doc(db, 'quizzes', id))
const count = quiz.data().submissionCount  // one read
```

3. **Cache profile data in localStorage** for returning users — profile data rarely changes.

---

## 6. EngagementTracker Performance

The MediaPipe face detection runs every 1 second. Performance considerations:

| Operation | Cost |
|-----------|------|
| `detectForVideo()` call | ~5-15ms on modern hardware |
| Frame capture from video | ~1ms |
| Firestore write (every 10s) | Network-bound |

**Optimization Suggestions:**
- Skip detection if tab is not visible (`document.hidden`)
- Reduce to every 2 seconds if CPU is high
- Load models asynchronously before session starts

```javascript
// Skip when tab not visible (saves CPU for students who alt-tab)
useEffect(() => {
  const handleVisibility = () => {
    if (document.hidden) clearInterval(intervalRef.current)
    else intervalRef.current = setInterval(sample, 1000)
  }
  document.addEventListener('visibilitychange', handleVisibility)
  return () => document.removeEventListener('visibilitychange', handleVisibility)
}, [])
```

---

## 7. Performance Monitoring Recommendations

Firebase includes Google Analytics. Add custom performance monitoring:

```javascript
// Track quiz load time
import { getPerformance, trace } from 'firebase/performance'

const perf = getPerformance(app)

// In StudentQuizTakePage
const t = trace(perf, 'quiz_load')
t.start()
const quiz = await getDoc(doc(db, 'quizzes', quizId))
t.stop()  // automatically logs to Firebase Console
```

This surfaces real user performance data in the Firebase Console → Performance tab.

---

## 8. Summary: Priority Optimizations

| Priority | Optimization | Effort | Impact |
|----------|-------------|--------|--------|
| 🔴 High | Code splitting (lazy loading pages) | 2h | Large — 40% smaller initial bundle |
| 🔴 High | Server-side student course filtering | 30min | Large — avoids growing data problem |
| 🟡 Medium | MediaPipe model caching | 1h | Medium — 3-5s faster session start |
| 🟡 Medium | Pagination for admin tables | 3h | Medium — prevents slow admin pages |
| 🟢 Low | React.memo for card components | 1h | Small — removes unnecessary re-renders |
| 🟢 Low | Lazy-load xlsx and jsPDF | 30min | Small — smaller initial bundle |
