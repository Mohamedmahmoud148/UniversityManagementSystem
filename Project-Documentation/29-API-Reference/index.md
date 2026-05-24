# API Reference — Complete Endpoint List

> **Base URL (Production):** `https://universitymanagementsystem-production-e58e.up.railway.app`
> **Auth:** All endpoints require `Authorization: Bearer <token>` unless marked `[Public]`
> **ID Format:** ULID strings (e.g. `01ARZ3NDEKTSV4RRFFQ69G5FAV`)

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [Students](#2-students)
3. [Doctors](#3-doctors)
4. [Admins](#4-admins)
5. [Subject Offerings](#5-subject-offerings)
6. [Enrollments](#6-enrollments)
7. [Schedule](#7-schedule)
8. [Grades](#8-grades)
9. [GPA](#9-gpa)
10. [Exams](#10-exams)
11. [Assignments](#11-assignments)
12. [Attendance](#12-attendance)
13. [Materials](#13-materials)
14. [Complaints](#14-complaints)
15. [Notifications](#15-notifications)
16. [Chat (AI)](#16-chat-ai)
17. [Academic Structure](#17-academic-structure)
18. [Regulations & Roadmap](#18-regulations--roadmap)
19. [Analytics & Dashboards](#19-analytics--dashboards)
20. [Risk System](#20-risk-system)
21. [RAG Search](#21-rag-search)
22. [Proctoring](#22-proctoring)
23. [Files](#23-files)
24. [Deletion Framework](#24-deletion-framework)
25. [Audit Logs](#25-audit-logs)

---

## 1. Authentication

**Base:** `/api/auth`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/login` | Public | Login with university email + password |
| `POST` | `/refresh-token` | Public | Get new access token using refresh token |
| `POST` | `/revoke-token` | Any | Invalidate refresh token (logout) |
| `POST` | `/logout` | Any | Logout current session |
| `POST` | `/change-password` | Any | Change own password |
| `GET`  | `/me` | Any | Get current user info from token |
| `POST` | `/register/student` | Admin, SuperAdmin | Register new student account |
| `POST` | `/register/doctor` | Admin, SuperAdmin | Register new doctor account |
| `POST` | `/register/admin` | SuperAdmin | Register new admin account |
| `POST` | `/admin/reset-password/{userId}` | Admin, SuperAdmin | Force-reset a user's password |

**Login Request:**
```json
{
  "universityEmail": "s202400001@uni.edu.eg",
  "password": "P@ssw0rd"
}
```

**Login Response:**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-05-24T11:00:00Z",
  "mustChangePassword": false,
  "role": "Student",
  "fullName": "Ahmed Ali"
}
```

---

## 2. Students

**Base:** `/api/students`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/me` | Student | Get own profile (includes batchId, groupId) |
| `GET` | `/` | Any | All students — paginated `?page=1&size=10` |
| `GET` | `/search?q=` | Any | Search by name / code / email |
| `GET` | `/filter` | Admin, Doctor, SuperAdmin | Advanced filter (see params below) |
| `GET` | `/{code}` | Any | Get student by university code |
| `GET` | `/by-batch/{batchId}` | Admin, Doctor, SuperAdmin | All students in a batch |
| `GET` | `/by-offering/{offeringId}` | Admin, Doctor, SuperAdmin | Students enrolled in an offering |
| `GET` | `/struggling` | Admin, SuperAdmin | Students below GPA threshold |
| `POST` | `/` | Admin, SuperAdmin | Create single student |
| `POST` | `/bulk-upload-direct` | Admin, SuperAdmin | Upload Excel (direct processing) |
| `POST` | `/bulk-upload-ai` | Admin, SuperAdmin | Upload Excel (AI-assisted processing) |
| `POST` | `/import-excel` | Admin, SuperAdmin | Import students from .xlsx |
| `POST` | `/import-excel/download-credentials` | Admin, SuperAdmin | Import + get credentials Excel |
| `GET` | `/import-excel/template` | Admin, SuperAdmin | Download blank import template |
| `PUT` | `/{code}` | Admin, SuperAdmin | Full update student by code |
| `PATCH` | `/{id}` | Admin, SuperAdmin | Partial update (only send changed fields) |
| `DELETE` | `/{code}` | Admin, SuperAdmin | Delete student by code |

**GET /me Response:**
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "code": "STU-2024-001",
  "fullName": "Ahmed Ali Mohamed",
  "email": "ahmed@gmail.com",
  "phone": "01012345678",
  "universityStudentId": "202400001",
  "isActive": true,
  "batchId": "01ARZ...",
  "batchName": "Batch 2024",
  "groupId": "01ARZ...",
  "groupName": "Group A",
  "departmentId": "01ARZ...",
  "departmentName": "Computer Science",
  "collegeId": "01ARZ...",
  "collegeName": "Faculty of Engineering"
}
```

**Filter params (`/filter`):**
```
?departmentId=&batchId=&groupId=&collageId=&isActive=true&search=ahmed&page=1&size=25
```

**Struggling params (`/struggling`):**
```
?threshold=2.0&departmentId=&batchId=&page=1&size=20
```

---

## 3. Doctors

**Base:** `/api/doctors`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/` | Any | All doctors — paginated |
| `GET` | `/search?q=` | Any | Search by name / code |
| `GET` | `/filter` | Admin, SuperAdmin | Advanced filter |
| `GET` | `/{code}` | Any | Get doctor by code |
| `GET` | `/{code}/subjects` | Any | Subjects taught by doctor |
| `GET` | `/by-offering/{offeringId}` | Admin, Doctor, SuperAdmin | Doctor assigned to offering |
| `GET` | `/by-subject/{subjectId}` | Admin, Doctor, SuperAdmin | Doctors teaching a subject |
| `POST` | `/` | Admin, SuperAdmin | Create single doctor |
| `POST` | `/bulk-upload` | Admin, SuperAdmin | Bulk upload doctors from Excel |
| `PUT` | `/{code}` | Admin, SuperAdmin | Full update |
| `PATCH` | `/{id}` | Admin, SuperAdmin | Partial update |
| `DELETE` | `/{code}` | Admin, SuperAdmin | Delete doctor |

---

## 4. Admins

**Base:** `/api/admins`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/` | SuperAdmin | All admins |
| `GET` | `/{id}` | SuperAdmin | Get admin by ID |
| `POST` | `/` | SuperAdmin | Create admin |
| `PUT` | `/{id}` | SuperAdmin | Update admin |
| `DELETE` | `/{id}` | SuperAdmin | Delete admin |

---

## 5. Subject Offerings

**Base:** `/api/subjectofferings`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/by-code/{code}` | Any | Get offering by code |
| `GET` | `/by-semester/{semesterId}` | Admin, SuperAdmin | All offerings in a semester |
| `GET` | `/by-department/{departmentId}` | Admin, Doctor, SuperAdmin | Offerings in a department |
| `GET` | `/by-doctor/{doctorId}` | Admin, Doctor, SuperAdmin | Offerings assigned to doctor |
| `GET` | `/by-batch/{batchId}` | Admin, Doctor, SuperAdmin | Offerings for a batch |
| `GET` | `/my-offerings` | Doctor, SuperAdmin | Doctor's own offerings |
| `GET` | `/my-enrollments` | Student, SuperAdmin | Student's enrolled offerings |
| `POST` | `/` | Admin, SuperAdmin | Create offering |
| `PUT` | `/{id}` | Admin, SuperAdmin | Update offering |
| `DELETE` | `/{id}` | Admin, SuperAdmin | Delete offering |

---

## 6. Enrollments

**Base:** `/api/enrollments`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/my-enrollments` | Student, SuperAdmin | Student's active enrollments |
| `GET` | `/by-offering/{offeringId}` | Doctor, Admin, SuperAdmin | Students in an offering |
| `POST` | `/` | Student, SuperAdmin | Manual enroll in one offering |
| `POST` | `/auto-enroll` | Student, SuperAdmin | Auto-enroll in all available offerings |
| `POST` | `/{offeringId}/admin-enroll` | Admin, SuperAdmin | Admin enrolls a student |
| `PUT` | `/{id}/reactivate` | Admin, SuperAdmin | Reactivate dropped enrollment |
| `DELETE` | `/{id}` | Admin, SuperAdmin | Remove enrollment |

**Auto-Enroll Response:**
```json
{
  "enrolled": 5,
  "alreadyEnrolled": 2,
  "skipped": 1,
  "details": [...]
}
```

---

## 7. Schedule

**Base:** `/api/schedule`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/` | Admin, SuperAdmin | Create schedule slot |
| `GET` | `/my-schedule` | Doctor, SuperAdmin | Doctor's full weekly schedule |
| `GET` | `/my-today` | Doctor, SuperAdmin | Doctor's schedule for today |
| `GET` | `/batch/{batchId}` | Any | Full weekly schedule for a batch |
| `GET` | `/batch/{batchId}/today` | Any | Today's schedule for a batch |
| `GET` | `/batch/{batchId}/day/{day}` | Any | Schedule for specific day (0=Sun…6=Sat) |
| `GET` | `/offering/{offeringId}` | Any | Schedule for a specific offering |

**Student flow:**
```
1. GET /api/students/me           → get batchId
2. GET /api/schedule/batch/{batchId}  → get weekly schedule
```

---

## 8. Grades

**Base:** `/api/grades`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/my-grades` | Student, SuperAdmin | Student's own grades |
| `GET` | `/my-gpa` | Student, SuperAdmin | Student's GPA summary |
| `POST` | `/import/{offeringId}` | Doctor, Admin, SuperAdmin | Import grades from Excel |
| `POST` | `/calculate/{offeringId}` | Doctor, SuperAdmin | Calculate + finalize all grades for offering |
| `POST` | `/{gradeId}/recalculate` | Admin, SuperAdmin | Recalculate one grade |
| `PUT` | `/{id}` | Admin, SuperAdmin | Override a grade manually |
| `DELETE` | `/{gradeId}` | Admin, SuperAdmin | Delete grade record |

**Grade Weights:**
```
Midterm:    20%
Coursework: 20%
Final:      50%
Platform:   10%
─────────────
Total:     100%
```

---

## 9. GPA

**Base:** `/api/gpa`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/my-gpa` | Student, SuperAdmin | Own cumulative GPA |
| `GET` | `/student/{studentId}` | Admin, SuperAdmin | Any student's GPA |
| `POST` | `/student/{studentId}/recalculate` | Admin, SuperAdmin | Force GPA recalculation |

---

## 10. Exams

**Base:** `/api/exams`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/` | Doctor, SuperAdmin | Create exam manually |
| `POST` | `/generate-ai` | Doctor, SuperAdmin | AI-generate exam from topics |
| `POST` | `/preview-questions-from-pdf` | Doctor, SuperAdmin | Extract questions from PDF |
| `POST` | `/{id}/submit` | Student, SuperAdmin | Submit exam answers |
| `POST` | `/{id}/save-progress` | Student, SuperAdmin | Save draft answers mid-exam |
| `POST` | `/{id}/auto-grade` | Doctor, SuperAdmin | AI auto-grade all submissions |
| `POST` | `/grade-submission` | Doctor, SuperAdmin | Grade single submission |
| `POST` | `/by-code/{code}/restore` | Admin, SuperAdmin | Restore soft-deleted exam |
| `GET` | `/by-code/{code}` | Any | Get exam by code |
| `GET` | `/{id}` | Any | Get exam by ID |
| `GET` | `/{id}/session` | Student, SuperAdmin | Start exam session (timed) |
| `GET` | `/{id}/my-variant` | Student, SuperAdmin | Get randomized question variant |
| `GET` | `/{id}/my-submission` | Student, SuperAdmin | View own submitted answers |
| `GET` | `/{id}/results` | Doctor, SuperAdmin | All submissions + results |
| `GET` | `/{id}/analytics` | Doctor, SuperAdmin | Exam statistics |
| `GET` | `/my-exams` | Doctor, SuperAdmin | Doctor's created exams |
| `GET` | `/my-enrolled-exams` | Student, SuperAdmin | Student's upcoming/active exams |
| `GET` | `/by-offering/{offeringId}` | Doctor, SuperAdmin | All exams for an offering |
| `PUT` | `/by-code/{code}` | Admin, SuperAdmin, Doctor | Update exam |
| `DELETE` | `/by-code/{code}` | Admin, SuperAdmin, Doctor | Delete exam |

---

## 11. Assignments

**Base:** `/api/assignments`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/` | Doctor, Admin, SuperAdmin | Create assignment |
| `POST` | `/{id}/submit` | Student | Submit answer (text + optional file) |
| `POST` | `/submissions/{id}/grade` | Doctor, Admin, SuperAdmin | Human grade submission |
| `POST` | `/submissions/{id}/ai-grade` | Doctor, Admin, SuperAdmin | AI grade submission |
| `GET` | `/offering/{offeringId}` | Any | All assignments in an offering |
| `GET` | `/{id}` | Any | Get assignment details |
| `GET` | `/{id}/submissions` | Doctor, Admin, SuperAdmin | All student submissions |
| `GET` | `/{id}/my-submission` | Student | Own submission + grade |
| `DELETE` | `/{id}` | Doctor, Admin, SuperAdmin | Delete assignment |

**Create Assignment:**
```json
{
  "title": "Database Design Assignment",
  "description": "Design an ERD for a hospital system",
  "offeringId": "01ARZ...",
  "deadline": "2026-06-01T23:59:00Z",
  "maxGrade": 20,
  "aiGradingEnabled": true,
  "gradingRubric": "Evaluate normalization (40%), relationships (30%), naming (30%)"
}
```

**AI Grade Response:**
```json
{
  "score": 17,
  "feedback": "Excellent normalization up to 3NF...",
  "strengths": ["Clear entity naming", "Proper use of foreign keys"],
  "weaknesses": ["Missing index annotations"],
  "confidence": 0.88,
  "requiresHumanReview": false
}
```

---

## 12. Attendance

**Base:** `/api/attendance`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/sessions` | Doctor, TeachingAssistant, Admin, SuperAdmin | Open attendance session |
| `POST` | `/check-in` | Student, SuperAdmin | Student checks in |
| `POST` | `/correct` | Admin, SuperAdmin | Correct attendance record |
| `GET` | `/student/{studentId}/report` | Doctor, TeachingAssistant, Admin, SuperAdmin | Attendance report per subject |
| `GET` | `/record/{sessionId}/{studentId}` | Admin, SuperAdmin | Single attendance record |
| `PUT` | `/record/{sessionId}/{studentId}` | Admin, SuperAdmin | Update record manually |
| `DELETE` | `/record/{sessionId}/{studentId}` | Admin, SuperAdmin | Delete record |

---

## 13. Materials

**Base:** `/api/materials`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/upload` | Doctor, SuperAdmin | Upload course material (PDF, video, etc.) |
| `GET` | `/by-offering/{offeringId}` | Any | All materials for an offering |
| `GET` | `/download/{id}` | Any | Download / get signed URL |
| `GET` | `/{id}/metadata` | Any | File metadata (name, size, type) |
| `DELETE` | `/{id}` | Doctor, SuperAdmin | Delete material |

---

## 14. Complaints

**Base:** `/api/complaints`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/` | Student, SuperAdmin | Submit complaint |
| `GET` | `/my-complaints` | Student, SuperAdmin | Own complaint history |
| `GET` | `/my-reports` | Doctor, SuperAdmin | Complaints filed against doctor |
| `GET` | `/all` | Admin, SuperAdmin | All complaints (filterable) |
| `GET` | `/clusters` | Admin, SuperAdmin, Doctor | AI-clustered complaint patterns |
| `GET` | `/doctor-options` | Student, SuperAdmin | Available doctors to complain about |

**Submit Complaint:**
```json
{
  "targetType": "Doctor",
  "targetId": "01ARZ...",
  "category": "Grading",
  "description": "The midterm grade was incorrect..."
}
```

**AI Analysis (auto-added to complaint):**
```json
{
  "sentiment": "Negative",
  "category": "Grading",
  "riskScore": 0.82,
  "summary": "Student disputes midterm grade accuracy",
  "priority": "High"
}
```

---

## 15. Notifications

**Base:** `/api/notification`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/` | Any | Own notifications `?unreadOnly=false` |
| `PUT` | `/{id}/read` | Any | Mark notification as read |
| `POST` | `/` | Admin, SuperAdmin | Send notification to specific user |
| `POST` | `/send-to-my-students` | Doctor, SuperAdmin | Broadcast to all students in doctor's offerings |
| `DELETE` | `/{id}` | Admin, SuperAdmin | Delete notification |

**Real-Time:** Connect to SignalR hub at `/hubs/notifications` — event name: `ReceiveNotification`

---

## 16. Chat (AI)

**Base:** `/api/chat`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/messages` | Any | Send message to AI (rate-limited) |
| `POST` | `/conversations` | Any | Start new conversation |
| `GET` | `/conversations` | Any | List own conversations |
| `GET` | `/conversations/{id}/messages` | Any | Get conversation history |
| `PUT` | `/conversations/{id}` | Any | Rename conversation |
| `DELETE` | `/conversations/{id}` | Any | Delete conversation |
| `DELETE` | `/messages/{id}` | Admin, SuperAdmin | Delete specific message |

**Send Message:**
```json
{
  "conversationId": "01ARZ...",
  "content": "كام ساعة خلصت من اللائحة؟"
}
```

---

## 17. Academic Structure

All follow the same CRUD pattern. `[Any]` = authenticated user.

### Universities — `/api/universities`
### Colleges — `/api/colleges`
### Departments — `/api/departments`
### Subjects — `/api/subjects`
### Batches — `/api/batches`
### Groups — `/api/groups`
### Semesters — `/api/semesters`
### Academic Years — `/api/academicyears`

| Method | Route | Roles |
|--------|-------|-------|
| `GET` | `/` | Any |
| `GET` | `/{id}` | Any |
| `POST` | `/` | Admin, SuperAdmin |
| `PUT` | `/{id}` | Admin, SuperAdmin |
| `DELETE` | `/{id}` | Admin, SuperAdmin |

---

## 18. Regulations & Roadmap

**Base:** `/api/regulations`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/` | Any | All regulations |
| `GET` | `/active` | Any | Active regulations only |
| `GET` | `/by-code/{code}` | Any | Get by code |
| `GET` | `/by-department/{departmentId}` | Any | All for a department |
| `GET` | `/student/{studentId}` | Any | Regulation applied to student |
| `GET` | `/my-roadmap` | Student, SuperAdmin | Full academic status (passed, remaining, GPA) |
| `POST` | `/` | Admin, SuperAdmin | Create regulation (multipart/form-data) |
| `PUT` | `/{id}` | Admin, SuperAdmin | Update regulation |
| `PUT` | `/by-code/{code}` | Admin, SuperAdmin | Update by code |
| `DELETE` | `/{id}` | Admin, SuperAdmin | Delete |
| `DELETE` | `/by-code/{code}` | Admin, SuperAdmin | Delete by code |

**My Roadmap Response:**
```json
{
  "studentName": "Ahmed Ali",
  "regulation": "Reg-2024",
  "totalCreditHours": 132,
  "completedCreditHours": 48,
  "remainingCreditHours": 84,
  "gpa": 3.4,
  "passedSubjects": [...],
  "remainingSubjects": [...],
  "currentSemesterEnrollments": [...]
}
```

---

## 19. Analytics & Dashboards

### Dashboards — `/api/dashboard`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/admin` | Admin, SuperAdmin | Admin KPIs |
| `GET` | `/student` | Student, SuperAdmin | Student KPIs (GPA, attendance, grades) |
| `GET` | `/doctor` | Doctor, SuperAdmin | Doctor KPIs (offerings, submissions) |

### Analytics — `/api/analytics`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/summary` | Admin, SuperAdmin | Overall system stats |
| `GET` | `/student-count-by-department` | Admin, SuperAdmin | Students per department |
| `GET` | `/student-count-by-batch` | Admin, SuperAdmin | Students per batch |
| `GET` | `/doctor-workload` | Admin, SuperAdmin | Doctor teaching hours `?dept=&college=` |
| `GET` | `/top-enrolled-subjects` | Admin, SuperAdmin | Most popular subjects `?top=10` |
| `GET` | `/offering-enrollment-stats` | Admin, SuperAdmin | Enrollment stats per offering |

---

## 20. Risk System

**Base:** `/api/risk`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/dashboard` | Admin, SuperAdmin | All at-risk students overview |
| `GET` | `/at-risk-students` | Doctor, Admin, SuperAdmin | At-risk students `?offeringId=` |

---

## 21. RAG Search

**Base:** `/api/rag`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/search` | Any | Semantic search over course materials |

**Request:**
```json
{
  "query": "what is database normalization?",
  "offeringId": "01ARZ..."
}
```

---

## 22. Proctoring

**Base:** `/api/proctoring`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/event` | Student, SuperAdmin | Report proctoring event (tab switch, etc.) |
| `GET` | `/exam/{examId}/summary` | Doctor, SuperAdmin | View proctoring events for exam |

**Proctoring Event:**
```json
{
  "examId": "01ARZ...",
  "eventType": "TabSwitch",
  "timestamp": "2026-05-24T10:30:00Z"
}
```

---

## 23. Files

**Base:** `/api/file`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/{fileId}` | Any | Get file (returns signed URL or stream) |

---

## 24. Deletion Framework

**Base:** `/api/deletion`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `POST` | `/analyze` | Admin, SuperAdmin | Preview impact before deleting |
| `POST` | `/execute` | Admin, SuperAdmin | Execute deletion after confirmation |

**Always call `/analyze` before `/execute` to see cascade impact.**

---

## 25. Audit Logs

**Base:** `/api/auditlogs`

| Method | Route | Roles | Description |
|--------|-------|-------|-------------|
| `GET` | `/` | SuperAdmin | All audit events (paginated) |

---

## Common Response Patterns

### Paginated Response
```json
{
  "data": [...],
  "totalCount": 150,
  "page": 1,
  "size": 20
}
```

### Error Response
```json
{
  "status": 400,
  "message": "Batch with code 'BATCH-X' not found."
}
```

### 401 Unauthorized
Token missing or expired → refresh using `POST /api/auth/refresh-token`

### 403 Forbidden
Valid token but wrong role for this endpoint.
