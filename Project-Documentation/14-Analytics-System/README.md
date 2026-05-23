# 📊 Analytics System — Complete Guide

## Overview

The analytics system provides **real-time institutional intelligence** for admins. All endpoints are optimized for performance using `AsNoTracking()`, batch loading, and projection queries — no full entity loading.

All analytics endpoints are at `/api/analytics/` and require `Admin` or `SuperAdmin` role.

---

## Available Analytics Endpoints

### 1. GET /api/analytics/summary
**The main admin dashboard endpoint — loads everything at once.**

```json
{
  "totalStudents":    1250,
  "totalDoctors":     85,
  "totalOfferings":   340,
  "totalEnrollments": 4800,
  "totalColleges":    9,
  "totalDepartments": 42,
  "totalBatches":     168,
  "topDepartments": [
    {
      "departmentId":   "01H...",
      "departmentName": "Computer Science",
      "collegeName":    "Engineering",
      "studentCount":   450,
      "doctorCount":    28
    }
  ],
  "topSubjects": [
    {
      "subjectId":    "01H...",
      "subjectCode":  "CS301",
      "subjectName":  "Data Structures",
      "offeringCount": 8,
      "enrolledCount": 380
    }
  ]
}
```

**Implementation:** 7 parallel COUNT queries + 2 aggregation queries, all in one round-trip using EF Core multi-query batching.

---

### 2. GET /api/analytics/student-count-by-department
**Shows student and doctor distribution across all departments.**

```json
[
  {
    "departmentId":   "01H...",
    "departmentName": "Computer Science",
    "collegeName":    "Engineering",
    "studentCount":   450,
    "doctorCount":    28
  },
  {
    "departmentId":   "01H...",
    "departmentName": "Electronics Engineering",
    "collegeName":    "Engineering",
    "studentCount":   380,
    "doctorCount":    22
  }
]
```

Sorted by `studentCount` descending.

**Query strategy:**
```csharp
// 3 separate queries, then joined in memory:
var studentCounts = GROUP BY DepartmentId → COUNT
var doctorCounts  = GROUP BY DepartmentId → COUNT
var departments   = SELECT all departments with College
// Merge using dictionary lookups → O(n) join
```

---

### 3. GET /api/analytics/student-count-by-batch
**Shows student distribution per batch/year.**

```json
[
  {
    "batchId":        "01H...",
    "batchName":      "Batch 2022",
    "batchCode":      "CS2022",
    "departmentName": "Computer Science",
    "collegeName":    "Engineering",
    "studentCount":   145
  }
]
```

---

### 4. GET /api/analytics/doctor-workload
**Shows each doctor's teaching load.**

**Query:** `?departmentId=&collegeId=`

```json
[
  {
    "doctorId":       "01H...",
    "doctorCode":     "DOC001",
    "fullName":       "Dr. Ahmed Mohamed",
    "departmentName": "Computer Science",
    "offeringCount":  6,
    "totalStudents":  380
  }
]
```

Sorted by `totalStudents` descending — shows busiest doctors first.

**Critical Implementation Note (SubjectOffering has no Enrollments nav property):**
```csharp
// CANNOT do: offering.Enrollments.Count() — navigation doesn't exist!

// CORRECT approach — batch load enrollment counts separately:
var workload = await offeringsQ
    .GroupBy(o => o.DoctorId)
    .Select(g => new {
        DoctorId = g.Key,
        OfferingCount = g.Count(),
        OfferingIds = g.Select(o => o.Id).ToList()
    })
    .ToListAsync();

var allOfferingIds = workload.SelectMany(w => w.OfferingIds).ToList();

var enrollmentCounts = await _context.Enrollments
    .Where(e => allOfferingIds.Contains(e.SubjectOfferingId) && e.IsActive)
    .GroupBy(e => e.SubjectOfferingId)
    .Select(g => new { g.Key, Count = g.Count() })
    .ToDictionaryAsync(x => x.Key, x => x.Count);

// TotalStudents = sum enrollment counts for this doctor's offerings
TotalStudents = w.OfferingIds.Sum(id => enrollmentCounts.GetValueOrDefault(id, 0))
```

---

### 5. GET /api/analytics/top-enrolled-subjects
**Shows most popular subjects.**

**Query:** `?top=10` (max 100)

```json
[
  {
    "subjectId":    "01H...",
    "subjectCode":  "CS101",
    "subjectName":  "Introduction to Programming",
    "offeringCount": 12,
    "enrolledCount": 580
  }
]
```

Groups by Subject (not SubjectOffering) to aggregate across all sections.

---

### 6. GET /api/analytics/offering-enrollment-stats
**Detailed per-offering stats — fill rate and average grade.**

**Query:** `?departmentId=&batchId=&doctorId=&semesterId=&page=1&size=20`

```json
{
  "data": [
    {
      "offeringId":     "01H...",
      "offeringCode":   "CS301-S1-2024",
      "subjectName":    "Data Structures",
      "doctorName":     "Dr. Ahmed",
      "departmentName": "Computer Science",
      "semesterName":   "Spring 2024",
      "enrolledCount":  55,
      "maxCapacity":    60,
      "fillRate":       91.7,
      "averageGrade":   75.3
    }
  ],
  "totalCount": 340,
  "page": 1,
  "size": 20
}
```

**Key metrics:**
- `fillRate` = (enrolled / maxCapacity) × 100 — shows capacity utilization
- `averageGrade` = mean FinalScore across finalized grades — shows course difficulty

---

## How Analytics Connect to AI

When an admin asks the AI:

| Question | AI Rule | Endpoint Called |
|---------|---------|----------------|
| "كام طالب في قسم CS؟" | Rule G1 | `/api/analytics/student-count-by-department` |
| "أكتر مادة فيها طلاب؟" | Rule G4 | `/api/analytics/top-enrolled-subjects` |
| "الدكتور الأكتر تدريساً؟" | Rule G3 | `/api/analytics/doctor-workload` |
| "ملخص الجامعة؟" | Rule G6 | `/api/analytics/summary` |
| "الطلاب بالدفعة؟" | Rule G2 | `/api/analytics/student-count-by-batch` |
| "إحصائيات التسجيل؟" | Rule G5 | `/api/analytics/offering-enrollment-stats` |

---

## Performance Architecture

All analytics endpoints are designed for speed:

```csharp
// 1. AsNoTracking() — no change tracking overhead
var data = await _context.Students.AsNoTracking()...

// 2. Select projection — never load full entities
.Select(g => new DepartmentCountDto { DepartmentId = g.Key, Count = g.Count() })

// 3. GroupBy in DB — aggregation done by PostgreSQL, not in memory
.GroupBy(s => s.DepartmentId)
.Select(g => new { g.Key, Count = g.Count() })

// 4. Batch dictionary loads — one query loads all counts, O(1) lookup
var counts = await ...ToDictionaryAsync(x => x.Key, x => x.Count);
var result = items.Select(i => new { ...counts.GetValueOrDefault(i.Id, 0) })

// 5. Paginated results — never load 10,000 rows
.Skip((page-1) * size).Take(size)
```

---

## Admin Dashboard Recommended Layout

```
┌─────────────────────────────────────────────────────────┐
│  SYSTEM SUMMARY (from /api/analytics/summary)            │
│  1,250 Students  |  85 Doctors  |  9 Colleges  |  ...   │
└─────────────────────────────────────────────────────────┘

┌──────────────────────┐  ┌───────────────────────────────┐
│ Students by Dept     │  │ Top 5 Subjects by Enrollment   │
│ (Bar Chart)          │  │ (Horizontal Bar Chart)         │
│ CS: 450              │  │ CS101: 580 students            │
│ EE: 380              │  │ MATH101: 520 students          │
│ Civil: 290           │  │ CS201: 480 students            │
└──────────────────────┘  └───────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ Doctor Workload Table (from /api/analytics/doctor-workload)│
│ Dr. Ahmed | CS | 6 offerings | 380 students               │
│ Dr. Sara  | EE | 5 offerings | 290 students               │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ Offering Fill Rates (from /api/analytics/offering-...-stats)│
│ CS301-Spring2024 | 55/60 students | 91.7% filled | 75.3 avg│
│ CS302-Spring2024 | 48/60 students | 80.0% filled | 68.1 avg│
└──────────────────────────────────────────────────────────┘
```

---

---

## Phase 4 Dashboard Endpoints (DashboardController)

Nine new endpoints were added to provide role-scoped analytics dashboards. These complement the existing `/api/analytics/*` endpoints with richer, pre-aggregated dashboard views.

### 7. GET /api/analytics/attendance/trends
**Role:** Doctor, Admin  
**Query:** `?offeringId=&weeks=8` (default 8 weeks)

Returns week-by-week attendance percentage for a subject offering.

```json
[
  { "weekStart": "2026-03-01", "weekEnd": "2026-03-07", "attendancePercent": 87.5, "sessionCount": 2 },
  { "weekStart": "2026-03-08", "weekEnd": "2026-03-14", "attendancePercent": 72.0, "sessionCount": 2 }
]
```

---

### 8. GET /api/analytics/grades/distribution
**Role:** Doctor, Admin  
**Query:** `?offeringId=`

Returns grade letter distribution for a subject offering as a histogram.

```json
{
  "offeringId": "01H...",
  "subjectName": "Data Structures",
  "totalStudents": 55,
  "histogram": [
    { "gradeLetter": "A", "count": 10, "percent": 18.2 },
    { "gradeLetter": "B", "count": 18, "percent": 32.7 },
    { "gradeLetter": "C", "count": 15, "percent": 27.3 },
    { "gradeLetter": "D", "count": 7,  "percent": 12.7 },
    { "gradeLetter": "F", "count": 5,  "percent": 9.1 }
  ],
  "averageScore": 72.4
}
```

---

### 9. GET /api/analytics/at-risk-students
**Role:** Admin  
**Query:** `?departmentId=` (optional)

Returns students flagged as at-risk (Medium/High/Critical) from `AcademicRiskScores`.

```json
[
  {
    "studentId": "01H...",
    "studentName": "Ahmed Ali",
    "riskLevel": "High",
    "attendancePercent": 58.0,
    "averageGrade": 51.5,
    "subjectName": "Algorithms",
    "aiRecommendation": "يجب على الطالب تحسين الحضور وطلب ساعات مكتبية — Attendance critical."
  }
]
```

---

### 10. GET /api/analytics/course-performance
**Role:** Admin  
**Query:** `?semesterId=`

Returns performance metrics per subject offering for a semester.

```json
[
  {
    "offeringId":      "01H...",
    "subjectName":     "Data Structures",
    "doctorName":      "Dr. Ahmed",
    "enrolledCount":   55,
    "averageGrade":    74.2,
    "passRate":        89.1,
    "attendanceAvg":   81.3
  }
]
```

---

### 11. GET /api/analytics/student/{studentId}/performance
**Role:** Admin, Doctor (own students), Student (own data)

Returns full performance profile for a specific student.

```json
{
  "studentId":         "01H...",
  "studentName":       "Ahmed Ali",
  "overallGpa":        3.1,
  "totalCreditHours":  87,
  "attendanceAvg":     78.5,
  "riskLevel":         "Low",
  "subjectPerformance": [
    { "subjectName": "Data Structures", "grade": "B", "score": 83.0, "attendance": 85.0 }
  ]
}
```

---

### 12. GET /api/analytics/department/comparison
**Role:** Admin, SuperAdmin

Compares key metrics across all departments.

```json
[
  {
    "departmentId":   "01H...",
    "departmentName": "Computer Science",
    "avgGpa":         3.15,
    "avgAttendance":  79.2,
    "atRiskPercent":  12.4,
    "passRate":       91.0
  }
]
```

---

### 13. GET /api/analytics/dashboard/admin
**Role:** Admin, SuperAdmin

Single endpoint that aggregates the full admin dashboard in one call.

```json
{
  "systemSummary":       { ...same as /api/analytics/summary... },
  "atRiskCount":         47,
  "avgSystemGpa":        2.98,
  "avgSystemAttendance": 76.5,
  "topRiskDepartments":  [ { "departmentName": "...", "atRiskCount": 12 } ],
  "recentAlerts":        [ { "studentName": "...", "riskLevel": "Critical", "subject": "..." } ]
}
```

---

### 14. GET /api/analytics/dashboard/doctor
**Role:** Doctor  
**Query:** `?doctorId=` (Admin can pass any doctorId; Doctor sees own data)

Doctor-scoped dashboard showing performance across all the doctor's offerings.

```json
{
  "doctorName":     "Dr. Ahmed Mohamed",
  "totalOfferings": 4,
  "totalStudents":  180,
  "avgAttendance":  77.3,
  "avgGrade":       73.1,
  "atRiskStudents": 15,
  "offerings": [
    {
      "offeringId":    "01H...",
      "subjectName":   "Data Structures",
      "enrolledCount": 55,
      "avgAttendance": 81.0,
      "avgGrade":      74.2
    }
  ]
}
```

---

### 15. GET /api/analytics/dashboard/student
**Role:** Student (JWT-identified, no param needed)

Student self-service dashboard showing their own academic standing.

```json
{
  "studentName":       "Ahmed Ali",
  "gpa":               3.1,
  "creditHoursCompleted": 87,
  "creditHoursTotal":  140,
  "attendanceAvg":     79.5,
  "riskLevel":         "Low",
  "currentSemester": {
    "subjects": [
      { "subjectName": "Algorithms", "attendancePercent": 88.0, "currentGrade": 82.5 }
    ]
  }
}
```

---

## Future Analytics Ideas (Not Yet Implemented)

| Feature | How to Build |
|---------|-------------|
| Grade distribution histogram | Group StudentGrades by GradeLetter |
| Department GPA comparison | Average GradePoints per department |
| Semester progression rate | Students advancing each semester |
| Dropout rate | Students with no recent enrollments |
| Exam score trends | Average exam scores over time |
| Attendance correlation with grades | JOIN AttendanceSessions with StudentGrades |
