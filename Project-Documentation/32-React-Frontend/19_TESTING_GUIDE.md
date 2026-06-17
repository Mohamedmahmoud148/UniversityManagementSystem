# 19 — Testing Guide
## Current Test Coverage, Testing Strategy, and Recommendations

---

## 1. Current Testing State

The project was created with Create React App which includes Jest and React Testing Library by default. However, **minimal automated tests are currently written** for the application code.

CRA's default test file (`App.test.js`) may exist but contains only the scaffold test:
```javascript
test('renders without crashing', () => {
  render(<App />)
})
```

This means the application relies primarily on **manual testing** for quality assurance.

---

## 2. Testing Infrastructure (Available)

### Jest (Built-in with CRA)
- **Type:** Unit and integration test runner
- **Location:** Any `*.test.js` or `*.spec.js` file, or `__tests__/` folders
- **Run:** `npm test`

### React Testing Library (Built-in with CRA)
- **Package:** `@testing-library/react`
- **Philosophy:** Test behavior, not implementation
- **Queries:** `getByText`, `getByRole`, `getByLabelText`, etc.

### Firebase Emulator Suite (For Integration Tests)
- **Package:** `firebase-tools` (already installed)
- **Command:** `firebase emulators:start`
- **What it emulates:** Firestore, Auth, Functions, Storage

---

## 3. What Should Be Tested

### 3.1 Unit Tests — `src/lib/quizUtils.js`

This is the most testable pure function in the project:

```javascript
// src/lib/quizUtils.test.js
import { calculateResult } from './quizUtils'

describe('calculateResult', () => {
  const questions = [
    { text: 'Q1', type: 'mcq', options: ['A', 'B', 'C', 'D'], correctAnswer: 'A', points: 5 },
    { text: 'Q2', type: 'trueFalse', correctAnswer: 'True', points: 5 },
    { text: 'Q3', type: 'mcq', options: ['A', 'B', 'C', 'D'], correctAnswer: 'C', points: 10 },
  ]

  test('perfect score', () => {
    const answers = { 0: 'A', 1: 'True', 2: 'C' }
    const result = calculateResult(questions, answers)
    expect(result.score).toBe(20)
    expect(result.totalPoints).toBe(20)
    expect(result.percentage).toBe(100)
    expect(result.wrongQuestions).toHaveLength(0)
  })

  test('zero score with no answers', () => {
    const result = calculateResult(questions, {})
    expect(result.score).toBe(0)
    expect(result.percentage).toBe(0)
    expect(result.wrongQuestions).toHaveLength(3)
  })

  test('partial score', () => {
    const answers = { 0: 'A', 1: 'False', 2: 'B' }  // only Q1 correct
    const result = calculateResult(questions, answers)
    expect(result.score).toBe(5)
    expect(result.percentage).toBe(25)
    expect(result.wrongQuestions).toHaveLength(2)
  })
})
```

### 3.2 Unit Tests — `src/utils/campusScheduleUtils.js`

```javascript
// src/utils/campusScheduleUtils.test.js
import { buildSlotKeyFromTimes, normalizeScheduleDocs } from './campusScheduleUtils'

describe('buildSlotKeyFromTimes', () => {
  test('builds correct slot key', () => {
    expect(buildSlotKeyFromTimes('09:00', '11:00')).toBe('09-11')
    expect(buildSlotKeyFromTimes('13:00', '15:00')).toBe('13-15')
  })
})

describe('normalizeScheduleDocs', () => {
  test('builds empty matrix when no docs', () => {
    const matrix = normalizeScheduleDocs([])
    expect(matrix.sat['09-11']).toBe('available')
  })

  test('marks booked slots as reserved', () => {
    const docs = [{
      dayKey: 'mon',
      slotKey: '11-13',
      status: 'reserved',
      courseName: 'CS101'
    }]
    const matrix = normalizeScheduleDocs(docs)
    expect(matrix.mon['11-13']).toBe('reserved')
    expect(matrix.sat['09-11']).toBe('available')
  })
})
```

### 3.3 Unit Tests — `src/utils/errorHelpers.js`

```javascript
describe('getErrorMessage', () => {
  test('returns string error as-is', () => {
    expect(getErrorMessage('My error')).toBe('My error')
  })

  test('extracts message from Error object', () => {
    expect(getErrorMessage(new Error('Test error'))).toBe('Test error')
  })

  test('returns fallback for null', () => {
    expect(getErrorMessage(null, 'Default')).toBe('Default')
  })

  test('extracts code from Firebase error', () => {
    const firebaseError = { code: 'auth/wrong-password', message: 'Wrong password' }
    expect(getErrorMessage(firebaseError)).toBe('Wrong password')
  })
})
```

### 3.4 Component Tests — `StudentQuizTakePage`

The quiz timer and auto-submit are critical paths that should be tested:

```javascript
// src/pages/student/StudentQuizTakePage.test.jsx
import { render, act } from '@testing-library/react'
import { jest } from '@jest/globals'

// Mock Firebase
jest.mock('../firebase/firebaseConfig', () => ({
  db: {},
  auth: { currentUser: { uid: 'test-uid', displayName: 'Test User' } }
}))

describe('Quiz Timer', () => {
  test('auto-submits when timer expires', async () => {
    jest.useFakeTimers()
    // ... render component with quiz that expires in 1 second
    act(() => jest.advanceTimersByTime(1000))
    // ... assert submission was called
    jest.useRealTimers()
  })
})
```

### 3.5 Component Tests — Authentication Guards

```javascript
describe('RequireRole', () => {
  test('redirects to / when no user', async () => {
    // Mock onAuthStateChanged to fire with null
    render(
      <MemoryRouter initialEntries={['/admin']}>
        <RequireRole role="admin">
          <div>Admin Content</div>
        </RequireRole>
      </MemoryRouter>
    )
    // Assert redirect happened
  })

  test('renders children with correct role', async () => {
    // Mock token claims with role: "admin"
    // Assert children rendered
  })
})
```

---

## 4. Integration Testing with Firebase Emulator

For tests that involve Firebase:

```javascript
// setupTests.js
import { connectFirestoreEmulator } from 'firebase/firestore'
import { connectAuthEmulator } from 'firebase/auth'
import { db, auth } from './firebase/firebaseConfig'

if (process.env.NODE_ENV === 'test') {
  connectFirestoreEmulator(db, 'localhost', 8080)
  connectAuthEmulator(auth, 'http://localhost:9099')
}
```

This redirects all Firebase calls to local emulators during tests — no production data is touched.

---

## 5. Manual Testing Checklist

Since automated tests are minimal, this manual checklist covers the critical paths:

### Authentication
- [ ] Student can sign in with valid credentials
- [ ] Student cannot access `/admin/*` routes
- [ ] Professor cannot access `/student/*` routes
- [ ] Invalid password shows error message
- [ ] Sign out works from all sidebars
- [ ] Refreshing page maintains login state

### Student Quiz Flow
- [ ] Student sees only published quizzes for their class
- [ ] Already-submitted quizzes show "Submitted" status
- [ ] Quiz timer starts and counts down correctly
- [ ] Timer changes to orange at 2 minutes
- [ ] Timer changes to red at 1 minute
- [ ] Quiz auto-submits when timer reaches 0
- [ ] Manual submit works
- [ ] Cannot submit twice
- [ ] Result page shows correct score

### Professor Quiz Flow
- [ ] Professor can create MCQ quiz
- [ ] Professor can create True/False quiz
- [ ] AI generation from PDF returns questions (requires FastAPI running)
- [ ] Published quiz appears for students in correct class
- [ ] Unpublished quiz does NOT appear for students
- [ ] Professor can view all submissions for their quiz

### Materials
- [ ] Professor can upload a PDF
- [ ] Material appears in course materials list
- [ ] PDF is downloadable by professor
- [ ] TA can upload assignment materials

### Admin
- [ ] Admin can create college
- [ ] Admin can create department inside college
- [ ] Admin can assign professor to course
- [ ] Excel import creates users
- [ ] Building creation works
- [ ] Room schedule booking prevents double-booking

### AI Chat
- [ ] Professor can send a message
- [ ] AI response appears (may take 3-10 seconds)
- [ ] Selecting different lectures changes context

---

## 6. Testing Recommendations

### Priority 1: Pure Function Unit Tests
Start with `quizUtils.js`, `campusScheduleUtils.js`, and `errorHelpers.js`. These are pure functions with no side effects — easiest to test and highest confidence value.

```bash
# Run tests once
npm test -- --watchAll=false

# Run with coverage
npm test -- --coverage --watchAll=false
```

### Priority 2: Custom Hook Tests
Test `useColleges` hook with mock Firestore data:
```javascript
import { renderHook, act } from '@testing-library/react'
import { useColleges } from './useColleges'

test('loads colleges on mount', async () => {
  const { result } = renderHook(() => useColleges())
  expect(result.current.loading).toBe(true)
  await act(async () => {}) // wait for async
  expect(result.current.loading).toBe(false)
})
```

### Priority 3: Critical Component Tests
Test the quiz submission guard to prevent regressions:
```javascript
test('prevents double submission', async () => {
  // render quiz page
  // click submit twice rapidly
  // assert write was called only once
})
```

### Priority 4: End-to-End Tests
Use Playwright or Cypress for full user journey tests:
```javascript
// example with Playwright
test('student completes quiz', async ({ page }) => {
  await page.goto('/signin')
  await page.fill('[name=email]', 'student@test.com')
  await page.fill('[name=password]', 'test123')
  await page.click('[type=submit]')
  await page.waitForURL('/student')
  
  await page.click('text=Quizzes')
  await page.click('text=Available Quiz')
  // ... complete quiz
})
```

---

## 7. CI/CD Testing (Not Currently Configured)

Recommended GitHub Actions workflow:

```yaml
# .github/workflows/test.yml
name: Test
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with: { node-version: '18' }
      - run: npm ci
      - run: npm test -- --coverage --watchAll=false --passWithNoTests
      - run: npm run build
```

This would run on every push and fail if tests fail or the build breaks.
