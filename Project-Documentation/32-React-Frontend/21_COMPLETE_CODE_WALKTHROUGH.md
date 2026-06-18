# 21 — Complete Code Walkthrough
## The Application From Startup to User Interaction

---

## How to Read This Document

This walkthrough follows the execution flow of the application from the moment a user opens the URL to when they interact with a feature. Each section explains what code runs, in what order, and why.

---

## Part 1: Application Bootstrap

### Step 1: Browser Loads `index.html`

Firebase Hosting serves `build/index.html`. The HTML contains:
```html
<div id="root"></div>
<script src="/static/js/main.abc123.js"></script>
```

The JavaScript bundle (`main.abc123.js`) is the entire compiled React application.

### Step 2: `src/index.js` Executes

```javascript
import React from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'  // loads Tailwind base styles

const container = document.getElementById('root')
const root = createRoot(container)
root.render(<App />)
```

React mounts to the `<div id="root">` element. The application starts rendering.

### Step 3: `src/App.js` Renders

```javascript
function App() {
  return <AppRoutes />  // single-purpose wrapper
}
```

Nothing happens here — just delegates to AppRoutes.

### Step 4: `src/routes/AppRoutes.jsx` Sets Up the Route Tree

```javascript
function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<SignIn />} />
        <Route path="/signin" element={<SignIn />} />
        <Route path="/student" element={
          <ProtectedRoute requiredRole="student">
            <StudentLayout />
          </ProtectedRoute>
        }>
          <Route index element={<StudentHome />} />
          <Route path="courses" element={<StudentCoursesPage />} />
          {/* ... */}
        </Route>
        {/* ... all other routes */}
      </Routes>
    </BrowserRouter>
  )
}
```

React Router reads the current URL and decides which route to render.

---

## Part 2: The Sign-In Flow

### Scenario: User visits `https://bsnu.web.app/signin`

**What runs:**

1. `AppRoutes` matches `/signin` → renders `<SignIn />`
2. `SignIn` renders the login form
3. User enters email + password + clicks "Sign In"
4. `handleLogin()` fires:

```javascript
// src/pages/shared/SignIn.jsx
const handleLogin = async () => {
  setLoading(true)
  setError(null)
  try {
    // Firebase Auth validates credentials
    const userCredential = await signInWithEmailAndPassword(auth, email, password)
    const user = userCredential.user
    
    // Force-refresh token to get latest custom claims
    const tokenResult = await user.getIdTokenResult(true)
    const role = tokenResult.claims.role
    
    // Navigate to the correct home based on role
    const destinations = {
      student:    '/student',
      professor:  '/prof',
      admin:      '/admin/home',
      super_admin: '/super_admin/home',
      assistant:  '/asst'
    }
    navigate(destinations[role] || '/', { replace: true })
    
  } catch (err) {
    // Firebase Auth errors
    setError(translateFirebaseError(err.code))
    setLoading(false)
  }
}
```

5. `onAuthStateChanged` (in `AuthContext`) fires with the new user
6. `AuthContext` updates: `user = FirebaseUser`, `role = "student"`, `loading = false`
7. React re-renders all components consuming `AuthContext`
8. Navigation to `/student` happens

---

## Part 3: Loading the Student Home

### What happens at `/student`

1. Router matches `/student` → checks `<ProtectedRoute requiredRole="student">`

2. `ProtectedRoute` runs:
```javascript
// 1. Get Firebase user
const { user, authLoading } = useAuthUser()
// 2. If loading, show spinner
// 3. Get token claims
const tokenResult = await user.getIdTokenResult()
const role = tokenResult.claims.role  // "student"
// 4. "student" === "student" → render children
```

3. `<StudentLayout />` renders:
{% raw %}
```javascript
function StudentLayout() {
  const { user } = useAuthUser()
  const [profile, setProfile] = useState(null)

  useEffect(() => {
    // Load student profile from Firestore
    getDoc(doc(db, 'users', user.uid)).then(d => setProfile(d.data()))
  }, [user])

  return (
    <div className="flex">
      <StudentSidebar profile={profile} />
      <main className="flex-1">
        <Outlet context={{ user, profile, profileLoading }} />
      </main>
    </div>
  )
}
```
{% endraw %}

4. `<StudentHome />` renders (index route):
```javascript
function StudentHome() {
  const { user, profile, profileLoading } = useOutletContext()

  if (profileLoading) return <CircularProgress />

  return (
    <div>
      <h1>Welcome, {profile?.fullName}</h1>
      <Link to="/student/courses">My Courses</Link>
      <Link to="/student/quizzes">Quizzes</Link>
    </div>
  )
}
```

**Result:** Student sees their name, college, and quick links.

---

## Part 4: Student Takes a Quiz

### Following the complete quiz flow

**Step 1: Student navigates to `/student/quizzes`**

`StudentQuizzesPage` mounts and starts two data loads:

```javascript
// Load published quizzes for this student's class
useEffect(() => {
  const q = query(
    collection(db, 'quizzes'),
    where('collegeId', '==', profile.collegeId),
    where('yearId', '==', profile.yearId),
    where('departmentId', '==', profile.departmentId),
    where('isPublished', '==', true)
  )
  const unsub = onSnapshot(q, (snap) => {
    setQuizzes(snap.docs.map(d => ({ id: d.id, ...d.data() })))
    setLoading(false)
  })
  return () => unsub()
}, [profile])

// Load student's existing submissions (to know which quizzes already done)
useEffect(() => {
  getDocs(query(
    collection(db, 'quizSubmissions'),
    where('studentUid', '==', user.uid)
  )).then(snap => {
    setSubmissions(snap.docs.map(d => d.data()))
  })
}, [user])
```

The `onSnapshot` makes this **live** — if the professor publishes a new quiz while the student has this page open, it appears automatically.

**Step 2: Student clicks on a quiz → `/student/quizzes/:quizId`**

`StudentQuizTakePage` mounts:

```javascript
const { quizId } = useParams()

useEffect(() => {
  // 1. Fetch quiz
  getDoc(doc(db, 'quizzes', quizId)).then(d => {
    setQuiz({ id: d.id, ...d.data() })
  })

  // 2. Check if already submitted
  getDocs(query(
    collection(db, 'quizSubmissions'),
    where('quizId', '==', quizId),
    where('studentUid', '==', user.uid)
  )).then(snap => {
    if (!snap.empty) setAlreadySubmitted(true)
  })
}, [quizId])
```

If already submitted → redirect to result page.

**Step 3: Timer starts**

```javascript
useEffect(() => {
  if (!quiz) return
  
  // endTime = quiz start + duration
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
  
  return () => clearInterval(interval)  // cleanup on unmount
}, [quiz])
```

**Step 4: Student answers questions**

```javascript
// When student selects an option:
const handleAnswer = (questionIndex, answer) => {
  setAnswers(prev => ({ ...prev, [questionIndex]: answer }))
}

// Navigation buttons:
<Button onClick={() => setCurrentQuestion(q => q - 1)}
        disabled={currentQuestion === 0}>
  Previous
</Button>
<Button onClick={() => setCurrentQuestion(q => q + 1)}
        disabled={currentQuestion === quiz.questions.length - 1}>
  Next
</Button>
```

**Step 5: Quiz submits**

Either timer expires (calls `handleAutoSubmit`) or student clicks "Submit" (calls `handleSubmit`):

```javascript
const submitGuardRef = useRef(false)

const handleSubmit = async () => {
  // Prevent double-submit
  if (submitGuardRef.current) return
  submitGuardRef.current = true
  setSubmitting(true)

  // Calculate score client-side
  const result = calculateResult(quiz.questions, answers)
  // result = { score: 15, totalPoints: 20, percentage: 75, wrongQuestions: [2, 4] }

  // Write to Firestore
  await addDoc(collection(db, 'quizSubmissions'), {
    quizId,
    studentUid: user.uid,
    studentName: profile.fullName,
    answers,
    ...result,
    submittedAt: serverTimestamp()
  })

  // Navigate to result
  navigate(`/student/quizzes/${quizId}/result`)
}
```

**Step 6: Result page shows**

```javascript
// StudentQuizResultPage.jsx
useEffect(() => {
  // Load quiz (for question text and correct answers)
  getDoc(doc(db, 'quizzes', quizId)).then(d => setQuiz(d.data()))

  // Load student's submission
  getDocs(query(
    collection(db, 'quizSubmissions'),
    where('quizId', '==', quizId),
    where('studentUid', '==', user.uid)
  )).then(snap => setSubmission(snap.docs[0]?.data()))
}, [quizId])

// Display
return (
  <div>
    <h2>{submission.percentage}%</h2>
    <p>{submission.score}/{submission.totalPoints}</p>
    <Chip
      label={submission.percentage >= 50 ? 'PASSED' : 'FAILED'}
      color={submission.percentage >= 50 ? 'success' : 'error'}
    />
    {quiz.questions.map((q, i) => (
      <QuestionResult
        key={i}
        question={q}
        studentAnswer={submission.answers[i]}
        isCorrect={!submission.wrongQuestions.includes(i)}
      />
    ))}
  </div>
)
```

---

## Part 5: Professor Creates a Quiz with AI

### Following the AI quiz generation flow

**Step 1: Professor at `/prof/quizzes`**

`ProfessorQuizzesPage` shows existing quizzes and a "Create Quiz" button.

**Step 2: Professor opens Create Quiz Modal**

```javascript
const [createOpen, setCreateOpen] = useState(false)
const [questions, setQuestions]   = useState([])
```

The modal has two tabs: "Manual" and "Generate from PDF".

**Step 3: Professor uploads PDF for AI generation**

```javascript
const handleGenerateFromPdf = async () => {
  setGenerating(true)
  setGenerateError(null)

  const formData = new FormData()
  formData.append('file', selectedPdfFile)

  try {
    const response = await fetch(process.env.REACT_APP_GENERATE_QUIZ_URL, {
      method: 'POST',
      body: formData
      // No Content-Type header — browser sets multipart boundary automatically
    })

    if (!response.ok) {
      throw new Error(`AI service returned ${response.status}`)
    }

    const data = await response.json()
    // data.questions = array of { question, options, correctIndex }
    
    // Normalize to our internal format
    const normalizedQuestions = data.questions.map(q => ({
      id: crypto.randomUUID(),
      text: q.question,
      type: 'mcq',
      options: q.options,
      correctAnswer: q.options[q.correctIndex],
      points: 5  // default
    }))
    
    setQuestions(normalizedQuestions)
    
  } catch (error) {
    setGenerateError(error.message)
  } finally {
    setGenerating(false)
  }
}
```

**Step 4: Professor publishes quiz**

```javascript
const handleCreateQuiz = async () => {
  // Validation
  if (!title.trim()) return setErrors({ title: 'Required' })
  if (questions.length === 0) return setErrors({ questions: 'Add at least one question' })

  // Build quiz document
  const quizData = {
    title,
    description,
    createdBy: user.uid,
    createdAt: serverTimestamp(),
    collegeId: selectedCollegeId,
    yearId: selectedYearId,
    departmentId: selectedDepartmentId,
    startTime: Timestamp.fromDate(new Date(startTime)),
    durationMinutes: parseInt(durationMinutes),
    questionType,
    isPublished,
    questions  // array of question objects
  }

  // Write to Firestore
  const docRef = doc(collection(db, 'quizzes'))
  await setDoc(docRef, quizData)
  
  // Close modal and refresh quiz list
  // (onSnapshot listener auto-updates the list)
  setCreateOpen(false)
  resetForm()
}
```

**Step 5: Real-time propagation to students**

The moment `setDoc` completes and `isPublished === true`:
- Any student who has `/student/quizzes` open receives the quiz via their `onSnapshot` subscription
- The quiz card appears without page refresh

---

## Part 6: EngagementTracker in a Session

### How the webcam AI tracks engagement

**Step 1: Component mounts, requests camera**

```javascript
// EngagementTracker.jsx
useEffect(() => {
  let cleanup = []

  const init = async () => {
    // 1. Request camera
    const stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'user', width: 320, height: 240 }
    })
    streamRef.current = stream
    videoRef.current.srcObject = stream
    cleanup.push(() => stream.getTracks().forEach(t => t.stop()))

    // 2. Load MediaPipe from CDN
    const vision = await FilesetResolver.forVisionTasks(
      'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.12/wasm'
    )

    landmarkerRef.current = await FaceLandmarker.createFromOptions(vision, {
      baseOptions: {
        modelAssetPath:
          'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task'
      },
      numFaces: 1,
      runningMode: 'VIDEO'
    })

    setReady(true)

    // 3. Start sampling interval
    intervalRef.current = setInterval(sample, 1000)
    cleanup.push(() => clearInterval(intervalRef.current))
  }

  init().catch(err => setError(err.message))
  return () => cleanup.forEach(fn => fn())
}, [])
```

**Step 2: Every 1 second, analyze a frame**

```javascript
const sample = () => {
  if (!videoRef.current || !landmarkerRef.current) return

  const result = landmarkerRef.current.detectForVideo(
    videoRef.current,
    Date.now()
  )

  let newStatus
  if (!result.faceLandmarks || result.faceLandmarks.length === 0) {
    newStatus = 'away'
  } else {
    // Nose tip landmark is index 1 in MediaPipe's 478-point face model
    const noseTipX = result.faceLandmarks[0][1].x  // 0.0 = left, 1.0 = right
    const offset = Math.abs(noseTipX - 0.5)  // 0.0 = center, 0.5 = edge
    newStatus = offset > 0.15 ? 'distracted' : 'focused'
  }

  setStatus(newStatus)

  // Accumulate in ref (not state — avoids re-render every second)
  countsRef.current[newStatus]++
  countsRef.current.total++

  // Every 10 samples, flush to Firebase
  if (countsRef.current.total % 10 === 0) {
    flushCounts()
  }
}
```

**Step 3: Every 10 seconds, push to Firebase**

```javascript
const flushCounts = async () => {
  const { focused, distracted, away, total } = countsRef.current

  if (total === 0) return

  await pushEngagement({
    sessionId,
    offeringId,
    studentId: user.uid,
    focusedCount: focused,
    distractedCount: distracted,
    awayCount: away,
    samplesCount: total
  })
  // ^ calls Firebase Function `pushEngagement`
  //   which upserts engagementAgg/{sessionId}_{studentId}

  // Reset counters
  countsRef.current = { focused: 0, distracted: 0, away: 0, total: 0 }
}
```

**Step 4: On unmount, flush remaining data and stop camera**

```javascript
return () => {
  flushCounts()  // send any unsent samples
  clearInterval(intervalRef.current)
  streamRef.current?.getTracks().forEach(t => t.stop())  // release webcam
}
```

---

## Part 7: Admin Creates Users from Excel

### Bulk import flow

```javascript
// BulkImportUsersPage.jsx

// Step 1: User uploads .xlsx file
const handleFileChange = (event) => {
  const file = event.target.files[0]
  const reader = new FileReader()
  
  reader.onload = (e) => {
    const arrayBuffer = e.target.result
    const workbook = XLSX.read(arrayBuffer, { type: 'array' })
    const sheet = workbook.Sheets[workbook.SheetNames[0]]
    const rows = XLSX.utils.sheet_to_json(sheet, { header: 1 })

    // rows[0] = headers, rows[1+] = data
    const headers = normalizeHeaders(rows[0])
    const users = rows.slice(1).map(row => {
      const obj = {}
      headers.forEach((h, i) => { obj[h] = row[i] })
      return obj
    })

    setParsedUsers(users)
    setValidationErrors(validateAll(users))
  }

  reader.readAsArrayBuffer(file)
}

// Step 2: Admin clicks "Import"
const handleImport = async () => {
  // Validate: don't send rows with errors
  const validUsers = parsedUsers.filter((_, i) => !validationErrors[i])

  setImporting(true)
  try {
    const fn = httpsCallable(functions, 'bulkCreateUsers')
    const result = await fn({ users: validUsers })
    // result.data = { created: 45, failed: 2, errors: [...] }
    
    setImportResult(result.data)
  } catch (error) {
    setImportError(error.message)
  } finally {
    setImporting(false)
  }
}
```

**Server-side (in Firebase Function `bulkCreateUsers`):**
```javascript
for (const user of users) {
  try {
    // Create Firebase Auth user
    const authUser = await admin.auth().createUser({
      email: user.email,
      password: user.password,
      displayName: user.username
    })

    // Set custom claims (role)
    await admin.auth().setCustomUserClaims(authUser.uid, { role: user.role })

    // Create Firestore profile
    await admin.firestore().collection('users').doc(authUser.uid).set({
      fullName: user.username,
      email: user.email,
      role: user.role,
      phone: user.phone,
      createdAt: admin.firestore.FieldValue.serverTimestamp()
    })

    created++
  } catch (err) {
    failed++
    errors.push({ email: user.email, error: err.message })
  }
}

return { created, failed, errors }
```

---

## Summary: Key Execution Paths

| Path | Files Involved |
|------|---------------|
| App startup | `index.js` → `App.js` → `AppRoutes.jsx` |
| Login | `SignIn.jsx` → Firebase Auth → `AuthContext` |
| Route guarding | `RequireRole.js` or `ProtectedRoute.jsx` → Firebase Auth |
| Layout loading | `StudentLayout.jsx` → `users/{uid}` Firestore → Outlet |
| Quiz taking | `StudentQuizTakePage.jsx` → `quizzes/{id}` → timer → `quizSubmissions` |
| Quiz creation | `ProfessorQuizzesPage.jsx` → FastAPI → `quizzes` collection |
| AI chat | `AIChat.jsx` → `courseAiApi.js` → Firebase Function `courseAiAssistant` |
| Engagement | `EngagementTracker.jsx` → MediaPipe → Firebase Function `pushEngagement` |
| Bulk import | `BulkImportUsersPage.jsx` → xlsx parse → Firebase Function `bulkCreateUsers` |
| Room booking | `RoomSchedulePage.jsx` → `scheduleApi.js` → Firestore transaction |
