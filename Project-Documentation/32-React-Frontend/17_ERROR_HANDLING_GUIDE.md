# 17 — Error Handling Guide
## How Errors Are Caught, Displayed, and Recovered

---

## 1. Error Handling Architecture

The application uses a **local error handling** approach — each component catches and displays its own errors. There is no global error boundary that catches all errors.

```
Error occurs
    │
    ├── Synchronous (render error) → would bubble up (no ErrorBoundary)
    │
    ├── Async operation (Firebase, API)
    │      └── try/catch → setError(message) → render <Alert>
    │
    └── Firestore subscription (onSnapshot onError)
           └── second callback argument → setError(message) → render <Alert>
```

---

## 2. Error Utility Function

**File:** `src/utils/errorHelpers.js`

```javascript
export const getErrorMessage = (error, fallback = 'An error occurred') => {
  if (!error) return fallback
  if (typeof error === 'string') return error
  if (error.message) return error.message
  if (error.code) return error.code
  return fallback
}
```

This handles the three main error shapes:
- Firebase errors: `{ code: 'auth/wrong-password', message: 'Wrong password' }`
- Firestore errors: `{ code: 'permission-denied', message: '...' }`
- Plain JavaScript errors: `{ message: 'Network error' }`
- String errors (when code throws a string): `"PDF only"`

---

## 3. Async Error Handling Pattern

The most common pattern across all pages:

```javascript
const [data, setData]     = useState(null)
const [loading, setLoading] = useState(true)
const [error, setError]   = useState(null)

useEffect(() => {
  const load = async () => {
    try {
      const result = await fetchSomeData(id)
      setData(result)
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to load data'))
    } finally {
      setLoading(false)
    }
  }
  load()
}, [id])

// Render:
if (loading) return <CircularProgress />
if (error)   return <Alert severity="error">{error}</Alert>
return <DataView data={data} />
```

---

## 4. Firebase-Specific Error Codes

### Firebase Auth Errors

| Error Code | User-Friendly Message |
|-----------|----------------------|
| `auth/wrong-password` | Incorrect password |
| `auth/user-not-found` | No account found with this email |
| `auth/invalid-email` | Invalid email address format |
| `auth/email-already-in-use` | This email is already registered |
| `auth/weak-password` | Password is too weak (min 6 characters) |
| `auth/too-many-requests` | Account temporarily locked due to many failed attempts |
| `auth/network-request-failed` | Network error — check your connection |
| `auth/user-disabled` | This account has been disabled |

**Usage in SignIn.jsx:**
```javascript
const handleLogin = async () => {
  try {
    await signInWithEmailAndPassword(auth, email, password)
  } catch (error) {
    switch (error.code) {
      case 'auth/wrong-password':
        setError('Incorrect password. Please try again.')
        break
      case 'auth/user-not-found':
        setError('No account found with this email.')
        break
      default:
        setError(getErrorMessage(error, 'Sign in failed'))
    }
  }
}
```

---

### Firestore Errors

| Error Code | Meaning | Cause |
|-----------|---------|-------|
| `permission-denied` | Security rules blocked the request | Wrong role, or accessing another user's data |
| `unavailable` | Firestore service down | Network or Firebase outage |
| `not-found` | Document doesn't exist | Deleted or wrong ID |
| `already-exists` | Creating a document that already exists | Should use `setDoc` with merge instead of `addDoc` |
| `failed-precondition` | Query needs an index | Missing composite index |
| `resource-exhausted` | Firestore quota exceeded | Too many reads/writes |

---

### Cloud Function Errors

| Error Code | Meaning |
|-----------|---------|
| `functions/unauthenticated` | Not signed in |
| `functions/permission-denied` | Signed in but wrong role |
| `functions/invalid-argument` | Malformed request payload |
| `functions/not-found` | Referenced document doesn't exist |
| `functions/internal` | Server-side error in the function |
| `functions/deadline-exceeded` | Function took too long (> 60s default) |

---

## 5. Real-Time Subscription Error Handling

`onSnapshot` takes two callbacks — a success callback and an error callback:

```javascript
const unsubscribe = onSnapshot(
  query,
  // Success: data updated
  (snapshot) => {
    setData(snapshot.docs.map(d => ({ id: d.id, ...d.data() })))
    setLoading(false)
  },
  // Error: subscription failed
  (error) => {
    setError(error.message)
    setLoading(false)
    // The subscription is automatically stopped on error
  }
)
```

**Important:** When the error callback fires, the subscription is terminated. The UI needs a "Retry" mechanism:

```javascript
const [retryKey, setRetryKey] = useState(0)

// Retry by forcing the useEffect to re-run
const handleRetry = () => setRetryKey(k => k + 1)

useEffect(() => {
  const unsub = onSnapshot(ref, setData, setError)
  return () => unsub()
}, [ref, retryKey])  // retryKey change re-subscribes
```

---

## 6. Quiz Submission Error Handling

The quiz submission is the most critical write operation — losing a student's answers is unacceptable.

```javascript
const submitGuardRef = useRef(false)

const handleSubmit = async () => {
  // Guard: prevent double-submit
  if (submitGuardRef.current) return
  submitGuardRef.current = true

  setSubmitting(true)

  try {
    // Calculate score client-side before writing
    const result = calculateResult(quiz.questions, answers)

    await addDoc(collection(db, 'quizSubmissions'), {
      quizId,
      studentUid: user.uid,
      answers,
      ...result,
      submittedAt: serverTimestamp()
    })

    navigate(`/student/quizzes/${quizId}/result`)

  } catch (error) {
    // CRITICAL: if write fails, let student retry
    submitGuardRef.current = false  // allow retry
    setSubmitting(false)
    setSubmitError(
      'Failed to submit. Your answers are saved — please try again.'
    )
  }
}
```

**Key:** If submission fails, `submitGuardRef.current` is reset to `false` so the student can retry. The answers are still in React state.

---

## 7. File Upload Error Handling

```javascript
const handleUpload = async () => {
  setLoading(true)
  setError(null)

  try {
    // Step 1: Validate file
    if (!file) throw new Error('Please select a PDF file')
    if (file.type !== 'application/pdf') throw new Error('Only PDF files are accepted')
    if (file.size > 50 * 1024 * 1024) throw new Error('File must be under 50MB')

    // Step 2: Upload to Storage
    const downloadUrl = await uploadMaterialPdf(file, { professorId, courseId })

    // Step 3: Save metadata to Firestore
    await createMaterialDoc({ downloadUrl, ...otherFields })

    onCreated()
    onClose()

  } catch (error) {
    // Categorize the error for the user
    if (error.code === 'storage/unauthorized') {
      setError('Permission denied. You can only upload to your own courses.')
    } else if (error.code === 'storage/quota-exceeded') {
      setError('Storage quota exceeded. Contact admin.')
    } else {
      setError(getErrorMessage(error, 'Upload failed. Please try again.'))
    }
  } finally {
    setLoading(false)
  }
}
```

---

## 8. AI Request Error Handling

### AI Chat (Cloud Function)

```javascript
try {
  await callCourseAiAssistant({
    conversationId, courseDocId, responseMessageId, lecture, recentMessages
  })
} catch (error) {
  // The AI message placeholder stays with status "processing"
  // Update it to show error state
  await updateCourseAiMessage({
    conversationId,
    messageId: responseMessageId,
    payload: {
      status: 'error',
      content: 'Failed to get AI response. Please try again.'
    }
  })
  setSendError('AI response failed. Please try again.')
}
```

### AI Quiz Generation (FastAPI)

```javascript
try {
  const response = await fetch(url, { method: 'POST', body: formData })

  if (!response.ok) {
    throw new Error(`Server error: ${response.status} ${response.statusText}`)
  }

  const data = await response.json()

  if (!data.questions || !Array.isArray(data.questions)) {
    throw new Error('Invalid response from AI service')
  }

  setGeneratedQuestions(data.questions)

} catch (error) {
  if (error.name === 'TypeError') {
    // Network error — can't reach FastAPI
    setGenerateError('Cannot connect to AI service. Check your connection.')
  } else {
    setGenerateError(getErrorMessage(error, 'Quiz generation failed'))
  }
}
```

---

## 9. Missing: Global Error Boundary

The application currently has **no React Error Boundary**. If any component throws a synchronous error during render (a JavaScript error, not a Promise rejection), the entire app will crash with a blank white screen.

**Recommendation:** Add a global error boundary:

```javascript
// src/components/ErrorBoundary.jsx
class ErrorBoundary extends React.Component {
  state = { hasError: false, error: null }

  static getDerivedStateFromError(error) {
    return { hasError: true, error }
  }

  componentDidCatch(error, info) {
    console.error('Uncaught error:', error, info)
    // Could send to error tracking service (Sentry, etc.)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center h-screen">
          <h2>Something went wrong</h2>
          <p>{this.state.error?.message}</p>
          <button onClick={() => window.location.reload()}>
            Reload Page
          </button>
        </div>
      )
    }
    return this.props.children
  }
}

// In index.js
root.render(
  <ErrorBoundary>
    <App />
  </ErrorBoundary>
)
```

---

## 10. Error Recovery Mechanisms

| Error Type | Recovery | User Action |
|-----------|----------|------------|
| Auth session expired | Firebase auto-refreshes token | None (transparent) |
| Firestore subscription error | Re-subscribe with retry key | Click "Retry" button |
| Upload failed | Reset guard, preserve form state | Click "Upload" again |
| Quiz submit failed | Reset guard ref | Click "Submit" again |
| AI generation failed | Show error, clear loading | Click "Generate" again |
| Network offline | Firestore serves cache | Auto-recovers on reconnect |
| Firebase Function timeout | Show error message | Retry after delay |

---

## 11. Console Error Suppression

The `professorApi.js` file implements a fallback query pattern that silently catches and retries on `failed-precondition` errors (missing indexes):

```javascript
// Tries ordered query first
// If it fails with 'failed-precondition' (missing index), retries without order
onSnapshot(
  query(ref, where('professorIds', 'array-contains', uid), orderBy('createdAt', 'desc')),
  onChange,
  async (error) => {
    if (error.code === 'failed-precondition') {
      // Silently retry without orderBy
      onSnapshot(
        query(ref, where('professorIds', 'array-contains', uid)),
        onChange,
        onError
      )
    } else {
      onError(error)
    }
  }
)
```

This prevents a missing Firestore index from breaking the entire professor assignment view — it just loses the ordering.
