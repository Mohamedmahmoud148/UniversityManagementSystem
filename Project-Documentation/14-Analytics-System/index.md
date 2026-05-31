# Analytics System

> **Last refreshed:** 2026-05-31

---

## 1. Overview

The Analytics System provides role-specific dashboards and detailed statistical views across the entire university. All queries use `AsNoTracking()` with direct DTO projection — no full entity loading.

---

## 2. Admin Dashboard (GET /api/analytics/dashboard/admin)

Returns 10 KPIs fetched in parallel:

| Field | Description |
|-------|-------------|
| `totalStudents` | Active students count |
| `totalDoctors` | Active doctors count |
| `activeCourses` | Subject offerings (not deleted) |
| `totalEnrollments` | Active enrollments |
| `totalColleges` | Colleges count |
| `totalDepartments` | Departments count |
| `totalBatches` | Batches count |
| `avgGpa` | Average GPA across all finalized grades |
| `passRate` | % of finalized grades with FinalScore ≥ 50 |
| `atRiskCount` | Students with avg GradePoints < 2.0 |

Also returns:
- **Students by Department** table (department name, college, student count, doctor count)
- **Top 10 Enrolled Subjects** table

---

## 3. Doctor Dashboard (GET /api/analytics/dashboard/doctor)

| Field | Description |
|-------|-------------|
| `totalOfferings` | Doctor's active offerings |
| `totalStudents` | Total enrolled across all offerings |
| `avgGrade` | Average grade across all offerings |
| `courseList` | Per-offering: name, avg score, enrollment count, pass rate |

---

## 4. Student Dashboard (GET /api/analytics/dashboard/student)

| Field | Description |
|-------|-------------|
| `currentGpa` | Student's current GPA |
| `overallAttendance` | Overall attendance % across all subjects |
| `enrolledCourses` | Number of active enrollments |
| `subjectDetails` | Per-subject: score, attendance %, status (Excellent/Good/Average/Failing) |

---

## 5. Grade Distribution (GET /api/analytics/grades/distribution)

For a specific subject offering:

```json
{
  "excellentPct": 30.0,
  "goodPct": 45.0,
  "averagePct": 20.0,
  "failingPct": 5.0,
  "histogram": [
    { "range": "0-49", "count": 2 },
    { "range": "50-59", "count": 5 },
    { "range": "60-69", "count": 8 },
    { "range": "70-79", "count": 12 },
    { "range": "80-89", "count": 10 },
    { "range": "90-100", "count": 3 }
  ]
}
```

---

## 6. Attendance Trends (GET /api/analytics/attendance/trends)

Weekly attendance percentages for the last N weeks (default 8):

```json
[
  { "week": "2026-W18", "attendancePct": 82.5, "sessionCount": 3, "presentCount": 25 },
  { "week": "2026-W19", "attendancePct": 91.0, "sessionCount": 4, "presentCount": 30 }
]
```

---

## 7. At-Risk Students (GET /api/analytics/at-risk-students)

Returns students with risk level = High or Medium:

```json
[
  {
    "studentId": "...",
    "studentCode": "CS2024001",
    "fullName": "Ahmed Mohamed",
    "departmentName": "AI & ML",
    "gpa": 1.8,
    "attendanceRate": 58.0,
    "failingSubjects": 2,
    "riskLevel": "High"
  }
]
```

Risk algorithm:
- **High**: GPA < 1.5 OR attendance < 60% OR failing ≥ 3 subjects
- **Medium**: GPA < 2.0 OR attendance < 75% OR failing ≥ 1 subject
- **Low**: all above conditions clear (excluded from response)

---

## 8. Department Comparison (GET /api/analytics/department/comparison)

Compares all departments:

| Field | Description |
|-------|-------------|
| `departmentName` | Dept name |
| `avgGpa` | Average GPA of dept students |
| `passRate` | % passing final score ≥ 50 |
| `attendanceRate` | Overall attendance % |
| `studentCount` | Active student count |

---

## 9. Student Performance (GET /api/analytics/student/{studentId}/performance)

Per-subject breakdown for a specific student:

```json
[
  {
    "subjectName": "Data Structures",
    "finalScore": 78.5,
    "attendanceRate": 85.0,
    "status": "Good"
  }
]
```

Status thresholds:
- Excellent: score ≥ 85
- Good: score ≥ 70
- Average: score ≥ 50
- Failing: score < 50

This endpoint is called by `StudyPlanModule` to identify weak subjects for the study plan.
