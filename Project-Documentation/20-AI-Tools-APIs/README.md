# AI Tools APIs

## Overview

`/api/ai-tools/*` is an internal API layer consumed exclusively by the **FastAPI AI service** — not by the frontend directly. When a user talks to the AI chatbot, the FastAPI brain calls these endpoints to read or write data on behalf of the user.

```
User → Frontend → FastAPI (AI Brain) → /api/ai-tools/* → Database
```

All endpoints require a valid JWT token. The token is forwarded from the original user request, so the backend enforces the same RBAC rules that apply to the regular API.

---

## Who Calls What

| Endpoint | Called From (FastAPI module) | Trigger |
|---|---|---|
| `GET resolve-offering` | `exam_generation.py` | Doctor asks AI to generate an exam |
| `GET student-overview/{id}` | `academic_advisor.py` | Student asks for academic advice (sparse context) |
| `POST create-complaint` | `complaint.py` | Student submits a complaint via AI chat |
| `GET get-complaints` | `complaint.py` | Doctor/Admin asks AI to summarize complaints |
| `POST bulk-create-students` | `file_processor.py` | Admin uploads an Excel file with student data |
| `POST bulk-upload-grades` | `file_processor.py` | Admin uploads an Excel file with grade data |

The remaining endpoints (`resolve-subject`, `resolve-student`, `resolve-doctor`, `student-gpa`, `offering-students`, `doctor-subjects`) are exposed in the Swagger schema and available to the AI planner — it may call them dynamically based on what the user asks.

---

## Endpoint Reference

### `GET /api/ai-tools/resolve-subject?name={name}`
**Auth:** Admin, Doctor  
Finds a subject by exact name match. Returns subject ID, name, and code.

```json
{ "subjectId": "01JFKX...", "subjectName": "Algorithms", "subjectCode": "CS301" }
```

---

### `GET /api/ai-tools/resolve-offering?subject={name}`
**Auth:** Admin, Doctor  
Finds all active SubjectOfferings for a subject name. Returns a list so the AI can disambiguate by semester or batch if there are multiple matches.

```json
{
  "count": 2,
  "note": "Multiple offerings found. Select using semesterName and/or batchId.",
  "offerings": [
    {
      "offeringId": "01JFKX...",
      "subjectName": "Algorithms",
      "semesterName": "Spring 2026",
      "batchId": "01JFKX..."
    }
  ]
}
```

---

### `GET /api/ai-tools/student-overview/{studentId}`
**Auth:** Admin, Doctor, Student  
Full academic snapshot for a student: GPA, enrolled subjects, finalized grades, exam scores.

```json
{
  "studentId": "01JFKX...",
  "gpa": 3.4,
  "totalCreditHours": 72,
  "subjects": [ { "subjectName": "Algorithms", "offeringId": "..." } ],
  "grades":   [ { "subjectName": "OS", "gradeLetter": "A", "finalScore": 92 } ],
  "exams":    [ { "examTitle": "OS Midterm", "score": 85, "totalMarks": 100, "isGraded": true } ]
}
```

---

### `GET /api/ai-tools/resolve-student?name={name}`
**Auth:** Admin, Doctor  
Fuzzy name search (ILIKE). Returns the first matching student's ID, full name, and code.

---

### `GET /api/ai-tools/resolve-doctor?name={name}`
**Auth:** Admin, Doctor  
Fuzzy name search (ILIKE). Returns the first matching doctor's ID, full name, and code.

---

### `GET /api/ai-tools/student-gpa/{studentId}`
**Auth:** Admin, Doctor, Student  
Credit-hour-weighted GPA via GradeService (the authoritative calculation).

```json
{ "studentId": "01JFKX...", "gpa": 3.4 }
```

---

### `GET /api/ai-tools/offering-students/{offeringId}`
**Auth:** Admin, Doctor  
All active enrolled students in an offering — ID, full name, student code.

---

### `GET /api/ai-tools/doctor-subjects/{doctorId}`
**Auth:** Admin, Doctor, Student  
All subjects a doctor teaches (distinct, across all offerings).

---

### `POST /api/ai-tools/create-complaint`
**Auth:** Student only  
Submit a complaint via the AI chatbot.

Request:
```json
{
  "title": "Unfair exam grading",
  "targetType": "Doctor",
  "targetId": "01JFKX...",
  "message": "The grading criteria were not communicated before the exam."
}
```

`targetType` allowed values: `Doctor`, `Exam`, `Grade`, `Other`

Response `201`:
```json
{ "id": "01JFKX...", "status": "Pending", "createdAt": "2026-05-16T10:00:00Z" }
```

---

### `GET /api/ai-tools/get-complaints`
**Auth:** Admin, Doctor  
Paginated, filtered complaint list.

- **Admin** sees all complaints.
- **Doctor** sees only complaints where `TargetType=Doctor` and `TargetId` matches their own doctor ID.

Query params: `from`, `to`, `targetType`, `targetId`, `status`, `page`, `pageSize`

---

### `POST /api/ai-tools/bulk-create-students`
**Auth:** Admin, SuperAdmin  
Upload an `.xlsx` file to bulk-import student accounts.

Required columns: `FullName | Email | UniversityStudentId | BatchCode | GroupCode`

Max file size: 10 MB. Returns inserted/skipped counts and per-row errors.

---

### `POST /api/ai-tools/bulk-upload-grades`
**Auth:** Admin, SuperAdmin  
Upload an `.xlsx` file to bulk-upsert student grades.

Required columns: `UniversityStudentId | SubjectOfferingId | FinalScore | GradeLetter | GradePoints`

Duplicate `(StudentId, OfferingId)` rows are updated (upsert). Returns inserted/updated/skipped counts.

---

## AI Discovery — How the AI Knows These Exist

On FastAPI startup, `api_discovery.py` downloads the Swagger JSON from the .NET backend and filters it through an allowlist. Only `GET` and specific `POST` paths survive. The resulting filtered schema is injected into the AI planner's system prompt so the LLM knows which endpoints it can call.

`PUT`, `PATCH`, and `DELETE` are blocked at the discovery layer — the AI can never call destructive methods.

---

## Changes Made (Session — May 2026)

- **Removed** `POST /api/ai-tools/distribute-exams` — was the old manual exam randomization approach, replaced by `StudentExamVariant` (lazy per-question allocation). Also removed from the FastAPI allowlist in `api_discovery.py`.
- **Fixed** `create-complaint` — was using a hardcoded `Title = "AI Tool Complaint"` instead of `dto.Title`.
- **Fixed** `academic_advisor.py` — was calling the non-existent `/api/ai-tools/student-academic-summary`; corrected to `/api/ai-tools/student-overview/{userId}`.
