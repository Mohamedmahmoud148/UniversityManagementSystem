---
render_with_liquid: false
---

# 03 — Folder Structure Guide
## Every Directory Explained — Purpose, Responsibilities, and Design Decisions

---

## Root Project Structure

```
graduation-project-feras/
├── public/                 ← Static assets served as-is
├── src/                    ← All application source code
├── functions/              ← Firebase Cloud Functions (Node.js, deployed server-side)
├── scripts/                ← Utility scripts (admin tools, data migration)
├── make-super-admin/       ← One-time utility to set super_admin claim
├── firebase.json           ← Firebase project configuration
├── firestore.rules         ← Firestore security rules
├── firestore.indexes.json  ← Firestore composite index definitions
├── storage.rules           ← Firebase Storage security rules
├── tailwind.config.js      ← Tailwind CSS configuration
├── package.json            ← Dependencies and scripts
└── package-lock.json       ← Exact dependency versions (lockfile)
```

---

## `public/`

**Purpose:** Contains static files that webpack serves as-is without processing.

**Contents:**
- `index.html` — the single HTML shell; React mounts to `<div id="root">`
- `favicon.ico`, `manifest.json` — PWA metadata
- Any static images referenced directly by URL

**Design Decision:** Keep this folder minimal. All dynamic assets (images used in components) live in `src/assets/` and go through webpack's import pipeline.

---

## `src/` — Application Source Root

This is where all React code lives. The structure follows a **hybrid feature-slice + type-based** organization:

```
src/
├── App.js                  ← Root component
├── index.js                ← ReactDOM.render entry point
├── index.css               ← Global CSS resets
│
├── routes/                 ← Route definitions
│   └── AppRoutes.jsx
│
├── context/                ← Global React Context providers
│   └── AuthContext.jsx
│
├── auth/                   ← Authentication logic and guards
│   ├── RequireRole.js
│   ├── RequireSuperAdmin.js
│   ├── authHelpers.js
│   └── useAuthUser.js
│
├── layouts/                ← Role-specific navigation shells
│   ├── StudentLayout.jsx
│   ├── ProfessorLayout.jsx
│   ├── AssistantLayout.jsx
│   ├── MainLayoutAdmin.jsx
│   ├── MainLayoutProfessor.jsx (legacy)
│   └── MainLayoutSuperAdmin.jsx
│
├── pages/                  ← Page-level components (one per route)
│   ├── admin/
│   ├── professor/
│   ├── student/
│   ├── assistant/
│   └── shared/             ← SignIn, SignUp, Unauthorized
│
├── features/               ← Feature-slice modules
│   ├── colleges/
│   ├── courses/
│   ├── departments/
│   ├── years/
│   ├── professors/
│   └── users/
│
├── firebase/               ← All Firestore/Storage/Functions API
│   ├── firebaseConfig.js   ← SDK initialization (exports db, auth, storage, functions)
│   ├── firestorePaths.js   ← Collection path constants
│   ├── firestoreRefs.js    ← DocumentRef/CollectionRef helpers
│   ├── firestoreColleges.js
│   ├── firestoreAssignments.js
│   ├── materialsApi.js
│   ├── assignmentMaterialsApi.js
│   ├── courseAiApi.js
│   ├── attendanceFunctions.js
│   ├── profCoursesApi.js
│   ├── courseProfApi.js
│   ├── professorApi.js
│   ├── assistantCoursesApi.js
│   ├── buildingsApi.js
│   ├── roomsApi.js
│   ├── scheduleApi.js
│   ├── firestoreCampusApi.js (façade)
│   └── coursesApi.js
│
├── components/             ← Reusable UI components
│   ├── common/             ← Shared across roles
│   ├── admin/              ← Admin-specific components
│   ├── professor/          ← Professor-specific components
│   │   └── course-ai/      ← AI chat components
│   ├── student/            ← Student-specific components
│   ├── assistant/          ← TA-specific components
│   └── engagement/         ← EngagementTracker (MediaPipe)
│
├── services/               ← Non-Firebase cross-cutting services
│   ├── http.js             ← Axios instance
│   ├── users.service.js    ← User creation + profile queries
│   └── colleges.service.js ← College queries
│
├── functions/              ← Client-side Firebase Function call wrappers
│   ├── index.js            ← createAdminUser, setUserRole functions
│   ├── firebaseClaims.js   ← Alternative setUserRole
│   └── deleteUserAccount.js
│
├── utils/                  ← Pure utility functions
│   ├── errorHelpers.js
│   └── campusScheduleUtils.js
│
└── lib/                    ← Domain-specific pure logic
    └── quizUtils.js        ← Quiz score calculation
```

---

## `src/routes/`

**Purpose:** Single source of truth for all application routes.

**File:** `AppRoutes.jsx`

**Responsibilities:**
- Defines the complete route tree using React Router v7's `<Routes>` + `<Route>` API
- Wraps routes with appropriate guards (`RequireRole` or `ProtectedRoute`)
- Wraps routes with role-specific layout components
- Handles 404 (catch-all route → redirect to `/signin`)

**Why centralized?** Having all routes in one file makes it easy to see the complete navigation structure at a glance, and prevents route conflicts between role trees.

**Dependencies:** All layout components, all page components, both guard components.

---

## `src/context/`

**Purpose:** React Context providers for global state that all components need.

**File:** `AuthContext.jsx`

**What it holds:**
- Current Firebase Auth user object
- Resolved role string (from token custom claims)
- Loading state while Firebase initializes

**Why Context and not a state manager?**  
The authentication state is truly global (needed by guards, layouts, and some pages). Context is the React-idiomatic solution for this. For page-level state (quiz data, assignments list), local `useState` is sufficient.

**Design Decision:** The context is initialized with `onAuthStateChanged` — a Firebase listener that fires immediately if there's a persisted session (from localStorage), avoiding a loading flash on page reload.

---

## `src/auth/`

**Purpose:** Authentication guards and helpers — the security layer.

```
RequireRole.js        ← HOC that checks Firebase token claim matches a required role
RequireSuperAdmin.js  ← Specialized guard for super_admin-only routes
authHelpers.js        ← Utility functions: getCurrentUserRole(), forceRefreshToken()
useAuthUser.js        ← Hook: returns { user, authLoading } with force token refresh
```

**Why two guard files?**  
`RequireRole.js` is the original guard. `RequireSuperAdmin.js` is a simplified version that uses `auth.currentUser` synchronously instead of an async listener — slightly faster for super_admin routes that are rarely accessed.

**Common Pitfall:** `RequireRole` forces a token refresh on every mount. This is intentional — custom claims can change (e.g., role upgrade) and stale claims would bypass security. The trade-off is a slight delay on first render.

---

## `src/layouts/`

**Purpose:** Role-specific application shells — sidebar + topbar + content area.

```
StudentLayout.jsx      ← /student/* routes
ProfessorLayout.jsx    ← /prof/* routes (modern)
AssistantLayout.jsx    ← /asst/* routes
MainLayoutAdmin.jsx    ← /admin/* routes
MainLayoutProfessor.jsx ← /professor/* routes (legacy)
MainLayoutSuperAdmin.jsx ← /super_admin/* routes
```

**Responsibilities:**
1. Load the logged-in user's profile from `users/{uid}` in Firestore
2. Render the role-appropriate Sidebar and Topbar
3. Render `<Outlet context={{ user, profile, profileLoading }} />` — passes loaded data to all child pages

**Why load profile in layout?**  
Multiple pages within a role need the user's `collegeId`, `departmentId`, `yearId`. Loading in the layout means it's fetched **once per session** and passed to all children, not fetched independently by each page.

**Design Decision:** Layouts use `getProfessorProfile(uid)` which reads `users/{uid}` — a unified user collection. This same function works for professors AND assistants, reducing code duplication.

---

## `src/pages/`

**Purpose:** Page-level components — one file per application screen.

```
pages/
├── admin/
│   ├── Home.jsx
│   ├── CreateAdminUser.jsx
│   ├── AssignmentsPage.jsx
│   ├── CreateCourseAssignment.jsx
│   ├── BulkImportUsersPage.jsx
│   ├── BuildingsList.jsx
│   ├── BuildingDetails.jsx
│   └── RoomSchedulePage.jsx
│
├── professor/
│   ├── ProfessorHome.jsx
│   ├── ProfessorDashboard.jsx
│   ├── ProfessorCoursesPage.jsx
│   ├── ProfessorCourseDetailsPage.jsx
│   ├── ProfessorQuizzesPage.jsx
│   └── ProfessorQuizResultsPage.jsx
│
├── student/
│   ├── StudentHome.jsx
│   ├── StudentCoursesPage.jsx
│   ├── StudentQuizzesPage.jsx
│   ├── StudentQuizTakePage.jsx
│   └── StudentQuizResultPage.jsx
│
├── assistant/
│   ├── AssistantHome.jsx
│   └── AssistantCoursesPage.jsx
│
└── shared/
    ├── SignIn.jsx
    ├── SignUp.jsx
    └── Unauthorized.jsx
```

**Responsibilities of a Page:**
- Receives context from Layout via `useOutletContext()`
- Calls API functions from `src/firebase/` or `src/features/*/api/`
- Manages local state (data, loading, error, modals)
- Renders UI using MUI + Tailwind + custom components
- Contains business logic (quiz timer, score calculation, file validation)

**What Pages Should NOT Do:**
- Call Firestore SDK directly (use `src/firebase/` instead)
- Manage global auth state (use `AuthContext` instead)
- Contain reusable UI (extract to `src/components/`)

---

## `src/features/`

**Purpose:** Domain-driven feature slices. Each feature encapsulates its own API, components, hooks, and pages.

```
features/
├── colleges/
│   ├── api/
│   │   └── collegesApi.js    ← CRUD for Firestore `colleges`
│   ├── components/
│   │   └── CollegeCard.jsx
│   ├── hooks/
│   │   └── useColleges.js    ← Optimistic update hook
│   └── pages/
│       └── CollegesPage.jsx
│
├── courses/
│   ├── api/
│   │   ├── coursesApi.js          ← CRUD for nested courses + allCourses mirror
│   │   └── courseAssignmentsApi.js ← upsertAssignment Firebase Function call
│   ├── components/
│   └── pages/
│       ├── DepartmentCoursesPage.jsx
│       └── AdminCourseDetailsPage.jsx
│
├── departments/
│   ├── api/
│   │   └── departmentsApi.js
│   └── pages/
│       └── DepartmentsPage.jsx
│
├── years/
│   ├── api/
│   │   └── yearsApi.js
│   └── pages/
│       └── YearsPage.jsx
│
├── professors/
│   └── api/
│       └── professorsApi.js
│
└── users/
    └── api/
        └── usersApi.js       ← Search, batch fetch, resolve IDs
```

**Why feature slices alongside `src/pages/`?**  
The feature-slice folders contain domain logic reused across multiple pages. For example, `useColleges.js` is used by both `CollegesPage` and `CreateCourseAssignment`. Pages in `src/pages/` are route-specific and don't get reused.

**Design Decision — Optimistic Updates:**  
`useColleges.js` implements optimistic updates: it updates local state immediately on mutation, then rolls back if the Firestore operation fails. This makes the UI feel instant.

---

## `src/firebase/`

**Purpose:** The complete data access layer. Every piece of code that touches Firestore, Firebase Storage, or Firebase Functions lives here.

**This is the most important folder in the project.** Pages import functions from here — they never use the Firestore SDK directly.

| File | Domain | Key Exports |
|------|--------|-------------|
| `firebaseConfig.js` | SDK init | `db, auth, storage, functions, analytics` |
| `firestorePaths.js` | Path helpers | Collection path string functions |
| `firestoreRefs.js` | DocumentRef helpers | Pre-built refs for campus hierarchy |
| `firestoreColleges.js` | Colleges | `fetchColleges, getCollegeById` |
| `firestoreAssignments.js` | Assignments | `fetchAssignments, addAssignment, updateAssignment, deleteAssignment` |
| `courseAssignmentsApi.js` | Course assignments | `createCourseAssignment, updateCourseAssignment, deleteCourseAssignment` |
| `materialsApi.js` | Professor materials | `createMaterialDoc, fetchMaterialsForCourse, deleteMaterial` |
| `assignmentMaterialsApi.js` | TA materials | `createAssignmentMaterial, listenAssignmentMaterials, deleteAssignmentMaterial` |
| `courseAiApi.js` | AI chat | `createCourseAiMessagePair, callCourseAiAssistant, listenCourseAiMessages` |
| `attendanceFunctions.js` | Attendance + engagement | `setAttendance, pushEngagement` |
| `profCoursesApi.js` | Prof course index | `listenProfCourses, getProfessorCourseById` |
| `courseProfApi.js` | Alt prof index | `getProfessorCourses, rebuildProfessorCourseIndex` |
| `professorApi.js` | Prof profile + assignments | `getProfessorProfile, listenProfessorAssignments` |
| `assistantCoursesApi.js` | TA courses | `getAssistantCoursesOnce, listenAssistantCourses` |
| `buildingsApi.js` | Campus buildings | `listBuildings, createBuilding, deleteBuilding` (cascade) |
| `roomsApi.js` | Campus rooms | `listRooms, createRoom, deleteRoom` (cascade) |
| `scheduleApi.js` | Room schedules | `createSchedule` (transaction), `deleteSchedule` |
| `firestoreCampusApi.js` | Campus façade | Combines buildings + rooms + schedules |
| `coursesApi.js` | Course queries | `fetchCollegeCourses` (collectionGroup) |

**Why not just write Firestore calls inside pages?**  
Separation of concerns. If Firestore's API changes, or the collection structure needs to change, you update one API file — not every page that touches that data.

---

## `src/components/`

**Purpose:** Reusable UI building blocks used by multiple pages.

```
components/
├── common/
│   └── ProtectedRoute.jsx      ← Modern auth guard
│
├── admin/
│   ├── AssignmentFormModal.jsx
│   ├── ConfirmDeleteDialog.jsx
│   ├── CollegeFormModal.jsx
│   └── UserSearchSelect.jsx
│
├── professor/
│   ├── CourseMaterialsSection.jsx
│   ├── MaterialCard.jsx
│   ├── AddMaterialModal.jsx
│   └── course-ai/
│       ├── AIChat.jsx          ← Main AI chat component
│       └── ChatBubble.jsx
│
├── student/
│   ├── StudentSidebar.jsx
│   └── StudentTopbar.jsx
│
├── assistant/
│   └── AssistantUploadModal.jsx
│
└── engagement/
    └── EngagementTracker.jsx   ← MediaPipe face detection
```

**What Qualifies as a Component?**  
If the same UI block appears in more than one page, extract it to `src/components/`. If it only appears once, keep it in the page file.

**Naming Convention:** PascalCase, descriptive, ends in the type if needed (e.g., `Modal`, `Page`, `Layout`, `Section`, `Card`).

---

## `src/services/`

**Purpose:** Cross-cutting services that don't belong to a single Firebase domain.

```
services/
├── http.js              ← Axios instance with base URL (.NET backend)
├── users.service.js     ← createUserProfile, getProfsByCollegeId
└── colleges.service.js  ← fetchColleges (used from non-Firebase context)
```

**`users.service.js` — why not in `src/firebase/`?**  
User creation involves writing to multiple Firestore collections simultaneously (`users/{uid}`, `users/roles/{role}/{uid}`, `{roleCollection}/{uid}`). This cross-collection logic is more of a "service" than a simple API call, so it lives here.

**`http.js` — Current Status:**  
The Axios base URL (`VITE_API_BASE`) points to the `.NET backend`. Currently no pages actively call this — it's configured for future integration with the `.NET API` for grades, GPA, and roadmap data.

---

## `src/utils/`

**Purpose:** Pure utility functions with no side effects, no Firebase calls, no React.

```
utils/
├── errorHelpers.js        ← getErrorMessage(error, fallback)
└── campusScheduleUtils.js ← Schedule matrix building, slot computation
```

**`campusScheduleUtils.js`** is the most complex utility. It:
- Defines day options (sat-thu) and time slot options (09-11, 11-13, etc.)
- Builds a 2D matrix `{ dayKey: { slotKey: "available" | "reserved" } }` from Firestore docs
- Computes whether a room is fully booked for a day or for the entire week

---

## `src/lib/`

**Purpose:** Domain-specific pure logic. Similar to `utils/` but tied to a specific business domain.

```
lib/
└── quizUtils.js    ← calculateResult(questions, answers) → score, percentage, wrong[]
```

**Why separate from `utils/`?**  
`utils/` is general-purpose. `lib/` contains business rules that have domain knowledge (quiz scoring rules, pass/fail thresholds).

---

## `functions/` (Firebase Cloud Functions — Server-Side)

**Purpose:** Node.js functions deployed to Firebase's serverless environment. These run on Google Cloud, not in the browser.

```
functions/
├── index.js         ← All function definitions (main entry point)
├── package.json     ← Functions-specific dependencies (firebase-admin, etc.)
└── ...
```

**Key functions defined here:**
- `createAdminUser` — creates a Firebase Auth user with admin role
- `setUserRole` — sets a custom claim on any user's token
- `courseAiAssistant` — AI chat processing
- `setAttendance` — attendance validation + Firestore write
- `pushEngagement` — engagement counter aggregation
- `bulkCreateUsers` — batch user creation from Excel data
- `upsertAssignment` — create or update course assignments

**Who deploys this?** `firebase deploy --only functions` from the project root.

**Important:** Changes to functions require re-deployment. They are NOT auto-deployed with `firebase deploy` unless you deploy all or specify functions.

---

## `scripts/`

**Purpose:** One-off administrative scripts for data migration, seeding, or maintenance.

These are run locally with Node.js, not deployed anywhere.

---

## `make-super-admin/`

**Purpose:** A one-time utility project used during initial setup to assign the `super_admin` role to the first administrator.

This runs separately from the main app. After the first super admin is created, this is no longer needed.

---

## Configuration Files

### `firebase.json`
Defines Firebase Hosting, Functions, Firestore, and Storage configuration. Key settings:
- `hosting.public`: `"build"` — deploy the `build/` folder
- `hosting.rewrites`: Single-page app rewrite (`**` → `index.html`)
- `functions.source`: `"functions"` — where Cloud Functions code lives

### `firestore.rules`
Security rules for Firestore. Defines who can read/write each collection. These are enforced server-side by Firebase — client-side Firestore SDK calls that violate rules receive a `permission-denied` error.

### `firestore.indexes.json`
Composite index definitions for Firestore queries that filter + order on multiple fields. If a query requires a composite index that doesn't exist, Firestore returns an error with a link to create it.

### `storage.rules`
Security rules for Firebase Storage (file uploads/downloads).

### `tailwind.config.js`
Tailwind CSS configuration. Defines content paths for purging unused styles, any custom theme extensions, and plugin registrations.
