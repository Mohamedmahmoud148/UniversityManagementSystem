---
render_with_liquid: false
---

# Frontend Integration Report
## Backend API Changes — Action Required

> **Date:** 2026-06-18  
> **From:** Backend Team  
> **To:** Frontend Team (React)  
> **Priority:** High — Breaking changes in `/api/Regulations/my-roadmap`  
> **Branch:** `main` — commit `edc2b66`

---

## Summary

The `GET /api/Regulations/my-roadmap` endpoint was completely redesigned.  
The response shape has **changed significantly**. Any page or component that calls this endpoint must be updated.

Additionally, one **environment variable bug** was found in the frontend codebase that prevents the .NET API from working at all — needs an immediate fix.

---

## 1. CRITICAL — Environment Variable Bug (axios never reaches .NET API)

**File:** `src/services/http.js`

**Problem:**
```javascript
// CURRENT (BROKEN) — Vite syntax, CRA ignores this
const api = axios.create({
  baseURL: process.env.VITE_API_BASE || 'http://localhost:5000'
})
```

**Fix:**
```javascript
// CORRECT — CRA syntax
const api = axios.create({
  baseURL: process.env.REACT_APP_API_BASE || 'http://localhost:5000'
})
```

**Impact:** All pages using `http.js` (Axios) to call the .NET backend are currently using the hardcoded fallback URL `http://localhost:5000` in all environments — production included. The `.env` variable is silently ignored.

**Action:** Fix the variable name + add `REACT_APP_API_BASE=https://your-backend-url.com` to `.env` and to Firebase Hosting environment config.

---

## 2. BREAKING CHANGE — `/api/Regulations/my-roadmap` Response Shape

### What changed and why

The old API grouped courses by **static regulation semester numbers** (int 1, 2, 3…). The new API groups by **real Semester entities** with actual dates, names, and GPA per semester. This gives students a real journey-based view instead of a theoretical curriculum view.

---

### 2.1 Top-Level Response (`AcademicRoadmapDto`)

#### Fields that CHANGED

| Field | Old | New | Notes |
|-------|-----|-----|-------|
| `regulationId` | `string` (required) | `string \| null` | Now nullable — student can have roadmap without a regulation assigned |
| `regulationTitle` | `string` (required) | `string \| null` | Now nullable |

#### Fields that are NEW (must add to TypeScript types)

```typescript
// Add these to your AcademicRoadmapDto type:
studentId:                  string
studentName:                string
studentCode:                string
graduationProgressPercent:  number    // 0–100, how close to graduation
recommendations:            string[]  // AI-generated advice list
academicWarnings:           string[]  // urgent warnings (low GPA, failed required, etc.)
```

#### Fields that STAYED THE SAME
`departmentName`, `collegeName`, `batchName`, `totalSemesters`, `totalCreditHours`, `completedCreditHours`, `remainingCreditHours`, `totalSubjects`, `passedSubjects`, `failedSubjects`, `currentlyEnrolled`, `currentGpa`, `semesters`, `recommendedNext`, `mustRetake`

---

### 2.2 Semester Object (`SemesterRoadmapDto`)

#### Fields that are NEW

```typescript
// Add to your SemesterRoadmapDto type:
semesterId:           string     // e.g. "01JFD5K3..."
semesterName:         string     // e.g. "الفصل الدراسي الأول"
academicYearName:     string     // e.g. "2024-2025"
startDate:            string     // ISO date "2024-09-01T00:00:00"
endDate:              string     // ISO date "2025-01-15T00:00:00"
semesterGpa:          number | null   // GPA for this semester only
cumulativeGpaAfter:   number | null   // cumulative GPA after this semester
withdrawnSubjects:    number     // count of withdrawn courses this semester
```

#### Fields that STAYED THE SAME
`semesterNumber`, `status`, `totalSubjects`, `passedSubjects`, `failedSubjects`, `enrolledSubjects`, `totalCreditHours`, `earnedCreditHours`, `subjects`

#### IMPORTANT — `semesterNumber` meaning changed
Old: linked to the regulation (semester 1 of the curriculum).  
New: **chronological position** in the student's real journey (1 = first real semester they enrolled in).

---

### 2.3 Subject Object (`SubjectStatusDto`)

#### Fields that are NEW

```typescript
// Add to your SubjectStatusDto type:
isRetake:      boolean               // true if student took this subject before
retakeCount:   number                // how many times retaken (0 if first attempt)
activities:    RoadmapActivityDto[]  // assignments + exams for this course
```

#### Status field — NEW value added

```typescript
// Old possible values:
status: "passed" | "failed" | "enrolled" | "upcoming"

// New possible values (add "in_progress" and "withdrawn"):
status: "passed" | "failed" | "in_progress" | "withdrawn" | "upcoming"
```

**Action:** Update all `switch/if` statements that check `subject.status` — add handling for `"in_progress"` and `"withdrawn"`.

---

### 2.4 New Object — `RoadmapActivityDto`

This is a **new type** that didn't exist before. Each subject now includes a list of its assignments and exams.

```typescript
interface RoadmapActivityDto {
  id:          string
  type:        "assignment" | "quiz" | "midterm" | "final"
  title:       string
  dueDate:     string     // ISO date
  submittedAt: string | null
  status:      "pending" | "submitted" | "graded" | "overdue" | "missed"
  score:       number | null    // student's score
  maxScore:    number           // maximum possible score
}
```

---

## 3. Complete Updated TypeScript Types

Copy this into your types file (e.g., `src/types/roadmap.ts`):

```typescript
export interface RoadmapActivityDto {
  id:          string
  type:        'assignment' | 'quiz' | 'midterm' | 'final'
  title:       string
  dueDate:     string
  submittedAt: string | null
  status:      'pending' | 'submitted' | 'graded' | 'overdue' | 'missed'
  score:       number | null
  maxScore:    number
}

export interface SubjectStatusDto {
  subjectId:    string
  subjectName:  string
  subjectCode:  string
  creditHours:  number
  isRequired:   boolean
  status:       'passed' | 'failed' | 'in_progress' | 'withdrawn' | 'upcoming'
  gradeLetter:  string | null
  gradePoints:  number | null
  finalScore:   number | null
  isRetake:     boolean         // NEW
  retakeCount:  number          // NEW
  activities:   RoadmapActivityDto[]  // NEW
}

export interface SemesterRoadmapDto {
  semesterId:          string    // NEW
  semesterName:        string    // NEW
  academicYearName:    string    // NEW
  startDate:           string    // NEW
  endDate:             string    // NEW
  semesterNumber:      number    // meaning changed — now chronological position
  status:              'completed' | 'in_progress' | 'upcoming'
  semesterGpa:         number | null   // NEW
  cumulativeGpaAfter:  number | null   // NEW
  totalSubjects:       number
  passedSubjects:      number
  failedSubjects:      number
  enrolledSubjects:    number    // = in_progress courses count
  withdrawnSubjects:   number    // NEW
  totalCreditHours:    number
  earnedCreditHours:   number
  subjects:            SubjectStatusDto[]
}

export interface AcademicRoadmapDto {
  // NEW student fields
  studentId:                  string
  studentName:                string
  studentCode:                string

  // Regulation (now optional)
  regulationId:               string | null   // WAS: string (required)
  regulationTitle:            string | null   // WAS: string (required)

  departmentName:             string
  collegeName:                string
  batchName:                  string

  totalSemesters:             number
  totalCreditHours:           number
  completedCreditHours:       number
  remainingCreditHours:       number
  totalSubjects:              number
  passedSubjects:             number
  failedSubjects:             number
  currentlyEnrolled:          number
  currentGpa:                 number | null

  graduationProgressPercent:  number    // NEW — 0 to 100
  
  semesters:                  SemesterRoadmapDto[]
  recommendedNext:            SubjectStatusDto[]
  mustRetake:                 SubjectStatusDto[]
  
  recommendations:            string[]  // NEW — AI advice
  academicWarnings:           string[]  // NEW — urgent warnings
}
```

---

## 4. What to Update in the Frontend Code

### 4.1 Find all files that call this endpoint

Search in your project for:
```
/my-roadmap
regulations/my-roadmap
Regulations/my-roadmap
```

These files need updating:
- `src/pages/student/StudentRoadmapPage.jsx` (or similar)
- Any component that renders semester/subject cards from roadmap data

### 4.2 Fix the status switch

```javascript
// BEFORE:
const getStatusColor = (status) => {
  switch (status) {
    case 'passed':   return 'green'
    case 'failed':   return 'red'
    case 'enrolled': return 'blue'    // ← OLD name, still works but "enrolled" 
                                      //   won't appear in new data
    case 'upcoming': return 'gray'
  }
}

// AFTER:
const getStatusColor = (status) => {
  switch (status) {
    case 'passed':      return 'green'
    case 'failed':      return 'red'
    case 'in_progress': return 'blue'    // NEW — replaces "enrolled" for active courses
    case 'withdrawn':   return 'orange'  // NEW
    case 'upcoming':    return 'gray'
    default:            return 'gray'
  }
}
```

### 4.3 Handle nullable regulation

```javascript
// BEFORE (could crash if regulation is null):
<h2>{roadmap.regulationTitle}</h2>

// AFTER:
<h2>{roadmap.regulationTitle ?? 'لم يتم تعيين لائحة'}</h2>
```

### 4.4 Display new fields (optional but recommended)

```jsx
// Show per-semester GPA
{semester.semesterGpa && (
  <p>GPA هذا الفصل: {semester.semesterGpa.toFixed(2)}</p>
)}
{semester.cumulativeGpaAfter && (
  <p>GPA التراكمي: {semester.cumulativeGpaAfter.toFixed(2)}</p>
)}

// Show real semester name instead of "Semester 1, 2, 3..."
<h3>{semester.semesterName} — {semester.academicYearName}</h3>

// Show graduation progress bar
<LinearProgress value={roadmap.graduationProgressPercent} variant="determinate" />
<p>{roadmap.graduationProgressPercent}% من متطلبات التخرج</p>

// Show AI recommendations
{roadmap.recommendations.map((rec, i) => (
  <Alert key={i} severity="info">{rec}</Alert>
))}

// Show academic warnings
{roadmap.academicWarnings.map((warn, i) => (
  <Alert key={i} severity="warning">{warn}</Alert>
))}

// Show retake badge on subject
{subject.isRetake && (
  <Chip label={`إعادة (${subject.retakeCount})`} color="warning" size="small" />
)}

// Show activities for a subject
{subject.activities.map(activity => (
  <div key={activity.id}>
    <span>{activity.title}</span>
    <span>{activity.status}</span>
    {activity.score !== null && (
      <span>{activity.score}/{activity.maxScore}</span>
    )}
  </div>
))}
```

---

## 5. No-Change APIs (Nothing to update)

These APIs were **not changed** — frontend code calling them works as-is:

| Endpoint | Status |
|----------|--------|
| `POST /api/Auth/login` | ✅ Unchanged |
| `GET /api/Students/my-profile` | ✅ Unchanged |
| `GET /api/Enrollments/my-enrollments` | ✅ Unchanged |
| `GET /api/Grades/my-grades` | ✅ Unchanged |
| `GET /api/Announcements` | ✅ Unchanged |
| `GET /api/Subjects` | ✅ Unchanged |
| All Admin endpoints | ✅ Unchanged |

---

## 6. Summary Checklist for Frontend Team

```
[ ] Fix REACT_APP_API_BASE in src/services/http.js  (CRITICAL — blocks all .NET calls)
[ ] Add REACT_APP_API_BASE to .env file
[ ] Update AcademicRoadmapDto TypeScript type (nullable regulation, new fields)
[ ] Update SemesterRoadmapDto TypeScript type (real semester data, GPA fields)
[ ] Update SubjectStatusDto TypeScript type (isRetake, retakeCount, activities)
[ ] Add RoadmapActivityDto TypeScript type (new)
[ ] Update status switch/if to handle "in_progress" and "withdrawn"
[ ] Guard against null regulationId/regulationTitle
[ ] (Optional) Display new GPA per semester
[ ] (Optional) Display graduation progress bar
[ ] (Optional) Display AI recommendations + warnings
[ ] (Optional) Display retake badge on subjects
[ ] (Optional) Display activity list per subject (assignments + exams)
```

---

## 7. Backend Contact

For questions about the API changes, see:
- `UniversityManagementSystem.Api/Controllers/RegulationsController.cs` — `GetMyRoadmap` method
- `UniversityManagementSystem.Core/DTOs/RegulationDtos.cs` — all DTO definitions
- Git commit: `edc2b66`
