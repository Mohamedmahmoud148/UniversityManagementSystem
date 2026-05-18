# 📡 API Reference — New Registration & Academic Endpoints

> All endpoints require `Authorization: Bearer {token}` header.  
> All responses are wrapped in `ApiResponse<T>` → read `data` field for payload.

---

## 1. GET `/api/registration/eligible-offerings`

**Role:** Student only  
**Purpose:** The heart of the registration page. Returns every offering for a semester with full eligibility analysis. Call this FIRST before rendering the registration page.

### Query Parameters
| Param | Type | Required | Example |
|---|---|---|---|
| `semesterId` | ULID string | ✅ | `01JXXXXXXXXXXXXXXXXXXXXXXXXX` |

### Request
```
GET /api/registration/eligible-offerings?semesterId=01JXXXXXXXXXXXXXXXXXXXXXXXXX
Authorization: Bearer {student_token}
```

### Response (200 OK)
```json
{
  "success": true,
  "message": "Found 8 offerings.",
  "data": [
    {
      "offeringId":    "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
      "subjectName":   "Data Structures",
      "subjectCode":   "CS301",
      "creditHours":   3,
      "doctorName":    "Dr. Ahmed Hassan",
      "semesterName":  "Fall 2026",
      "maxCapacity":   120,
      "enrolledCount": 87,
      "waitlistCount": 3,
      "isFull":        false,
      "isEligible":    true,
      "blockers":      [],
      "warnings":      []
    },
    {
      "offeringId":    "01JXXXXXXXXXXXXXXXXXXXXXXXXY",
      "subjectName":   "Advanced AI",
      "subjectCode":   "CS501",
      "creditHours":   3,
      "doctorName":    "Dr. Sara Mostafa",
      "semesterName":  "Fall 2026",
      "maxCapacity":   60,
      "enrolledCount": 60,
      "waitlistCount": 5,
      "isFull":        true,
      "isEligible":    false,
      "blockers":      [
        "Prerequisite not completed: Operating Systems",
        "Must PASS prerequisite: Algorithms (currently: F)"
      ],
      "warnings":      []
    },
    {
      "offeringId":    "01JXXXXXXXXXXXXXXXXXXXXXXXXZ",
      "subjectName":   "Software Engineering",
      "subjectCode":   "CS401",
      "creditHours":   3,
      "doctorName":    "Dr. Omar Said",
      "semesterName":  "Fall 2026",
      "maxCapacity":   80,
      "enrolledCount": 78,
      "waitlistCount": 2,
      "isFull":        false,
      "isEligible":    true,
      "blockers":      [],
      "warnings":      [
        "⚠️ Academic Warning: GPA 1.85. Max 12 hours.",
        "Offering almost full (78/80)"
      ]
    }
  ]
}
```

### Possible Blocker Messages (show exactly as-is in red)
| Message Pattern | Meaning |
|---|---|
| `"You are already enrolled in this offering."` | Duplicate enrollment attempt |
| `"You have already passed this subject."` | Subject already completed |
| `"Prerequisite not completed: {SubjectName}"` | Must complete prerequisite first |
| `"Must PASS prerequisite: {SubjectName} (currently: {Grade})"` | Failed prerequisite |
| `"Minimum score {N} required in {SubjectName} (scored {X})"` | Below minimum prereq grade |
| `"Credit hours limit exceeded: {current} + {adding} = {total} > max {max} (GPA: {gpa})"` | Over credit limit |
| `"Your account is suspended. Contact academic affairs."` | Suspended student |

### Possible Warning Messages (show in yellow/amber)
| Message Pattern | Meaning |
|---|---|
| `"⚠️ Academic Warning: GPA {X}. Max {N} hours."` | Student on academic warning |
| `"⚠️ Probation: GPA {X} < {threshold}. Max {N} hours."` | Student on probation |
| `"Offering almost full ({enrolled}/{max})"` | Near capacity |
| `"Offering is full ({enrolled}/{max}). You can join the waitlist (position {N})."` | At capacity — waitlist available |

### Frontend Behavior
- Show eligible offerings FIRST, blocked ones AFTER (backend already sorts them).
- Eligible (`isEligible: true`) → green "Enroll" button.
- Blocked (`isEligible: false, blockers.length > 0`) → gray disabled button + red blocker pills.
- Full + eligible warnings → amber "Join Waitlist" button.
- Show `enrolledCount / maxCapacity` as a progress bar on each card.
- Show `waitlistCount` as a badge if > 0.

---

## 2. POST `/api/registration/enroll/{offeringId}`

**Role:** Student only  
**Purpose:** Register the student in an offering. Runs full validation pipeline server-side. If offering is full, auto-adds to waitlist.

### Path Parameter
| Param | Type | Example |
|---|---|---|
| `offeringId` | ULID string in path | `/api/registration/enroll/01JXXX...` |

### Request
```
POST /api/registration/enroll/01JXXXXXXXXXXXXXXXXXXXXXXXXX
Authorization: Bearer {student_token}
(no body)
```

### Response — Success (200 OK)
```json
{
  "success": true,
  "message": "Successfully enrolled in Data Structures.",
  "data": {
    "success":         true,
    "addedToWaitlist": false,
    "waitlistPosition": null,
    "message":         "Successfully enrolled in Data Structures.",
    "errors":          [],
    "warnings":        []
  }
}
```

### Response — Added to Waitlist (200 OK — NOT an error)
```json
{
  "success": true,
  "message": "Added to waitlist at position 4. You will be notified when a spot opens.",
  "data": {
    "success":         false,
    "addedToWaitlist": true,
    "waitlistPosition": 4,
    "message":         "Added to waitlist at position 4. You will be notified when a spot opens.",
    "errors":          [],
    "warnings":        ["Offering is full (120/120). You can join the waitlist (position 4)."]
  }
}
```

### Response — Blocked (409 Conflict)
```json
{
  "success": false,
  "message": "Registration blocked.",
  "errors":  [
    "Prerequisite not completed: Operating Systems"
  ]
}
```

### Frontend Behavior
- On **200 with `addedToWaitlist: false`** → show green success toast → update card to "Enrolled" state.
- On **200 with `addedToWaitlist: true`** → show amber toast "Added to waitlist (#4)" → update card to waitlist state.
- On **409** → show red toast with `errors[0]` message → button returns to "Enroll" state.
- Disable the enroll button immediately on click (prevent double submit).

---

## 3. POST `/api/registration/waitlist/{offeringId}`

**Role:** Student only  
**Purpose:** Manually join the waitlist without attempting enrollment.

### Response (200 OK)
```json
{
  "success": true,
  "message": "Added to waitlist at position 3.",
  "data": {
    "success":  true,
    "position": 3,
    "message":  "Added to waitlist at position 3. You will be notified when a spot opens."
  }
}
```

---

## 4. DELETE `/api/registration/waitlist/{offeringId}`

**Role:** Student only  
**Purpose:** Remove from waitlist.

### Response (200 OK)
```json
{
  "success": true,
  "message": "Removed from waitlist.",
  "data":    null
}
```

---

## 5. GET `/api/registration/academic-status`

**Role:** Student only  
**Purpose:** GPA dashboard data. Call this on the student dashboard home page.

### Response (200 OK)
```json
{
  "success": true,
  "data": {
    "studentId":       "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
    "studentName":     "محمد محمود",
    "gpa":             3.42,
    "cgpa":            3.38,
    "lastSemesterGPA": 3.65,
    "standing":        "Good",
    "standingColor":   "green",
    "earnedHours":     87,
    "remainingHours":  58,
    "totalRequired":   145,
    "currentLevel":    3,
    "maxAllowedHours": 18,
    "warningCount":    0,
    "hasWarning":      false,
    "warningMessage":  null
  }
}
```

### Standing Values
| `standing` | `standingColor` | Meaning |
|---|---|---|
| `"Good"` | `"green"` | GPA ≥ 2.0 — normal standing |
| `"Warning"` | `"yellow"` | GPA < 2.0 (1st occurrence) — max 12h |
| `"Probation"` | `"orange"` | GPA < 1.5 (repeated) — max 9h |
| `"Suspended"` | `"red"` | Cannot register at all |
| `"Graduated"` | `"blue"` | Completed all requirements |
| `"Expelled"` | `"gray"` | Terminated |

### `warningMessage` (show as dismissible alert banner when not null)
```
"Academic warning: GPA 1.85 is below 2.0. Max 12 credit hours this semester."
"Academic probation: GPA 1.40 is below 1.5. Max 9 credit hours. Improve GPA to avoid suspension."
"Your academic enrollment is suspended. Please contact the academic affairs office."
```

---

## 6. GET `/api/registration/my-enrollments-summary`

**Role:** Student only  
**Purpose:** Quick dashboard widget showing current semester enrolled subjects.

### Response (200 OK)
```json
{
  "success": true,
  "data": {
    "totalEnrollments": 4,
    "totalCreditHours": 12,
    "subjects": [
      {
        "subjectName":  "Data Structures",
        "subjectCode":  "CS301",
        "creditHours":  3,
        "doctorName":   "Dr. Ahmed Hassan",
        "semesterName": "Fall 2026",
        "enrolledAt":   "2026-05-18T10:30:00Z"
      }
    ]
  }
}
```

---

## 7. GET `/api/registration/prerequisites/{subjectId}`

**Role:** Admin, SuperAdmin, Doctor  
**Purpose:** View prerequisites for a subject.

### Response (200 OK)
```json
{
  "success": true,
  "data": [
    {
      "id":             "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
      "prerequisiteId": "01JXXXXXXXXXXXXXXXXXXXXXXXXY",
      "name":           "Algorithms",
      "code":           "CS201",
      "minimumGrade":   null
    }
  ]
}
```

---

## 8. POST `/api/registration/prerequisites`

**Role:** Admin, SuperAdmin  
**Purpose:** Add a prerequisite relationship between two subjects.

### Request Body
```json
{
  "subjectId":             "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
  "prerequisiteSubjectId": "01JXXXXXXXXXXXXXXXXXXXXXXXXY",
  "minimumGrade":          70.0
}
```
`minimumGrade` is optional. If null, any passing grade (D or above) is accepted.

### Error Responses
```json
{ "success": false, "message": "A subject cannot be its own prerequisite." }
{ "success": false, "message": "Prerequisite already exists." }
```

---

## 9. GET `/api/registration/policy`

**Role:** Admin, SuperAdmin  
**Purpose:** View the active academic policy (credit hour limits, GPA thresholds).

### Query Parameters
| Param | Required | Description |
|---|---|---|
| `departmentId` | ❌ Optional | If omitted, returns global policy |

### Response (200 OK)
```json
{
  "success": true,
  "data": {
    "id":                   "01JXXXXXXXXXXXXXXXXXXXXXXXXX",
    "departmentId":         null,
    "defaultMaxHours":      18,
    "honorMaxHours":        21,
    "warningMaxHours":      12,
    "probationMaxHours":    9,
    "warningGpaThreshold":  2.0,
    "probationGpaThreshold":1.5,
    "honorGpaThreshold":    3.5,
    "graduationMinGpa":     2.0
  }
}
```
