# University Management System — API Documentation
> For Frontend Developers

---

## Table of Contents
1. [Tech Stack & Base Info](#tech-stack)
2. [Authentication](#authentication)
3. [Standard Response Format](#standard-response-format)
4. [User Roles](#user-roles)
5. [Endpoints](#endpoints)
   - [Auth](#1-auth---apiauthentication)
   - [Students](#2-students---apistudents)
   - [Doctors](#3-doctors---apidoctors)
   - [Admins](#4-admins---apiadmins)
   - [Subjects](#5-subjects---apisubjects)
   - [Subject Offerings](#6-subject-offerings---apisubject-offerings)
   - [Enrollments](#7-enrollments---apienrollments)
   - [Exams](#8-exams---apiexams)
   - [Grades](#9-grades---apigrades)
   - [GPA](#10-gpa---apigpa)
   - [Attendance](#11-attendance---apiattendance)
   - [Materials](#12-materials---apimaterials)
   - [Notifications](#13-notifications---apinotifications)
   - [Chat](#14-chat---apichat)
   - [AI Features](#15-ai-features---apiai)
   - [Complaints](#16-complaints---apicomplaints)
   - [Regulations](#17-regulations---apiregulations)
   - [Academic Years](#18-academic-years---apiacademic-years)
   - [Semesters](#19-semesters---apisemesters)
   - [Student Files](#20-student-files---apistudent-files)
   - [File Upload](#21-file-upload---apifile)
   - [Dashboard](#22-dashboard---apidashboard)
   - [Audit Logs](#23-audit-logs---apiaudit-logs)
6. [Shared Enums](#shared-enums)

---

## Tech Stack

| Item | Value |
|------|-------|
| Framework | .NET 9.0 (C#) |
| Database | PostgreSQL |
| Cache | Redis |
| File Storage | Cloudflare R2 (S3-compatible) |
| Background Jobs | Hangfire + MassTransit |
| ID Format | **ULID** (string, not integer) |
| Auth | JWT Bearer Token |

---

## Authentication

All protected endpoints require the following header:

```
Authorization: Bearer <token>
```

### Login
`POST /api/auth/login`

```json
// Request
{
  "email": "user@example.com",
  "password": "yourpassword"
}

// Response (inside data field)
{
  "token": "eyJ...",
  "refreshToken": "abc123...",
  "email": "user@example.com",
  "role": "Student",
  "userId": "01HXYZ...",
  "fullName": "Mohamed Ahmed",
  "universityEmail": "s12345@uni.edu"
}
```

### Refresh Token
`POST /api/auth/refresh-token`

```json
// Request
{
  "token": "expired_jwt_token",
  "refreshToken": "refresh_token_string"
}
```

### Token Claims (JWT Payload)
| Claim | Description |
|-------|-------------|
| `nameid` | SystemUser ID |
| `ProfileId` | Profile entity ID (Student / Doctor / Admin) |
| `role` | User role string |

---

## Standard Response Format

**Every** API response is wrapped in this envelope:

```json
{
  "data": { },
  "success": true,
  "statusCode": 200,
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### Error Response
```json
{
  "message": "Detailed error message here",
  "statusCode": 400,
  "timestamp": "2024-01-01T00:00:00Z"
}
```

> Common status codes: `200` OK, `201` Created, `204` No Content, `400` Bad Request, `401` Unauthorized, `403` Forbidden, `404` Not Found, `429` Too Many Requests

### Rate Limiting
- **Global**: 1000 requests/minute per user
- **Login**: 5 requests/minute per IP

---

## User Roles

| Role | Value | Description |
|------|-------|-------------|
| `Admin` | 0 | College administrator |
| `Student` | 1 | Enrolled student |
| `Doctor` | 2 | Faculty member / professor |
| `TeachingAssistant` | 3 | Teaching assistant |
| `SuperAdmin` | 4 | System super admin |

---

## Endpoints

---

### 1. Auth — `/api/auth`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/login` | No | — | Login and get JWT |
| POST | `/refresh-token` | No | — | Refresh access token |
| POST | `/revoke-token` | Yes | Any | Revoke a refresh token |
| POST | `/change-password` | Yes | Any | Change own password |
| POST | `/logout` | Yes | Any | Logout (revoke token) |
| GET | `/me` | Yes | Any | Get current user claims |
| POST | `/register/student` | Yes | Admin | Register a new student |
| POST | `/register/doctor` | Yes | Admin | Register a new doctor |
| POST | `/register/admin` | Yes | SuperAdmin | Register a new admin |

#### Register Student Body
```json
{
  "fullName": "Ahmed Ali",
  "email": "ahmed@example.com",
  "colledgeCode": "ENG",
  "departmentCode": "CS",
  "nationalId": "12345678901234",
  "universityStudentId": "2021001",
  "batchCode": "BATCH-2021",
  "groupCode": "GROUP-A",
  "phone": "01012345678"
}
```

#### Register Doctor Body
```json
{
  "fullName": "Dr. Mohamed Hassan",
  "universityStaffId": "STAFF-001",
  "departmentCode": "CS",
  "nationalId": "12345678901234",
  "phone": "01012345678"
}
```

#### Change Password Body
```json
{
  "currentPassword": "OldPass123",
  "newPassword": "NewPass456"
}
```

---

### 2. Students — `/api/students`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/search?q=name` | Yes | Any | Search students (max 20) |
| GET | `/?page=1&size=20` | Yes | Any | List all students (paginated) |
| GET | `/{code}` | Yes | Any | Get student by code |
| GET | `/by-batch/{batchId}` | Yes | Admin, Doctor, TA | Get students in a batch |
| POST | `/` | Yes | Admin | Create student |
| PUT | `/{code}` | Yes | Admin | Update student |
| DELETE | `/{code}` | Yes | Admin | Delete student |
| POST | `/bulk-upload-direct` | Yes | Admin | Bulk upload via Excel (background job) |
| POST | `/bulk-upload-ai` | Yes | Admin | Bulk upload via Excel with AI parsing |
| POST | `/import-excel` | Yes | Admin | Import Excel (returns result immediately) |

#### StudentDto
```json
{
  "id": "01HXYZ...",
  "code": "STU-2021-001",
  "fullName": "Ahmed Ali",
  "email": "ahmed@example.com",
  "phone": "01012345678",
  "nationalId": "12345678901234",
  "universityStudentId": "2021001",
  "universityEmail": "s2021001@uni.edu",
  "universityId": "01HABC...",
  "batchId": "01HDEF...",
  "groupId": "01HGHI...",
  "isActive": true
}
```

#### Create Student Body
```json
{
  "fullName": "Ahmed Ali",
  "nationalId": "12345678901234",
  "phone": "01012345678",
  "batchCode": "BATCH-2021",
  "groupCode": "GROUP-A",
  "colledgeCode": "ENG",
  "departmentCode": "CS",
  "universityStudentId": "2021001"
}
```

#### Update Student Body
```json
{
  "fullName": "Ahmed Ali Updated",
  "phone": "01099999999",
  "batchCode": "BATCH-2022",
  "groupCode": "GROUP-B"
}
```

---

### 3. Doctors — `/api/doctors`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/search?q=name` | Yes | Any | Search doctors (max 20) |
| GET | `/?page=1&size=20` | Yes | Any | List all doctors (paginated) |
| GET | `/{code}` | Yes | Any | Get doctor by code |
| POST | `/` | Yes | Admin | Create doctor |
| PUT | `/{code}` | Yes | Admin | Update doctor |
| DELETE | `/{code}` | Yes | Admin | Delete doctor |
| GET | `/{code}/subjects` | Yes | Any | Get doctor's subjects |
| POST | `/bulk-upload` | Yes | Admin | Bulk upload via Excel |

#### DoctorDto
```json
{
  "id": "01HXYZ...",
  "code": "DOC-001",
  "fullName": "Dr. Mohamed Hassan",
  "email": "m.hassan@example.com",
  "phone": "01012345678",
  "universityStaffId": "STAFF-001",
  "universityEmail": "m.hassan@uni.edu",
  "departmentId": "01HABC..."
}
```

---

### 4. Admins — `/api/admins`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/` | Yes | SuperAdmin, Admin | List admins (admin sees only self) |
| GET | `/{id}` | Yes | SuperAdmin, Admin | Get admin by ID |
| PUT | `/{id}` | Yes | SuperAdmin, Admin | Update admin |
| DELETE | `/{id}` | Yes | SuperAdmin | Delete admin |
| PUT | `/{id}/activate` | Yes | SuperAdmin | Activate admin account |
| PUT | `/{id}/deactivate` | Yes | SuperAdmin | Deactivate admin account |

---

### 5. Subjects — `/api/subjects`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/search?name=x` | Yes | Any | Search subjects |
| GET | `/by-batch/{batchId}` | Yes | Any | Subjects for a batch (cached 5min) |
| GET | `/{code}` | Yes | Any | Get subject by code |
| GET | `/by-department/{departmentId}` | Yes | Any | Subjects by department |
| GET | `/by-college/{collegeId}` | Yes | Any | Subjects by college |
| GET | `/my-subjects` | Yes | Doctor, Student | Get my subjects |
| POST | `/` | Yes | Admin | Create subject |
| PUT | `/{code}` | Yes | Admin | Update subject |
| DELETE | `/{code}` | Yes | Admin | Delete subject |
| PUT | `/assign-doctor?subjectCode=X&doctorCode=Y` | Yes | Admin | Assign doctor to subject |
| PUT | `/assign-assistant?subjectCode=X&assistantCode=Y` | Yes | Admin | Assign TA to subject |

#### SubjectDto
```json
{
  "id": "01HXYZ...",
  "code": "CS101",
  "name": "Introduction to Programming",
  "creditHours": 3,
  "departmentId": "01HABC...",
  "doctorId": "01HDEF...",
  "doctorName": "Dr. Mohamed Hassan"
}
```

---

### 6. Subject Offerings — `/api/subject-offerings`

A **Subject Offering** is a specific instance of a subject in a semester.

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/by-code/{code}` | Yes | Any | Get offering by code |
| POST | `/` | Yes | Admin | Create subject offering |
| GET | `/by-semester/{semesterId}` | Yes | Admin | Get offerings in a semester |
| GET | `/my-offerings` | Yes | Doctor | Get my current offerings |
| GET | `/my-enrollments` | Yes | Student | Get my enrolled offerings |

#### SubjectOfferingDto
```json
{
  "id": "01HXYZ...",
  "code": "CS101-2024-1",
  "subjectId": "01HABC...",
  "subjectName": "Introduction to Programming",
  "subjectCode": "CS101",
  "semesterId": "01HDEF...",
  "semesterName": "Fall 2024",
  "doctorId": "01HGHI...",
  "doctorName": "Dr. Mohamed Hassan",
  "creditHours": 3
}
```

#### Create Subject Offering Body
```json
{
  "subjectCode": "CS101",
  "semesterId": "01HDEF...",
  "doctorCode": "DOC-001"
}
```

---

### 7. Enrollments — `/api/enrollments`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/{offeringId}` | Yes | Student | Enroll in a subject offering |
| GET | `/my-enrollments` | Yes | Student | Get my enrollments |
| GET | `/by-offering/{offeringId}` | Yes | Doctor, Admin | Get enrolled students |
| DELETE | `/{id}` | Yes | Admin | Remove enrollment |
| PUT | `/{id}/reactivate` | Yes | Admin, SuperAdmin | Reactivate enrollment |

#### EnrollmentDto
```json
{
  "id": "01HXYZ...",
  "studentId": "01HABC...",
  "studentName": "Ahmed Ali",
  "subjectOfferingId": "01HDEF...",
  "subjectCode": "CS101",
  "subjectName": "Introduction to Programming",
  "departmentName": "Computer Science",
  "doctorName": "Dr. Mohamed Hassan",
  "semesterName": "Fall 2024",
  "enrolledAt": "2024-09-01T10:00:00Z",
  "isActive": true
}
```

---

### 8. Exams — `/api/exams`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/?subjectOfferingId=X` | Yes | Doctor | Create structured exam |
| POST | `/generate-ai` | Yes | Doctor | Generate exam with AI |
| POST | `/upload-pdf` | Yes | Doctor | Upload PDF as exam |
| GET | `/{id}` | Yes | Doctor, Student, Admin | Get exam by ID |
| GET | `/by-code/{code}` | Yes | Admin, Doctor, Student | Get exam by code |
| PUT | `/{id}` | Yes | Admin | Update exam |
| PUT | `/by-code/{code}` | Yes | Admin | Update exam by code |
| DELETE | `/{id}` | Yes | Admin | Delete exam |
| DELETE | `/by-code/{code}` | Yes | Admin | Delete exam by code |
| POST | `/{id}/restore` | Yes | Admin | Restore deleted exam |
| POST | `/{id}/submit` | Yes | Student | Submit exam answers |
| GET | `/my-exams` | Yes | Doctor | Get my exams |
| GET | `/by-offering/{offeringId}` | Yes | Doctor | Get exams in offering |
| GET | `/{id}/results` | Yes | Doctor | Get all submissions for exam |
| GET | `/my-enrolled-exams` | Yes | Student | Get exams I can take |
| GET | `/{id}/my-submission` | Yes | Student | Get my submission for an exam |
| POST | `/grade-submission` | Yes | Doctor | Manually grade a submission |
| POST | `/{id}/auto-grade` | Yes | Doctor | Auto-grade all submissions |

#### ExamDto
```json
{
  "id": "01HXYZ...",
  "code": "EXAM-CS101-001",
  "title": "Midterm Exam",
  "subjectOfferingId": "01HABC...",
  "mode": 0,
  "status": 0,
  "durationMinutes": 90,
  "totalMarks": 100,
  "questions": [
    {
      "id": "01HDEF...",
      "text": "What is a variable?",
      "marks": 10,
      "type": "MCQ",
      "options": ["A definition", "A loop", "A function", "None"]
    }
  ]
}
```

#### Create Exam Body (`?subjectOfferingId=X`)
```json
{
  "title": "Midterm Exam",
  "durationMinutes": 90,
  "totalMarks": 100,
  "questions": [
    {
      "text": "What is a variable?",
      "marks": 10,
      "type": "MCQ",
      "options": ["A definition", "A loop", "A function", "None"],
      "correctAnswer": "A definition"
    }
  ]
}
```

#### Submit Exam Body
```json
{
  "answers": [
    {
      "questionId": "01HDEF...",
      "answer": "A definition"
    }
  ]
}
```

#### Exam Mode Enum
| Value | Name | Description |
|-------|------|-------------|
| 0 | Structured | Manual question creation |
| 1 | AI | AI-generated exam |
| 2 | File | PDF upload |

#### Exam Status Enum
| Value | Name | Description |
|-------|------|-------------|
| 0 | Draft | Not yet visible to students |
| 1 | Published | Open for students |
| 2 | Closed | Submissions closed |

---

### 9. Grades — `/api/grades`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/import/{offeringId}` | Yes | Doctor, Admin | Import grades from Excel |
| POST | `/calculate/{offeringId}` | Yes | Doctor | Calculate grades for offering |
| POST | `/{gradeId}/recalculate` | Yes | Admin, SuperAdmin | Recalculate a specific grade |
| PUT | `/{id}` | Yes | Admin | Update a grade |
| DELETE | `/{gradeId}` | Yes | Admin, SuperAdmin | Delete a grade |

#### GradeDto
```json
{
  "id": "01HXYZ...",
  "studentId": "01HABC...",
  "studentName": "Ahmed Ali",
  "subjectOfferingId": "01HDEF...",
  "subjectName": "Introduction to Programming",
  "midtermGrade": 45.5,
  "finalGrade": 78.0,
  "totalGrade": 85.5,
  "letterGrade": "A",
  "gpaPoints": 4.0
}
```

---

### 10. GPA — `/api/gpa`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/my-gpa` | Yes | Student | Get my GPA |
| GET | `/student/{studentId}` | Yes | Admin | Get student's GPA |
| POST | `/student/{studentId}/recalculate` | Yes | Admin, SuperAdmin | Recalculate GPA |

#### StudentGpaDto
```json
{
  "studentId": "01HXYZ...",
  "gpa": 3.75,
  "totalCreditHours": 90
}
```

---

### 11. Attendance — `/api/attendance`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/sessions` | Yes | Doctor, TA, Admin, SuperAdmin | Create attendance session |
| POST | `/check-in` | Yes | Student | Check in to session |
| GET | `/student/{studentId}/report?subjectId=X` | Yes | Doctor, TA, Admin, SuperAdmin | Get attendance report |
| POST | `/correct?sessionId=X&studentId=Y&isPresent=true` | Yes | Admin, SuperAdmin | Correct attendance |
| GET | `/record/{sessionId}/{studentId}` | Yes | Admin | Get single record |
| PUT | `/record/{sessionId}/{studentId}` | Yes | Admin | Update attendance record |
| DELETE | `/record/{sessionId}/{studentId}` | Yes | Admin | Delete attendance record |

#### Create Session Body
```json
{
  "subjectOfferingId": "01HXYZ...",
  "sessionDate": "2024-10-15T09:00:00Z",
  "title": "Lecture 5"
}
```

#### Check-in Body
```json
{
  "sessionId": "01HXYZ..."
}
```

#### Attendance Report Response
```json
{
  "studentId": "01HXYZ...",
  "studentName": "Ahmed Ali",
  "subjectName": "Introduction to Programming",
  "totalSessions": 20,
  "attendedSessions": 17,
  "attendancePercentage": 85.0,
  "records": [
    {
      "sessionId": "01HABC...",
      "sessionDate": "2024-10-15T09:00:00Z",
      "isPresent": true
    }
  ]
}
```

---

### 12. Materials — `/api/materials`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/upload` | Yes | Doctor | Upload course material |
| DELETE | `/{id}` | Yes | Doctor | Delete material |
| GET | `/by-offering/{offeringId}?page=1&pageSize=10&search=x` | Yes | Student | List materials |
| GET | `/download/{id}` | Yes | Student | Get signed download URL (60min) |
| GET | `/{id}/metadata` | Yes | Any | Get file metadata + signed URL |

#### Upload Material (multipart/form-data)
| Field | Type | Description |
|-------|------|-------------|
| `file` | File | The material file |
| `subjectOfferingId` | string | Offering ID |
| `title` | string | Material title |
| `description` | string | Optional description |

**Allowed file types**: PDF, DOCX, XLSX, PPT, PPTX, Images, MP4, WEBM, TXT, ZIP  
**Max size**: 500 MB

#### Download URL Response
```json
{
  "url": "https://r2.cloudflare.com/...",
  "expiresAt": "2024-10-15T10:00:00Z"
}
```

---

### 13. Notifications — `/api/notifications`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/?unreadOnly=true` | Yes | Any | Get my notifications |
| PUT | `/{id}/read` | Yes | Any | Mark notification as read |
| POST | `/` | Yes | Admin | Send admin notification |
| DELETE | `/{id}` | Yes | Admin | Delete notification |

#### NotificationDto
```json
{
  "id": "01HXYZ...",
  "title": "New Exam Published",
  "body": "CS101 Midterm is now available",
  "isRead": false,
  "createdAt": "2024-10-01T08:00:00Z"
}
```

#### Create Notification Body (Admin)
```json
{
  "title": "Important Announcement",
  "body": "Message content here",
  "targetRole": "Student"
}
```

---

### 14. Chat — `/api/chat`

AI-powered chat (per user, conversation-based).

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/conversations` | Yes | Any | Create new conversation |
| GET | `/conversations` | Yes | Any | List my conversations |
| PUT | `/conversations/{id}` | Yes | Any | Rename conversation |
| DELETE | `/conversations/{id}` | Yes | Any | Delete conversation |
| GET | `/conversations/{id}/messages?page=1&pageSize=20` | Yes | Any | Get messages (paginated) |
| POST | `/messages` | Yes | Any | Send message to AI |
| DELETE | `/messages/{id}` | Yes | Admin, SuperAdmin | Delete message |

#### Create Conversation Body
```json
{
  "title": "My Study Chat"
}
```

#### Send Message Body
```json
{
  "conversationId": "01HXYZ...",
  "content": "Explain what a linked list is"
}
```

---

### 15. AI Features — `/api/ai`

Summarize or ask questions about uploaded files.

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/summarize` | Yes | Student | Summarize a file |
| POST | `/ask` | Yes | Student | Ask question about a file |

#### Summarize Body
```json
{
  "fileId": "01HXYZ..."
}
```

#### Ask Body
```json
{
  "fileId": "01HXYZ...",
  "question": "What are the main topics in this document?"
}
```

#### AI Response
```json
{
  "fileId": "01HXYZ...",
  "result": "This document covers...",
  "usedExtractedText": true
}
```

---

### 16. Complaints — `/api/complaints`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/` | Yes | Student | Submit complaint |
| GET | `/my-complaints` | Yes | Student | My submitted complaints |
| GET | `/my-reports` | Yes | Doctor | Complaints about my courses |
| GET | `/all` | Yes | Admin, SuperAdmin | All complaints (paginated) |
| GET | `/clusters` | Yes | Admin, SuperAdmin, Doctor | AI-grouped complaint clusters |

#### Create Complaint Body
```json
{
  "targetType": "Subject",
  "targetId": "01HXYZ...",
  "message": "The exam was unfair and unclear"
}
```

#### ComplaintDto
```json
{
  "id": "01HXYZ...",
  "studentId": "01HABC...",
  "title": "Exam Complaint",
  "targetType": "Subject",
  "targetId": "01HDEF...",
  "message": "The exam was unfair",
  "status": "Pending",
  "priority": "High",
  "resolutionNote": null,
  "createdAt": "2024-10-01T10:00:00Z"
}
```

#### Paginated Complaints Query Params
| Param | Type | Description |
|-------|------|-------------|
| `page` | int | Page number |
| `pageSize` | int | Items per page |
| `from` | datetime | Start date filter |
| `to` | datetime | End date filter |
| `targetType` | string | Filter by target type |
| `targetId` | string | Filter by target ID |
| `status` | string | Filter by status |

---

### 17. Regulations — `/api/regulations`

Academic regulations documents per department.

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/` | Yes | Any | All regulations (cached 5min) |
| GET | `/active` | Yes | Any | Active regulations only |
| GET | `/by-code/{code}` | Yes | Any | Get by code |
| GET | `/by-department/{departmentId}` | Yes | Any | Get by department |
| GET | `/student/{studentId}` | Yes | Any | Get student's regulation |
| POST | `/` | Yes | Admin, SuperAdmin | Create regulation (multipart) |
| PUT | `/{id}` | Yes | Admin | Update regulation (multipart) |
| PUT | `/by-code/{code}` | Yes | Admin | Update by code (multipart) |
| DELETE | `/{id}` | Yes | Admin | Delete regulation |
| DELETE | `/by-code/{code}` | Yes | Admin | Delete by code |

#### Create/Update Regulation (multipart/form-data)
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File | Yes (Create) | Regulation document |
| `code` | string | Yes | Unique code |
| `name` | string | Yes | Regulation name |
| `departmentId` | string | Yes | Department ID |
| `isActive` | bool | No | Active status |

**Allowed types**: PDF, DOC, DOCX, XLS, XLSX, TXT — Max 50 MB

---

### 18. Academic Years — `/api/academic-years`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/` | Yes | Admin, SuperAdmin | Create academic year |
| GET | `/` | Yes | Any | List all academic years |
| GET | `/by-college/{collegeId}` | Yes | Any | By college |
| POST | `/{id}/activate` | Yes | Admin, SuperAdmin | Activate year |
| PUT | `/{id}` | Yes | Admin, SuperAdmin | Update year |
| DELETE | `/{id}` | Yes | Admin, SuperAdmin | Delete year |
| GET | `/{yearId}/departments` | Yes | Any | Active department mappings |
| GET | `/{yearId}/departments/all` | Yes | Admin, SuperAdmin | All department mappings |
| POST | `/{yearId}/departments` | Yes | Admin, SuperAdmin | Assign department to year |
| PATCH | `/{yearId}/departments/{mappingId}` | Yes | Admin, SuperAdmin | Update mapping |
| DELETE | `/{yearId}/departments/{mappingId}` | Yes | Admin, SuperAdmin | Remove mapping |

#### AcademicYearDto
```json
{
  "id": "01HXYZ...",
  "name": "2024-2025",
  "startDate": "2024-09-01",
  "endDate": "2025-06-30",
  "isActive": true,
  "collegeId": "01HABC..."
}
```

---

### 19. Semesters — `/api/semesters`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/` | Yes | Admin | Create semester |
| GET | `/by-academic-year/{academicYearId}` | Yes | Admin | Semesters for a year |
| PUT | `/{id}` | Yes | Admin | Update semester |
| DELETE | `/{id}` | Yes | Admin | Delete semester |

#### SemesterDto
```json
{
  "id": "01HXYZ...",
  "name": "Fall 2024",
  "academicYearId": "01HABC...",
  "startDate": "2024-09-01",
  "endDate": "2025-01-31",
  "isActive": true
}
```

---

### 20. Student Files — `/api/student-files`

Student personal file storage (for AI features).

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/upload` | Yes | Student | Upload personal file |
| GET | `/my` | Yes | Student | List my uploaded files |

**Allowed types**: PDF, TXT, JPEG, PNG, WEBP, DOC, DOCX, XLS, XLSX — Max 30 MB

#### StudentFileDto
```json
{
  "id": "01HXYZ...",
  "fileName": "lecture_notes.pdf",
  "fileSize": 204800,
  "mimeType": "application/pdf",
  "uploadedAt": "2024-10-01T10:00:00Z"
}
```

---

### 21. File Upload — `/api/file`

General-purpose file management.

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| POST | `/upload` | Yes | Any | Upload a file |
| GET | `/` | Yes | Any | List my files |
| PUT | `/{id}/rename` | Yes | Admin | Rename file |
| DELETE | `/{id}` | Yes | Admin | Delete file |

**Allowed types**: PDF, Images, DOC, DOCX, XLS, XLSX, TXT, CSV, ZIP — Max 50 MB

#### Upload Response
```json
{
  "fileId": "01HXYZ..."
}
```

---

### 22. Dashboard — `/api/dashboard`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/` | Yes | Admin, SuperAdmin | System-wide statistics |

#### Dashboard Response
```json
{
  "totalStudents": 1500,
  "totalDoctors": 80,
  "totalSubjects": 120,
  "activeEnrollments": 4500,
  "pendingComplaints": 12,
  "recentActivity": []
}
```

---

### 23. Audit Logs — `/api/audit-logs`

| Method | Endpoint | Auth | Roles | Description |
|--------|----------|------|-------|-------------|
| GET | `/?page=1&pageSize=50&entity=X&userId=Y&action=Z` | Yes | Admin, SuperAdmin | Paginated audit trail |

---

## Shared Enums

```
UserRole:
  Admin = 0
  Student = 1
  Doctor = 2
  TeachingAssistant = 3
  SuperAdmin = 4

ExamMode:
  Structured = 0   (manual questions)
  AI = 1           (AI generated)
  File = 2         (PDF upload)

ExamStatus:
  Draft = 0        (hidden from students)
  Published = 1    (open)
  Closed = 2       (no more submissions)
```

---

## Important Notes for Frontend

1. **All IDs are ULIDs** — string format, not integers. Example: `"01HXYZ123ABC..."`
2. **All responses are wrapped** in `{ data, success, statusCode, timestamp }`
3. **File uploads** use `multipart/form-data` content type
4. **Pagination** typically uses `page` (1-indexed) and `size`/`pageSize` query params
5. **Signed URLs** for file downloads expire after **60 minutes**
6. **Redis caching** is used on some list endpoints (e.g., regulations, subjects by batch) — changes may take up to **5 minutes** to reflect
7. **Background jobs** (bulk upload) return `202 Accepted` immediately — results are processed asynchronously
8. **JWT token** should be refreshed before expiry using `/api/auth/refresh-token`

---

*Generated: 2026-05-12 — University Management System API*
