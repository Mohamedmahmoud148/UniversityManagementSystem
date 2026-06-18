# 13 — Forms and Validation
## Form Architecture, Validation Rules, and Submission Flows

---

## 1. Form Architecture

The project uses **controlled components** for all forms. This means:
- Every input's `value` is driven by React state
- Every input change updates state via `onChange` handler
- Submission reads from state (not DOM)

There is no form library (no React Hook Form, no Formik). All validation is manual.

### Standard Form Pattern

```javascript
// State
const [formData, setFormData] = useState({
  name: '',
  email: '',
  password: '',
  role: 'student'
})
const [errors, setErrors]   = useState({})
const [loading, setLoading] = useState(false)
const [submitError, setSubmitError] = useState(null)

// Generic field handler
const handleChange = (field) => (e) => {
  setFormData(prev => ({ ...prev, [field]: e.target.value }))
  // Clear field error on change
  if (errors[field]) setErrors(prev => ({ ...prev, [field]: null }))
}

// Validation
const validate = () => {
  const newErrors = {}
  if (!formData.name.trim()) newErrors.name = 'Name is required'
  if (!formData.email.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/))
    newErrors.email = 'Invalid email address'
  if (formData.password.length < 6)
    newErrors.password = 'Password must be at least 6 characters'
  setErrors(newErrors)
  return Object.keys(newErrors).length === 0
}

// Submit handler
const handleSubmit = async (e) => {
  e.preventDefault()
  if (!validate()) return
  setLoading(true)
  try {
    await createUser(formData)
    onSuccess()
  } catch (err) {
    setSubmitError(err.message)
  } finally {
    setLoading(false)
  }
}
```

---

## 2. Form Inventory

### 2.1 Login Form (SignIn)

**Fields:**
| Field | Type | Validation |
|-------|------|-----------|
| Email | email input | Required, valid email format |
| Password | password input | Required |

**Submit:** `signInWithEmailAndPassword(auth, email, password)`

**Error Handling:**
```javascript
switch (error.code) {
  case 'auth/wrong-password':    msg = 'Incorrect password'; break
  case 'auth/user-not-found':    msg = 'No account with this email'; break
  case 'auth/invalid-email':     msg = 'Invalid email format'; break
  case 'auth/too-many-requests': msg = 'Account temporarily locked'; break
  default:                       msg = 'Sign in failed. Please try again.'
}
```

---

### 2.2 Create User Form (CreateAdminUser)

**Fields:**
| Field | Type | Validation | Notes |
|-------|------|-----------|-------|
| Full Name | text | Min 2 chars | |
| Email | email | Valid format, unique | |
| Password | password | Min 6 chars | |
| Role | select | Required | student/professor/assistant/admin |
| Phone | text | Optional, 10-11 digits | |
| College | select | Required for student | Cascade: college → year → dept |
| Year | select | Required for student | Depends on college selection |
| Department | select | Required for student | Depends on year selection |

**Submit Flow:**
1. Validate all fields
2. `createUserWithEmailAndPassword(secondaryAuth, email, password)` — secondary app
3. `createUserProfile(db, uid, formData)` — writes to multiple collections
4. Role-specific: for professor/assistant, call `setUserRole` function (super_admin only)
5. Close modal, refresh user list

**Why Secondary App?** Creating a user with Firebase Auth signs that user in. To prevent the admin from being signed out, a second Firebase app instance (`secondaryAuth`) handles the creation.

---

### 2.3 Excel Import Validation (BulkImportUsersPage)

**File parsed with `xlsx` library:**

**Row Validation:**
```javascript
const validateRow = (row, index) => {
  const errors = []
  
  // Username
  if (!row.username || row.username.trim().length < 2)
    errors.push(`Row ${index + 1}: Username must be at least 2 characters`)
  
  // Email
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  if (!emailRegex.test(row.email))
    errors.push(`Row ${index + 1}: Invalid email format`)
  
  // Password
  if (!row.password || row.password.length < 6)
    errors.push(`Row ${index + 1}: Password must be at least 6 characters`)
  
  // Role
  const validRoles = ['student', 'professor', 'assistant', 'admin']
  if (!validRoles.includes(row.role))
    errors.push(`Row ${index + 1}: Role must be one of: ${validRoles.join(', ')}`)
  
  return errors
}
```

**Column Name Normalization:**
The parser handles multiple spellings via a normalization step:
```javascript
const normalizeHeaders = (headers) => {
  const map = {
    'full name': 'username',
    'user name': 'username',
    'phone number': 'phone',
    'e mail': 'email',
    'e-mail': 'email'
  }
  return headers.map(h => map[h.toLowerCase().trim()] || h.toLowerCase().trim())
}
```

---

### 2.4 Quiz Creation Form (ProfessorQuizzesPage)

**Fields:**
| Field | Validation |
|-------|-----------|
| Title | Required, min 3 chars |
| Description | Optional |
| Target College | Required |
| Target Year | Required |
| Target Department | Required |
| Start Time | Required, must be in future |
| Duration (minutes) | Required, min 5, max 180 |
| Question Type | Required: "mcq" or "trueFalse" |
| Questions | At least 1 question required |

**Per-Question Validation (MCQ):**
| Field | Validation |
|-------|-----------|
| Question text | Required |
| Option A-D | All 4 required |
| Correct answer | Must be one of the options |

**Per-Question Validation (True/False):**
| Field | Validation |
|-------|-----------|
| Question text | Required |
| Correct answer | Must be "True" or "False" |

**Special Validation — Start Time:**
```javascript
if (startTime <= new Date()) {
  errors.startTime = 'Start time must be in the future'
}
```

---

### 2.5 Material Upload Form (AddMaterialModal, AssistantUploadModal)

**Fields:**
| Field | Validation |
|-------|-----------|
| Lecture Title | Required |
| Lecture Number | Required, positive integer |
| Notes | Optional |
| PDF File | Required, must be PDF |

**File Type Validation:**
```javascript
const validateFile = (file) => {
  if (!file) return 'File is required'
  if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf'))
    return 'Only PDF files are accepted'
  if (file.size > 50 * 1024 * 1024)
    return 'File size must be less than 50MB'
  return null
}
```

**Why validate MIME type AND extension?** MIME type is set by the browser based on the OS file associations — it can sometimes be wrong. Checking the extension as a fallback catches edge cases.

---

### 2.6 Course Assignment Form (CreateCourseAssignment, AssignmentsPage)

**Fields:**
| Field | Validation |
|-------|-----------|
| Course | Required |
| Term ID | Required |
| Term Label | Required |
| Professor(s) | At least 1 required |
| Assistant(s) | Optional |

**Duplicate Prevention:**
```javascript
const checkDuplicate = async (courseId, termId) => {
  const q = query(
    collection(db, 'courseAssignments'),
    where('courseId', '==', courseId),
    where('termId', '==', termId)
  )
  const snap = await getDocs(q)
  return !snap.empty  // returns true if duplicate exists
}

// Before creating:
if (await checkDuplicate(formData.courseId, formData.termId)) {
  setError('A professor is already assigned to this course for this term')
  return
}
```

---

### 2.7 Room Schedule Form (RoomSchedulePage)

**Fields:**
| Field | Validation |
|-------|-----------|
| Day | Selected from grid (sat-thu) |
| Time slot | Selected from grid (09-11, 11-13, etc.) |
| Course | Required |
| Section | Optional |

**Collision Detection (Transaction-based):**
```javascript
await runTransaction(db, async (tx) => {
  const slotRef = doc(db, `.../${dayKey}_${slotKey}`)
  const existing = await tx.get(slotRef)
  if (existing.exists() && existing.data().status !== 'available') {
    throw new Error('This slot is already booked')
  }
  tx.set(slotRef, { courseId, courseName, dayKey, slotKey, status: 'reserved' })
})
```

The transaction ensures that if two admins simultaneously try to book the same slot, only one succeeds.

---

## 3. Error Display Patterns

### Inline Field Errors
```jsx
<TextField
  label="Email"
  value={formData.email}
  onChange={handleChange('email')}
  error={!!errors.email}             // red border when error exists
  helperText={errors.email}          // error message below field
/>
```

### Form-Level Error
{% raw %}
```jsx
{submitError && (
  <Alert severity="error" sx={{ mb: 2 }}>
    {submitError}
  </Alert>
)}
```
{% endraw %}

### Loading State on Submit Button
```jsx
<Button
  type="submit"
  disabled={loading}
  startIcon={loading ? <CircularProgress size={20} /> : null}
>
  {loading ? 'Creating...' : 'Create User'}
</Button>
```

---

## 4. Form Reset Pattern

After successful submission, forms are reset:

```javascript
const resetForm = () => {
  setFormData({ title: '', description: '', durationMinutes: 30 })
  setErrors({})
  setSubmitError(null)
  setFile(null)
}

const handleSubmit = async () => {
  // ... submit logic ...
  resetForm()
  onClose()
}
```

---

## 5. Cascade Selects

The user creation form and quiz creation form use cascade selects — selecting one field populates options for the next:

```javascript
// When college changes, reset year and department
const handleCollegeChange = async (collegeId) => {
  setSelectedCollegeId(collegeId)
  setSelectedYearId('')
  setSelectedDeptId('')
  setYears([])
  setDepts([])
  
  if (collegeId) {
    const yearsData = await fetchYears(collegeId)
    setYears(yearsData)
  }
}

// When year changes, reset department
const handleYearChange = async (yearId) => {
  setSelectedYearId(yearId)
  setSelectedDeptId('')
  setDepts([])
  
  if (yearId && selectedCollegeId) {
    const deptsData = await fetchDepartments(selectedCollegeId, yearId)
    setDepts(deptsData)
  }
}
```

This prevents invalid combinations (e.g., selecting a department that belongs to a different college).

---

## 6. Common Form Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|---------|
| Form submits twice | Button not disabled during loading | Add `disabled={loading}` to submit button |
| Stale validation errors | Error not cleared on input change | Clear field error on `onChange` |
| File input not clearing | HTML file inputs can't be controlled | Use a `key` prop to force re-mount after upload |
| Cascade select out of sync | Year not reset when college changes | Always reset downstream fields when upstream changes |
| Firebase error not shown | Only catching client validation, not API errors | Catch API errors in `handleSubmit` and set `submitError` |
