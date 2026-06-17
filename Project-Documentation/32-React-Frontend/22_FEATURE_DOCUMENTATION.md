# 22 — Feature Documentation
## Every Feature Module: Purpose, Design, Data, and Flow

---

## Feature 1: Quiz Engine

### What It Does
Allows professors to create quizzes (manually or via AI), publish them to a specific class, and have students take them with a countdown timer. Scores are calculated client-side and stored in Firestore.

### Who Uses It
- **Professor:** Creates, manages, publishes quizzes. Views all submissions and individual student results.
- **Student:** Takes published quizzes, sees their result immediately after submission.
- **Assistant (TA):** Can view course quizzes (read-only access depending on rules).

### Data Model

**Firestore Collection: `quizzes/{quizId}`**
```
{
  title:           string       // "Midterm Chapter 3"
  description:     string       // optional
  createdBy:       string       // professor UID
  createdAt:       Timestamp
  collegeId:       string       // restricts which students see this quiz
  yearId:          string
  departmentId:    string
  startTime:       Timestamp    // when the quiz becomes active
  durationMinutes: number       // timer countdown
  questionType:    "mcq" | "trueFalse" | "mixed"
  isPublished:     boolean      // true = students can see it
  questions: [
    {
      id:            string      // crypto.randomUUID()
      text:          string
      type:          "mcq" | "trueFalse"
      options:       string[]    // ["A", "B", "C", "D"]
      correctAnswer: string      // "A"
      points:        number      // 5
    }
  ]
}
```

**Firestore Collection: `quizSubmissions/{submissionId}`**
```
{
  quizId:         string
  studentUid:     string
  studentName:    string
  answers:        { [questionIndex: string]: string }  // { "0": "A", "1": "True" }
  score:          number       // actual points earned
  totalPoints:    number       // max possible
  percentage:     number       // 0–100
  wrongQuestions: number[]     // indices of incorrect answers
  submittedAt:    Timestamp
}
```

### Key Files
| File | Role |
|------|------|
| `src/pages/professor/ProfessorQuizzesPage.jsx` | Create, manage, publish quizzes |
| `src/pages/student/StudentQuizTakePage.jsx` | Timer, answers, auto-submit |
| `src/pages/student/StudentQuizResultPage.jsx` | Show result with correct answers |
| `src/lib/quizUtils.js` | `calculateResult()` pure function |

### Score Calculation Logic (`quizUtils.js`)
```javascript
export const calculateResult = (questions, answers) => {
  let score = 0
  const wrongQuestions = []

  questions.forEach((q, i) => {
    if (answers[i] === q.correctAnswer) {
      score += q.points
    } else {
      wrongQuestions.push(i)
    }
  })

  const totalPoints = questions.reduce((sum, q) => sum + q.points, 0)
  const percentage = totalPoints > 0
    ? Math.round((score / totalPoints) * 100)
    : 0

  return { score, totalPoints, percentage, wrongQuestions }
}
```

### Timer Behavior
- Countdown starts immediately on page load (uses quiz `startTime + durationMinutes`)
- Color changes: normal → orange (2 min left) → red (1 min left)
- Auto-submits on expiry with whatever answers the student has selected
- Double-submit protection via `useRef(false)` guard

### AI Quiz Generation
1. Professor selects PDF file
2. Frontend sends `POST /api/generate-quiz` with `multipart/form-data` to FastAPI
3. FastAPI extracts text → sends to LLM → returns structured questions
4. Questions are normalized to the internal format and shown in the quiz editor
5. Professor reviews/edits before publishing

---

## Feature 2: AI Chat (Course Assistant)

### What It Does
Professors can chat with an AI about any of their course lectures. The AI has context about the lecture content (RAG-based) and responds with course-specific answers.

### Who Uses It
- **Professor only** — available in the professor course detail pages.

### Architecture

```
Professor types message
        ↓
AIChat.jsx — addCourseAiMessage() → writes to Firestore
        ↓
Firestore: conversations/{id}/messages/{msgId} → status: "processing"
        ↓
Firebase Function trigger (or callable): courseAiAssistant
        ↓
FastAPI /chat endpoint (RAG with lecture PDFs)
        ↓
Function writes response → conversations/{id}/messages/{responseId}
        ↓
onSnapshot listener in AIChat.jsx fires → UI updates
```

### Data Model

**Firestore Collection: `conversations/{conversationId}`**
```
{
  courseDocId:  string
  professorId:  string
  createdAt:    Timestamp
}
```

**Firestore Subcollection: `conversations/{id}/messages/{msgId}`**
```
{
  role:      "user" | "assistant"
  content:   string
  status:    "done" | "processing" | "error"
  createdAt: Timestamp
  lecture:   string     // which lecture context was used
}
```

### Key Files
| File | Role |
|------|------|
| `src/components/professor/course-ai/AIChat.jsx` | Full chat UI |
| `src/firebase/courseAiApi.js` | Firestore read/write for chat |
| `functions/index.js` → `courseAiAssistant` | Serverless AI function |

### Real-Time Streaming via Firestore
The response from AI doesn't stream word-by-word (no WebSocket). Instead:
1. A "processing" placeholder appears immediately
2. The Firebase Function completes and writes the full response
3. `onSnapshot` fires and the response replaces the placeholder

---

## Feature 3: Attendance System

### What It Does
Professors or TAs record student attendance for each session. Students can see their own attendance history. Admins can view full reports.

### Who Uses It
- **Professor/Assistant:** Mark students as present/absent/late
- **Student:** View own attendance per course
- **Admin:** View attendance reports across departments

### Data Model

**Firestore Collection: `attendance/{attendanceId}`**
```
{
  offeringId:   string       // SubjectOffering ID (links to .NET)
  sessionDate:  Timestamp
  professorId:  string
  students: [
    {
      uid:      string
      name:     string
      status:   "present" | "absent" | "late"
    }
  ]
}
```

### Key Files
| File | Role |
|------|------|
| `src/firebase/attendanceFunctions.js` | `setAttendance()` — calls Firebase Function |
| `functions/index.js` → `setAttendance` | Server-side write with RBAC check |

### Why a Firebase Function?
Writing attendance uses a Firebase Cloud Function (not direct Firestore write) because:
- The function validates the caller's role before writing
- Prevents students from modifying attendance records
- Logs the action server-side for audit trail

---

## Feature 4: Engagement Tracker

### What It Does
During a live session, the student's webcam is analyzed every second using MediaPipe's face landmark detection to classify attention as "focused", "distracted", or "away". Aggregated counts are pushed to Firebase every 10 seconds.

### Who Uses It
- **Student:** Their browser runs the tracker silently during sessions (with permission)
- **Professor/Admin:** Can view per-student engagement reports

### Technical Architecture

```
Browser webcam → video element
                    ↓
          MediaPipe FaceLandmarker (WASM, runs locally)
                    ↓
          1-second interval: detectForVideo()
                    ↓
          Nose tip X position analysis:
          |offset| < 0.15 → "focused"
          |offset| > 0.15 → "distracted"
          no face detected → "away"
                    ↓
          Accumulate in countsRef (no render)
                    ↓
          Every 10 samples → Firebase Function pushEngagement()
                    ↓
          Upsert engagementAgg/{sessionId}_{studentId}
```

### Classification Formula
```javascript
const noseTipX = landmarks[0][1].x   // 0.0 = far left, 1.0 = far right
const offset = Math.abs(noseTipX - 0.5)
// offset = 0 when face is perfectly centered
// offset = 0.5 when face is at the edge
const status = offset > 0.15 ? 'distracted' : 'focused'
```

The threshold `0.15` means if the student's nose is more than 15% off-center, they are classified as distracted.

### Data Model

**Firestore: `engagementAgg/{sessionId}_{studentId}`**
```
{
  sessionId:       string
  studentId:       string
  offeringId:      string
  focusedCount:    number    // cumulative
  distractedCount: number
  awayCount:       number
  samplesCount:    number
  lastUpdated:     Timestamp
}
```

### Privacy Notes
- **No video is ever sent to any server** — analysis runs entirely in the browser (client-side WASM)
- Only integer counts are sent (focused: 45, distracted: 12, away: 3)
- Camera stream is released on component unmount

### Key Files
| File | Role |
|------|------|
| `src/components/engagement/EngagementTracker.jsx` | All MediaPipe logic |
| `src/firebase/attendanceFunctions.js` → `pushEngagement` | Firebase Function call |

---

## Feature 5: Materials Management

### What It Does
Professors upload course material files (PDFs, slides, etc.) to Firebase Storage. Students see the materials and can download or open them. Materials are linked to specific courses.

### Who Uses It
- **Professor:** Upload, view, delete course materials
- **Assistant:** Upload materials for assigned courses
- **Student:** Download/view materials for enrolled courses

### Data Flow
```
Professor selects file
        ↓
uploadMaterialPdf(file, { professorId, courseDocId })
        ↓
Firebase Storage: /materials/{professorId}/{courseDocId}/{filename}
        ↓
Get download URL
        ↓
createMaterialDoc({ downloadUrl, name, type, courseDocId, professorId })
        ↓
Firestore: prof_courses/{profId}/courses/{courseDocId}/materials/{docId}
```

### Data Model

**Firestore: `prof_courses/{profId}/courses/{courseDocId}/materials/{materialId}`**
```
{
  name:        string    // "Chapter 3 Slides"
  fileName:    string    // "chapter3.pdf"
  fileType:    string    // "application/pdf"
  fileSize:    number    // bytes
  downloadUrl: string    // Firebase Storage URL
  uploadedBy:  string    // professor UID
  createdAt:   Timestamp
}
```

### Key Files
| File | Role |
|------|------|
| `src/firebase/materialsApi.js` | All CRUD operations for materials |
| `src/pages/professor/ProfessorCourseMaterialsPage.jsx` | Materials management UI |
| `src/pages/student/StudentCourseMaterialsPage.jsx` | Materials viewing UI |

---

## Feature 6: Buildings and Room Scheduling

### What It Does
Admins manage university buildings and their rooms. Rooms can be booked for specific time slots, and the system prevents double-booking using Firestore transactions.

### Who Uses It
- **Admin/Super Admin:** Create buildings, add rooms, manage schedules
- **Professor/Admin:** Book rooms for classes

### Transaction-Based Booking (Prevents Race Conditions)
```javascript
// roomsApi.js
export const bookRoomSlot = async (roomId, slot) => {
  const slotRef = doc(db, `rooms/${roomId}/schedule/${slot.id}`)

  await runTransaction(db, async (transaction) => {
    const slotDoc = await transaction.get(slotRef)

    if (slotDoc.exists() && slotDoc.data().status === 'reserved') {
      throw new Error('This slot is already booked')
      // transaction aborts — no write happens
    }

    transaction.set(slotRef, {
      ...slot,
      status: 'reserved',
      bookedAt: serverTimestamp()
    })
  })
}
```

If two admins try to book the same slot simultaneously, only one succeeds. The other gets the "already booked" error.

### Data Model

**Firestore: `buildings/{buildingId}`**
```
{ name: string, location: string, floorsCount: number }
```

**Firestore: `buildings/{buildingId}/rooms/{roomId}`**
```
{ name: string, capacity: number, type: "lecture" | "lab" | "office" }
```

**Firestore: `rooms/{roomId}/schedule/{slotId}`**
```
{ dayKey: string, slotKey: string, status: "available" | "reserved", courseName: string }
```

### Key Files
| File | Role |
|------|------|
| `src/firebase/buildingsApi.js` | Building CRUD |
| `src/firebase/roomsApi.js` | Room CRUD + `bookRoomSlot` |
| `src/firebase/scheduleApi.js` | Schedule read/write |
| `src/utils/campusScheduleUtils.js` | Matrix building helpers |
| `src/pages/admin/RoomSchedulePage.jsx` | Schedule grid UI |

---

## Feature 7: User Management & Bulk Import

### What It Does
Super admins can create individual users (students, professors, TAs) or import hundreds at once from an Excel file. Creating a user involves: Firebase Auth account + custom role claim + Firestore profile.

### Who Uses It
- **Super Admin / Admin:** Individual user creation, bulk import

### Single User Creation Flow
```
Admin fills form (name, email, password, role)
        ↓
CreateUserModal.jsx: httpsCallable(functions, 'createNewUser')
        ↓
Firebase Function createNewUser:
  1. admin.auth().createUser({ email, password, displayName })
  2. admin.auth().setCustomUserClaims(uid, { role })
  3. admin.firestore().collection('users').doc(uid).set({ profile data })
        ↓
Returns { success: true, uid }
```

**Why a Cloud Function?** `admin.auth().createUser()` requires Firebase Admin SDK (server-side only). The web SDK cannot create other users — only the Admin SDK can.

### Secondary Firebase App (CreateAdminUser Pattern)
When creating a new user, using `createUserWithEmailAndPassword` on the default app would sign out the current admin. The solution:

```javascript
// Initialize a secondary Firebase app
const secondaryApp = initializeApp(firebaseConfig, 'secondary')
const secondaryAuth = getAuth(secondaryApp)

// Create user on secondary app (doesn't affect current session)
await createUserWithEmailAndPassword(secondaryAuth, email, password)

// Sign out of secondary immediately
await signOut(secondaryAuth)
```

This is used in some admin pages as an alternative to the Cloud Function approach.

### Bulk Import Flow
```
Admin uploads .xlsx file
        ↓
FileReader + xlsx.read() → parse rows
        ↓
Validate: check required fields, email format, duplicate emails
        ↓
Admin reviews preview table with validation errors highlighted
        ↓
Admin clicks "Import"
        ↓
httpsCallable(functions, 'bulkCreateUsers')({ users: validUsers })
        ↓
Function loops: createUser + setClaims + createProfile for each row
        ↓
Returns { created: N, failed: M, errors: [...] }
```

### Data Model

**Firestore: `users/{uid}`**
```
{
  fullName:     string
  email:        string
  role:         "student" | "professor" | "assistant" | "admin" | "super_admin"
  phone:        string
  collegeId:    string    // for students
  yearId:       string    // for students
  departmentId: string    // for students
  createdAt:    Timestamp
}
```

### Key Files
| File | Role |
|------|------|
| `src/pages/admin/BulkImportUsersPage.jsx` | Excel upload and preview |
| `src/services/users.service.js` | Multi-collection user creation |
| `src/pages/admin/CreateUserModal.jsx` | Single user creation modal |

---

## Feature 8: Academic Structure Management

### What It Does
Admins build the university's academic hierarchy: Colleges → Years → Departments → Courses. This hierarchy is used throughout the system for filtering, access control, and organization.

### Data Structure (Nested Hierarchy)

```
colleges/{collegeId}
  years/{yearId}
    departments/{departmentId}
      courses/{courseId}
```

**Also mirrored in flat collection:**
```
allCourses/{courseId}   ← same data, denormalized for easy querying
```

The flat `allCourses` collection avoids collection-group queries when you need to find courses across all colleges.

### Optimistic Update Pattern (useColleges)

```javascript
// src/hooks/useColleges.js
const addCollege = async (collegeName) => {
  // 1. Optimistic: update UI immediately
  const tempId = `temp-${Date.now()}`
  setColleges(prev => [...prev, { id: tempId, name: collegeName }])

  try {
    // 2. Write to Firestore
    const docRef = await addDoc(collection(db, 'colleges'), {
      name: collegeName,
      createdAt: serverTimestamp()
    })

    // 3. Replace temp entry with real Firestore document
    setColleges(prev =>
      prev.map(c => c.id === tempId ? { ...c, id: docRef.id } : c)
    )
  } catch (error) {
    // 4. Rollback: remove the optimistic entry
    setColleges(prev => prev.filter(c => c.id !== tempId))
    throw error
  }
}
```

This makes the UI feel instant — no loading spinner for simple add operations.

### Key Files
| File | Role |
|------|------|
| `src/hooks/useColleges.js` | College CRUD with optimistic updates |
| `src/firebase/firestorePaths.js` | All collection path constants |
| `src/firebase/firestoreRefs.js` | Collection reference builder functions |
| `src/pages/admin/CollegesPage.jsx` | College management UI |
| `src/pages/admin/DepartmentsPage.jsx` | Department management UI |

---

## Feature 9: Professor Course Management

### What It Does
Professors see their assigned courses (set by admin in Firestore). For each course, they can manage: materials, quizzes, assignments, attendance, and student rosters.

### Assignment Flow
```
Admin assigns course to professor:
  → Writes professorUid to courses/{courseId}.professorIds
  → Creates prof_courses/{profId}/courses/{courseDocId}

Professor opens their courses page:
  → listenProfessorAssignments(uid, onChange)
  → onSnapshot on prof_courses/{uid}/courses
  → Returns list of course objects
```

### Key Quirk: Index Fallback
When listening for professor assignments, the code tries a sorted query first. If the Firestore composite index is missing, it retries without sorting (silent fallback):

```javascript
// professorApi.js
onSnapshot(
  orderedQuery,
  onChange,
  (error) => {
    if (error.code === 'failed-precondition') {
      // Index missing — fall back to unsorted
      onSnapshot(unsortedQuery, onChange, onError)
    } else {
      onError(error)
    }
  }
)
```

---

## Feature 10: Student Academic Overview

### What It Does
Students can see their enrolled courses, grades (via .NET API), academic roadmap (via .NET API), and materials for each course.

### .NET API Integration
Unlike all other features (Firebase-native), the academic roadmap and grade data comes from the .NET backend via Axios:

```javascript
// src/services/http.js
const api = axios.create({
  baseURL: process.env.REACT_APP_API_BASE,  // .NET backend URL
  headers: { 'Content-Type': 'application/json' }
})

// Auth interceptor — adds Firebase JWT to every request
api.interceptors.request.use(async (config) => {
  const token = await auth.currentUser?.getIdToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
```

**Known issue:** The file uses `process.env.VITE_API_BASE` (Vite syntax) — should be `REACT_APP_API_BASE` (CRA syntax). The Axios instance defaults to the hardcoded fallback URL.

### Key Files
| File | Role |
|------|------|
| `src/pages/student/StudentCoursesPage.jsx` | Enrolled courses list |
| `src/pages/student/StudentRoadmapPage.jsx` | Academic roadmap (calls .NET) |
| `src/pages/student/StudentGradesPage.jsx` | Grades (calls .NET) |
| `src/services/http.js` | Axios instance with auth interceptor |
