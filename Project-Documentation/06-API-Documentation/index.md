# API Documentation

> **Last refreshed:** 2026-05-31
> **Base URL:** `https://universitymanagementsystem-production-e58e.up.railway.app`
> All endpoints require `Authorization: Bearer <jwt>` unless marked **[Public]**.

---

## Authentication

| Method | Endpoint | Auth | Body / Params |
|--------|----------|------|--------------|
| POST | `/api/auth/login` | Public | `{ email, password }` |
| POST | `/api/auth/register` | Admin | `{ email, password, role, ... }` |
| POST | `/api/auth/refresh` | Public | `{ refreshToken }` |
| PUT | `/api/auth/change-password` | Auth | `{ currentPassword, newPassword }` |

**Login Response:**
```json
{ "token": "eyJ...", "refreshToken": "...", "role": "Student", "userId": "..." }
```

---

## Students

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/students` | Admin | Paginated list |
| GET | `/api/students/{id}` | Admin | By ID |
| POST | `/api/students` | Admin | Create |
| PUT | `/api/students/{id}` | Admin | Update |
| DELETE | `/api/students/{id}` | Admin | Soft delete |
| GET | `/api/students/my-profile` | Student | Own profile |

---

## Subject Offerings

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/subjectofferings` | Admin | All offerings |
| GET | `/api/subjectofferings/{id}` | Auth | Detail |
| POST | `/api/subjectofferings` | Admin | Create |
| GET | `/api/subjectofferings/my-offerings` | Doctor | Doctor's offerings |
| GET | `/api/subjectofferings/my-enrollments` | Student | Enrolled offerings |

---

## Enrollments

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/enrollments` | Admin | Manual enrol |
| POST | `/api/enrollments/auto-enrol` | Student | AI-assisted auto-enrol |
| DELETE | `/api/enrollments/{id}` | Admin | Withdraw |
| GET | `/api/enrollments/my-enrollments` | Student | Own enrollments |

---

## Materials

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/materials/upload` | Doctor, Admin | Upload (multipart, 500 MB max) |
| GET | `/api/materials/by-offering/{offeringId}` | Auth | List for offering |
| GET | `/api/materials/download/{id}` | Auth | 60-min signed URL |
| GET | `/api/materials/{id}/metadata` | Auth | File metadata |
| DELETE | `/api/materials/{id}` | Doctor, Admin | Delete |

**Upload form fields:** `title`, `description`, `subjectOfferingId`, `file` (IFormFile)

**Supported MIME types:** PDF, DOC, DOCX, PPT, PPTX, XLS, XLSX, TXT, ZIP, JPEG, PNG, GIF, MP4, WebM

---

## Assignments

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/assignments` | Doctor, Admin | Create |
| GET | `/api/assignments/offering/{offeringId}` | Auth | List |
| GET | `/api/assignments/{id}` | Auth | Detail |
| POST | `/api/assignments/{id}/submit` | Student | Submit (multipart, 100 MB max) |
| GET | `/api/assignments/{id}/submissions` | Doctor | All submissions |
| POST | `/api/assignments/submissions/{id}/grade` | Doctor | Manual grade |
| POST | `/api/assignments/submissions/{id}/ai-grade` | Doctor | Trigger AI grade |
| GET | `/api/assignments/{id}/my-submission` | Student | Own submission |
| DELETE | `/api/assignments/{id}` | Doctor, Admin | Delete |

**Create Body:**
```json
{
  "title": "Assignment 1",
  "description": "Solve...",
  "subjectOfferingId": "01HX...",
  "deadline": "2026-06-15T23:59:00Z",
  "maxGrade": 100,
  "allowLateSubmission": false,
  "aiGradingEnabled": true,
  "gradingRubric": "..."
}
```

---

## Exams

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/exams` | Doctor, Admin | Create |
| GET | `/api/exams/offering/{offeringId}` | Auth | List |
| GET | `/api/exams/{id}` | Auth | Detail + questions |
| PUT | `/api/exams/{id}/publish` | Doctor | Publish |
| POST | `/api/exams/{id}/submit` | Student | Submit answers |
| GET | `/api/exams/{id}/results` | Doctor | All submissions |
| GET | `/api/exams/{id}/my-submission` | Student | Own result |
| GET | `/api/exams/my-exams` | Doctor | Doctor's exams |

---

## Regulations

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/regulations` | **Public** | All regulations |
| GET | `/api/regulations/active` | **Public** | Active only |
| GET | `/api/regulations/my-roadmap` | Student | Full personal roadmap |
| GET | `/api/regulations/by-code/{code}` | Auth | By slug |
| POST | `/api/regulations` | Admin | Create (multipart) |
| PUT | `/api/regulations/by-code/{code}` | Admin | Update |
| DELETE | `/api/regulations/by-code/{code}` | Admin | Delete |

**my-roadmap Response (abbreviated):**
```json
{
  "currentGpa": 3.2,
  "totalCreditHours": 130,
  "completedCreditHours": 65,
  "passedSubjects": 12,
  "failedSubjects": 1,
  "currentlyEnrolled": 5,
  "semesters": [{ "semesterNumber": 1, "status": "completed", "subjects": [...] }],
  "mustRetake": [{ "subjectName": "Math 2", "gradeLetter": "F" }],
  "recommendedNext": [{ "subjectName": "Algorithms", "creditHours": 3 }]
}
```

---

## Notifications

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/notification` | Auth | List (`?unreadOnly=true`) |
| PUT | `/api/notification/{id}/read` | Auth | Mark as read |
| POST | `/api/notification` | Admin | Send notification |
| DELETE | `/api/notification/{id}` | Admin | Delete |
| POST | `/api/notification/send-to-my-students` | Doctor | Broadcast to offering |

---

## Analytics & Dashboard

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/analytics/dashboard/admin` | Admin | 10 KPIs |
| GET | `/api/analytics/dashboard/doctor` | Doctor | Course stats |
| GET | `/api/analytics/dashboard/student` | Student | Personal stats |
| GET | `/api/analytics/at-risk-students` | Admin | At-risk list |
| GET | `/api/analytics/grades/distribution?offeringId=` | Doctor, Admin | Grade histogram |
| GET | `/api/analytics/attendance/trends?offeringId=&weeks=` | Doctor, Admin | Weekly trends |
| GET | `/api/analytics/department/comparison` | Admin | Dept comparison |
| GET | `/api/analytics/student/{id}/performance` | Auth | Per-subject stats |

---

## Chat (AI)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/chat` | Auth | Send message |
| GET | `/api/chat/conversations` | Auth | List |
| GET | `/api/chat/conversations/{id}/messages` | Auth | History |
| DELETE | `/api/chat/conversations/{id}` | Auth | Delete |

**Request:** `{ "message": "...", "conversationId": "optional" }`

**Response:**
```json
{
  "message": "...",
  "intent": "study_plan",
  "module": "StudyPlanModule",
  "suggestions": ["خطة يومية", "مراجعة امتحانات"],
  "conversationId": "..."
}
```

---

## AI Tools (Internal — used by AI service)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/ai-tools/student-overview/{userId}` | Auth | Grades + exams |
| GET | `/api/ai-tools/student-gpa/{userId}` | Auth | GPA value |
| GET | `/api/ai-tools/student-schedule/{userId}` | Auth | Timetable |
| GET | `/api/ai-tools/academic-summary/{userId}` | Auth | Full summary |

---

## Standard Response Envelope

```json
{
  "success": true,
  "message": "OK",
  "data": { ... }
}
```

**Error responses:**

| Status | Meaning |
|--------|---------|
| 400 | Validation error / bad request body |
| 401 | Missing or invalid JWT |
| 403 | Role not permitted for this operation |
| 404 | Resource not found |
| 429 | Rate limit exceeded |
| 502 | AI service unreachable (circuit breaker open) |
| 500 | Internal server error |
