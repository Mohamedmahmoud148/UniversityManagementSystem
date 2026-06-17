---
render_with_liquid: false
---

# 02 — System Architecture
## React Frontend — Architecture Deep Dive

---

## Table of Contents

1. [Architecture Philosophy](#1-architecture-philosophy)
2. [High-Level Architecture Diagram](#2-high-level-architecture-diagram)
3. [Layered Architecture](#3-layered-architecture)
4. [Data Flow Architecture](#4-data-flow-architecture)
5. [Component Hierarchy](#5-component-hierarchy)
6. [Authentication Flow](#6-authentication-flow)
7. [State Management Architecture](#7-state-management-architecture)
8. [Rendering Flow](#8-rendering-flow)
9. [Error Handling Flow](#9-error-handling-flow)
10. [Dependency Map](#10-dependency-map)
11. [Dual Route System](#11-dual-route-system)
12. [Firebase Architecture Patterns](#12-firebase-architecture-patterns)

---

## 1. Architecture Philosophy

The frontend follows a **feature-slice + service-layer** hybrid architecture:

- **Feature slices** group related pages, API calls, hooks, and components by domain (colleges, courses, departments, users, professors)
- **Service layer** (`src/firebase/`) centralizes all Firestore reads/writes — no component directly uses the Firestore SDK
- **Context layer** (`src/context/`) manages cross-cutting concerns (authentication state) available globally
- **Layout layer** (`src/layouts/`) provides role-specific navigation shells
- **Functional components everywhere** — zero class components, hooks-only state management

### Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Database | Firebase Firestore | Real-time subscriptions without WebSocket server management |
| State | React useState + useEffect + Context | Simple enough for the feature scope; no Redux overhead |
| Auth guards | Two coexisting systems | Legacy RequireRole + modern ProtectedRoute evolved independently |
| API layer | Centralized in `src/firebase/` | All Firestore code in one place, pages import from this layer |
| AI | Firebase Function (chat) + FastAPI (quiz gen) | Different latency/complexity profiles needed different solutions |

---

## 2. High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                     REACT FRONTEND (bsnu.web.app)                    │
│                     Create React App · React 18                      │
│                                                                      │
│  ┌──────────────┐  ┌─────────────────────────────────────────────┐  │
│  │  AuthContext  │  │            AppRoutes (React Router v7)       │  │
│  │  user        │  │                                             │  │
│  │  role        │  │  /student/*  /prof/*  /admin/*  /asst/*    │  │
│  │  loading     │  │  Route guards: RequireRole + ProtectedRoute │  │
│  └──────┬───────┘  └─────────────────┬───────────────────────────┘  │
│         │                            │                              │
│         │              ┌─────────────▼──────────────┐              │
│         │              │          Layouts             │              │
│         │              │  StudentLayout  ProfLayout   │              │
│         │              │  AdminLayout   AssistLayout  │              │
│         │              └─────────────┬──────────────┘              │
│         │                            │                              │
│         │              ┌─────────────▼──────────────┐              │
│         │              │          Pages               │              │
│         │              │  StudentQuizTakePage         │              │
│         │              │  ProfessorQuizzesPage        │              │
│         │              │  CollegesPage etc.           │              │
│         │              └──────┬─────────────┬────────┘              │
│         │                     │             │                       │
│         │          ┌──────────▼──┐   ┌──────▼──────┐               │
│         │          │  Firebase/  │   │  Features/  │               │
│         │          │  API Layer  │   │  API Layer  │               │
│         │          └──────┬──────┘   └──────┬──────┘               │
└─────────┼─────────────────┼────────────────┼──────────────────────┘
          │                 │                │
          ▼                 ▼                ▼
  Firebase Auth    Firebase Firestore   FastAPI AI
  (token claims)   Firebase Storage     (quiz gen)
                   Firebase Functions
                   (AI chat, attendance
                    engagement, bulk import)
```

---

## 3. Layered Architecture

The codebase has five distinct layers:

### Layer 1: Entry Point
```
src/index.js  →  src/App.js  →  src/routes/AppRoutes.jsx
```
- `index.js` mounts React to `<div id="root">`
- `App.js` is a thin wrapper rendering `<AppRoutes />`
- `AppRoutes.jsx` defines the entire route tree with guards and layouts

### Layer 2: Context (Global State)
```
src/context/AuthContext.jsx
```
- Single global context: `AuthContext`
- Provides `{ user, role, loading, refreshUser }` to all descendants
- Initialized once at app startup via `onAuthStateChanged`

### Layer 3: Layouts (Navigation Shell)
```
src/layouts/
  StudentLayout.jsx      ← StudentSidebar + StudentTopbar
  ProfessorLayout.jsx    ← ProfessorSidebar + ProfessorTopbar
  AssistantLayout.jsx    ← AssistantSidebar + AssistantTopbar
  MainLayoutAdmin.jsx    ← Admin navigation
  MainLayoutProfessor.jsx
  MainLayoutSuperAdmin.jsx
```
- Each layout loads its user profile from Firestore
- Passes `{ user, profile, profileLoading }` to child pages via React Router's `<Outlet context>`
- Handles sidebar navigation and topbar

### Layer 4: Pages
```
src/pages/
  student/     professor/     admin/     assistant/     shared/
```
- Pages consume context and layout data via `useOutletContext()`
- Call the Firebase API layer for data
- Contain business logic (form handling, quiz timer, score calculation)

### Layer 5: API & Services
```
src/firebase/    ← All Firestore/Storage/Functions calls
src/features/    ← Feature-sliced CRUD APIs
src/services/    ← Cross-cutting services (user creation, http)
```
- No Firestore SDK calls appear in pages directly
- All data operations go through named functions in these layers

---

## 4. Data Flow Architecture

### 4.1 Read Flow (Real-Time Subscription)

```
Page Component
    │
    ├── useEffect(() => {
    │      const unsub = listenXxx(id, (data) => {
    │          setData(data)
    │      })
    │      return () => unsub()
    │   }, [id])
    │
    └── Renders data from state
          ▲
          │ (onSnapshot triggers)
    Firebase Firestore
```

Example: `StudentQuizzesPage` subscribes to `quizzes` where `collegeId == profile.collegeId`.

### 4.2 Read Flow (One-Time Fetch)

```
Page Component
    │
    ├── useEffect(() => {
    │      setLoading(true)
    │      fetchXxx(id).then(data => {
    │          setData(data)
    │          setLoading(false)
    │      })
    │   }, [id])
    │
    └── Renders data from state
```

Example: `ProfessorQuizResultsPage` does a one-time fetch of quiz data and submissions.

### 4.3 Write Flow

```
User Action (form submit, button click)
    │
    ├── Validation (client-side)
    │
    ├── Call API function (addXxx / updateXxx / deleteXxx)
    │      │
    │      └── Firestore SDK: setDoc / addDoc / updateDoc / deleteDoc
    │
    └── Update local state OR let Firestore subscription auto-update UI
```

### 4.4 AI Chat Flow

```
Professor types message
    │
    ├── createCourseAiMessagePair()
    │      ├── Writes professor message to Firestore
    │      └── Writes placeholder AI message (status: "processing")
    │
    ├── callCourseAiAssistant()
    │      └── Calls Firebase Callable Function "courseAiAssistant"
    │             └── Function: reads course materials → LLM → updates AI message
    │
    └── onSnapshot on messages collection
           └── UI auto-updates when AI message changes from "processing" to final content
```

### 4.5 Quiz Generation AI Flow

```
Professor uploads PDF
    │
    ├── POST to FastAPI /api/generate-quiz (multipart/form-data)
    │      └── FastAPI: extracts text → LLM prompt → returns 10 questions
    │
    └── Frontend populates quiz form with returned questions
```

---

## 5. Component Hierarchy

```
App
└── AppRoutes
    ├── SignIn / SignUp          (public)
    │
    ├── RequireRole("admin")
    │   └── MainLayoutAdmin
    │       ├── CollegesPage
    │       │   └── CollegeCard
    │       ├── AssignmentsPage
    │       │   └── AssignmentFormModal
    │       │       └── UserSearchSelect
    │       ├── BulkImportUsersPage
    │       │   └── ImportTable
    │       ├── BuildingsList
    │       │   └── BuildingCard
    │       │       └── FloorRoomsView
    │       └── CreateAdminUser
    │           └── UserDirectoryTable
    │
    ├── ProtectedRoute("professor")
    │   └── ProfessorLayout
    │       ├── ProfessorCoursesPage
    │       │   └── CourseCard
    │       │       ├── CourseMaterialsSection
    │       │       │   └── MaterialCard
    │       │       └── AddMaterialModal
    │       ├── ProfessorCourseDetailsPage
    │       │   ├── CourseMaterialsSection
    │       │   └── AIChat
    │       │       └── ChatBubble
    │       ├── ProfessorQuizzesPage
    │       │   ├── QuizCard
    │       │   └── CreateQuizModal
    │       │       └── QuestionBuilder
    │       └── ProfessorQuizResultsPage
    │           └── ResultsTable
    │
    ├── ProtectedRoute("student")
    │   └── StudentLayout
    │       ├── StudentHome
    │       ├── StudentCoursesPage
    │       │   └── CourseCard
    │       ├── StudentQuizzesPage
    │       │   └── QuizCard
    │       ├── StudentQuizTakePage
    │       │   ├── CountdownTimer
    │       │   └── QuestionCard
    │       └── StudentQuizResultPage
    │           └── ResultBreakdown
    │
    └── ProtectedRoute("assistant")
        └── AssistantLayout
            ├── AssistantHome
            └── AssistantCoursesPage
                └── AssistantUploadModal
```

---

## 6. Authentication Flow

```
User visits protected route
        │
        ▼
  Route Guard fires
  (RequireRole or ProtectedRoute)
        │
        ▼
  onAuthStateChanged(auth)
        │
   ┌────┴────┐
   No user   User found
   │         │
   ▼         ▼
redirect   getIdTokenResult(user, forceRefresh=true)
to /signin         │
              ┌────┴────┐
              No claim   claim.role found
              │          │
              ▼          ▼
         (ProtectedRoute  Check claim.role
          fallback:       matches required role
          query Firestore │
          users/{uid})    ├── Match → render children
                          │
                          └── No match → redirect to /unauthorized
```

**Two guard implementations coexist:**

| Guard | Where Used | How It Gets Role |
|-------|-----------|-----------------|
| `RequireRole` | `/admin`, `/professor`, `/super_admin` | Firebase token claims only |
| `ProtectedRoute` | `/prof`, `/student`, `/asst` | Token claims, then Firestore fallback |

Both guards show a loading spinner while waiting for the async role check.

---

## 7. State Management Architecture

The application uses **local component state + React Context** — no Redux, no Zustand, no MobX.

### Global State: AuthContext

```javascript
// AuthContext provides:
{
  user: FirebaseUser | null,
  role: "student" | "professor" | "admin" | "assistant" | "super_admin" | null,
  loading: boolean,
  refreshUser: () => Promise<void>
}
```

Everything else is local state in components.

### Local State Patterns

**Pattern 1: List + Loading + Error**
```javascript
const [items, setItems]     = useState([])
const [loading, setLoading] = useState(true)
const [error, setError]     = useState(null)
```

**Pattern 2: Real-Time Subscription**
```javascript
useEffect(() => {
  const unsub = listenCollection(id, setData, setError)
  return () => unsub()  // cleanup on unmount
}, [id])
```

**Pattern 3: Form State**
```javascript
const [formData, setFormData] = useState({ title: "", durationMinutes: 30 })
const handleChange = (field) => (e) => setFormData(p => ({...p, [field]: e.target.value}))
```

**Pattern 4: Modal Control**
```javascript
const [modalOpen, setModalOpen] = useState(false)
const [editTarget, setEditTarget] = useState(null)
```

### Layout Data Passing (Outlet Context)

Layouts load the user profile once and pass it to all child pages:

```javascript
// In ProfessorLayout.jsx
return <Outlet context={{ user, profile, profileLoading }} />

// In any professor page
const { user, profile, profileLoading } = useOutletContext()
```

This avoids each page independently fetching the same profile.

---

## 8. Rendering Flow

```
Browser loads bsnu.web.app
        │
        ▼
index.html downloaded from Firebase Hosting CDN
        │
        ▼
React bundle loads (main.chunk.js etc.)
        │
        ▼
ReactDOM.render(<App />) → mounts to #root
        │
        ▼
AppRoutes renders BrowserRouter + route tree
        │
        ▼
Route guard fires for current URL
        │
        ▼
Firebase Auth SDK checks persisted session
(from localStorage / IndexedDB)
        │
   ┌────┴────┐
 No session  Session found
      │          │
      ▼          ▼
  <SignIn />  getIdTokenResult()
               determines role
                  │
                  ▼
           Layout renders
           (loads profile from Firestore)
                  │
                  ▼
           Page renders
           (starts Firestore subscriptions)
                  │
                  ▼
           UI displays data
           (live-updating via onSnapshot)
```

### First Paint Optimization

- Firebase Auth SDK persists sessions in browser storage — no round trip needed on reload
- Layouts show skeleton/spinner while profile loads
- Pages show loading states while Firestore data arrives

---

## 9. Error Handling Flow

```
Error occurs (API failure, permission denied, network)
        │
        ▼
Caught in try/catch or onError callback
        │
        ▼
setError(errorMessage)
        │
        ▼
Component renders error state:
  - Alert component with message
  - Retry button if applicable
  - Graceful degradation (show partial data)
```

No global error boundary is implemented — each component handles its own errors.

Common error scenarios:

| Scenario | Where Caught | User Sees |
|----------|-------------|-----------|
| Firestore permission denied | `onError` callback in subscription | Error alert in component |
| Firebase Function timeout | try/catch around `httpsCallable` | Error message in UI |
| Quiz already submitted | Check before render | "Already submitted" message |
| File too large / wrong type | Client-side validation | Inline form error |
| Network offline | Firebase offline cache serves stale data | Data appears from cache |

---

## 10. Dependency Map

```
Pages
  └── depends on: Layouts, Firebase API Layer, Context

Layouts
  └── depends on: Firebase API Layer, Context, Components/shared

Firebase API Layer (src/firebase/)
  └── depends on: firebaseConfig.js (Firebase SDK instances)

Features API Layer (src/features/*/api/)
  └── depends on: Firebase SDK directly

Components
  └── depends on: Firebase API Layer, MUI, react-icons

Context (AuthContext)
  └── depends on: Firebase Auth SDK

Route Guards
  └── depends on: Firebase Auth SDK, Firestore (ProtectedRoute only)
```

### Circular Dependency Risk

**None identified.** The dependency graph is strictly top-down:
`Pages → API Layer → Firebase SDK`

---

## 11. Dual Route System

The project has **two coexisting professor route trees**. This is a historical artifact:

| Route Tree | Guard | Layout | Status |
|-----------|-------|--------|--------|
| `/professor/...` | `RequireRole("professor")` | `MainLayoutProfessor` | Legacy |
| `/prof/...` | `ProtectedRoute("professor")` | `ProfessorLayout` | Active/Modern |

Both route trees render the same page components (`ProfessorCoursesPage`, etc.). The `/prof/` tree is the one linked from navigation. The `/professor/` tree exists for backward compatibility.

**Impact:** No functional difference for users. Both trees work correctly.

---

## 12. Firebase Architecture Patterns

### Pattern 1: Firestore API Files
Each domain has its own API file in `src/firebase/`:

```
firestoreColleges.js     ← colleges CRUD
materialsApi.js          ← professor materials
quizzesApi.js            ← quiz CRUD (embedded in page)
courseAiApi.js           ← AI chat
attendanceFunctions.js   ← attendance + engagement
buildingsApi.js          ← campus buildings
roomsApi.js              ← campus rooms
scheduleApi.js           ← room schedules
```

### Pattern 2: Collection Mirroring
When an entity belongs to a hierarchy (e.g., a course inside a department inside a year inside a college), it is **also** written to a flat `allCourses` collection. This enables fast global queries without traversing subcollections.

```javascript
// createCourse writes to BOTH:
colleges/{c}/years/{y}/departments/{d}/courses/{id}   // hierarchical
allCourses/{id}                                        // flat mirror
```

### Pattern 3: Aggregated Counters
Firebase Functions maintain denormalized aggregate documents:

```
attendanceAgg_session/{sessionId} → { present, late, absent, excused }
engagementAgg/{sessionId}_{studentId} → { focused, distracted, away }
```

This avoids expensive `count()` queries on large collections.

### Pattern 4: Transaction-Based Scheduling
Room schedule creation uses Firestore `runTransaction` to prevent race conditions when two admins try to book the same slot simultaneously:

```javascript
// createSchedule in scheduleApi.js
runTransaction(db, async (tx) => {
  const existing = await tx.get(slotRef)
  if (existing.exists() && existing.data().status !== "available") {
    throw new Error("Slot already booked")
  }
  tx.set(slotRef, payload)
})
```
