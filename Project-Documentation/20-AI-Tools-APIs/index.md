# AI Tools APIs

> **Last refreshed:** 2026-05-31

These are the .NET backend endpoints the FastAPI AI service calls to fetch student data. They are authenticated (JWT forwarded from the user's session) and optimized for AI consumption.

---

## 1. Student Overview

```
GET /api/ai-tools/student-overview/{userId}
Authorization: Bearer <student-jwt>
```

**Response:**
```json
{
  "userId": "...",
  "studentId": "...",
  "gpa": 3.2,
  "totalCreditHours": 45,
  "subjects": [
    {
      "subjectId": "...",
      "name": "Data Structures",
      "code": "CS301",
      "offeringId": "..."
    }
  ],
  "grades": [
    {
      "subjectName": "Algorithms",
      "subjectCode": "CS302",
      "gradeLetter": "A",
      "finalScore": 91,
      "gradePoints": 4.0
    }
  ],
  "exams": [
    {
      "examTitle": "Midterm",
      "subjectName": "Data Structures",
      "score": 18,
      "totalMarks": 20,
      "isGraded": true
    }
  ]
}
```

**Used by:** AcademicAdvisorModule, StudyPlanModule

---

## 2. Student GPA

```
GET /api/ai-tools/student-gpa/{userId}
```

**Response:** `{ "studentId": "...", "gpa": 3.2 }`

---

## 3. Student Schedule

```
GET /api/ai-tools/student-schedule/{userId}
```

Returns the student's weekly timetable (subject offerings with time slots).

---

## 4. Academic Summary

```
GET /api/ai-tools/academic-summary/{userId}
```

Combined view: profile + GPA + current subjects + recent grades.

---

## 5. Student Performance (Analytics)

```
GET /api/analytics/student/{userId}/performance
```

Per-subject performance with attendance rate:

```json
[
  {
    "subjectName": "Data Structures",
    "finalScore": 78.5,
    "attendanceRate": 85.0,
    "status": "Good"
  },
  {
    "subjectName": "Networks",
    "finalScore": 45.0,
    "attendanceRate": 62.0,
    "status": "Failing"
  }
]
```

**Used by:** StudyPlanModule (identifies weak subjects and attendance issues)

---

## 6. My Roadmap

```
GET /api/regulations/my-roadmap
Authorization: Bearer <student-jwt>
```

See [10-Academic-Roadmap-System](../10-Academic-Roadmap-System/index.md) for full response schema.

**Used by:** StudyPlanModule, AcademicAdvisorModule

---

## 7. Assignments by Offering

```
GET /api/assignments/offering/{offeringId}
Authorization: Bearer <student-jwt>
```

**Used by:** StudyPlanModule (fetch upcoming deadlines), AssignmentQueryModule

---

## 8. My Submission

```
GET /api/assignments/{assignmentId}/my-submission
Authorization: Bearer <student-jwt>
```

**Used by:** AssignmentQueryModule (check if already submitted)

---

## 9. Tool Registry (FastAPI)

The FastAPI `ALLOWED_TOOL_NAMES` frozenset controls which backend endpoints the AI can call from multi-step plans:

```python
ALLOWED_TOOL_NAMES = frozenset({
    "ResolveSubjectOffering",
    "GetStudentResults",
    "GetStudentGrades",
    "GetGPASummary",
    "GetTranscript",
    "GetSchedule",
    "GetStudentSchedule",
    "GetSubjectOfferings",
    "GetCourseEnrollments",
    "GenerateExam",
    "DistributeExam",
    "GetExamQuestions",
    "SubmitComplaint",
    "GetComplaints",
    "GetStudentAcademicSummary",
    "BulkCreateStudents",
    "BulkUploadGrades",
    "GetMaterials",
    "GetStudentAssignments",
})
```

---

## 10. Dynamic API Module

The `DynamicApiModule` (handles `backend_api_query` + `action_execute` intents) discovers endpoints dynamically from the .NET Swagger schema. It:

1. Fetches allowed endpoint schema on startup
2. When user asks a data question, asks LLM to select the best endpoint
3. Validates selection against allowlist
4. Calls the endpoint with JWT forwarding
5. Narrates the JSON response in natural language
