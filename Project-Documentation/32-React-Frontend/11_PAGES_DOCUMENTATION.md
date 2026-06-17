---
render_with_liquid: false
---

# 11 — Pages Documentation
## Every Page Explained — Purpose, User Journey, APIs, and State

---

## STUDENT PAGES

---

### StudentHome

**Route:** `/student` (index)  
**File:** `src/pages/student/StudentHome.jsx`

**Purpose:** Welcome dashboard for students. Shows a greeting and quick navigation links.

**Data Sources:**
- Layout outlet context: `{ user, profile, profileLoading }` — no independent Firestore call

**Renders:**
- Welcome message with student's `profile.fullName`
- College and year information from `profile`
- Quick link cards: "My Courses", "Quizzes"

**User Journey:** Student logs in → lands here → clicks "Courses" or "Quizzes"

---

### StudentCoursesPage

**Route:** `/student/courses`  
**File:** `src/pages/student/StudentCoursesPage.jsx`

**Purpose:** Shows all courses available to the student based on their college/year/department.

**Data Sources:**
- `allCourses` — Firestore query: `where('collegeId', '==', profile.collegeId) AND where('yearId', '==', profile.yearId) AND where('departmentId', '==', profile.departmentId)` (real-time)
- CollectionGroup `schedule` — for each course, queries `where('courseId', 'in', courseIds)` in chunks of 30
- `colleges/{id}/buildings/{id}` — resolves building names for schedule display

**State:**
```javascript
rawCourses          // list of course documents
schedulesByCourse   // Map<courseId, ScheduleEntry[]>
buildingNames       // Map<buildingId, name>
loading, error
collegeName, yearLabel, departmentName  // display names
```

**Features:**
- Displays course cards with room, schedule (days + times), instructor
- "Export to PDF" button — generates A4 PDF with all courses using jsPDF
- Real-time: if a course is added/removed from admin, student's view updates automatically

**Performance Note:** Schedule query uses chunking to avoid the Firestore `in` limit of 30.

---

### StudentQuizzesPage

**Route:** `/student/quizzes`  
**File:** `src/pages/student/StudentQuizzesPage.jsx`

**Purpose:** Lists all published quizzes the student can take, filtered by their college/year/department.

**Data Sources:**
- `quizzes` — real-time: `where('collegeId', '==', ...) AND where('yearId', '==', ...) AND where('isPublished', '==', true)`
- `quizSubmissions` — one-time: `where('studentUid', '==', user.uid)` to know which quizzes were already submitted

**State:**
```javascript
quizzes         // published quizzes for student's class
submissions     // student's existing submissions (Map<quizId, submission>)
now             // current time (refreshed every 30 seconds for status updates)
```

**Quiz Status Logic:**
```javascript
const getStatus = (quiz) => {
  const sub = submissions.find(s => s.quizId === quiz.id)
  if (sub) return "submitted"
  if (quiz.startTime.toDate() > now) return "not_started"
  return "available"
}
```

**Why refresh `now` every 30 seconds?** A quiz might become "available" while the page is open (its start time arrives). Refreshing the time state triggers a re-render with updated statuses.

---

### StudentQuizTakePage

**Route:** `/student/quizzes/:quizId`  
**File:** `src/pages/student/StudentQuizTakePage.jsx`

**Purpose:** The quiz-taking interface. Most complex page in the student flow.

**Data Sources:**
- `quizzes/{quizId}` — one-time fetch on mount
- `quizSubmissions` — one-time check: `where('quizId', '==', quizId) AND where('studentUid', '==', uid)` to prevent re-taking

**State:**
```javascript
quiz                // quiz document with questions
loading
alreadySubmitted    // bool — redirect to result if true
answers             // { [questionIndex]: selectedAnswer }
currentQuestion     // index 0 to quiz.questions.length-1
timeRemaining       // seconds
isExpired           // when timer hits 0
submitting, submitted
```

**Timer Logic:**
```javascript
// Calculate end time from quiz start time + duration
const endTime = quiz.startTime.toMillis() + quiz.durationMinutes * 60 * 1000

const interval = setInterval(() => {
  const remaining = Math.floor((endTime - Date.now()) / 1000)
  if (remaining <= 0) {
    setIsExpired(true)
    clearInterval(interval)
    handleAutoSubmit()
  } else {
    setTimeRemaining(remaining)
  }
}, 1000)
```

**Submit Logic:**
```javascript
const submitGuardRef = useRef(false)  // prevents double-submit

const handleSubmit = async () => {
  if (submitGuardRef.current) return
  submitGuardRef.current = true

  const result = calculateResult(quiz.questions, answers)
  await addDoc(collection(db, 'quizSubmissions'), {
    quizId,
    studentUid: user.uid,
    studentName: profile.fullName,
    answers,
    score: result.score,
    totalPoints: result.totalPoints,
    percentage: result.percentage,
    wrongQuestions: result.wrongQuestions,
    submittedAt: serverTimestamp()
  })
  navigate(`/student/quizzes/${quizId}/result`)
}
```

**User Journey:**
1. Page loads → checks if already submitted (redirects to result if yes)
2. Shows quiz info (title, duration, question count)
3. Student clicks "Start" → timer begins, questions appear
4. Student navigates questions (Previous/Next buttons)
5. Timer shows countdown (turns red when < 2 minutes)
6. Student clicks "Submit" OR timer expires → auto-submit
7. Navigate to result page

---

### StudentQuizResultPage

**Route:** `/student/quizzes/:quizId/result`  
**File:** `src/pages/student/StudentQuizResultPage.jsx`

**Purpose:** Displays the student's quiz result with score and per-question breakdown.

**Data Sources:**
- `quizzes/{quizId}` — one-time fetch (to get correct answers)
- `quizSubmissions` — one-time: student's submission for this quiz

**Renders:**
- Score card: `{score}/{totalPoints}` — `{percentage}%`
- Pass/Fail indicator (threshold: 50%)
- Per-question breakdown: student answer vs. correct answer, ✓ or ✗ indicator
- "Back to Quizzes" navigation

---

## PROFESSOR PAGES

---

### ProfessorHome

**Route:** `/prof` (index)  
**File:** `src/pages/professor/ProfessorHome.jsx`

**Purpose:** Welcome page with quick stats.

**Data:** Uses outlet context (profile). No independent Firestore calls.

---

### ProfessorDashboard

**Route:** `/prof/dashboard`  
**File:** `src/pages/professor/ProfessorDashboard.jsx`

**Purpose:** Analytics dashboard showing the professor's teaching assignments and course statistics.

**Data Sources:**
- Two real-time `onSnapshot` subscriptions:
  1. `courseAssignments where professorIds array-contains uid`
  2. `courseAssignments where assistantUids array-contains uid`

Results are merged and deduplicated.

**Renders:**
- ApexCharts bar chart: assignment count by term
- Assignment cards: course name, term, section, assistant count
- TA assignments tagged with "TA" badge

---

### ProfessorCoursesPage

**Route:** `/prof/courses`  
**File:** `src/pages/professor/ProfessorCoursesPage.jsx`

**Purpose:** Shows all courses assigned to the professor, with materials management inline.

**Data Sources:**
- `courseAssignments where professorIds array-contains uid` (real-time `onSnapshot`)

**State per course card:**
- `materialsOpen` — boolean to toggle materials section for that card

**Renders:**
- Course cards with: name, code, term, year, section, assistant count, building, room
- Each card expandable to show `CourseMaterialsSection`
- "Add Material" button inside materials section

---

### ProfessorCourseDetailsPage

**Route:** `/prof/courses/:courseDocId`  
**File:** `src/pages/professor/ProfessorCourseDetailsPage.jsx`

**Purpose:** Detailed view of a single course with materials and AI chat.

**Params:** `courseDocId` — Firestore document ID

**Data Sources:**
- `prof_courses/{uid}/courses/{courseDocId}` — one-time fetch via `getProfessorCourseById()`

**Components:**
- `CourseMaterialsSection` — full materials management
- `AIChat` — AI conversation for this course

**Renders:**
- Course info header (name, ID, college, assistants, term)
- Two tabs: "Materials" and "AI Assistant"

---

### ProfessorQuizzesPage

**Route:** `/prof/quizzes`  
**File:** `src/pages/professor/ProfessorQuizzesPage.jsx`

**Purpose:** Full quiz management — create, edit, delete, and view results.

**Data Sources:**
- `quizzes where createdBy == uid` (real-time)
- `colleges`, `colleges/{id}/years`, `colleges/{id}/years/{id}/departments` (for quiz creation form)

**State:**
```javascript
quizzes             // professor's quizzes
createModalOpen
editTarget          // null or quiz being edited
deleteTarget
// For quiz creation form:
title, description
selectedCollegeId, selectedYearId, selectedDepartmentId
startTime, durationMinutes
questionType        // "mcq" | "trueFalse"
questions           // array of question objects
isPublished
// AI generation:
generating
pdfFile
generateError
```

**Quiz Creation Flow:**
```
1. Prof clicks "Create Quiz"
2. Fills form: title, target class (college/year/dept), start time, duration
3. Adds questions:
   Option A: Manually add MCQ or T/F questions
   Option B: Upload PDF → POST to FastAPI → auto-fills questions
4. Reviews questions (can edit/delete each)
5. Toggles "Publish" if ready for students
6. Submit → setDoc(doc(db,'quizzes',id), quizData)
```

**AI Quiz Generation Flow:**
```
Prof uploads PDF file
    │
    ▼
fetch(REACT_APP_GENERATE_QUIZ_URL, { method:'POST', body: formData })
    │
    ▼
FastAPI returns { questions: [...] }
    │
    ▼
questions populated in form state
    │
    ▼
Prof reviews and edits generated questions
    │
    ▼
Prof publishes quiz
```

---

### ProfessorQuizResultsPage

**Route:** `/prof/quizzes/:quizId/results`  
**File:** `src/pages/professor/ProfessorQuizResultsPage.jsx`

**Purpose:** Professor views all student submissions for a specific quiz.

**Params:** `quizId`

**Authorization:** Checks `quiz.createdBy === user.uid` — professors can only view results for their own quizzes.

**Data Sources:**
- `quizzes/{quizId}` — one-time fetch
- `quizSubmissions where quizId == quizId` — one-time fetch

**Computed Statistics:**
```javascript
const stats = {
  total: submissions.length,
  average: mean(submissions.map(s => s.percentage)),
  highest: max(submissions.map(s => s.percentage)),
  lowest: min(submissions.map(s => s.percentage)),
  passCount: submissions.filter(s => s.percentage >= 50).length
}
```

**Renders:**
- Stats cards (total, average, highest, lowest)
- Submissions table: student name, score, percentage, submitted time
- Per-question analysis: how many got each question right

---

## ADMIN PAGES

---

### CollegesPage

**Route:** `/admin/colleges`  
**File:** `src/features/colleges/pages/CollegesPage.jsx`

**Purpose:** Manage all colleges in the system.

**Hook:** `useColleges()` — provides `{ colleges, loading, error, addCollege, updateCollege, deleteCollege }`

**Features:**
- Create college (name + code) via modal
- Edit college via modal
- Delete college (with confirm dialog)
- Optimistic updates — UI responds instantly

---

### YearsPage

**Route:** `/admin/colleges/:collegeId/years`  
**File:** `src/features/years/pages/YearsPage.jsx`

**Purpose:** Manage academic years within a college.

**Params:** `collegeId`

**Data:** `colleges/{collegeId}/years` (Firestore subcollection)

---

### DepartmentsPage

**Route:** `/admin/colleges/:collegeId/years/:yearId/departments`  
**File:** `src/features/departments/pages/DepartmentsPage.jsx`

**Purpose:** Manage departments within a year.

**Params:** `collegeId`, `yearId`

---

### DepartmentCoursesPage

**Route:** `/admin/colleges/:collegeId/years/:yearId/departments/:deptId/courses`  
**File:** `src/features/courses/pages/DepartmentCoursesPage.jsx`

**Purpose:** Manage courses within a department.

**Params:** `collegeId`, `yearId`, `deptId`

**Data:** Writes to both `colleges/{c}/years/{y}/departments/{d}/courses` AND `allCourses` (mirrored)

---

### AssignmentsPage

**Route:** `/admin/assignments`  
**File:** `src/pages/admin/AssignmentsPage.jsx`

**Purpose:** View and manage all course-to-professor assignments.

**Data Sources:**
- `courseAssignments` (real-time)
- `users where role in ["professor", "assistant"]` (one-time, for form options)

**Features:**
- Create, edit, delete assignments
- Filter by term
- Search by course name

---

### CreateCourseAssignment

**Route:** `/admin/assignments/new`  
**File:** `src/pages/admin/CreateCourseAssignment.jsx`

**Purpose:** Assign a professor (and optional TAs) to a course for a specific term.

**Form Fields:**
- Course selector (from `allCourses`)
- Term ID and label
- Professor multi-select (from `users where role == "professor"`)
- TA multi-select (from `users where role == "assistant"`)

**Validation:**
- Duplicate check: queries `courseAssignments where courseId == x AND termId == y`
- If assignment exists → show error "Already assigned for this term"

---

### BulkImportUsersPage

**Route:** `/admin/bulk-import-users`  
**File:** `src/pages/admin/BulkImportUsersPage.jsx`

**Purpose:** Create many users at once from an Excel file.

**Flow:**
1. Admin downloads Excel template (5 columns: username, email, phone, password, role)
2. Fills in user data
3. Uploads file → parsed by `xlsx` library client-side
4. Validates each row (email format, password length, valid role)
5. Shows preview table with validation status per row
6. Admin confirms → calls `bulkCreateUsers` Firebase Function
7. Shows success/failure count per user

**Supported Roles:** student, professor, assistant, admin

**Header Aliases:** The parser accepts multiple spellings:
- "full name" or "user name" → `username`
- "phone number" → `phone`
- "e mail" → `email`

---

### BuildingsList

**Route:** `/admin/campus-buildings`  
**File:** `src/pages/admin/BuildingsList.jsx`

**Purpose:** Manage all campus buildings.

**Data:** `colleges/{collegeId}/buildings` (real-time)

**Features:** Create/edit/delete buildings. Navigate to building details.

---

### BuildingDetails

**Route:** `/admin/campus-buildings/:buildingId`  
**File:** `src/pages/admin/BuildingDetails.jsx`

**Purpose:** Manage floors and rooms within a building.

**Data:** `colleges/{collegeId}/buildings/{buildingId}/rooms` (real-time)

---

### RoomSchedulePage

**Route:** `/admin/campus-buildings/:buildingId/floors/:floorId/rooms/:roomId`  
**File:** `src/pages/admin/RoomSchedulePage.jsx`

**Purpose:** Manage the weekly schedule for a specific room.

**Data:** `colleges/{c}/buildings/{b}/rooms/{r}/schedule` (real-time subscription)

**Schedule Grid:**

Days (columns): Saturday, Sunday, Monday, Tuesday, Wednesday, Thursday  
Slots (rows): 09:00-11:00, 11:00-13:00, 13:00-15:00, 15:00-17:00

**Booking Flow:**
```
Admin clicks empty slot
    │
    ▼
Select course from dropdown
    │
    ▼
createSchedule() → runTransaction() checks slot not taken
    │
    ├── Slot taken → show error
    └── Slot free → write schedule doc
                    onSnapshot updates → grid cell changes color
```

---

### CreateAdminUser

**Route:** `/admin/create-admin` and `/super_admin/create-admin`  
**File:** `src/pages/SuperAdmin/CreateAdminUser.jsx`

**Purpose:** Create, edit, and delete user accounts. The most complex admin page.

**Data Sources:**
- `users` — all users for directory view (paginated 4/page)
- `colleges`, years, departments — for student creation (cascade selects)

**Key Features:**
- Create users for all roles (student, professor, assistant, admin)
- Secondary Firebase app for user creation (prevents signing out current admin)
- Edit user profile (calls `editUserAccount` Firebase Function)
- Delete user (calls `deleteUserAccount` Firebase Function → deletes Auth + Firestore)
- Role filtering in user directory
- Search by name/email

**Multi-Collection Write on User Creation:**
1. `createUserWithEmailAndPassword(secondaryAuth, email, password)` — creates Firebase Auth user
2. `createUserProfile(db, uid, profileData)` → writes to:
   - `users/{uid}`
   - `users/roles/{roleCollection}/{uid}`
   - `{roleCollection}/{uid}` (e.g., `students/{uid}`)

---

## ASSISTANT PAGES

---

### AssistantHome

**Route:** `/asst` (index)  
**File:** `src/pages/assistant/AssistantHome.jsx`

**Purpose:** TA's home page with assignment overview.

**Data:** `courseAssignments where assistantUids array-contains uid` (real-time)

**Features:**
- Term filter dropdown
- Stats: total assignments, filtered count
- Recent 5 assignments list

---

### AssistantCoursesPage

**Route:** `/asst/courses`  
**File:** `src/pages/assistant/AssistantCoursesPage.jsx`

**Purpose:** TA's course list with materials upload capability.

**Note:** Uses `assistantIds` (not `assistantUids`) in the Firestore query — different field name than AssistantHome. This is a naming inconsistency in the data schema.

**Per-course:** Real-time materials subscription + `AssistantUploadModal`

---

## SHARED PAGES

---

### SignIn

**Route:** `/signin`, `/`

**Fields:** Email, Password

**Flow:**
```
signInWithEmailAndPassword(auth, email, password)
    │
getIdTokenResult(true) → role
    │
navigate to role-specific home
```

**Error Handling:**
- "auth/wrong-password" → "Incorrect password"
- "auth/user-not-found" → "No account found with this email"
- "auth/too-many-requests" → "Account temporarily locked"

---

### Unauthorized

**Route:** `/unauthorized` (also `/not-allowed`)  
**Purpose:** Shown when a user tries to access a route they don't have permission for.

Renders a friendly message with a "Go Home" button that navigates to their role's home page.
