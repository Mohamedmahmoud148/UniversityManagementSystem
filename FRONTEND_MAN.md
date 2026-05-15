# FRONTEND MAN — Complete Technical Integration Guide
## University Management System + AI Orchestration Platform

> **Who this is for:** Frontend developer building the UI layer.
> **What this covers:** Every API endpoint, every request/response shape, every system flow — end to end.
> **Stack:** .NET 9 Backend (REST) + FastAPI AI Service — both behind JWT Bearer auth.

---

## TABLE OF CONTENTS

1. [Authentication & Session Management](#1-authentication--session-management)
2. [Student APIs](#2-student-apis)
3. [Doctor APIs](#3-doctor-apis)
4. [Subject & Subject Offering APIs](#4-subject--subject-offering-apis)
5. [Enrollment APIs](#5-enrollment-apis)
6. [Materials APIs](#6-materials-apis)
7. [Exam APIs](#7-exam-apis)
8. [Grades & GPA APIs](#8-grades--gpa-apis)
9. [Attendance APIs](#9-attendance-apis)
10. [Complaint APIs](#10-complaint-apis)
11. [Chat & AI APIs](#11-chat--ai-apis)
12. [Admin & Structure APIs](#12-admin--structure-apis)
13. [AI Orchestration Service (FastAPI)](#13-ai-orchestration-service-fastapi)
14. [Complete System Flow Diagrams](#14-complete-system-flow-diagrams)
15. [JWT Token Reference](#15-jwt-token-reference)
16. [Error Handling Reference](#16-error-handling-reference)
17. [academic_context Field Guide](#17-academic_context-field-guide)

---

## BASE URLS

```
.NET Backend:   https://your-backend.railway.app   (all /api/* routes)
FastAPI AI:     https://your-ai.railway.app        (POST /api/chat, POST /api/ai/analyze-complaint)
```

Every request to both services requires:
```
Authorization: Bearer <jwt_access_token>
Content-Type: application/json     (or multipart/form-data for uploads)
```

---

## 1. AUTHENTICATION & SESSION MANAGEMENT

### 1.1 — Login

**POST** `/api/Auth/login`

> Public — no token required. Rate limited: 5 requests/min per IP.

**Request Body:**
```json
{
  "email": "ahmed.ali@university.edu",
  "password": "Str0ngP@ss"
}
```

**Success Response — 200:**
```json
{
  "token": "eyJhbGciOi...",
  "refreshToken": "d1f2a3b4c5...",
  "expiresIn": 3600,
  "user": {
    "id": "01JXXXXXXXXXXXXXXXXXXXXXXX",
    "email": "ahmed.ali@university.edu",
    "role": "Student",
    "mustChangePassword": false
  }
}
```

**Error Responses:**
- `401` — Invalid credentials or account locked

**What the frontend must do:**
1. Store `token` in memory (NOT localStorage for security — use httpOnly cookie or in-memory)
2. Store `refreshToken` in httpOnly cookie
3. Read `mustChangePassword` — if `true`, redirect to change-password screen immediately
4. Read `role` from response — use it to conditionally render navigation

**JWT Claims inside the token (decode it):**
```json
{
  "nameid":    "01JXXX...",   ← SystemUser.Id (used for chat, conversation ownership)
  "ProfileId": "01JYYY...",   ← Doctor.Id / Student.Id / Admin.Id (used for academic queries)
  "role":      "Student",      ← "Student" | "Doctor" | "Admin" | "SuperAdmin"
  "userCode":  "STU20260001", ← Short code used in profile URLs
  "email":     "ahmed@...",
  "exp":       1748000000
}
```

**Internal Flow:**
1. `AuthService.LoginAsync` — queries `SystemUsers` table by email
2. Verifies BCrypt password hash
3. If `MustChangePassword=true` → still returns token (user must be redirected client-side)
4. Generates JWT (1-hour expiry) + RefreshToken (7-day expiry, saved to `RefreshTokens` table)
5. Returns `AuthResponseDto`

---

### 1.2 — Refresh Token

**POST** `/api/Auth/refresh-token`

> Public — send the expired access token + refresh token. Rate limited: 10/min per user.

**Request Body:**
```json
{
  "token": "eyJhbGci... (expired)",
  "refreshToken": "d1f2a3b4c5..."
}
```

**Success Response — 200:** Same shape as Login response (new `token` + new `refreshToken`)

**Error:** `401` if refresh token is invalid/expired/revoked

**Frontend Pattern:**
```
On any 401 from backend API → call /refresh-token → retry original request
If refresh also fails → logout user
```

---

### 1.3 — Change Password

**POST** `/api/Auth/change-password` *(Requires token)*

**Request Body:**
```json
{
  "currentPassword": "OldP@ss1",
  "newPassword": "NewP@ss2024"
}
```

**Response:** `200 "Password changed successfully."` | `400` on failure

---

### 1.4 — Logout

**POST** `/api/Auth/logout` *(Requires token)*

**Request Body:**
```json
{ "refreshToken": "d1f2a3b4c5..." }
```

**Response:** `200 "Logged out."` — the refresh token is revoked in DB

---

### 1.5 — Get Current User Claims

**GET** `/api/Auth/me` *(Requires token)*

**Response:**
```json
[
  { "type": "nameid", "value": "01JXXX..." },
  { "type": "ProfileId", "value": "01JYYY..." },
  { "type": "role", "value": "Student" },
  { "type": "email", "value": "ahmed@..." }
]
```

> Use this to verify a token is valid and get current identity.

---

### 1.6 — Register Student *(Admin only)*

**POST** `/api/Auth/register/student`

**Request Body:**
```json
{
  "fullName": "Ahmed Mohamed Ali",
  "email": "ahmed.ali@university.edu",
  "nationalId": "12345678901234",
  "phone": "01012345678",
  "universityStudentId": "20260001",
  "batchCode": "BATCH-CS-2026",
  "groupCode": "GROUP-A"
}
```

**Response 200:** Full `AuthResponseDto` (token issued immediately)

---

### 1.7 — Register Doctor *(Admin only)*

**POST** `/api/Auth/register/doctor`

**Request Body:**
```json
{
  "fullName": "Dr. Khaled Hassan",
  "nationalId": "12345678901234",
  "phone": "01098765432",
  "universityStaffId": "STAFF-001",
  "departmentCode": "CS-DEPT"
}
```

---

### 1.8 — Admin Password Reset *(Admin/SuperAdmin only)*

**POST** `/api/Auth/admin/reset-password/{userId}`

**Response 200:**
```json
{
  "message": "Password reset successfully.",
  "newPassword": "Tmp@123456",
  "mustChangePassword": true,
  "note": "Share this password with the user. They will be required to change it on first login."
}
```

---

## 2. STUDENT APIs

### 2.1 — Get Students (Paginated)

**GET** `/api/Students?page=1&size=10`

**Auth:** Admin, Doctor, TeachingAssistant

**Response 200:**
```json
{
  "items": [
    {
      "id": "01JXXX...",
      "code": "STU20260001",
      "fullName": "Ahmed Mohamed Ali",
      "email": "ahmed.ali@university.edu",
      "universityEmail": "a.ali@bsnu.edu.eg",
      "phone": "01012345678",
      "universityStudentId": "20260001",
      "batchId": "01JBATCH...",
      "batchName": "Level 3 - CS 2026",
      "groupId": "01JGROUP...",
      "groupName": "Group A",
      "departmentName": "Computer Science",
      "collegeName": "Faculty of Engineering",
      "isActive": true
    }
  ],
  "totalCount": 250,
  "page": 1,
  "pageSize": 10
}
```

---

### 2.2 — Search Students

**GET** `/api/Students/search?q=ahmed`

**Auth:** Admin, Doctor, TeachingAssistant

**Response:** Array of `StudentDto` matching name/code/email

---

### 2.3 — Get Student by Code

**GET** `/api/Students/{code}`

Example: `/api/Students/STU20260001`

**Auth:** Admin, Doctor (own students only at data level)

**Response 200:**
```json
{
  "id": "01JXXX...",
  "code": "STU20260001",
  "fullName": "Ahmed Mohamed Ali",
  "email": "ahmed.ali@university.edu",
  "universityEmail": "a.ali@bsnu.edu.eg",
  "nationalId": "12345678901234",
  "phone": "01012345678",
  "universityStudentId": "20260001",
  "isActive": true,
  "batchId": "01JBATCH...",
  "batchName": "Level 3 - CS 2026",
  "groupId": "01JGROUP...",
  "groupName": "Group A",
  "departmentId": "01JDEPT...",
  "departmentName": "Computer Science",
  "collegeId": "01JCOLLEGE...",
  "collegeName": "Faculty of Engineering"
}
```

---

### 2.4 — Filter Students

**GET** `/api/Students/filter?page=1&size=20&departmentId=01J...&batchId=01J...&isActive=true`

**Query Params:**
| Param | Type | Description |
|-------|------|-------------|
| page | int | Page number (default 1) |
| size | int | Page size (default 20) |
| departmentId | string? | Filter by department ULID |
| collegeId | string? | Filter by college ULID |
| batchId | string? | Filter by batch ULID |
| groupId | string? | Filter by group ULID |
| isActive | bool? | Filter active/inactive |

---

### 2.5 — Get Students by Batch

**GET** `/api/Students/by-batch/{batchId}`

**Auth:** Admin, Doctor, TeachingAssistant

---

### 2.6 — Update Student *(Admin only)*

**PATCH** `/api/Students/{id}`

**Request Body (all fields optional):**
```json
{
  "fullName": "Ahmed Mohamed Ali Updated",
  "phone": "01099999999",
  "isActive": true
}
```

---

### 2.7 — Delete Student *(Admin only)*

**DELETE** `/api/Students/{code}`

> Soft delete — sets `DeletedAt` timestamp. Student cannot log in after this.

**Response:** `204 No Content`

---

### 2.8 — Bulk Upload Students via Excel *(Admin only)*

**POST** `/api/Students/import-excel`

**Content-Type:** `multipart/form-data`

**Body:** `file` field with `.xlsx` file

**Excel columns expected:** `FullName`, `Email`, `NationalId`, `Phone`, `UniversityStudentId`, `BatchCode`, `GroupCode`

**Response:** `200` with count of created students

---

## 3. DOCTOR APIs

### 3.1 — Get Doctors (Paginated)

**GET** `/api/Doctors?page=1&size=10`

**Auth:** Admin, Doctor, Student

**Response 200:**
```json
{
  "items": [
    {
      "id": "01JDOC...",
      "code": "DOC-AI-001",
      "fullName": "Dr. Khaled Hassan",
      "email": "k.hassan@university.edu",
      "universityEmail": "k.hassan@bsnu.edu.eg",
      "phone": "01098765432",
      "universityStaffId": "STAFF-001",
      "departmentId": "01JDEPT...",
      "departmentName": "Computer Science",
      "collegeName": "Faculty of Engineering"
    }
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 10
}
```

---

### 3.2 — Get Doctor by Code

**GET** `/api/Doctors/{code}`

Example: `/api/Doctors/DOC-AI-001`

---

### 3.3 — Get Doctor's Subjects

**GET** `/api/Doctors/{code}/subjects`

**Response:** Array of `SubjectDto` that this doctor teaches

---

### 3.4 — Search Doctors

**GET** `/api/Doctors/search?q=khaled`

---

### 3.5 — Update Doctor *(Admin only)*

**PATCH** `/api/Doctors/{id}`

```json
{
  "fullName": "Dr. Khaled Hassan Updated",
  "phone": "01099999999"
}
```

---

## 4. SUBJECT & SUBJECT OFFERING APIs

### 4.1 — Get Subjects by Batch

**GET** `/api/Subjects/by-batch/{batchId}`

> Cached for 5 minutes. Returns all subjects in the curriculum for that batch level.

**Response:**
```json
[
  {
    "id": "01JSUB...",
    "code": "CS301",
    "name": "Data Structures",
    "creditHours": 3,
    "departmentName": "Computer Science",
    "collegeName": "Faculty of Engineering",
    "batchName": "Level 3 - CS 2026",
    "doctorName": "Dr. Khaled Hassan"
  }
]
```

---

### 4.2 — My Subjects (Role-Aware)

**GET** `/api/Subjects/my-subjects`

- **As Student:** Returns subjects in your batch/curriculum
- **As Doctor:** Returns subjects you are assigned to teach

---

### 4.3 — Get My Subject Offerings (Student)

**GET** `/api/SubjectOfferings/my-enrollments`

**Auth:** Student

**Response:**
```json
[
  {
    "id": "01JOFFER...",
    "subjectId": "01JSUB...",
    "subjectCode": "CS301",
    "subjectName": "Data Structures",
    "semesterName": "Fall 2026",
    "doctorName": "Dr. Khaled Hassan",
    "departmentName": "Computer Science",
    "batchName": "Level 3",
    "groupName": "Group A"
  }
]
```

---

### 4.4 — Get My Offerings (Doctor)

**GET** `/api/SubjectOfferings/my-offerings`

**Auth:** Doctor

> JWT-aware endpoint — no ID needed in URL. Returns subjects this doctor is teaching this semester.

**Response:** Same shape as above, scoped to doctor's assignments

---

### 4.5 — Get Offering by Code

**GET** `/api/SubjectOfferings/by-code/{code}`

---

### 4.6 — Create Subject Offering *(Admin only)*

**POST** `/api/SubjectOfferings`

```json
{
  "subjectCode": "CS301",
  "semesterId": "01JSEM...",
  "doctorCode": "DOC-AI-001",
  "departmentCode": "CS-DEPT",
  "batchCode": "BATCH-CS-2026",
  "groupCode": "GROUP-A"
}
```

---

### 4.7 — Create Subject *(Admin only)*

**POST** `/api/Subjects`

```json
{
  "name": "Data Structures",
  "code": "CS301",
  "creditHours": 3,
  "departmentCode": "CS-DEPT",
  "collegeCode": "ENG-COL",
  "batchCode": "BATCH-CS-2026"
}
```

---

### 4.8 — Search Subjects

**GET** `/api/Subjects/search?name=data`

---

### 4.9 — Assign Doctor to Subject *(Admin only)*

**PUT** `/api/Subjects/assign-doctor?subjectCode=CS301&doctorCode=DOC-AI-001`

---

## 5. ENROLLMENT APIs

### 5.1 — Enroll in Subject *(Student only)*

**POST** `/api/Enrollments/{offeringId}`

> Student JWT is used to identify who is enrolling — no body needed.

**Response 200:**
```json
{
  "id": "01JENROLL...",
  "studentId": "01JSTU...",
  "studentName": "Ahmed Mohamed Ali",
  "subjectOfferingId": "01JOFFER...",
  "subjectName": "Data Structures",
  "doctorName": "Dr. Khaled Hassan",
  "semesterName": "Fall 2026",
  "enrolledAt": "2026-05-14T10:00:00Z",
  "isActive": true
}
```

**Error:** `409` if already enrolled | `404` if offering not found

---

### 5.2 — My Enrollments *(Student only)*

**GET** `/api/Enrollments/my-enrollments`

**Response:** Array of enrollment objects (see 5.1 shape)

---

### 5.3 — Enrollments by Offering *(Doctor/Admin)*

**GET** `/api/Enrollments/by-offering/{offeringId}`

**Response:** All students enrolled in that subject offering

---

### 5.4 — Reactivate Enrollment *(Admin/SuperAdmin)*

**PUT** `/api/Enrollments/{id}/reactivate`

---

### 5.5 — Unenroll *(Admin only)*

**DELETE** `/api/Enrollments/{id}`

---

## 6. MATERIALS APIs

> Materials are files (PDF, Word, PPT, video, etc.) uploaded by doctors to a subject offering.
> Files are stored in **Cloudflare R2** — the backend never streams file content.
> Students/Doctors receive a **pre-signed URL valid for 60 minutes** to download directly from R2.

### 6.1 — Upload Material *(Doctor only)*

**POST** `/api/Materials/upload`

**Content-Type:** `multipart/form-data`

**Form Fields:**
| Field | Type | Required | Notes |
|-------|------|----------|-------|
| OfferingId | string (ULID) | ✅ | SubjectOffering this file belongs to |
| File | file | ✅ | Max 500MB |

**Allowed File Types:**
- PDF, Word (.doc, .docx), PowerPoint (.ppt, .pptx), Excel (.xls, .xlsx)
- Images (JPEG, PNG, GIF), Video (MP4, WebM), Text, ZIP

**Response 201:**
```json
{
  "id": "01JMAT...",
  "fileName": "lecture-01.pdf",
  "contentType": "application/pdf",
  "fileSize": 2048576,
  "uploadedAt": "2026-05-14T10:30:00Z",
  "uploadedByDoctor": "Dr. Khaled Hassan",
  "subjectOfferingId": "01JOFFER..."
}
```

**Error Responses:**
- `400` — No file provided
- `400` — File type not allowed
- `400` — File exceeds 500 MB

**Internal Flow:**
```
Doctor → POST /api/Materials/upload
  → MaterialsController extracts doctorId from JWT (ProfileId claim)
  → Validates file type + size
  → MaterialService.UploadMaterialAsync()
    → Looks up Doctor.SystemUserId (for FK constraint on UploadedFiles table)
    → FileService.UploadFileStreamAsync(systemUserId, stream, fileName, contentType, size)
      → Uploads to Cloudflare R2
      → Saves UploadedFile record in DB (storageKey, fileName, size, uploadedByUserId)
    → Saves Material record in DB linked to SubjectOffering + UploadedFile
  → Returns MaterialDto
```

---

### 6.2 — Get Materials by Offering

**GET** `/api/Materials/by-offering/{offeringId}?page=1&pageSize=10&search=lecture`

**Auth:** Student (enrolled), Doctor (assigned), Admin

**Query Params:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| page | int | 1 | Page number |
| pageSize | int | 10 | Items per page |
| search | string? | - | Filter by filename |

**Response 200:**
```json
{
  "materials": [
    {
      "id": "01JMAT...",
      "fileName": "lecture-01.pdf",
      "contentType": "application/pdf",
      "fileSize": 2048576,
      "uploadedAt": "2026-05-14T10:30:00Z",
      "uploadedByDoctor": "Dr. Khaled Hassan"
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 10
}
```

**Business Rule:** Students can only see materials if they are actively enrolled in that offering.

---

### 6.3 — Download Material (Get Signed URL)

**GET** `/api/Materials/download/{id}`

**Auth:** Student, Doctor, Admin

**Response 200:**
```json
{
  "SignedUrl": "https://r2.cloudflare.com/bucket/lecture-01.pdf?X-Amz-Signature=abc...&X-Amz-Expires=3600",
  "ExpiresInMinutes": 60
}
```

**Frontend must:** Open `SignedUrl` directly in browser (link/download) — URL expires in 60 min. Do NOT store it — always fetch fresh.

---

### 6.4 — Get Material Metadata

**GET** `/api/Materials/{id}/metadata`

**Auth:** Any authenticated user

**Response 200:**
```json
{
  "materialId": "01JMAT...",
  "fileName": "lecture-01.pdf",
  "contentType": "application/pdf",
  "fileSize": 2048576,
  "uploadedAt": "2026-05-14T10:30:00Z",
  "signedUrl": "https://r2.cloudflare.com/...",
  "expiresInMinutes": 60
}
```

---

### 6.5 — Delete Material *(Doctor only)*

**DELETE** `/api/Materials/{id}`

> Only the doctor who uploaded the material can delete it.

**Response:** `204 No Content`

---

## 7. EXAM APIs

### 7.1 — Create Exam *(Doctor only)*

**POST** `/api/Exams?subjectOfferingId={offeringId}`

**Request Body:**
```json
{
  "title": "Midterm Exam - Data Structures",
  "type": "MCQ",
  "totalMarks": 100,
  "startTime": "2026-06-01T09:00:00Z",
  "endTime": "2026-06-01T11:00:00Z",
  "status": "Draft",
  "questions": [
    {
      "questionText": "What is the time complexity of binary search?",
      "correctAnswer": "O(log n)",
      "mark": 10
    }
  ]
}
```

**Exam Types:** `MCQ` | `Essay` | `Mixed` | `PDF`

**Exam Status:** `Draft` | `Published` | `Active` | `Closed`

**Response 201:** Full `ExamDto` with generated `id` and `code`

---

### 7.2 — Generate AI Exam *(Doctor only)*

**POST** `/api/Exams/generate-ai`

> The doctor sends topic/subject context and the AI generates questions.

**Request Body:**
```json
{
  "subjectOfferingId": "01JOFFER...",
  "topic": "Binary Trees and Graph Traversal",
  "questionCount": 20,
  "difficulty": "Medium",
  "examType": "MCQ",
  "totalMarks": 100,
  "startTime": "2026-06-01T09:00:00Z",
  "endTime": "2026-06-01T11:00:00Z"
}
```

**Internal Flow:**
```
Doctor → POST /api/Exams/generate-ai
  → ExamController extracts doctorId from JWT
  → ExamService.GenerateAiExamAsync()
    → Calls FastAPI AI service (POST /api/chat internally? No — backend calls AI directly)
    → OR uses local AI model to generate questions
    → Saves Exam + ExamQuestion records in DB
  → Returns ExamDto with generated questions
```

---

### 7.3 — Upload PDF Exam *(Doctor only)*

**POST** `/api/Exams/upload-pdf?subjectOfferingId={offeringId}`

**Content-Type:** `multipart/form-data`

**Body:** `file` field with PDF

**Response 201:** `ExamDto` with `type: "PDF"` and `filePath` set

---

### 7.4 — Get My Exams *(Doctor only)*

**GET** `/api/Exams/my-exams`

**Response:** Array of `ExamDto` created by the authenticated doctor

---

### 7.5 — Get Exams by Offering

**GET** `/api/Exams/by-offering/{offeringId}`

**Auth:** Doctor, Admin

**Response:** Array of `ExamDto` for that offering

---

### 7.6 — Get My Enrolled Exams *(Student only)*

**GET** `/api/Exams/my-enrolled-exams`

**Response:** Array of exams the student is eligible to take (based on their enrollments)

---

### 7.7 — Get Exam by ID

**GET** `/api/Exams/{id}`

**Auth:** Doctor (own exams), Student (enrolled exams), Admin

**Response:**
```json
{
  "id": "01JEXAM...",
  "code": "EXAM-CS301-001",
  "title": "Midterm Exam - Data Structures",
  "type": "MCQ",
  "totalMarks": 100,
  "startTime": "2026-06-01T09:00:00Z",
  "endTime": "2026-06-01T11:00:00Z",
  "status": "Published",
  "subjectName": "Data Structures",
  "createdByDoctorId": "01JDOC...",
  "questions": [
    {
      "id": "01JQUEST...",
      "questionText": "What is the time complexity of binary search?",
      "mark": 10
    }
  ]
}
```

> **Note:** `correctAnswer` is NOT returned to students — only to doctors.

---

### 7.8 — Submit Exam *(Student only)*

**POST** `/api/Exams/{id}/submit`

```json
{
  "answers": [
    {
      "questionId": "01JQUEST...",
      "answer": "O(log n)"
    }
  ]
}
```

**Response:** `ExamSubmissionDto` with `submittedAt`

**Business Rules:**
- Can only submit once per exam
- Can only submit within the `startTime` → `endTime` window
- Late submission → `400`

---

### 7.9 — Get My Submission *(Student only)*

**GET** `/api/Exams/{id}/my-submission`

---

### 7.10 — Get All Submissions (Doctor)

**GET** `/api/Exams/{id}/results`

**Auth:** Doctor (own exam only)

**Response:** Array of `ExamSubmissionDto` with student name, answers, score

---

### 7.11 — Grade Submission *(Doctor only)*

**POST** `/api/Exams/grade-submission`

```json
{
  "submissionId": "01JSUB...",
  "score": 85,
  "feedback": "Good work on trees, weak on graphs."
}
```

---

### 7.12 — Auto-Grade Exam *(Doctor only)*

**POST** `/api/Exams/{id}/auto-grade`

> Automatically grades all MCQ submissions by comparing answers to `correctAnswer` fields.

---

## 8. GRADES & GPA APIs

### 8.1 — Import Grades from Excel *(Doctor/Admin)*

**POST** `/api/Grades/import/{offeringId}`

**Content-Type:** `multipart/form-data`

**Body:** `file` field with `.xlsx`

**Excel Columns:** `StudentCode`, `AssignmentGrade`, `MidGrade`, `ParticipationGrade`, `FinalGrade`

**Internal Flow:**
```
Doctor → Upload Excel
  → GradesController extracts offeringId
  → GradeService reads Excel rows
  → For each row: upserts StudentGrade record
  → Calculates totalGrade = assignment + mid + participation + final
  → Assigns letterGrade (A=90+, B=80+, C=70+, D=60+, F=below)
  → Triggers GPA recalculation for each affected student
```

---

### 8.2 — Calculate Grades for Offering *(Doctor)*

**POST** `/api/Grades/calculate/{offeringId}`

> Triggers recalculation of all grades for all enrolled students in that offering.

---

### 8.3 — Update Single Grade *(Admin only)*

**PUT** `/api/Grades/{id}`

```json
{
  "assignmentGrade": 20,
  "midGrade": 25,
  "participationGrade": 5,
  "finalGrade": 40
}
```

---

### 8.4 — Get My GPA *(Student only)*

**GET** `/api/Gpa/my-gpa`

> JWT-aware endpoint — no ID needed. Returns the authenticated student's cumulative GPA.

**Response:**
```json
{
  "studentId": "01JSTU...",
  "studentName": "Ahmed Mohamed Ali",
  "gpa": 3.45,
  "totalCreditHours": 90,
  "completedCreditHours": 72,
  "letterGrade": "B+",
  "semesterGpas": [
    {
      "semesterName": "Fall 2025",
      "gpa": 3.6,
      "creditHours": 18
    }
  ]
}
```

---

### 8.5 — Get Student GPA *(Admin/Doctor)*

**GET** `/api/Gpa/student/{studentId}`

---

### 8.6 — Recalculate GPA *(Admin/SuperAdmin)*

**POST** `/api/Grades/{gradeId}/recalculate`

> Forces fresh GPA calculation for the student linked to this grade record.

---

## 9. ATTENDANCE APIs

### 9.1 — Create Attendance Session *(Doctor/TA/Admin)*

**POST** `/api/Attendance/sessions`

```json
{
  "subjectId": "01JSUB...",
  "doctorId": "01JDOC...",
  "teachingAssistantId": null
}
```

**Response:** `AttendanceSessionDto` with `sessionId` and `startTime`

---

### 9.2 — Record Attendance *(Student)*

**POST** `/api/Attendance/check-in`

```json
{
  "sessionId": "01JSESSION..."
}
```

> Student checks in to an active session. Time-limited — session must be open.

**Response:** `200 { "isPresent": true, "recordedAt": "..." }`

---

### 9.3 — Get Student Attendance Report *(Doctor/TA/Admin)*

**GET** `/api/Attendance/student/{studentId}/report?subjectId={subjectId}`

**Response:**
```json
{
  "studentId": "01JSTU...",
  "studentName": "Ahmed Mohamed Ali",
  "subjectName": "Data Structures",
  "totalSessions": 24,
  "presentCount": 20,
  "absentCount": 4,
  "attendancePercentage": 83.3,
  "records": [
    {
      "sessionId": "01JSESSION...",
      "date": "2026-04-01T09:00:00Z",
      "isPresent": true
    }
  ]
}
```

---

### 9.4 — Correct Attendance *(Admin/SuperAdmin)*

**POST** `/api/Attendance/correct?sessionId={id}&studentId={id}&isPresent=true`

---

## 10. COMPLAINT APIs

### 10.1 — Create Complaint *(Student only)*

**POST** `/api/Complaints`

```json
{
  "title": "Unfair grading on midterm",
  "message": "I believe my midterm grade was incorrectly calculated...",
  "targetType": "Doctor",
  "targetId": "01JDOC..."
}
```

**`targetType` values:** `Doctor` | `Subject` | `Department` | `University` | `General`

**Response 201:**
```json
{
  "id": "01JCOMP...",
  "title": "Unfair grading on midterm",
  "message": "...",
  "status": "Pending",
  "priority": "Medium",
  "createdAt": "2026-05-14T10:00:00Z",
  "analysis": {
    "category": "grading",
    "severity": "medium",
    "sentiment": "negative",
    "isDuplicate": false
  }
}
```

**Internal Flow:**
```
Student → POST /api/Complaints
  → ComplaintService.CreateComplaintAsync()
  → Saves complaint to DB
  → Calls FastAPI AI: POST /api/ai/analyze-complaint
    → LLM analysis: category, severity, sentiment, duplicate detection
  → Saves ComplaintAnalysis linked to complaint
  → Updates/creates ComplaintCluster for this targetType+targetId
  → Returns ComplaintDto with analysis
```

---

### 10.2 — My Complaints *(Student)*

**GET** `/api/Complaints/my-complaints?page=1&size=10&status=Pending`

**Query Params:** `page`, `size`, `status` (Pending/Resolved/Dismissed)

---

### 10.3 — My Reports *(Doctor)*

**GET** `/api/Complaints/my-reports?page=1&size=10`

> Returns complaints filed against this doctor

---

### 10.4 — All Complaints *(Admin/SuperAdmin)*

**GET** `/api/Complaints/all?page=1&size=20&status=Pending&priority=High`

---

### 10.5 — Complaint Clusters *(Admin/Doctor)*

**GET** `/api/Complaints/clusters?targetType=Doctor&targetId=01JDOC...`

> Returns AI-generated clusters of similar complaints. Used for pattern detection and priority escalation.

**Response:**
```json
[
  {
    "id": "01JCLUSTER...",
    "targetType": "Doctor",
    "targetId": "01JDOC...",
    "topic": "Grading unfairness",
    "complaintCount": 7,
    "lastUpdated": "2026-05-14T08:00:00Z"
  }
]
```

---

## 11. CHAT & AI APIs

### 11.1 — Create Conversation

**POST** `/api/Chat/conversations`

```json
{ "title": "My Academic Questions" }
```

**Response:** `{ "id": "01JCONV..." }`

---

### 11.2 — Get Conversations

**GET** `/api/Chat/conversations`

**Response:**
```json
[
  {
    "id": "01JCONV...",
    "title": "My Academic Questions",
    "createdAt": "2026-05-14T10:00:00Z",
    "updatedAt": "2026-05-14T11:30:00Z"
  }
]
```

---

### 11.3 — Get Conversation Messages

**GET** `/api/Chat/conversations/{id}/messages?page=1&pageSize=50`

**Response:**
```json
{
  "messages": [
    {
      "id": "01JMSG...",
      "role": "user",
      "content": "What's my GPA?",
      "timestamp": "2026-05-14T10:01:00Z"
    },
    {
      "id": "01JMSG2...",
      "role": "assistant",
      "content": "يا أحمد 👋\nالـ GPA بتاعك حالياً هو 3.45 — وده كويس جداً!",
      "timestamp": "2026-05-14T10:01:03Z"
    }
  ],
  "totalCount": 24,
  "page": 1,
  "pageSize": 50
}
```

---

### 11.4 — Send Message (AI Chat) — **MAIN ENDPOINT**

**POST** `/api/Chat/messages`

> This is the most important endpoint. It routes through the .NET backend → FastAPI AI.

**Request Body:**
```json
{
  "conversationId": "01JCONV...",
  "message": "كام مادة عندي الفصل الحالي؟"
}
```

**Response 200:**
```json
{
  "userMessage": {
    "id": "01JMSG...",
    "role": "user",
    "content": "كام مادة عندي الفصل الحالي؟",
    "timestamp": "2026-05-14T10:01:00Z"
  },
  "aiMessage": {
    "id": "01JMSG2...",
    "role": "assistant",
    "content": "يا أحمد 👋\nعندك 5 مواد الفصل الحالي:\n1. Data Structures\n2. Algorithms\n3. Database Systems\n4. Networks\n5. Software Engineering\n\nعايز تعرف تفاصيل مادة معينة؟",
    "timestamp": "2026-05-14T10:01:03Z"
  },
  "suggestions": [
    "Check my grades",
    "What's my schedule today?",
    "Explain a course topic"
  ]
}
```

**Full Internal Flow:**
```
User → POST /api/Chat/messages (to .NET backend)
  → ChatController.SendMessageAsync()
    → Extracts userId (from JWT nameid), role (from JWT role), profileId
    → ChatService.SendMessageAsync()
      → Saves user message to ChatMessages table
      → Loads conversation history (last 10 messages)
      → Loads student academic context (GPA, courses, batch, department)
      → Calls FastAPI AI: POST /api/chat
          Body: {
            message, user_id, role,
            conversation_id, history[],
            academic_context: {
              studentName, batchId, batchName, departmentName,
              collegeName, gpa, enrolledCourses[], subjectOfferingIds[]
            }
          }
      ← FastAPI returns ChatResponse
      → Saves AI response to ChatMessages table
      → Returns both messages to client
```

---

### 11.5 — Delete Message *(Admin/SuperAdmin only)*

**DELETE** `/api/Chat/messages/{id}`

**Response:** `204 No Content`

---

### 11.6 — Delete Conversation

**DELETE** `/api/Chat/conversations/{id}`

> Only the owner can delete. Returns `403` if not owner, `404` if not found.

**Response:** `204 No Content`

---

### 11.7 — Rename Conversation

**PUT** `/api/Chat/conversations/{id}`

```json
{ "title": "New Conversation Title" }
```

**Response:** `200 { "title": "New Conversation Title" }`

---

## 12. ADMIN & STRUCTURE APIs

### 12.1 — University Structure (Colleges, Departments, Batches)

**GET** `/api/Colleges` — List all colleges
**GET** `/api/Departments` — List all departments
**GET** `/api/Departments/by-college/{collegeId}` — Departments in a college
**GET** `/api/Batches` — All batches
**GET** `/api/Groups` — All groups
**GET** `/api/academic-years` — All academic years
**GET** `/api/Semesters/by-academic-year/{academicYearId}` — Semesters in a year

---

### 12.2 — Dashboard Stats *(Admin)*

**GET** `/api/Dashboard`

**Response:**
```json
{
  "totalStudents": 1250,
  "totalDoctors": 45,
  "totalSubjects": 120,
  "totalEnrollments": 8500,
  "activeComplaints": 23,
  "recentActivity": [...]
}
```

---

### 12.3 — Schedule APIs

**GET** `/api/Schedule/batch/{batchId}` — Full weekly schedule for a batch
**GET** `/api/Schedule/batch/{batchId}/today` — Today's classes for batch
**GET** `/api/Schedule/batch/{batchId}/day/{dayNumber}` — Specific day (0=Sun, 6=Sat)
**GET** `/api/Schedule/my-schedule` — Doctor's full weekly schedule (JWT-aware)
**GET** `/api/Schedule/my-today` — Doctor's classes today (JWT-aware)
**GET** `/api/Schedule/offering/{offeringId}` — Schedule for a specific offering

---

### 12.4 — Regulations

**GET** `/api/Regulations` — All academic regulations
**GET** `/api/Regulations/{id}` — Specific regulation with linked subjects

---

### 12.5 — Notifications

**GET** `/api/Notifications` — My notifications
**PUT** `/api/Notifications/{id}/read` — Mark as read
**DELETE** `/api/Notifications/{id}` — Delete notification

---

### 12.6 — Audit Logs *(Admin/SuperAdmin)*

**GET** `/api/AuditLogs?page=1&size=50&entityType=Student`

**Response:** Paginated list of all create/update/delete operations with old+new values

---

## 13. AI ORCHESTRATION SERVICE (FastAPI)

> The frontend does NOT call this service directly.
> The .NET backend calls it internally when routing chat messages.
> However, you need to understand the request/response shape to debug issues.

### 13.1 — Chat Endpoint

**POST** `/api/chat` *(FastAPI service)*

**Full Request Schema:**
```json
{
  "message": "كام مادة عندي الفصل الحالي؟",
  "user_id": "01JXXX...",
  "role": "student",
  "conversation_id": "01JCONV...",
  "history": [
    { "role": "user", "content": "مرحبا" },
    { "role": "assistant", "content": "أهلاً يا أحمد!" }
  ],
  "academic_context": {
    "studentName": "Ahmed Mohamed Ali",
    "userId": "01JXXX...",
    "profileId": "01JSTU...",
    "userCode": "STU20260001",
    "batchId": "01JBATCH...",
    "batchName": "Level 3 - CS 2026",
    "departmentId": "01JDEPT...",
    "departmentName": "Computer Science",
    "collegeId": "01JCOL...",
    "collegeName": "Faculty of Engineering",
    "gpa": 3.45,
    "enrolledCourses": [
      { "subjectOfferingId": "01JOFFER...", "subjectName": "Data Structures" }
    ],
    "subjectOfferingIds": ["01JOFFER..."]
  },
  "explain": false
}
```

**Full Response Schema:**
```json
{
  "response": "يا أحمد 👋\nعندك 5 مواد الفصل الحالي:\n...",
  "conversation_id": "01JCONV...",
  "intent_executed": "backend_api_query",
  "tool_used": "dynamic_api_module",
  "model_used": "openai/gpt-4o-mini",
  "metadata": {
    "auth_header": "Bearer eyJ...",
    "explain": false,
    "executor_data": {
      "suggestions": ["Check my grades", "What's my schedule?"],
      "actions_available": ["Check my grades", "What's my schedule?"]
    }
  },
  "suggestions": ["Check my grades", "What's my schedule?"],
  "actions_available": ["Check my grades", "What's my schedule?"]
}
```

---

### 13.2 — AI Complaint Analysis

**POST** `/api/ai/analyze-complaint` *(FastAPI service)*

> Called internally by .NET backend after a complaint is created.

**Request:**
```json
{
  "complaintText": "I believe my midterm grade was incorrectly calculated...",
  "targetType": "Doctor",
  "targetId": "01JDOC..."
}
```

**Response:**
```json
{
  "category": "grading",
  "severity": "medium",
  "sentiment": "negative",
  "isDuplicate": true,
  "clusterInfo": {
    "clusterId": "01JCLUSTER...",
    "topic": "Grading unfairness",
    "existingComplaintCount": 6
  }
}
```

---

### 13.3 — Health Check

**GET** `/health` *(FastAPI service)*

```json
{ "status": "ok", "service": "fastapi-ai-service" }
```

---

## 14. COMPLETE SYSTEM FLOW DIAGRAMS

### 14.1 — Authentication Flow

```
User submits login form
  → POST /api/Auth/login
  → .NET: AuthService verifies password hash (BCrypt)
  → If OK: Generate JWT (1h) + RefreshToken (7 days, saved to DB)
  → Return token + role + mustChangePassword
Frontend:
  → Store token in memory
  → Store refreshToken in httpOnly cookie
  → Decode JWT to get role + profileId + nameid
  → If mustChangePassword == true → redirect to /change-password
  → Else → redirect to role-appropriate dashboard
```

---

### 14.2 — AI Chat Full Flow

```
Student types: "كام مادة عندي الفصل الحالي؟"
  → Frontend: POST /api/Chat/messages { conversationId, message }
  
  .NET ChatController:
    → Extract userId (JWT nameid), role, profileId
    → Load conversation history from ChatMessages table
    → Load academic context:
        - Student name, batch, department, college
        - GPA from Gpa table
        - Enrolled courses from Enrollments + SubjectOfferings
    → POST to FastAPI /api/chat with full context
    
  FastAPI Pipeline:
    [1] Rate Limit Check (30 RPM per user)
    [2] Correlation ID assigned (X-Request-ID header)
    [3] Build ExecutionContext
    [4] Agent.run(context):
    
        [Planner] PlannerAgent:
          → Layer-2 deterministic check (keyword scan for exam generation)
          → LLM call (gpt-4o-mini) to classify intent + extract params
          → Intent classified: "backend_api_query"
          → Returns ExecutionPlan { intent: "backend_api_query", steps: [] }
          
        [RBAC Gate] PlanExecutor:
          → Check: is "backend_api_query" allowed for role "student"?
          → Answer: YES ✅
          
        [Module Dispatch] → "dynamic_api_module" module selected
        
        [DynamicApiModule]:
          → Load allowed Swagger schema from api_discovery cache
          → LLM call 1 (routing): "which endpoint answers this question?"
            → LLM selects: GET /api/SubjectOfferings/my-enrollments
          → Validate endpoint against allowlist ✅
          → Execute: GET /api/SubjectOfferings/my-enrollments
              with Authorization: Bearer <forwarded JWT>
          → Receive raw JSON from .NET backend
          → LLM call 2 (summarization): convert JSON → natural language
          → Return narrative + suggestions
          
    FastAPI → .NET: ChatResponse { response, intent_executed, suggestions }
    
  .NET ChatService:
    → Save AI response to ChatMessages table
    → Return both messages to frontend
    
  Frontend:
    → Display AI response in chat UI
    → Show suggestion chips below response
```

---

### 14.3 — Material Upload Flow

```
Doctor opens "Upload Material" screen
  → Selects file (PDF, max 500MB) + SubjectOffering
  → POST /api/Materials/upload (multipart/form-data)
  
  .NET MaterialsController:
    → Extract doctorId from JWT (ProfileId claim)
    → Validate file type (MIME allowlist)
    → Validate file size (≤ 500MB)
    
  MaterialService.UploadMaterialAsync():
    → Lookup Doctor → get Doctor.SystemUserId
      (needed because UploadedFiles.UploadedByUserId FK → SystemUsers, not Doctors)
    → FileService.UploadFileStreamAsync(systemUserId, fileStream, fileName, contentType, size)
      → Generate unique storageKey (ULID + filename)
      → Upload to Cloudflare R2 bucket
      → Save UploadedFile record in DB
    → Save Material record in DB:
        { fileName, contentType, fileSize, storageKey, subjectOfferingId,
          uploadedByDoctorId, fileId (FK to UploadedFiles) }
    → Return MaterialDto
    
  Frontend receives 201 MaterialDto
  → Update material list in UI
  
Later, student accesses material:
  → GET /api/Materials/by-offering/{offeringId}
  → GET /api/Materials/download/{materialId}
    → Backend generates 60-minute pre-signed R2 URL
    → Frontend opens URL in browser → R2 serves file directly
```

---

### 14.4 — Exam Generation via AI Chat

```
Doctor types: "عايز تعمل امتحان لمادة Data Structures عن Binary Trees"

  .NET ChatController → FastAPI:
    → academic_context includes: doctorId, subjectOfferingIds, departmentName
    
  FastAPI Planner:
    → Layer-2 deterministic scan: "عايز تعمل امتحان" → matches Arabic exam keywords
    → Overrides LLM → intent = "generate_exam" ✅ (guaranteed no misclassification)
    
  PlanExecutor RBAC:
    → "generate_exam" allowed for "doctor"? YES ✅
    
  ExamGenerationModule:
    → Extracts: topic, questionCount, subjectOfferingId from academic_context
    → LLM call: generate N questions with answers for topic X
    → POST /api/Exams/generate-ai to .NET backend:
        { subjectOfferingId, topic, questions: [...], totalMarks, startTime, endTime }
    → .NET ExamService creates Exam + ExamQuestion records in DB
    → Returns ExamDto
    → Module formats response: "تم إنشاء الامتحان! فيه 20 سؤال عن Binary Trees ..."
    
  Doctor can then:
    → POST /api/Exams/{id}/auto-grade (for MCQ)
    → GET /api/Exams/{id}/results (to view submissions)
```

---

### 14.5 — GPA Calculation Flow

```
Doctor uploads grade Excel:
  → POST /api/Grades/import/{offeringId} (multipart Excel file)
  
  GradeService:
    → Parse Excel rows
    → For each student row:
        → Upsert StudentGrade record
        → totalGrade = assignmentGrade + midGrade + participationGrade + finalGrade
        → letterGrade = (90+=A, 80+=B, 70+=C, 60+=D, <60=F)
    → For each affected student:
        → GpaService.RecalculateGpaAsync(studentId):
            → Load all StudentGrade records for student
            → For each passing grade: weight by subject creditHours
            → GPA = Σ(grade × creditHours) / Σ(creditHours)   [4.0 scale]
            → Update Student.GPA field
```

---

### 14.6 — Dynamic API Module Flow (AI → Backend)

```
User asks anything that requires live data from the backend:
  → "مين أنا؟" / "جدولي امتى" / "الطلاب في قسمي" / etc.
  
  DynamicApiModule:
    [Step 1] Load Swagger schema from cache (fetched at startup, filtered by allowlist)
    
    [Step 2] LLM routing call (JSON mode):
      Input: message + role + academic_context + schema
      Output: { "endpoint": "/api/SubjectOfferings/my-enrollments", "method": "GET", "params": {} }
      
    [Step 3] Placeholder substitution:
      If endpoint contains {batchId} → substitute from academic_context.batchId
      If endpoint contains {profileId} → substitute from academic_context.profileId
      Any unresolved placeholder → return error (never guess)
      
    [Step 4] Allowlist validation:
      validate_endpoint(method, endpoint) — checks against allowed set
      DELETE/PUT/PATCH → always blocked
      /api/auth/* → always blocked
      /api/dev/* → always blocked
      
    [Step 5] Execute HTTP request to .NET backend:
      GET → backend_client.fetch(route, auth_header, params)
      POST → backend_client.post(route, payload, auth_header)
      The student's JWT is forwarded → .NET backend enforces data-level RBAC
      
    [Step 6] Handle response:
      401/403 from backend → return friendly "no permission" message in Arabic
      Empty response → "مش لاقي بيانات"
      
    [Step 7] LLM summarization call (JSON mode):
      Input: raw_backend_json + user_message + role + academic_context
      Output: { narrative, suggestions, explain_text }
      
    [Step 8] Return to user:
      narrative = human-readable Arabic/English response
      suggestions = 3 follow-up action chips
```

---

### 14.7 — RBAC Two-Layer Security

```
Layer 1 — FastAPI RBAC (Intent Level):
  Prevents users from triggering operations outside their role.
  Source of truth: app/core/rbac.py

  student  → CAN:  general_chat, backend_api_query, complaint_submit,
                   result_query, file_extraction, summarization,
                   cv_analysis, academic_advice, material_explanation
  student  → CANNOT: generate_exam, complaint_summary, file_processing

  doctor   → CAN:  general_chat, backend_api_query, generate_exam,
                   complaint_summary, result_query, summarization,
                   academic_advice, file_extraction, cv_analysis
  doctor   → CANNOT: complaint_submit, file_processing

  admin    → CAN: everything

  If blocked → bilingual denial message (Arabic + English)
  All blocked attempts → logged to audit trail (structured WARNING log)

Layer 2 — .NET Backend RBAC (Data Level):
  Even if FastAPI calls an allowed endpoint, .NET validates the JWT again.
  A student's JWT cannot access another student's data.
  A doctor's JWT cannot access admin endpoints.
  This means: FastAPI never needs to trust its own RBAC alone.
  The backend is the final authority on data access.
```

---

### 14.8 — Hallucination Prevention Architecture

```
PROBLEM: LLMs can fabricate grades, GPA, student names, or course lists.
SOLUTION: Multi-layer anti-hallucination system.

[Gate 1] Data-Sensitive Intent Detection:
  Intents: backend_api_query, complaint_summary, file_processing,
           generate_exam, academic_advice
  If these intents reach _fallback_model_call() without backend data having been fetched:
  → BLOCKED: "مش لاقي بيانات من السيستم" returned immediately.
  → LLM never generates a response for data it doesn't have.

[Gate 2] Global Data Guard (_is_raw_data_request):
  Keyword scan on user message for: "كم", "عدد", "درجات", "gpa", "جدول", etc.
  If keywords present AND no backend call made → BLOCKED.

[Gate 3] Academic Context Injection:
  Real verified data (name, GPA, courses) injected into system prompt as:
  "⚠️ VERIFIED STUDENT PROFILE — use this in your response"
  LLM instructed: "NEVER invent data not listed above"

[Gate 4] Input Sanitization:
  All user input HTML-escaped + truncated to 4000 chars before LLM injection.
  Prevents prompt injection attacks.

[Gate 5] Raw JSON Never Shown:
  DynamicApiModule always summarizes raw backend JSON via LLM.
  Binary fields, internal keys stripped before LLM sees data.
  Technical identifiers, ULIDs, field names never shown to user.
```

---

## 15. JWT TOKEN REFERENCE

### Claims in Every JWT

| Claim | Value | Used For |
|-------|-------|----------|
| `nameid` | SystemUser ULID | Chat ownership, conversation linking |
| `ProfileId` | Doctor/Student/Admin profile ULID | Academic queries, material upload auth |
| `role` | Student / Doctor / Admin / SuperAdmin | Role-based UI rendering + API authorization |
| `userCode` | STU20260001 / DOC-AI-001 | URL params for profile endpoints |
| `email` | user@university.edu | Display |
| `exp` | Unix timestamp | Token expiry |

### How to Use Claims on Frontend

```javascript
// After login, decode the JWT:
const claims = JSON.parse(atob(token.split('.')[1]));

const systemUserId = claims.nameid;      // for chat API
const profileId    = claims.ProfileId;   // for academic context
const role         = claims.role;        // for conditional UI
const userCode     = claims.userCode;    // for profile page URL

// Build academic_context to pass with every chat message:
const academicContext = {
  userId:      systemUserId,
  profileId:   profileId,
  userCode:    userCode,
  role:        role,
  // + fetch these from API on login and cache:
  studentName: currentUser.fullName,
  batchId:     currentUser.batchId,
  batchName:   currentUser.batchName,
  departmentId: currentUser.departmentId,
  departmentName: currentUser.departmentName,
  collegeId:   currentUser.collegeId,
  collegeName: currentUser.collegeName,
  gpa:         currentUser.gpa,
  enrolledCourses: [],   // fetch from /api/SubjectOfferings/my-enrollments
};
```

---

## 16. ERROR HANDLING REFERENCE

### Standard HTTP Errors

| Status | Meaning | When |
|--------|---------|------|
| `400` | Bad Request | Invalid IDs, missing required fields, file type not allowed |
| `401` | Unauthorized | Missing/expired JWT or wrong credentials |
| `403` | Forbidden | Valid JWT but wrong role for this endpoint |
| `404` | Not Found | Entity doesn't exist or was soft-deleted |
| `409` | Conflict | Duplicate enrollment, duplicate code |
| `413` | Payload Too Large | File > 500MB |
| `422` | Validation Error | DTO validation failed (e.g., missing required field) |
| `429` | Too Many Requests | Rate limit exceeded (FastAPI AI: 30 req/min per user) |
| `500` | Internal Server Error | Unexpected exception |
| `502` | Bad Gateway | FastAPI cannot reach .NET backend |
| `503` | Service Unavailable | AI service not ready (startup in progress) |

### FastAPI-Specific Error Shapes

**Rate limit exceeded (429):**
```json
{
  "detail": "Too many requests. Please wait a moment before trying again."
}
```

**RBAC denied (200 — returned as user message, NOT HTTP error):**
```json
{
  "response": "عذراً، ليس لديك صلاحية للقيام بهذا الإجراء.\nSorry, you don't have permission to perform this action.",
  "intent_executed": "generate_exam",
  "tool_used": "none",
  "model_used": "unknown"
}
```

**No backend data found (200):**
```json
{
  "response": "مش لاقي بيانات من السيستم حالياً، حاول تاني.\n(No data retrieved from the system right now — please try again.)",
  "intent_executed": "backend_api_query"
}
```

---

## 17. academic_context FIELD GUIDE

> This object is passed by the .NET backend to FastAPI with every chat message.
> The AI uses it to answer questions about the user WITHOUT fabricating anything.
> **The frontend should NOT build this manually** — the .NET `ChatService` assembles it.
> But you need to understand it to debug AI responses.

### For Students

```json
{
  "userId":           "01JXXX...",      ← SystemUser.Id (JWT nameid)
  "profileId":        "01JSTU...",      ← Student.Id (JWT ProfileId)
  "userCode":         "STU20260001",    ← Student.Code (for URL params)
  "studentName":      "Ahmed Mohamed Ali",
  "batchId":          "01JBATCH...",
  "batchName":        "Level 3 - CS 2026",
  "departmentId":     "01JDEPT...",
  "departmentName":   "Computer Science",
  "collegeId":        "01JCOL...",
  "collegeName":      "Faculty of Engineering",
  "gpa":              3.45,
  "enrolledCourses":  [
    {
      "subjectOfferingId": "01JOFFER...",
      "subjectName": "Data Structures",
      "doctorName":  "Dr. Khaled Hassan"
    }
  ],
  "subjectOfferingIds": ["01JOFFER...", "01JOFFER2..."]
}
```

### For Doctors

```json
{
  "userId":           "01JXXX...",
  "profileId":        "01JDOC...",
  "userCode":         "DOC-AI-001",
  "doctorName":       "Dr. Khaled Hassan",
  "departmentId":     "01JDEPT...",
  "departmentName":   "Computer Science",
  "teachingOfferings": [
    {
      "subjectOfferingId": "01JOFFER...",
      "subjectName": "Data Structures",
      "batchName":   "Level 3 - CS 2026"
    }
  ]
}
```

### For Admins

```json
{
  "userId":     "01JXXX...",
  "profileId":  "01JADMIN...",
  "userCode":   "ADMIN-001",
  "adminName":  "System Administrator",
  "role":       "Admin"
}
```

---

## QUICK REFERENCE — ENDPOINTS BY ROLE

### Student Can Call

```
POST   /api/Auth/login
POST   /api/Auth/logout
POST   /api/Auth/refresh-token
POST   /api/Auth/change-password
GET    /api/Auth/me
GET    /api/Students/{code}              (own profile only)
GET    /api/Subjects/my-subjects
GET    /api/SubjectOfferings/my-enrollments
POST   /api/Enrollments/{offeringId}
GET    /api/Enrollments/my-enrollments
GET    /api/Materials/by-offering/{id}
GET    /api/Materials/download/{id}
GET    /api/Materials/{id}/metadata
GET    /api/Exams/my-enrolled-exams
GET    /api/Exams/{id}
POST   /api/Exams/{id}/submit
GET    /api/Exams/{id}/my-submission
GET    /api/Gpa/my-gpa
POST   /api/Attendance/check-in
GET    /api/Schedule/batch/{batchId}
GET    /api/Schedule/batch/{batchId}/today
GET    /api/Schedule/batch/{batchId}/day/{day}
POST   /api/Complaints
GET    /api/Complaints/my-complaints
POST   /api/Chat/conversations
GET    /api/Chat/conversations
GET    /api/Chat/conversations/{id}/messages
POST   /api/Chat/messages
DELETE /api/Chat/conversations/{id}
PUT    /api/Chat/conversations/{id}
GET    /api/Notifications
```

### Doctor Can Call (+ all Student endpoints)

```
GET    /api/Students/filter
GET    /api/Students/by-batch/{batchId}
GET    /api/Doctors/{code}
GET    /api/Doctors/{code}/subjects
GET    /api/SubjectOfferings/my-offerings
POST   /api/Materials/upload
DELETE /api/Materials/{id}
POST   /api/Exams
POST   /api/Exams/generate-ai
POST   /api/Exams/upload-pdf
GET    /api/Exams/my-exams
GET    /api/Exams/by-offering/{id}
GET    /api/Exams/{id}/results
POST   /api/Exams/grade-submission
POST   /api/Exams/{id}/auto-grade
POST   /api/Grades/import/{offeringId}
POST   /api/Grades/calculate/{offeringId}
GET    /api/Attendance/student/{id}/report
POST   /api/Attendance/sessions
GET    /api/Complaints/my-reports
GET    /api/Complaints/clusters
GET    /api/Schedule/my-schedule
GET    /api/Schedule/my-today
```

### Admin Can Call (+ all Doctor endpoints)

```
POST   /api/Auth/register/student
POST   /api/Auth/register/doctor
POST   /api/Auth/admin/reset-password/{userId}
GET    /api/Students (all)
PATCH  /api/Students/{id}
DELETE /api/Students/{code}
POST   /api/Students/import-excel
POST   /api/Doctors
PATCH  /api/Doctors/{id}
POST   /api/Subjects
PUT    /api/Subjects/assign-doctor
POST   /api/SubjectOfferings
POST   /api/Semesters
GET    /api/Dashboard
GET    /api/AuditLogs
GET    /api/Complaints/all
PUT    /api/Grades/{id}
POST   /api/Grades/{gradeId}/recalculate
PUT    /api/Enrollments/{id}/reactivate
DELETE /api/Enrollments/{id}
POST   /api/Attendance/correct
DELETE /api/Chat/messages/{id}
```

---

## FRONTEND IMPLEMENTATION CHECKLIST

### Session Management
- [ ] Store JWT in memory (NOT localStorage)
- [ ] Store refreshToken in httpOnly cookie
- [ ] Decode JWT on login to extract role, profileId, userCode
- [ ] Implement axios/fetch interceptor: on 401 → auto-refresh → retry
- [ ] On login: check `mustChangePassword` → redirect if true
- [ ] On logout: call `/api/Auth/logout` → clear all stored tokens

### Chat UI
- [ ] Always send `conversationId` with every message
- [ ] Display AI response with markdown rendering (response can contain `**bold**`, `\n`, emojis)
- [ ] Render `suggestions[]` as clickable chips below the response
- [ ] Show `intent_executed` field in debug mode (admin view)
- [ ] Handle 429 rate limit: show "انتظر لحظة" toast, retry after 60s
- [ ] Handle RBAC denial (response with denial message): display as normal AI message, NOT as error

### Materials
- [ ] Never store signed URLs — always fetch fresh from `/api/Materials/download/{id}`
- [ ] Signed URLs expire in 60 minutes
- [ ] For large files: show upload progress (multipart streaming)
- [ ] Validate file type client-side before upload (match server allowlist)

### academic_context
- [ ] Fetch student/doctor profile on login and cache in app state
- [ ] Fetch enrollments on login for student
- [ ] Build and pass full `academic_context` with every chat message
- [ ] Include `subjectOfferingIds[]` array — AI uses this for schedule/material queries

### Error Handling
- [ ] Show Arabic error messages for Arabic-speaking users
- [ ] Never show raw JSON or stack traces to users
- [ ] 503 on chat → "الخدمة غير متاحة حالياً، حاول بعد قليل"
- [ ] 429 on chat → "لو سمحت استنى ثانية وحاول تاني"
- [ ] RBAC denial (200 with denial message) → display as normal AI message

---

*Last updated: 2026-05-14*
*Backend: .NET 9 / EF Core / PostgreSQL / Cloudflare R2*
*AI Service: FastAPI / OpenRouter (gpt-4o-mini) / Redis*
