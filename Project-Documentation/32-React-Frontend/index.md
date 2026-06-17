---
render_with_liquid: false
---

# React Frontend — Complete Documentation
## University Management System — Student & Staff Portal

> **Repository:** `graduation-project-feras`  
> **Live URL:** `https://bsnu.web.app`  
> **Last updated:** 2026-06-18  
> **Tech Lead:** Feras Hatem

---

## Complete Documentation Map (24 Files)

| # | File | What's Inside |
|---|------|--------------|
| 01 | [01_PROJECT_OVERVIEW.md](01_PROJECT_OVERVIEW.md) | Vision, goals, all 5 roles, complete feature list, metrics |
| 02 | [02_SYSTEM_ARCHITECTURE.md](02_SYSTEM_ARCHITECTURE.md) | 3-layer architecture, Firebase patterns, data flows |
| 03 | [03_FOLDER_STRUCTURE_GUIDE.md](03_FOLDER_STRUCTURE_GUIDE.md) | Every directory explained recursively |
| 04 | [04_TECH_STACK_DOCUMENTATION.md](04_TECH_STACK_DOCUMENTATION.md) | Every library: why chosen, how used, what breaks without it |
| 05 | [05_CONFIGURATION_GUIDE.md](05_CONFIGURATION_GUIDE.md) | package.json, firebase.json, rules, env vars, Tailwind |
| 06 | [06_ROUTING_SYSTEM.md](06_ROUTING_SYSTEM.md) | Complete route tree, dual guard system, navigation flows |
| 07 | [07_AUTHENTICATION_AND_AUTHORIZATION.md](07_AUTHENTICATION_AND_AUTHORIZATION.md) | Login flow, custom claims, RBAC, token lifecycle |
| 08 | [08_STATE_MANAGEMENT_GUIDE.md](08_STATE_MANAGEMENT_GUIDE.md) | Context, local state, optimistic updates, useRef patterns |
| 09 | [09_API_INTEGRATION_GUIDE.md](09_API_INTEGRATION_GUIDE.md) | Firestore API layer, Firebase Functions, FastAPI calls |
| 10 | [10_COMPONENT_LIBRARY_DOCUMENTATION.md](10_COMPONENT_LIBRARY_DOCUMENTATION.md) | All reusable components with props and behavior |
| 11 | [11_PAGES_DOCUMENTATION.md](11_PAGES_DOCUMENTATION.md) | Every page: purpose, user journey, APIs, state |
| 12 | [12_CUSTOM_HOOKS_GUIDE.md](12_CUSTOM_HOOKS_GUIDE.md) | useAuth, useAuthUser, useColleges — patterns and usage |
| 13 | [13_FORMS_AND_VALIDATION.md](13_FORMS_AND_VALIDATION.md) | All forms, validation rules, submission flows |
| 14 | [14_UI_UX_GUIDE.md](14_UI_UX_GUIDE.md) | Design system, colors, typography, responsive, animations |
| 15 | [15_PERFORMANCE_OPTIMIZATION_GUIDE.md](15_PERFORMANCE_OPTIMIZATION_GUIDE.md) | Current issues, recommendations, priorities |
| 16 | [16_SECURITY_GUIDE.md](16_SECURITY_GUIDE.md) | Auth security, Firestore rules, XSS/CSRF, checklist |
| 17 | [17_ERROR_HANDLING_GUIDE.md](17_ERROR_HANDLING_GUIDE.md) | Error patterns, Firebase error codes, recovery strategies |
| 18 | [18_DEPLOYMENT_GUIDE.md](18_DEPLOYMENT_GUIDE.md) | Setup, build, firebase deploy, env vars, rollback |
| 19 | [19_TESTING_GUIDE.md](19_TESTING_GUIDE.md) | Current state, what to test, examples, CI/CD |
| 20 | [20_DEVELOPER_ONBOARDING_GUIDE.md](20_DEVELOPER_ONBOARDING_GUIDE.md) | 30-minute setup guide, conventions, workflow |
| 21 | [21_COMPLETE_CODE_WALKTHROUGH.md](21_COMPLETE_CODE_WALKTHROUGH.md) | App startup → login → quiz → AI — full execution trace |
| 22 | [22_FEATURE_DOCUMENTATION.md](22_FEATURE_DOCUMENTATION.md) | 10 features: Quiz Engine, AI Chat, Engagement, Materials, Buildings… |
| 23 | [23_DIAGRAMS.md](23_DIAGRAMS.md) | 14 Mermaid diagrams: architecture, auth flow, sequences, ERD |
| 24 | [24_GLOSSARY_AND_INDEX.md](24_GLOSSARY_AND_INDEX.md) | Glossary, acronyms, file index, component index, API index |

---

## 1. Overview

The React Frontend is the user-facing web application for the University Management System. It serves five user roles — students, professors, teaching assistants, admins, and super admins — with a role-based interface that adapts entirely to who is logged in.

The frontend uses **Firebase** as its primary real-time data layer for classroom operations (quizzes, attendance, course materials, AI chat), and connects to the **FastAPI AI service** for intelligent quiz generation from uploaded lecture PDFs.

---

## 2. Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Framework | **React 18** (Create React App) | UI rendering engine |
| Routing | **React Router DOM v7** | Client-side navigation + role-based route guards |
| UI Library | **MUI (Material UI v5)** | Component library — forms, tables, dialogs, cards |
| Styling | **Tailwind CSS v3** | Utility-first CSS classes |
| Charts | **ApexCharts** + react-apexcharts | Analytics dashboards |
| Icons | react-icons v5 | UI iconography |
| Primary Database | **Firebase Firestore** | Real-time NoSQL — quizzes, attendance, courses, chats |
| Authentication | **Firebase Authentication** | Email/password login + custom claims for roles |
| File Storage | **Firebase Storage** | Lecture PDF uploads (materials) |
| Serverless | **Firebase Callable Functions** | AI chat, attendance operations, bulk import |
| AI (quiz gen) | **FastAPI** (`REACT_APP_GENERATE_QUIZ_URL`) | Generate quiz questions from lecture PDFs |
| Computer Vision | **Google MediaPipe** | In-browser face detection for engagement tracking |
| HTTP Client | **Axios** | REST API integration |
| Export | xlsx + jspdf + json-2-csv | Data export to Excel, PDF, CSV |
| Deployment | **Firebase Hosting** | `firebase deploy` → `bsnu.web.app` |

---

## 3. Architecture — How the Three Systems Connect

```
┌──────────────────────────────────────────────────┐
│            React Frontend (bsnu.web.app)          │
│                                                   │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────┐ │
│  │  Firebase   │  │  FastAPI AI  │  │  .NET    │ │
│  │  Firestore  │  │  Service     │  │  Backend │ │
│  │  Auth/Stor  │  │  (quiz gen)  │  │  (REST)  │ │
│  └─────────────┘  └──────────────┘  └──────────┘ │
└──────────────────────────────────────────────────┘
```

### Data Layer Responsibilities

| System | Owns |
|--------|------|
| **Firebase Firestore** | Quizzes, quiz submissions, attendance, sessions, course materials metadata, AI chat conversations, engagement data, campus buildings, course assignments, user roles |
| **Firebase Storage** | Lecture PDF files |
| **Firebase Functions** | AI chat processing, attendance writes, engagement aggregation, bulk user creation |
| **FastAPI AI Service** | Quiz generation from PDF content (called directly from browser) |
| **.NET Backend** | Student academic records, enrollments, grades, GPA, regulations, announcements — NOT directly called from this frontend yet (Axios base URL configured for future integration) |

---

## 4. Authentication & Role System

### Login Flow

1. User enters email + password on `/signin`
2. Firebase Authentication validates credentials
3. `onAuthStateChanged` fires — `getIdTokenResult()` fetches the token with custom claims
4. The `role` claim extracted from token: `"student" | "professor" | "assistant" | "admin" | "super_admin"`
5. Role stored in `AuthContext` — all routes check this context
6. User redirected to their role-specific home page

### Route Guards

Two guard components protect all authenticated routes:

| Guard | Works By |
|-------|---------|
| `RequireRole` | Checks `AuthContext.role` — redirects to `/unauthorized` if mismatch |
| `ProtectedRoute` | Checks Firebase `currentUser` — redirects to `/signin` if not logged in |

### Custom Claims Setup

Roles are set on Firebase Auth user tokens as custom claims. The `bulkCreateUsers` Firebase Function assigns roles when creating users from the Excel import.

---

## 5. User Roles & Pages

### 5.1 Student Role (`/student/...`)

| Route | Page | Description |
|-------|------|-------------|
| `/student` | Home | Welcome dashboard with links to courses and quizzes |
| `/student/courses` | My Courses | List of all courses the student is enrolled in |
| `/student/quizzes` | Available Quizzes | All published quizzes across enrolled courses |
| `/student/quizzes/:quizId` | Take Quiz | Timed quiz interface — MCQ/True-False, countdown timer, auto-submit |
| `/student/quizzes/:quizId/result` | Quiz Result | Score, correct/incorrect answers, percentage |

**Student experience:**
- Views courses they are assigned to
- Sees all quizzes published by their professors
- Takes quizzes with a live countdown timer (auto-submits on expiry)
- Views detailed results per quiz attempt

---

### 5.2 Professor Role (`/prof/...`)

| Route | Page | Description |
|-------|------|-------------|
| `/prof` | Home | Welcome card with quick stats |
| `/prof/dashboard` | Analytics Dashboard | Charts: course stats, student performance |
| `/prof/courses` | My Courses | All courses assigned to this professor |
| `/prof/courses/:courseDocId` | Course Detail | Materials section + AI chat interface |
| `/prof/quizzes` | Quiz Manager | Create/manage all quizzes — MCQ and True/False |
| `/prof/quizzes/:quizId/results` | Quiz Results | Per-student submission results and scores |

**Professor features:**
- Uploads lecture PDFs to course materials
- Chats with AI assistant grounded in their uploaded lectures
- Creates quizzes manually OR uses AI to auto-generate questions from a PDF
- Views detailed results for every quiz

---

### 5.3 Teaching Assistant Role (`/asst/...`)

| Route | Page | Description |
|-------|------|-------------|
| `/asst` | Home | Welcome page |
| `/asst/courses` | Assigned Courses | Courses the TA is assigned to support |

Teaching assistants have a read-only view of their courses and can assist in course management.

---

### 5.4 Admin Role (`/admin/...`)

| Route | Page | Description |
|-------|------|-------------|
| `/admin/home` | Admin Dashboard | Overview stats and navigation |
| `/admin/create-admin` | Create Admin | Create new admin user |
| `/admin/colleges` | Colleges | Full CRUD — manage all colleges |
| `/admin/colleges/:id/years` | Academic Years | Manage academic years within a college |
| `/admin/colleges/:id/years/:id/departments` | Departments | Manage departments per year |
| `/admin/colleges/:id/.../departments/:id/courses` | Courses | Manage courses per department |
| `/admin/assignments` | Course Assignments | View all professor-to-course assignments |
| `/admin/assignments/new` | New Assignment | Assign professor + assistants to a course |
| `/admin/bulk-import-users` | Bulk Import | Upload Excel file → bulk-create user accounts |
| `/admin/campus-buildings` | Buildings | Manage campus buildings CRUD |
| `/admin/campus-buildings/:id` | Building Detail | Floors and rooms inside a building |
| `/admin/campus-buildings/:id/floors/:id/rooms/:id` | Room Schedule | Manage room timetable (day + time slots) |

**Admin capabilities:**
- Full management of academic hierarchy (college → year → department → course)
- Course-to-professor assignment management
- Bulk user creation via Excel upload
- Complete campus physical structure management (buildings, floors, rooms, schedules)

---

### 5.5 Super Admin Role (`/super_admin/...`)

| Route | Page | Description |
|-------|------|-------------|
| `/super_admin/home` | Home | Overview |
| `/super_admin/create-admin` | Create Admin | Create admin users |
| `/super_admin/bulk-import-users` | Bulk Import | Mass user creation via Excel |

Super Admin inherits all Admin capabilities and can additionally manage admin accounts.

---

## 6. Feature Deep Dives

### 6.1 Quiz Engine

The quiz engine is the most complete end-to-end feature in the frontend.

**Professor side:**
1. Opens `/prof/quizzes`
2. Creates a quiz — sets title, start time, duration, question type (MCQ or True/False)
3. Adds questions manually OR uploads a lecture PDF → AI auto-generates 10 questions
4. Publishes quiz (sets `isPublished = true` in Firestore)
5. Monitors results at `/prof/quizzes/:quizId/results`

**Student side:**
1. Opens `/student/quizzes` — sees all published quizzes for their courses
2. Clicks a quiz — countdown timer starts immediately
3. Answers MCQ/True-False questions, can navigate back and forth
4. Timer reaches zero → auto-submit fires regardless of completion
5. Views score and answer breakdown on result page

**Firestore schema:**
```
quizzes/{quizId}
  ├── title, startTime, duration, courseDocId, professorId
  ├── isPublished, questionType
  └── questions[] — { text, options[], correctAnswer }

quizSubmissions/{submissionId}
  ├── quizId, studentId, submittedAt
  ├── answers[] — { questionIndex, selectedAnswer }
  └── score, totalQuestions, percentage
```

---

### 6.2 AI Quiz Generation

When a professor uploads a PDF to the quiz creator:

1. Frontend POSTs the PDF file as `multipart/form-data` to `REACT_APP_GENERATE_QUIZ_URL` (FastAPI endpoint)
2. FastAPI extracts text from the PDF, sends to LLM with a prompt to generate 10 questions
3. Returns JSON array of `{ text, options[], correctAnswer }` objects
4. Frontend populates the quiz form with the generated questions
5. Professor reviews and edits before publishing

This is the primary integration point between the React frontend and the FastAPI AI service.

---

### 6.3 AI Chat per Course (Professor)

Each professor gets a persistent AI conversation per course, powered by their uploaded lecture materials.

**Flow:**
1. Professor opens a course → AI Chat tab appears
2. Types a question about the course content
3. Frontend creates a placeholder AI message in Firestore instantly
4. Calls Firebase Callable Function `courseAiAssistant` with:
   - The professor's recent messages
   - Selected lecture context
   - The course's indexed materials
5. Function calls the LLM → streams response back
6. Response updates the placeholder message in Firestore via `onSnapshot`

**Firestore schema:**
```
ai_conversations/{professorId}_{courseDocId}
  └── messages[] — { role, content, createdAt }
```

---

### 6.4 Attendance System

Sessions and attendance are tracked in real time using Firestore with server-side validation via Firebase Functions.

**Flow:**
1. Instructor creates a session in Firestore (linked to an offering)
2. Opens the session page → sees full student roster
3. Marks each student: `present` / `late` / `absent` / `excused`
4. Firebase Callable Function `setAttendance` validates and upserts the attendance record
5. Firestore trigger updates `attendanceAgg_session` document with aggregate counts
6. All instructors watching the session see live updates via `onSnapshot`

**Firestore schema:**
```
sessions/{sessionId}
  ├── offeringId, date, title
  └── attendanceAgg_session → { present, late, absent, excused }

attendanceRecords/{recordId}
  ├── sessionId, studentId, status
  └── updatedAt
```

---

### 6.5 Engagement Tracker (MediaPipe)

The engagement tracker measures student attention during live sessions using in-browser computer vision — no video is stored.

**How it works:**
1. Student's webcam activates during a session
2. `EngagementTracker` component loads Google MediaPipe FaceLandmarker from CDN
3. Every **1 second**, samples a frame and detects face landmarks
4. Classifies based on nose-tip X position offset from frame center:
   - `focused` — face centered
   - `distracted` — face angled away
   - `away` — no face detected
5. Every **10 samples** (≈10 seconds), calls Firebase Function `pushEngagement` with aggregated counts
6. Function stores in `engagementAgg/{sessionId}_{studentId}`

**Privacy:** Only aggregated counters are stored. No frames, images, or video leave the device.

**Firestore schema:**
```
engagementAgg/{sessionId}_{studentId}
  ├── focused, distracted, away  (int counters)
  └── lastUpdated
```

---

### 6.6 Course Materials Management

**Professor uploads:**
1. Opens a course → Materials section
2. Uploads PDF lecture file (Firebase Storage path: `materials/{professorId}/{courseId}/{materialId}.pdf`)
3. Metadata stored in Firestore under the course document
4. Students can browse and download materials

**Firestore schema:**
```
prof_courses/{professorId}/courses/{courseDocId}/materials/{materialId}
  ├── name, uploadedAt, storageRef
  └── downloadURL (60-min signed URL generated on access)
```

---

### 6.7 Campus Building Management (Admin)

Full CRUD hierarchy: **Building → Floors → Rooms → Schedules**

- Admin creates buildings with name, location, image
- Adds floors inside each building
- Adds rooms inside each floor (with capacity, type: lecture hall / lab / office)
- Sets room schedule: which course occupies the room on which day/time slot

**Firestore schema:**
```
campusBuildings/{buildingId}
  ├── name, location, imageUrl
  └── floors/{floorId}
        └── rooms/{roomId}
              ├── name, capacity, type
              └── schedules/{scheduleId}
                    ├── day, startTime, endTime
                    └── courseId, courseName
```

---

### 6.8 Bulk User Import

Admins can create hundreds of users in one operation:

1. Downloads provided Excel template
2. Fills in: name, email, password, role, department
3. Uploads `.xlsx` file at `/admin/bulk-import-users`
4. Frontend reads the file with the `xlsx` library client-side
5. Calls Firebase Callable Function `bulkCreateUsers` with the parsed data
6. Function creates each user in Firebase Auth + sets custom role claim + creates Firestore user document
7. Returns success/failure count per row

---

## 7. Firestore Data Architecture

### Core Collections

| Collection | Purpose |
|-----------|---------|
| `users` | User profiles — name, email, role, department, profilePicture |
| `prof_courses` | Professor → courses mapping with materials subcollection |
| `courseAssignments` | Links professors and assistants to courses |
| `quizzes` | Quiz definitions: questions, settings, timing |
| `quizSubmissions` | Student quiz answers and scores |
| `sessions` | Live class sessions with attendance aggregates |
| `attendanceRecords` | Per-student attendance status per session |
| `engagementAgg` | Aggregated engagement counters per student per session |
| `ai_conversations` | AI chat history per professor per course |
| `campusBuildings` | Building → floor → room → schedule hierarchy |
| `colleges` | Academic structure: colleges |
| `years` | Academic years per college |
| `departments` | Departments per year |
| `courses` | Courses per department |

### Security Rules

Firebase Security Rules (`firestore.rules`) enforce:
- Students can only read quizzes for their enrolled courses
- Students can only write their own quiz submissions
- Professors can only manage their own courses
- Admins have full read/write
- Engagement data is write-only from client, read-only for instructors

---

## 8. Firebase Functions

| Function | Trigger | What It Does |
|----------|---------|-------------|
| `courseAiAssistant` | HTTP Callable | Processes AI chat messages using course material context |
| `setAttendance` | HTTP Callable | Validates and writes attendance records with aggregate update |
| `pushEngagement` | HTTP Callable | Aggregates and stores engagement counter snapshots |
| `bulkCreateUsers` | HTTP Callable | Creates Firebase Auth users in batch with custom role claims |

---

## 9. AI Integration Points

The frontend integrates with two AI systems:

### FastAPI AI Service (External HTTP)
- **Endpoint:** `REACT_APP_GENERATE_QUIZ_URL` (environment variable)
- **Used for:** Quiz generation from uploaded PDF
- **Method:** POST with `multipart/form-data` containing the PDF file
- **Returns:** Array of 10 generated questions

### Firebase `courseAiAssistant` Function (Firebase Callable)
- **Used for:** Professor AI chat grounded in course materials
- **Method:** Firebase SDK `httpsCallable` — no raw HTTP needed
- **Returns:** LLM-generated response streamed back via Firestore

---

## 10. Project Structure

```
src/
├── App.js                    # Root component — route definitions
├── components/
│   ├── auth/
│   │   ├── RequireRole.jsx   # Role-based route guard
│   │   └── ProtectedRoute.jsx # Auth gate
│   ├── shared/               # Reusable UI: Navbar, Sidebar, LoadingSpinner
│   └── engagement/
│       └── EngagementTracker.jsx # MediaPipe face detection
│
├── context/
│   └── AuthContext.jsx       # Global auth state — user, role, loading
│
├── features/                 # Feature-slice architecture
│   ├── colleges/             # api/, components/, hooks/, pages/
│   ├── courses/
│   ├── departments/
│   ├── years/
│   ├── professors/
│   └── users/
│
├── firebase/                 # All Firestore query functions
│   ├── auth.js               # Sign in, sign up, token claims
│   ├── firestore.js          # Core CRUD helpers
│   └── functions.js          # Callable Function wrappers
│
├── pages/
│   ├── admin/                # Admin pages
│   ├── professor/            # Professor pages (quiz, courses, dashboard)
│   ├── student/              # Student pages (quiz, courses)
│   ├── assistant/            # TA pages
│   └── shared/               # SignIn, SignUp, Unauthorized
│
└── services/
    └── http.js               # Axios instance (base URL: .NET backend)
```

---

## 11. Environment Variables

| Variable | Purpose |
|----------|---------|
| `REACT_APP_GENERATE_QUIZ_URL` | FastAPI endpoint for AI quiz generation |
| `REACT_APP_FIREBASE_API_KEY` | Firebase project API key |
| `REACT_APP_FIREBASE_AUTH_DOMAIN` | Firebase Auth domain |
| `REACT_APP_FIREBASE_PROJECT_ID` | Firebase project ID |
| `REACT_APP_FIREBASE_STORAGE_BUCKET` | Firebase Storage bucket |
| `REACT_APP_FIREBASE_MESSAGING_SENDER_ID` | Firebase Messaging sender |
| `REACT_APP_FIREBASE_APP_ID` | Firebase App ID |

---

## 12. Deployment

```bash
# Install dependencies
npm install

# Run locally
npm start

# Build for production
npm run build

# Deploy to Firebase Hosting
firebase deploy
```

Live at: **`https://bsnu.web.app`**

Firebase project config: `firebase.json`  
Firestore indexes: `firestore.indexes.json`  
Security rules: `firestore.rules`, `storage.rules`

---

## 13. Brand & Design System

| Element | Value |
|---------|-------|
| Primary color | `#0b2c4a` (dark navy blue) |
| UI Kit | Material UI v5 + Tailwind CSS |
| Typography | MUI default (Roboto) |
| Icons | react-icons (Font Awesome + Material Design) |
| Chart library | ApexCharts |

---

## 14. How Frontend Connects to Full System

```
┌─────────────────────────────────────────────────────────┐
│                  React Frontend                          │
│                  bsnu.web.app                           │
│                                                         │
│  ┌───────────────┐  ┌────────────────┐  ┌───────────┐  │
│  │   Firebase    │  │   FastAPI AI   │  │  .NET API │  │
│  │               │  │                │  │ (planned) │  │
│  │ Quizzes       │  │ Quiz from PDF  │  │ Grades    │  │
│  │ Attendance    │  │ AI Advisor     │  │ GPA       │  │
│  │ Materials     │  │ Study Plan     │  │ Roadmap   │  │
│  │ Chat (AI)     │  │                │  │ Enroll    │  │
│  │ Engagement    │  └────────────────┘  └───────────┘  │
│  │ Buildings     │                                     │
│  └───────────────┘                                     │
└─────────────────────────────────────────────────────────┘
```

The frontend is architected to work with all three backends:
- **Firebase** — current primary data layer for classroom operations
- **FastAPI** — integrated for AI quiz generation
- **Axios (.NET)** — configured base URL ready for academic data integration (enrollments, grades, GPA, regulations, roadmap)

---

## 15. Summary of What's Built

| Feature | Status | Backend |
|---------|--------|---------|
| User authentication + roles | ✅ Complete | Firebase Auth |
| Student quiz-taking with timer | ✅ Complete | Firestore |
| Professor quiz creation (manual) | ✅ Complete | Firestore |
| AI quiz generation from PDF | ✅ Complete | FastAPI |
| Course materials upload & view | ✅ Complete | Firebase Storage |
| AI chat per course (professor) | ✅ Complete | Firebase Functions |
| Live attendance marking | ✅ Complete | Firebase Functions |
| Real-time engagement tracking | ✅ Complete | Firebase Functions + MediaPipe |
| Campus buildings management | ✅ Complete | Firestore |
| Academic structure management | ✅ Complete | Firestore |
| Bulk user import from Excel | ✅ Complete | Firebase Functions |
| Professor analytics dashboard | ✅ Complete | Firestore aggregates |
| Grade / GPA display | 🔄 Planned | .NET Backend |
| Academic roadmap view | 🔄 Planned | .NET Backend |
| AI academic advisor chat | 🔄 Planned | FastAPI |
| Assignment submission | 🔄 Planned | .NET Backend |
