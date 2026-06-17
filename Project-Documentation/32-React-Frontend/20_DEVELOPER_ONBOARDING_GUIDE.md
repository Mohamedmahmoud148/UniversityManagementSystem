---
render_with_liquid: false
---

# 20 — Developer Onboarding Guide
## Get Up and Running in 30 Minutes

---

## Welcome to the Project

This guide will take you from zero to having the development environment running and understanding the codebase. Estimated time: 30-60 minutes.

---

## 1. Prerequisites

Install these tools before starting:

```bash
# Check if you have Node.js 18+
node --version   # should show v18.x.x or higher

# Check if you have npm
npm --version    # should show 9.x.x or higher

# Check if you have Git
git --version
```

**If Node.js is not installed:** Download from https://nodejs.org (choose "LTS" version)

**Install Firebase CLI:**
```bash
npm install -g firebase-tools
firebase --version  # verify installation
```

---

## 2. Getting the Code

```bash
# Clone the repository
git clone https://github.com/ferashatem/Graduation-Project.git
cd Graduation-Project

# See what's in the project root
ls
# You should see: src/, functions/, public/, firebase.json, package.json, etc.
```

---

## 3. Install Dependencies

```bash
# Install frontend dependencies (run from project root)
npm install

# Install Firebase Functions dependencies
cd functions && npm install && cd ..
```

This creates `node_modules/` directories (large! ~200MB each). They are gitignored.

---

## 4. Environment Variables

Create a `.env` file in the project root. Ask a team member for the actual values, or find them in Firebase Console → Project Settings.

```bash
# Copy the template
cp .env.example .env  # if .env.example exists
# OR create it manually:
```

Required contents:
```
REACT_APP_FIREBASE_API_KEY=AIzaSy...
REACT_APP_FIREBASE_AUTH_DOMAIN=graduation-project-61aa9.firebaseapp.com
REACT_APP_FIREBASE_STORAGE_BUCKET=graduation-project-61aa9.appspot.com
REACT_APP_FIREBASE_MESSAGING_SENDER_ID=528199941216
REACT_APP_FIREBASE_APP_ID=1:528199941216:web:...
REACT_APP_GENERATE_QUIZ_URL=https://your-fastapi-service.com/api/generate-quiz
```

**NEVER commit the `.env` file to git.**

---

## 5. Start the Development Server

```bash
npm start
```

You should see:
```
Compiled successfully!
Local:            http://localhost:3000
On Your Network:  http://192.168.x.x:3000
```

Open http://localhost:3000 in your browser. You should see the sign-in page.

**Tip:** For development, ask a team member for a test account for each role. You'll want at least one student account and one professor account.

---

## 6. Understanding the Codebase — Quick Tour

### 6.1 Start Here: The Route Map

Open `src/routes/AppRoutes.jsx`. This file shows you **everything** the app can do — every page, its URL, and who can access it.

### 6.2 Then: The Firebase API Layer

Open `src/firebase/` folder. Every file here is a domain-specific set of Firestore functions. Want to know how quizzes are stored? Look at the quiz-related code in `ProfessorQuizzesPage` — it calls Firestore directly (inline). For materials, look at `materialsApi.js`.

### 6.3 Then: Pick a Feature to Follow

Follow one feature end-to-end. Start with the quiz feature:
1. `src/pages/professor/ProfessorQuizzesPage.jsx` — how professor creates a quiz
2. `src/pages/student/StudentQuizTakePage.jsx` — how student takes it
3. `src/pages/student/StudentQuizResultPage.jsx` — how results are shown
4. `src/lib/quizUtils.js` — how the score is calculated

This gives you the full lifecycle of one feature.

---

## 7. Project Conventions

### 7.1 File Naming

| Type | Convention | Example |
|------|-----------|---------|
| React components | PascalCase | `StudentQuizTakePage.jsx` |
| Hooks | camelCase with `use` prefix | `useColleges.js` |
| API files | camelCase + descriptive | `materialsApi.js`, `buildingsApi.js` |
| Utilities | camelCase | `errorHelpers.js`, `quizUtils.js` |
| Tests | Same as file + `.test` | `quizUtils.test.js` |

### 7.2 Component Structure

Follow this order within a component file:
```javascript
// 1. Imports
import React, { useState, useEffect } from 'react'
import { ... } from '@mui/material'
import { ... } from '../firebase/someApi'

// 2. Component definition
function MyComponent({ prop1, prop2 }) {
  // 3. State declarations
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)

  // 4. Context/outlet
  const { user } = useOutletContext()

  // 5. Effects
  useEffect(() => {
    // fetch data
  }, [dep])

  // 6. Handlers
  const handleSubmit = async () => {
    // handle action
  }

  // 7. Conditional renders (loading, error)
  if (loading) return <CircularProgress />

  // 8. Main render
  return (
    <div>
      {/* JSX */}
    </div>
  )
}

export default MyComponent
```

### 7.3 State Naming

```javascript
// Data + loading + error trio
const [colleges, setColleges]     = useState([])
const [loading, setLoading]       = useState(true)
const [error, setError]           = useState(null)

// Modal open/close
const [createOpen, setCreateOpen] = useState(false)
const [editTarget, setEditTarget] = useState(null)

// Form data
const [formData, setFormData]     = useState({ name: '', code: '' })
const [formErrors, setFormErrors] = useState({})
```

### 7.4 Firebase API Functions

Every function in `src/firebase/` should:
- Accept parameters (IDs, data)
- Return a Promise
- Handle errors by re-throwing (let the caller handle UI)
- Use meaningful function names: `fetchXxx`, `createXxx`, `updateXxx`, `deleteXxx`, `listenXxx`

```javascript
// Good pattern
export const fetchMaterialsForCourse = async (professorId, courseDocId) => {
  const colRef = collection(db,
    `prof_courses/${professorId}/courses/${courseDocId}/materials`)
  const q = query(colRef, orderBy('createdAt', 'desc'))
  const snap = await getDocs(q)
  return snap.docs.map(d => ({ id: d.id, ...d.data() }))
  // Don't catch here — let the caller catch and show UI error
}
```

### 7.5 Firestore Collection Paths

All collection paths are defined in `src/firebase/firestorePaths.js` and `firestoreRefs.js`. Use these instead of hardcoding paths:

```javascript
// Bad
const ref = collection(db, `prof_courses/${profId}/courses/${courseId}/materials`)

// Good
const ref = getMaterialsRef(db, profId, courseDocId)  // from firestoreRefs.js
```

---

## 8. Making Changes

### 8.1 Adding a New Page

1. Create the component file in the right `pages/` subfolder
2. Add the route in `AppRoutes.jsx`
3. Add navigation link in the role's sidebar component
4. If it needs data, add API functions to `src/firebase/` or `src/features/`

**Example: Adding "Student Grades" page**

```javascript
// Step 1: Create src/pages/student/StudentGradesPage.jsx
function StudentGradesPage() {
  const { user, profile } = useOutletContext()
  const [grades, setGrades] = useState([])
  // ... fetch from .NET backend via Axios
  return <div>Grades page</div>
}
export default StudentGradesPage

// Step 2: Add to AppRoutes.jsx
import StudentGradesPage from '../pages/student/StudentGradesPage'
// Inside student routes:
<Route path="grades" element={<StudentGradesPage />} />

// Step 3: Add to StudentSidebar.jsx
{ label: 'My Grades', icon: <GradeIcon />, path: '/student/grades' }
```

### 8.2 Adding a New Firestore API

Create a new file in `src/firebase/`:

```javascript
// src/firebase/gradesApi.js
import { collection, query, where, getDocs } from 'firebase/firestore'
import { db } from './firebaseConfig'

export const fetchStudentGrades = async (studentId) => {
  const q = query(
    collection(db, 'grades'),
    where('studentId', '==', studentId)
  )
  const snap = await getDocs(q)
  return snap.docs.map(d => ({ id: d.id, ...d.data() }))
}
```

### 8.3 Updating Firestore Security Rules

After adding a new collection, update `firestore.rules`:

```javascript
match /grades/{gradeId} {
  allow read: if request.auth.uid == resource.data.studentId
            || hasRole('professor')
            || isAdmin()
}
```

Then deploy: `firebase deploy --only firestore:rules`

---

## 9. Git Workflow

```bash
# Create a feature branch
git checkout -b feature/student-grades-page

# Make your changes
# ...

# Check what changed
git status
git diff

# Stage and commit
git add src/pages/student/StudentGradesPage.jsx
git add src/routes/AppRoutes.jsx
git commit -m "feat: add student grades page"

# Push to remote
git push origin feature/student-grades-page

# Create a Pull Request on GitHub
```

### Commit Message Convention

```
feat: add student grades page
fix: quiz timer not stopping on unmount  
chore: update firebase SDK to v9.23
docs: add grades page to routing docs
refactor: extract quiz calculation to separate utility
```

---

## 10. Debugging Tips

### Firebase SDK Debugging

```javascript
// Enable Firestore logging
import { setLogLevel } from 'firebase/firestore'
setLogLevel('debug')  // shows all Firestore operations in console
```

### React DevTools

Install React DevTools browser extension. It lets you:
- Inspect component props and state
- See the component tree
- Profile render performance

### Common Issues and Solutions

| Problem | Cause | Solution |
|---------|-------|---------|
| Page shows blank screen | JavaScript runtime error | Check browser console |
| Data not loading | Firestore permission denied | Check security rules + user role |
| Login fails | Wrong credentials or Firebase not configured | Check `.env` values |
| Functions not working | Functions not deployed | Run `firebase deploy --only functions` |
| Quiz questions not showing | Firebase index missing | Click the link in console error to create index |
| `process.env.REACT_APP_*` is undefined | Variable missing from `.env` | Add to `.env` and restart `npm start` |
| CORS error calling FastAPI | FastAPI not configured for CORS | Add CORS middleware to FastAPI |

### Viewing Firebase Data

1. Firebase Console → Firestore → view and edit documents
2. Firebase Console → Authentication → view/delete users
3. Firebase Console → Storage → view uploaded files
4. Firebase Console → Functions → view logs

### Local Emulator (for testing without affecting production)

```bash
firebase emulators:start --only firestore,auth,functions,storage
```
Access emulator UI at http://localhost:4000

---

## 11. Key Files to Know

| File | Why Important |
|------|--------------|
| `src/routes/AppRoutes.jsx` | Complete route map |
| `src/firebase/firebaseConfig.js` | Firebase initialization |
| `src/context/AuthContext.jsx` | Auth state |
| `src/firebase/materialsApi.js` | Materials pattern to follow |
| `src/pages/student/StudentQuizTakePage.jsx` | Most complex page |
| `src/components/engagement/EngagementTracker.jsx` | Most complex component |
| `functions/index.js` | All server-side functions |
| `firestore.rules` | Security rules |
| `firebase.json` | Deployment config |
