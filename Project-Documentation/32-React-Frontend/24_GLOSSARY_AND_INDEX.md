---
render_with_liquid: false
---

# 24 — Glossary, Acronyms, and Master Index
## Complete Reference for Terms, Files, Components, and APIs

---

## Part 1: Technical Glossary

### A

**Auth Token (JWT)**
A JSON Web Token issued by Firebase Auth when a user signs in. Contains the user's UID and custom claims (including role). Automatically refreshed by the Firebase SDK before expiry. Sent to .NET API as `Authorization: Bearer <token>`.

**AuthContext**
React Context (`src/context/AuthContext.jsx`) that makes the Firebase Auth user, role, and loading state available to all components. Wraps the entire app via `<AuthProvider>` in `AppRoutes.jsx`.

**Auto-Submit**
The quiz feature that automatically submits the student's current answers when the countdown timer reaches zero. Implemented with `setInterval` in `StudentQuizTakePage.jsx`.

---

### B

**Blaze Plan**
Firebase's pay-as-you-go pricing tier (vs. Spark free tier). Required for outbound HTTP requests from Cloud Functions (needed to call FastAPI or external LLMs).

**BrowserRouter**
React Router component that uses the HTML5 history API for navigation. Wraps all routes in `AppRoutes.jsx`.

**Bundle**
The single JavaScript file (`main.[hash].js`) that Webpack creates from all `src/` files. Served by Firebase Hosting CDN.

---

### C

**Cache Busting**
Technique where file names include a content hash (`main.abc123.js`). When code changes, the hash changes, forcing browsers to download the new file instead of using cached old versions. Handled automatically by CRA's Webpack build.

**Callable Function**
A Firebase Cloud Function invoked directly from the frontend using `httpsCallable(functions, 'functionName')`. Automatically includes the caller's Firebase Auth token. Contrast with HTTP Functions (plain REST endpoints).

**Claims (Custom Claims)**
Extra data embedded in a Firebase JWT by the Firebase Admin SDK. Used to store the user's role (`role: "student"`). Read via `getIdTokenResult().claims`. Cannot be set from the frontend — server-side only.

**CRA (Create React App)**
The scaffolding tool used to initialize this project. Provides a pre-configured Webpack, Babel, Jest, and ESLint setup. The project is not "ejected" — CRA manages the build config.

**CRUD**
Create, Read, Update, Delete — the four basic Firestore operations. In this codebase: `addDoc`/`setDoc`, `getDoc`/`getDocs`/`onSnapshot`, `updateDoc`, `deleteDoc`.

---

### D

**Denormalization**
Storing the same data in multiple places for query efficiency. Example: courses stored in both `colleges/{c}/years/{y}/departments/{d}/courses/{id}` (for hierarchy navigation) and `allCourses/{id}` (for flat lookups).

**Double-Submit Guard**
`useRef(false)` pattern in quiz submission. On first submit attempt, the ref is set to `true` — all subsequent calls return early. Reset to `false` only if the write fails (allowing retry).

---

### E

**Engagement Tracker**
`EngagementTracker.jsx` component that uses MediaPipe's face landmark detection to classify student attention as focused, distracted, or away during live sessions.

**ESLint**
JavaScript linter configured by CRA. Runs as part of `npm start` and `npm run build`. Configuration in `.eslintrc` or `package.json`.

---

### F

**Face Landmark**
One of 478 points on a detected face in MediaPipe's model. Index 1 is the nose tip, which is used for gaze direction estimation.

**Firestore**
Google's NoSQL document database. Data is organized in collections of documents. Supports real-time listeners (`onSnapshot`), offline caching, and atomic transactions.

**Firestore Rules**
Server-side security rules in `firestore.rules` that define who can read/write each collection. Checked on every request — the only true security boundary.

**Firestore Transaction**
`runTransaction()` — atomic read-then-write operation that aborts if the document changes between read and write. Used for room booking to prevent double-booking race conditions.

**Firebase Admin SDK**
Node.js SDK with elevated privileges. Runs only in Firebase Cloud Functions (server-side). Can create users, set custom claims, and bypass security rules.

**Firebase Hosting**
Firebase's static file hosting service with global CDN. Serves the compiled React app from `build/`. Configured in `firebase.json`.

---

### G

**GPA (Grade Point Average)**
Credit-hour weighted average of grade points. Calculated by the .NET backend. Formula: `sum(gradePoints × creditHours) / sum(creditHours)`.

**Guard (Route Guard)**
A component that checks authentication/authorization before rendering a protected route. Two guards exist: `RequireRole.js` (legacy, for admin routes) and `ProtectedRoute.jsx` (modern, for student/professor/assistant routes).

---

### H

**HMR (Hot Module Replacement)**
CRA's development feature that updates changed modules in the browser without a full page reload. Active during `npm start`.

**HTTP Function**
A Firebase Cloud Function exposed as a plain REST endpoint. Contrast with Callable Functions. Used for webhooks or third-party integrations.

---

### I

**`index.js` (Functions)**
`functions/index.js` — single file containing all Firebase Cloud Function definitions. Each exported function becomes a deployed function.

**`index.js` (React)**
`src/index.js` — application entry point. Mounts the React app to `#root`. First file executed by the browser.

---

### J

**JWT (JSON Web Token)**
The token format Firebase Auth uses. Has three parts: header, payload (contains UID + claims), signature. Expires after 1 hour; Firebase SDK auto-refreshes.

---

### L

**Lazy Loading**
Code splitting technique where page components are loaded only when navigated to. **Not currently implemented** in this project — the entire app is bundled into one file.

**Listener (onSnapshot)**
Firestore real-time subscription. Fires once immediately with current data, then fires again on every document/collection change. Must be unsubscribed (returned cleanup function) on component unmount.

---

### M

**MaterialUI (MUI)**
React component library used throughout the app. Provides `Button`, `TextField`, `Dialog`, `CircularProgress`, `Alert`, `Chip`, `Table`, etc.

**MediaPipe**
Google's ML framework for real-time media processing. Used via `@mediapipe/tasks-vision` npm package. Runs as WebAssembly in the browser.

**Modular Firebase API (v9)**
Firebase SDK v9 uses tree-shakeable named imports: `import { getDoc, setDoc } from 'firebase/firestore'`. Contrast with v8's namespace API: `firebase.firestore().doc()`.

**Memoization**
React optimization: `useMemo` for values, `useCallback` for functions. Prevents unnecessary re-renders. Minimally used in this codebase.

---

### N

**Nested Collection**
A Firestore collection inside a document. Example: `conversations/{id}/messages/{msgId}`. Querying nested collections requires knowing the parent document ID.

**`null` vs `undefined` in Firestore**
Firestore stores `null` but ignores `undefined` fields. Setting a field to `undefined` in a `setDoc` call removes the field from the document.

---

### O

**Offline Support (Firestore)**
Firestore SDK caches data locally. If the user goes offline, reads still work from cache, and writes are queued and synced when connection returns.

**Optimistic Update**
UI pattern where the state is updated immediately on user action, then the actual database write happens in the background. If the write fails, the state rolls back. Used in `useColleges.js`.

**Outlet**
React Router's `<Outlet />` component. Renders the matched child route inside a layout component. Used in all layout files (`StudentLayout`, `ProfessorLayout`, etc.).

---

### P

**Phantom Reads**
In concurrent systems, two transactions reading the same document before either writes. Firestore transactions prevent this for single documents but not across multiple documents without a transaction.

**Protected Route**
See Guard (Route Guard).

**`process.env.REACT_APP_*`**
Environment variable naming convention for Create React App. Variables must be prefixed with `REACT_APP_` to be accessible in browser code. Set in `.env` file.

---

### R

**RBAC (Role-Based Access Control)**
The permission model: each user has a role, each action is allowed for certain roles. Enforced at three levels: React route guards (UX), Firebase Functions (API), Firestore Rules (data).

**React Context**
Built-in React state-sharing mechanism. Avoids prop drilling for global state. Used for `AuthContext`. Not Redux or Zustand — plain React.

**Real-Time Listener**
See Listener (onSnapshot).

**Role**
A string stored in Firebase custom claims: `"student"`, `"professor"`, `"assistant"`, `"admin"`, `"super_admin"`.

---

### S

**Secondary Firebase App**
A second initialized Firebase app instance (`initializeApp(config, 'secondary')`). Used in admin user creation to avoid signing out the current admin when creating a new user.

**`serverTimestamp()`**
Firestore sentinel value that is replaced with the server's current time when the document is written. Always use this instead of `new Date()` for `createdAt`/`updatedAt` fields.

**Soft Delete**
.NET backend pattern where records are marked `DeletedAt = now` instead of physically deleted. Firestore does not use soft deletes — documents are physically deleted.

**Storage Rules**
Firebase Storage security rules in `storage.rules`. Control who can upload/download files. Similar syntax to Firestore Rules.

---

### T

**Tailwind CSS**
Utility-first CSS framework. Instead of writing CSS classes, apply pre-built utilities: `className="flex items-center bg-blue-500 p-4"`. Configured via `tailwind.config.js`.

**Transaction**
See Firestore Transaction.

---

### U

**`useEffect` Cleanup**
The function returned from a `useEffect` callback. Called when the component unmounts or before the effect re-runs. Used to cancel Firestore subscriptions, clear intervals, and stop the webcam stream.

**`useOutletContext()`**
React Router hook to receive data passed from a layout's `<Outlet context={...}>`. Used by all page components inside layouts to access `user`, `profile`, `profileLoading`.

**`useRef`**
React hook for a mutable ref container that persists across renders. Does **not** trigger re-renders when changed. Used for: double-submit guard, timer interval ID, webcam stream, engagement count accumulator.

---

### W

**WASM (WebAssembly)**
Binary instruction format that runs in browsers at near-native speed. MediaPipe's ML models run as WASM, allowing face detection without a server round-trip.

**`where()` (Firestore Query)**
Firestore filter: `query(ref, where('field', '==', 'value'))`. Requires a composite index when combining `where` + `orderBy` on different fields.

---

## Part 2: Acronyms

| Acronym | Full Form | Context |
|---------|-----------|---------|
| AI | Artificial Intelligence | AI chat, quiz generation |
| API | Application Programming Interface | REST APIs, Firebase API |
| CRA | Create React App | Build tool |
| CRUD | Create Read Update Delete | Firestore operations |
| CDN | Content Delivery Network | Firebase Hosting |
| DB | Database | Firestore, PostgreSQL |
| DTO | Data Transfer Object | .NET API shapes |
| ENV | Environment Variable | `.env` file, `REACT_APP_*` |
| FCM | Firebase Cloud Messaging | (not used in this project) |
| GPA | Grade Point Average | Academic records |
| HMR | Hot Module Replacement | Dev server |
| HTTP | HyperText Transfer Protocol | REST calls |
| JWT | JSON Web Token | Firebase Auth tokens |
| MCQ | Multiple Choice Question | Quiz question type |
| ML | Machine Learning | MediaPipe, LLM |
| MUI | Material UI | Component library |
| RBAC | Role-Based Access Control | Authorization model |
| RAG | Retrieval-Augmented Generation | AI chat with lecture context |
| SDK | Software Development Kit | Firebase SDK |
| SPA | Single Page Application | React app type |
| SQL | Structured Query Language | PostgreSQL (backend) |
| TA | Teaching Assistant | `assistant` role |
| UI | User Interface | Frontend |
| UID | User ID | Firebase Auth unique ID |
| URL | Uniform Resource Locator | Web addresses |
| UX | User Experience | Design |
| WASM | WebAssembly | MediaPipe execution |

---

## Part 3: Architecture Index

| Component | Location | Purpose |
|-----------|----------|---------|
| App root | `src/index.js` | React mount point |
| App wrapper | `src/App.js` | Single component, delegates to AppRoutes |
| Route tree | `src/routes/AppRoutes.jsx` | All 38+ routes |
| Auth state | `src/context/AuthContext.jsx` | Global auth state |
| Legacy guard | `src/auth/RequireRole.js` | Admin/professor/super_admin routes |
| Modern guard | `src/auth/ProtectedRoute.jsx` | Student/prof/asst routes |
| Firebase init | `src/firebase/firebaseConfig.js` | All Firebase service instances |
| HTTP client | `src/services/http.js` | Axios for .NET API |
| Score engine | `src/lib/quizUtils.js` | Pure quiz calculation |
| Schedule utils | `src/utils/campusScheduleUtils.js` | Room schedule matrix |
| Error utils | `src/utils/errorHelpers.js` | Error message normalization |
| Engagement AI | `src/components/engagement/EngagementTracker.jsx` | MediaPipe integration |
| AI chat | `src/components/professor/course-ai/AIChat.jsx` | Chat UI |
| All functions | `functions/index.js` | All Cloud Functions |
| Security rules | `firestore.rules` | Firestore RBAC |
| Storage rules | `storage.rules` | Storage RBAC |
| Indexes | `firestore.indexes.json` | Composite index definitions |
| Deploy config | `firebase.json` | Hosting + emulator config |
| Tailwind config | `tailwind.config.js` | CSS utility config |

---

## Part 4: File Index (All Source Files)

### `src/routes/`
| File | Purpose |
|------|---------|
| `AppRoutes.jsx` | Complete route tree for all 5 roles |

### `src/context/`
| File | Purpose |
|------|---------|
| `AuthContext.jsx` | `useAuth()` hook + `<AuthProvider>` |

### `src/auth/`
| File | Purpose |
|------|---------|
| `RequireRole.js` | Legacy route guard using `onAuthStateChanged` |
| `ProtectedRoute.jsx` | Modern route guard using `useAuthUser` |

### `src/firebase/`
| File | Purpose |
|------|---------|
| `firebaseConfig.js` | Exports `db`, `auth`, `storage`, `functions`, `analytics` |
| `materialsApi.js` | Material upload/fetch/delete |
| `courseAiApi.js` | AI conversation read/write |
| `attendanceFunctions.js` | Attendance + engagement Firebase Function calls |
| `buildingsApi.js` | Building CRUD |
| `roomsApi.js` | Room CRUD + transaction booking |
| `scheduleApi.js` | Schedule slot read/write |
| `firestorePaths.js` | Collection path string constants |
| `firestoreRefs.js` | Collection reference builder functions |

### `src/hooks/`
| File | Purpose |
|------|---------|
| `useAuth.js` | Firebase Auth state subscription |
| `useAuthUser.js` | Synced Firebase user with Firestore profile |
| `useColleges.js` | College CRUD with optimistic updates |

### `src/services/`
| File | Purpose |
|------|---------|
| `http.js` | Axios instance with Firebase JWT interceptor |
| `users.service.js` | Multi-collection user creation |

### `src/lib/`
| File | Purpose |
|------|---------|
| `quizUtils.js` | `calculateResult()` — pure score calculation |

### `src/utils/`
| File | Purpose |
|------|---------|
| `errorHelpers.js` | `getErrorMessage()` — normalize all error types |
| `campusScheduleUtils.js` | Build schedule matrix, slot key helpers |

### `src/components/`
| File | Purpose |
|------|---------|
| `engagement/EngagementTracker.jsx` | MediaPipe webcam engagement analysis |
| `professor/course-ai/AIChat.jsx` | AI chat UI with Firestore streaming |

### `src/pages/student/`
| File | Purpose |
|------|---------|
| `StudentHome.jsx` | Student dashboard |
| `StudentCoursesPage.jsx` | Enrolled courses list |
| `StudentQuizzesPage.jsx` | Published quizzes list |
| `StudentQuizTakePage.jsx` | Timer + questions + submit |
| `StudentQuizResultPage.jsx` | Score + correct answers |
| `StudentRoadmapPage.jsx` | Academic roadmap (from .NET) |
| `StudentCourseMaterialsPage.jsx` | Download course materials |

### `src/pages/professor/`
| File | Purpose |
|------|---------|
| `ProfessorQuizzesPage.jsx` | Create, manage, publish quizzes |
| `ProfessorCourseMaterialsPage.jsx` | Upload course materials |
| `ProfessorAttendancePage.jsx` | Mark attendance |
| `ProfessorCourseDetailPage.jsx` | Course overview with AI chat |

### `src/pages/admin/`
| File | Purpose |
|------|---------|
| `CollegesPage.jsx` | College management |
| `DepartmentsPage.jsx` | Department management |
| `BulkImportUsersPage.jsx` | Excel user import |
| `CreateUserModal.jsx` | Single user creation |
| `BuildingsPage.jsx` | Building management |
| `RoomSchedulePage.jsx` | Room booking grid |

### `src/pages/shared/`
| File | Purpose |
|------|---------|
| `SignIn.jsx` | Login page (all roles) |
| `NotFound.jsx` | 404 page |

---

## Part 5: Component Index

| Component | File | Props | Purpose |
|-----------|------|-------|---------|
| `App` | `App.js` | none | Root wrapper |
| `AppRoutes` | `routes/AppRoutes.jsx` | none | Full route tree |
| `AuthProvider` | `context/AuthContext.jsx` | `children` | Provides auth context |
| `RequireRole` | `auth/RequireRole.js` | `role`, `children` | Legacy route guard |
| `ProtectedRoute` | `auth/ProtectedRoute.jsx` | `requiredRole`, `children` | Modern route guard |
| `EngagementTracker` | `components/engagement/EngagementTracker.jsx` | `sessionId`, `offeringId` | MediaPipe tracker |
| `AIChat` | `components/professor/course-ai/AIChat.jsx` | `courseDocId`, `professorId` | AI chat interface |
| `StudentLayout` | `pages/student/StudentLayout.jsx` | none (outlet) | Student shell |
| `ProfessorLayout` | `pages/professor/ProfessorLayout.jsx` | none (outlet) | Professor shell |
| `AdminLayout` | `pages/admin/AdminLayout.jsx` | none (outlet) | Admin shell |

---

## Part 6: Firebase API Index

### Direct Firestore Operations (inline in pages)
| Operation | Collection | Page |
|-----------|-----------|------|
| `onSnapshot` quizzes | `quizzes` | `StudentQuizzesPage`, `ProfQuizzesPage` |
| `getDoc` quiz | `quizzes/{id}` | `StudentQuizTakePage` |
| `addDoc` submission | `quizSubmissions` | `StudentQuizTakePage` |
| `getDocs` submissions | `quizSubmissions` | `StudentQuizzesPage` |
| `setDoc` quiz | `quizzes/{id}` | `ProfessorQuizzesPage` |
| `getDoc` user profile | `users/{uid}` | Layout components |

### Firebase API Layer Functions (`src/firebase/`)
| Function | File | Operation |
|----------|------|-----------|
| `fetchMaterialsForCourse()` | `materialsApi.js` | getDocs materials |
| `uploadMaterialPdf()` | `materialsApi.js` | Storage uploadBytes |
| `createMaterialDoc()` | `materialsApi.js` | addDoc |
| `deleteMaterial()` | `materialsApi.js` | deleteDoc + Storage deleteObject |
| `addCourseAiMessage()` | `courseAiApi.js` | addDoc to messages |
| `listenCourseAiMessages()` | `courseAiApi.js` | onSnapshot messages |
| `callCourseAiAssistant()` | `courseAiApi.js` | httpsCallable |
| `setAttendance()` | `attendanceFunctions.js` | httpsCallable |
| `pushEngagement()` | `attendanceFunctions.js` | httpsCallable |
| `fetchBuildings()` | `buildingsApi.js` | getDocs |
| `createBuilding()` | `buildingsApi.js` | addDoc |
| `fetchRooms()` | `roomsApi.js` | getDocs |
| `bookRoomSlot()` | `roomsApi.js` | runTransaction |
| `fetchSchedule()` | `scheduleApi.js` | getDocs |

### Firebase Cloud Functions (`functions/index.js`)
| Function Name | Trigger | Purpose |
|--------------|---------|---------|
| `createNewUser` | Callable | Create Firebase Auth user + claims + Firestore profile |
| `bulkCreateUsers` | Callable | Batch create users from array |
| `setAttendance` | Callable | Write attendance with RBAC check |
| `pushEngagement` | Callable | Upsert engagement aggregate |
| `courseAiAssistant` | Callable | Relay to FastAPI AI chat |

---

## Part 7: Firestore Collections Index

| Collection Path | Purpose | Written By | Read By |
|----------------|---------|-----------|--------|
| `users/{uid}` | User profiles | Admin/Functions | All roles |
| `colleges/{id}` | College hierarchy | Admin | All |
| `colleges/{id}/years/{id}` | Year hierarchy | Admin | All |
| `.../departments/{id}` | Department hierarchy | Admin | All |
| `.../courses/{id}` | Course definitions | Admin | All |
| `allCourses/{id}` | Flat course mirror | Admin | All |
| `quizzes/{id}` | Quiz definitions | Professor | Student (published only) |
| `quizSubmissions/{id}` | Student quiz results | Student | Student (own) + Professor |
| `conversations/{id}` | AI chat sessions | Professor | Professor (own) |
| `conversations/{id}/messages/{id}` | Chat messages | Professor + Functions | Professor (own) |
| `attendance/{id}` | Attendance records | Functions | Professor + Admin |
| `engagementAgg/{id}` | Engagement aggregates | Functions | Professor + Admin |
| `buildings/{id}` | Building info | Admin | All |
| `buildings/{id}/rooms/{id}` | Room info | Admin | All |
| `rooms/{id}/schedule/{id}` | Room bookings | Admin (via transaction) | All |
| `prof_courses/{uid}/courses/{id}` | Professor assignments | Admin | Professor (own) |

---

## Part 8: Environment Variables Reference

| Variable | Required | Purpose |
|----------|----------|---------|
| `REACT_APP_FIREBASE_API_KEY` | Yes | Firebase project API key |
| `REACT_APP_FIREBASE_AUTH_DOMAIN` | Yes | Firebase Auth domain |
| `REACT_APP_FIREBASE_STORAGE_BUCKET` | Yes | Firebase Storage bucket |
| `REACT_APP_FIREBASE_MESSAGING_SENDER_ID` | Yes | FCM sender ID |
| `REACT_APP_FIREBASE_APP_ID` | Yes | Firebase app ID |
| `REACT_APP_GENERATE_QUIZ_URL` | Yes* | FastAPI quiz generation endpoint |
| `REACT_APP_API_BASE` | Yes* | .NET backend base URL |

*Required for those features to function.

**Note:** `src/services/http.js` mistakenly uses `process.env.VITE_API_BASE` (Vite syntax) instead of `REACT_APP_API_BASE`. This means the Axios instance uses the hardcoded fallback URL in all environments until this bug is fixed.
