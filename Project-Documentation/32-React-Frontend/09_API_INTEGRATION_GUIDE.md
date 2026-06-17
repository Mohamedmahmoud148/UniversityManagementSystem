# 09 — API Integration Guide
## Every Data Source Documented — Firestore, Firebase Functions, FastAPI

---

## 1. Overview of API Integration Points

The frontend integrates with three external systems:

| System | How | What For |
|--------|-----|---------|
| Firebase Firestore | Firebase SDK (`getDoc`, `onSnapshot`, etc.) | Primary data (quizzes, courses, attendance, buildings) |
| Firebase Cloud Functions | `httpsCallable()` | AI chat, attendance, engagement, user management |
| FastAPI AI Service | Native `fetch()` with FormData | Quiz generation from PDF |

---

## 2. Firestore API Layer

All Firestore calls are centralized in `src/firebase/`. Pages never call the Firestore SDK directly.

### 2.1 Colleges API (`firestoreColleges.js`)

```javascript
// Fetch all colleges (one-time)
fetchColleges() → Promise<College[]>

// Fetch single college by ID
getCollegeById(collegeId) → Promise<College | null>
```

**Collection:** `colleges`
**Usage:** Admin pages (CollegesPage), layout components that need college name

---

### 2.2 Course Assignments API (`firestoreAssignments.js`, `courseAssignmentsApi.js`)

```javascript
// Get all assignments ordered by creation date
fetchAssignments() → Promise<Assignment[]>

// Real-time listener
onSnapshot(query(collection(db, 'courseAssignments')), callback)

// CRUD
addAssignment(payload) → Promise<string>  // returns new doc ID
updateAssignment(id, partial) → Promise<void>
deleteAssignment(id) → Promise<void>
```

**Collection:** `courseAssignments`

**Assignment Document Shape:**
```javascript
{
  id: "auto-generated",
  courseId: "cs101",
  courseName: "Introduction to CS",
  termId: "2026-spring",
  termLabel: "Spring 2026",
  yearLevel: 2,
  section: "A",
  professorIds: ["uid_prof_1"],
  assistantIds: ["uid_asst_1", "uid_asst_2"],
  collegeId: "coll_01",
  collegeName: "Faculty of Computer Science",
  collegeCode: "FCS",
  createdAt: Timestamp
}
```

**Queries Used:**
```javascript
// Professor sees their courses
query(
  collection(db, 'courseAssignments'),
  where('professorIds', 'array-contains', professorUid),
  orderBy('createdAt', 'desc')
)

// Assistant sees their courses
query(
  collection(db, 'courseAssignments'),
  where('assistantIds', 'array-contains', assistantUid)
)
```

---

### 2.3 Materials API (`materialsApi.js`)

```javascript
// Upload lecture PDF
uploadMaterialPdf(file, { professorId, courseId, courseDocId, materialId })
  → Promise<string>  // download URL

// Create material document after upload
createMaterialDoc({
  professorId, courseDocId, courseId, courseName,
  lectureTitle, lectureNumber, notes, file
}) → Promise<void>

// Get all materials for a course (real-time)
fetchMaterialsForCourse(professorId, courseDocId) → Promise<Material[]>

// Delete material (storage + Firestore)
deleteMaterial({ professorId, courseDocId, materialId, storagePath }) → Promise<void>
```

**Collection:** `prof_courses/{professorId}/courses/{courseDocId}/materials`
**Storage:** `materials/{professorId}/{courseId}/{materialId}.pdf`

**Material Document Shape:**
```javascript
{
  id: "auto-generated",
  professorId: "uid_prof",
  courseId: "cs101",
  courseDocId: "firestore-doc-id",
  courseName: "Intro to CS",
  lectureTitle: "Chapter 3: Recursion",
  lectureNumber: 3,
  notes: "See textbook pages 45-67",
  fileName: "lecture3_recursion.pdf",
  storagePath: "materials/uid_prof/cs101/mat_01.pdf",
  downloadUrl: "https://firebasestorage...",
  createdAt: Timestamp
}
```

---

### 2.4 AI Chat API (`courseAiApi.js`)

```javascript
// Ensure conversation document exists
ensureCourseAiConversation({ professorId, courseDocId }) → Promise<string>  // conversationId

// Subscribe to messages
listenCourseAiMessages(conversationId, onChange, onError) → () => unsubscribe

// Send a message + create AI placeholder
createCourseAiMessagePair({
  conversationId, professorId, courseDocId, content
}) → Promise<{ professorMsgId, aiMsgId }>

// Trigger AI response (calls Cloud Function)
callCourseAiAssistant({
  conversationId, courseDocId, responseMessageId,
  lecture,         // selected lecture context
  recentMessages   // last N messages for context
}) → Promise<void>

// Update AI message after function returns
updateCourseAiMessage({ conversationId, messageId, payload }) → Promise<void>
```

**Collection:** `ai_conversations/{profId}_{courseDocId}/messages`

**Message Document Shape:**
```javascript
{
  id: "auto-generated",
  role: "professor" | "ai",
  content: "What is the difference between BFS and DFS?",
  status: "processing" | "done" | "error",  // AI messages only
  createdAt: Timestamp
}
```

**Sequence Diagram:**
```
Professor types → createCourseAiMessagePair()
                    ├── writes professor message { role: "professor", content }
                    └── writes AI placeholder { role: "ai", status: "processing" }
                              │
                              ▼
                  callCourseAiAssistant()  → Cloud Function
                              │
                              ▼
                  Cloud Function:
                    reads course materials
                    calls LLM with context
                    writes final response to AI message
                              │
                              ▼
                  listenCourseAiMessages onSnapshot fires
                  UI updates: status "processing" → content visible
```

---

### 2.5 Attendance & Engagement (`attendanceFunctions.js`)

```javascript
// Mark student attendance in a session
setAttendance({
  sessionId, studentId, status,   // "present"|"late"|"absent"|"excused"
  offeringId, updatedBy
}) → Promise<void>

// Push engagement sample batch
pushEngagement({
  sessionId, offeringId, studentId,
  focusedCount, distractedCount, awayCount, samplesCount
}) → Promise<void>
```

Both are thin wrappers around `httpsCallable`:
```javascript
const setAttendanceFn = httpsCallable(functions, 'setAttendance')
await setAttendanceFn(payload)
```

---

### 2.6 Campus Building APIs

**Buildings (`buildingsApi.js`):**
```javascript
listBuildings(collegeId) → Promise<Building[]>
subscribeBuildings(collegeId, onChange, onError) → unsubscribe
createBuilding(collegeId, { name, location, imageUrl }) → Promise<string>
updateBuilding(collegeId, buildingId, partial) → Promise<void>
deleteBuilding(collegeId, buildingId)  // cascade: deletes rooms → schedules → building
  → Promise<void>
```

**Rooms (`roomsApi.js`):**
```javascript
listRooms(collegeId, buildingId) → Promise<Room[]>
createRoom(collegeId, buildingId, { name, capacity, floor, type }) → Promise<string>
deleteRoom(collegeId, buildingId, roomId)  // cascade: deletes schedules → room
  → Promise<void>
```

**Schedule (`scheduleApi.js`):**
```javascript
// Transaction-based creation (prevents double-booking)
createSchedule(collegeId, buildingId, roomId, {
  dayKey,   // "mon", "tue", etc.
  slotKey,  // "09-11", "11-13", etc.
  courseId, courseName, section
}) → Promise<string>

// Delete a slot
deleteSchedule(collegeId, buildingId, roomId, scheduleId) → Promise<void>
```

**Schedule Document ID Pattern:** `{dayKey}_{slotKey}` e.g. `"mon_09-11"`

---

### 2.7 Professor/User Queries

```javascript
// Professor's assigned courses (real-time)
listenProfessorAssignments(profUid, options, onChange, onError)
  → tries ordered query first
  → falls back to unordered on "failed-precondition" (missing index)
  → unsubscribe function

// Professor profile from Firestore
getProfessorProfile(uid) → reads users/{uid} → Promise<Profile>

// Search users by role (for assignment form)
searchUsersByRole({ roles: ["professor", "assistant"], search: "Ahmed", limitCount: 20 })
  → Promise<User[]>

// Resolve multiple user IDs to profiles (chunked)
resolveUsersByIds(db, userIds[])
  → chunks into groups of 10 (Firestore 'in' limit)
  → Promise<Map<uid, Profile>>
```

---

## 3. Firebase Cloud Functions Integration

Functions are called via `httpsCallable()`. The Firebase SDK automatically:
- Attaches the current user's JWT token to the request
- Handles serialization (JSON)
- Returns the result or throws an `HttpsError`

### Functions Called from Frontend

| Function Name | Called From | Purpose |
|--------------|------------|---------|
| `createAdminUser` | `CreateAdminUser.jsx` | Create admin via SuperAdmin |
| `deleteUserAccount` | `CreateAdminUser.jsx` | Delete user (Auth + Firestore) |
| `editUserAccount` | `CreateAdminUser.jsx` | Update user profile |
| `bulkCreateUsers` | `BulkImportUsersPage.jsx` | Batch create users from Excel |
| `courseAiAssistant` | `courseAiApi.js` | AI response for course chat |
| `setAttendance` | `attendanceFunctions.js` | Write attendance record |
| `pushEngagement` | `attendanceFunctions.js` | Write engagement aggregate |
| `upsertAssignment` | `courseAssignmentsApi.js` | Create/update course assignment |
| `rebuildProfessorCourseIndex` | `courseProfApi.js` | Rebuild professor's course index |

### Error Handling Pattern for Functions

```javascript
try {
  const fn = httpsCallable(functions, 'bulkCreateUsers')
  const result = await fn({ users: parsedRows })
  // result.data is the function's return value
  setSuccess(`Created ${result.data.created} users`)
} catch (error) {
  if (error.code === 'functions/permission-denied') {
    setError('You do not have permission to perform this action')
  } else if (error.code === 'functions/invalid-argument') {
    setError('Invalid data format')
  } else {
    setError(error.message)
  }
}
```

**Firebase Function Error Codes:**
- `functions/permission-denied` — insufficient role
- `functions/invalid-argument` — malformed payload
- `functions/not-found` — document doesn't exist
- `functions/internal` — server-side error
- `functions/deadline-exceeded` — function timeout (default 60s)

---

## 4. FastAPI AI Integration — Quiz Generation

```javascript
// In ProfessorQuizzesPage.jsx
const handleGenerateFromPdf = async (pdfFile) => {
  setGenerating(true)
  setGenerateError(null)

  const formData = new FormData()
  formData.append('file', pdfFile)

  try {
    const response = await fetch(process.env.REACT_APP_GENERATE_QUIZ_URL, {
      method: 'POST',
      body: formData
      // Note: NO Content-Type header — browser sets it automatically for FormData
      // with correct multipart boundary
    })

    if (!response.ok) throw new Error(`HTTP ${response.status}`)

    const data = await response.json()
    // data.questions is an array of question objects
    setGeneratedQuestions(data.questions)
  } catch (err) {
    setGenerateError(err.message)
  } finally {
    setGenerating(false)
  }
}
```

**Request Format:**
```
POST REACT_APP_GENERATE_QUIZ_URL
Content-Type: multipart/form-data; boundary=----FormBoundary...

------FormBoundary...
Content-Disposition: form-data; name="file"; filename="lecture3.pdf"
Content-Type: application/pdf

[PDF binary data]
------FormBoundary-----
```

**Response Format:**
```json
{
  "questions": [
    {
      "question": "What is the time complexity of BFS?",
      "options": ["O(n)", "O(n²)", "O(V+E)", "O(log n)"],
      "correctIndex": 2
    },
    ...
  ]
}
```

**No authentication** is sent with this request — the FastAPI endpoint for quiz generation is currently not authenticated. For production, add an API key or Firebase token validation.

---

## 5. API Error Handling Reference

| Error Type | Source | How Handled |
|-----------|--------|------------|
| Firestore permission-denied | Wrong role accessing collection | Rules block access; `onError` callback fires |
| Firestore unavailable | Network down | Firestore returns cached data |
| Firestore not-found | Document doesn't exist | `doc.exists()` check before reading |
| Function permission-denied | Token missing required role | Caught in try/catch, shown as error |
| Function timeout | AI call too slow | Caught in try/catch |
| FastAPI HTTP error | Service down or error | `response.ok` check, show error message |
| FastAPI network error | Can't reach server | try/catch on fetch() |

---

## 6. Offline Behavior

Firebase Firestore has built-in offline support:

| Operation | Offline Behavior |
|-----------|-----------------|
| `getDoc()` | Returns cached data if available |
| `onSnapshot()` | Continues with cached data, syncs when online |
| `setDoc()`, `addDoc()` | Queued locally, synced when online |
| `httpsCallable()` | Fails immediately (requires network) |
| `fetch()` (FastAPI) | Fails immediately (requires network) |

**Result:** Read operations work offline. Write operations are queued for retry. Function calls and AI requests fail.

---

## 7. Firestore Query Reference

### Composite Queries Requiring Indexes

| Query | Index Needed |
|-------|-------------|
| `where('professorIds', 'array-contains', uid) + orderBy('createdAt', 'desc')` | Yes — defined in `firestore.indexes.json` |
| `where('quizId', '==', id) + where('studentUid', '==', uid)` | Yes |
| `where('collegeId', '==', id) + orderBy('name')` | Yes |

### CollectionGroup Queries

Used for querying across subcollections:
```javascript
// Find all schedules for a building (across all rooms)
query(
  collectionGroup(db, 'schedule'),
  where('buildingId', '==', buildingId)
)
```

**Requirement:** CollectionGroup queries require a Firestore index with `queryScope: "COLLECTION_GROUP"`.

### Chunked Queries (Workaround for 'in' Limit)

Firestore's `where(field, 'in', array)` supports a maximum of **30 values**. For larger arrays:

```javascript
// From usersApi.js
const resolveUsersByIds = async (db, userIds) => {
  const chunkSize = 10  // safe limit
  const chunks = []
  for (let i = 0; i < userIds.length; i += chunkSize) {
    chunks.push(userIds.slice(i, i + chunkSize))
  }

  const results = {}
  for (const chunk of chunks) {
    const q = query(collection(db, 'users'), where(documentId(), 'in', chunk))
    const snap = await getDocs(q)
    snap.docs.forEach(d => { results[d.id] = { uid: d.id, ...d.data() } })
  }
  return results
}
```
