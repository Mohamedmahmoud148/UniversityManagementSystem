# 10 — Component Library Documentation
## Every Reusable Component — Props, State, Behavior, and Dependencies

---

## 1. Component Organization

Components are organized by role and function:

```
src/components/
├── common/              ← Cross-role reusable components
│   └── ProtectedRoute.jsx
├── admin/               ← Admin-only UI components
│   ├── AssignmentFormModal.jsx
│   ├── ConfirmDeleteDialog.jsx
│   ├── CollegeFormModal.jsx
│   └── UserSearchSelect.jsx
├── professor/           ← Professor-specific components
│   ├── CourseMaterialsSection.jsx
│   ├── MaterialCard.jsx
│   ├── AddMaterialModal.jsx
│   └── course-ai/
│       ├── AIChat.jsx
│       └── ChatBubble.jsx
├── student/             ← Student navigation components
│   ├── StudentSidebar.jsx
│   └── StudentTopbar.jsx
├── assistant/
│   └── AssistantUploadModal.jsx
└── engagement/
    └── EngagementTracker.jsx
```

---

## 2. ProtectedRoute

**File:** `src/components/common/ProtectedRoute.jsx`  
**Purpose:** Modern authentication guard — wraps protected routes, verifies role before rendering children.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `children` | ReactNode | Yes | Content to render when authorized |
| `requiredRole` | string | No | Expected role (e.g., `"student"`). If omitted, any authenticated user passes |
| `redirectTo` | string | No | Default: `"/signin"`. Where to redirect if not authenticated |

### State

| State | Type | Description |
|-------|------|-------------|
| `role` | string\|null | Resolved role from token claims or Firestore |
| `roleLoading` | boolean | While resolving role |
| `error` | string\|null | Error message if role resolution fails |

### Behavior

1. Uses `useAuthUser()` to get current Firebase user
2. On user load: tries token claims first; falls back to Firestore `users/{uid}` doc
3. While loading → renders `<CircularProgress />`
4. No user → `<Navigate to={redirectTo} replace />`
5. Role error → renders error message
6. Role mismatch → renders `<UnauthorizedMessage />`
7. Role match → renders `children`

### Usage

```jsx
<Route path="/student" element={
  <ProtectedRoute requiredRole="student">
    <StudentLayout />
  </ProtectedRoute>
}>
```

### Dependencies
- `useAuthUser` hook (for Firebase Auth state)
- Firebase Firestore `getDoc` (fallback role check)
- React Router `useNavigate`, `Navigate`

---

## 3. EngagementTracker

**File:** `src/components/engagement/EngagementTracker.jsx`  
**Purpose:** In-browser engagement monitoring using webcam + MediaPipe face detection.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `sessionId` | string | Yes | Firestore session ID for storing engagement data |
| `offeringId` | string | Yes | Subject offering ID |

### State

| State | Type | Description |
|-------|------|-------------|
| `status` | string | Current engagement: `"idle"` / `"focused"` / `"distracted"` / `"away"` |
| `error` | string\|null | Camera or MediaPipe initialization error |
| `ready` | boolean | `true` when MediaPipe models are loaded and camera is active |

### Refs (mutable, non-rendering state)

| Ref | Purpose |
|-----|---------|
| `videoRef` | The `<video>` DOM element |
| `streamRef` | Active MediaStream from getUserMedia |
| `landmarkerRef` | FaceLandmarker model instance |
| `detectorRef` | FaceDetector model instance |
| `intervalRef` | setInterval handle for 1-second sampling |
| `countsRef` | Accumulated engagement counts (not state — avoids re-renders) |

### Lifecycle

```
Mount
  │
  ▼
requestCamera() → getUserMedia({ video: { facingMode: "user" } })
  │
  ▼
loadMediaPipeModels() → downloads FaceLandmarker + FaceDetector from CDN
  │
  ▼
ready = true
  │
  ▼
setInterval(sample, 1000)
  │  (every second)
  ▼
detectForVideo() → analyze current frame
  │
  ├── Calculate nose X offset from center
  │
  ├── Update status: "focused" / "distracted" / "away"
  │
  └── Every 10 samples → pushEngagement() → Cloud Function

Unmount
  │
  ▼
flushCounts() → send remaining accumulated data
stopCameraStream()
clearInterval()
```

### Classification Logic

```javascript
const noseX = landmarks[0][1].x  // landmark index 1 = nose tip, x = 0.0-1.0

if (!facesDetected) {
  status = "away"
} else if (Math.abs(noseX - 0.5) > 0.15) {
  status = "distracted"  // face angled > 15% from center
} else {
  status = "focused"
}
```

### Renders

{% raw %}
```jsx
<div className="engagement-tracker">
  <video ref={videoRef} autoPlay muted playsInline
         style={{ width: 120, height: 90, borderRadius: 8 }} />
  <StatusBadge status={status} />
  {error && <ErrorMessage text={error} />}
</div>
```
{% endraw %}

### Privacy Architecture

Only these integers are ever sent outside the browser:
```javascript
{ focusedCount: 8, distractedCount: 1, awayCount: 1, samplesCount: 10 }
```

No frames, no images, no biometric data.

### Dependencies

- `@mediapipe/tasks-vision` (FaceLandmarker, FaceDetector)
- `attendanceFunctions.js` (pushEngagement)
- `useAuth()` context (gets studentId)

---

## 4. AIChat

**File:** `src/components/professor/course-ai/AIChat.jsx`  
**Purpose:** AI conversation interface for professors, grounded in their course materials.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `professorId` | string | Yes | Professor's UID |
| `courseDocId` | string | Yes | Firestore document ID of the course |
| `courseName` | string | Yes | Display name for the chat header |

### State

| State | Description |
|-------|-------------|
| `conversationId` | Deterministic ID: `{professorId}_{courseDocId}` |
| `messages` | Array of `{ id, role, content, status, createdAt }` |
| `conversationLoading` | Loading the conversation on mount |
| `conversationError` | Error initializing conversation |
| `inputText` | Current input field value |
| `sendLoading` | While waiting for AI response |
| `sendError` | Error sending message |
| `materials` | Available lecture materials for context selection |
| `materialsLoading` | Loading material list |
| `selectedLectureId` | Which lecture to use as AI context |

### Behavior

```
1. Mount
   ├── ensureCourseAiConversation() → creates conversation doc if needed
   ├── listenCourseAiMessages(conversationId) → real-time subscription
   └── fetchMaterialsForCourse() → populate lecture selector

2. Professor sends message
   ├── createCourseAiMessagePair() → writes 2 Firestore docs
   │     ├── Professor message: { role: "professor", content }
   │     └── AI placeholder: { role: "ai", status: "processing" }
   └── callCourseAiAssistant() → triggers Cloud Function
         └── Function updates AI placeholder with final response

3. onSnapshot fires when AI placeholder is updated
   └── UI re-renders with AI response visible

4. Unmount → unsubscribe from messages listener
```

### Auto-scroll

```javascript
const messagesEndRef = useRef(null)

useEffect(() => {
  messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
}, [messages])
```

### Dependencies

- `courseAiApi.js` (all operations)
- `materialsApi.js` (fetchMaterialsForCourse)
- MUI: TextField, Button, Select, CircularProgress

---

## 5. CourseMaterialsSection

**File:** `src/components/professor/CourseMaterialsSection.jsx`  
**Purpose:** Displays and manages lecture materials for a course. Professor can view, upload, and delete PDFs.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `professorId` | string | Yes | Professor's UID |
| `courseDocId` | string | Yes | Course Firestore doc ID |
| `courseId` | string | Yes | Course code (e.g., "CS101") |
| `courseName` | string | Yes | Course display name |

### State

| State | Description |
|-------|-------------|
| `materials` | Fetched material list |
| `loading` | While fetching |
| `error` | Fetch error |
| `addModalOpen` | Controls AddMaterialModal visibility |

### Behavior

- On mount: calls `fetchMaterialsForCourse(professorId, courseDocId)` — one-time fetch
- Renders a list of `MaterialCard` components
- "Add Material" button opens `AddMaterialModal`
- After upload completes: refreshes the material list

### Dependencies

- `materialsApi.js` (fetchMaterialsForCourse)
- `MaterialCard` component
- `AddMaterialModal` component

---

## 6. AddMaterialModal

**File:** `src/components/professor/AddMaterialModal.jsx`  
**Purpose:** Form for uploading a new lecture PDF to a course.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `open` | boolean | Yes | Controls modal visibility |
| `onClose` | function | Yes | Called when modal should close |
| `onCreated` | function | Yes | Called after successful upload (triggers list refresh) |
| `professorId` | string | Yes | Professor's UID |
| `courseDocId` | string | Yes | Course Firestore doc ID |
| `courseId` | string | Yes | Course code |
| `courseName` | string | Yes | Course name |

### Form Fields

| Field | Validation |
|-------|-----------|
| Lecture title | Required |
| Lecture number | Positive integer |
| Notes | Optional |
| PDF file | Required, PDF type only |

### State

```javascript
const [lectureTitle, setLectureTitle] = useState('')
const [lectureNumber, setLectureNumber] = useState('')
const [notes, setNotes]           = useState('')
const [file, setFile]             = useState(null)
const [loading, setLoading]       = useState(false)
const [error, setError]           = useState(null)
```

### Submit Flow

```
1. Validate fields client-side
2. Generate materialId = nanoid() or crypto.randomUUID()
3. uploadMaterialPdf(file, { professorId, courseId, courseDocId, materialId })
   → Firebase Storage upload → returns downloadUrl
4. createMaterialDoc({ ... all fields ... , downloadUrl })
   → Firestore write
5. onCreated() callback
6. onClose()
```

---

## 7. AssistantUploadModal

**File:** `src/components/assistant/AssistantUploadModal.jsx`  
**Purpose:** Form for TAs to upload supplementary materials to a course section.

### Props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `open` | boolean | Yes | Modal visibility |
| `onClose` | function | Yes | Close callback |
| `onCreated` | function | Yes | Success callback (refreshes list) |
| `assistantId` | string | Yes | TA's UID |
| `assistantName` | string | Yes | TA's display name |
| `assignment` | object | Yes | The course assignment object |

### Validation

```javascript
if (!lectureTitle.trim()) throw "Lecture title is required"
if (lectureNumber <= 0) throw "Lecture number must be positive"
if (!file) throw "PDF file is required"
if (file.type !== 'application/pdf' && !file.name.endsWith('.pdf'))
  throw "Only PDF files are accepted"
```

### Upload Target

```
Storage: assignmentMaterials/{assignment.id}/{materialId}.pdf
Firestore: courseAssignments/{assignment.id}/materials/{materialId}
```

---

## 8. StudentSidebar

**File:** `src/components/student/StudentSidebar.jsx`  
**Purpose:** Navigation sidebar for the student layout.

### Props

| Prop | Type | Description |
|------|------|-------------|
| `open` | boolean | Sidebar open/closed (mobile drawer) |
| `onClose` | function | Close callback |
| `onNavigate` | function | Called when a nav item is clicked |
| `profile` | object | Student profile (name, college, year) |
| `user` | object | Firebase Auth user |

### Nav Items

| Label | Icon | Route |
|-------|------|-------|
| الرئيسية (Home) | HomeIcon | `/student` |
| مواد دراسي (My Courses) | BookIcon | `/student/courses` |
| الاختبارات (Quizzes) | QuizIcon | `/student/quizzes` |

### Logout

```javascript
const handleLogout = async () => {
  await signOut(auth)
  navigate('/signin')
}
```

### Renders

- Profile picture (Avatar with initials fallback)
- Student name from `profile.fullName`
- College and year from `profile`
- Navigation links
- Logout button at bottom

---

## 9. Component Interaction Diagram

```
ProfessorCourseDetailsPage
    │
    ├── CourseMaterialsSection
    │       ├── MaterialCard (×N)
    │       └── AddMaterialModal
    │               └── Firebase Storage upload
    │
    └── AIChat
            ├── ChatBubble (×N messages)
            ├── Lecture selector (MUI Select)
            └── Message input + send button

StudentLayout
    ├── StudentSidebar
    │       └── Nav links
    └── StudentTopbar
            └── Profile + logout

[Pages using real-time data]
    └── uses onSnapshot → automatic re-render when Firestore data changes
```

---

## 10. MUI Components Used Across All Components

| MUI Component | Usage |
|--------------|-------|
| `Modal` + `Box` | All modals (upload, create, edit) |
| `TextField` | All text inputs |
| `Button` | All action buttons |
| `CircularProgress` | Loading spinners inside buttons and pages |
| `Alert` | Error/success messages |
| `Avatar` | User profile pictures with initials |
| `Drawer` | Mobile-responsive sidebar |
| `AppBar` + `Toolbar` | Topbar navigation |
| `IconButton` | Delete, edit, close buttons |
| `Tooltip` | Icon button labels |
| `Select` + `MenuItem` | Dropdowns |
| `Chip` | Role badges, status indicators |
| `Divider` | Section separators |
| `Typography` | Text with semantic variants (h1-h6, body1, body2, caption) |
| `Grid` | Responsive layout |
| `Card` + `CardContent` | Material cards, course cards |
| `Table` + `TableRow` + `TableCell` | Data tables |
| `LinearProgress` | File upload progress bar |
