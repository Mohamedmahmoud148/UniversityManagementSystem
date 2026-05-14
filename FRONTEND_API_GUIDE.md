# University Management System — Frontend API Guide

> **Base URL**: All endpoints are prefixed with `/api`
> **Auth**: Bearer JWT token in `Authorization: Bearer <token>` header
> **Content-Type**: `application/json` unless noted (file uploads use `multipart/form-data`)

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [Data Models & Relationships](#2-data-models--relationships)
3. [Students API](#3-students-api)
4. [Doctors API](#4-doctors-api)
5. [Subjects (Courses) API](#5-subjects-courses-api)
6. [Subject Offerings API](#6-subject-offerings-api)
7. [Enrollments API](#7-enrollments-api)
8. [Materials API](#8-materials-api)
9. [Common Patterns](#9-common-patterns)
10. [Roles & Permissions Matrix](#10-roles--permissions-matrix)
11. [Business Rules & Validations](#11-business-rules--validations)

---

## 1. Authentication

### Login
**POST** `/api/auth/login`
> Public — no token required. Rate-limited.

**Request Body:**
```json
{
  "email": "string",       // Can be personal email OR university email
  "password": "string"
}
```

**Response `200 OK`:**
```json
{
  "token": "eyJhbGci...",
  "refreshToken": "abc123...",
  "email": "string",
  "universityEmail": "string | null",
  "role": "Admin | Doctor | Student | TeachingAssistant | SuperAdmin",
  "userId": "ulid-string",
  "fullName": "string",
  "requiresPasswordChange": true,        // If true → force user to change password before using app
  "generatedUniversityId": "string | null",
  "temporaryPassword": "string | null",
  "generatedPassword": "string | null"
}
```

> **Important:** If `requiresPasswordChange: true`, redirect the user to the change-password screen immediately. The system enforces this.

---

### Refresh Token
**POST** `/api/auth/refresh-token`
> Public — no token required. Rate-limited.

**Request Body:**
```json
{
  "token": "current-jwt-string",
  "refreshToken": "refresh-token-string"
}
```

**Response `200 OK`:** Same as Login response.

---

### Revoke Token (Logout)
**POST** `/api/auth/revoke-token`
> Requires: Any authenticated user

**Request Body:**
```json
{
  "refreshToken": "refresh-token-string"
}
```

**Response `200 OK`:** Success message string.

---

### Logout
**POST** `/api/auth/logout`
> Requires: Any authenticated user

No body. Revokes current session.

**Response `200 OK`:** Success message string.

---

### Change Password
**POST** `/api/auth/change-password`
> Requires: Any authenticated user

**Request Body:**
```json
{
  "currentPassword": "string",
  "newPassword": "string"    // min 6 characters
}
```

**Response `200 OK`:** Success message string.

---

### Get Current User Info
**GET** `/api/auth/me`
> Requires: Any authenticated user

No body.

**Response `200 OK`:** Object with all JWT claims of the logged-in user.

---

### Register Student
**POST** `/api/auth/register/student`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",          // required
  "email": "string",             // required, valid email
  "nationalId": "string",        // required
  "universityStudentId": "string | null",
  "phone": "string",             // required, Egyptian format: 01XXXXXXXXX
  "collegeCode": "string",       // required
  "departmentCode": "string",    // required
  "batchCode": "string",         // required
  "groupCode": "string"          // required
}
```

**Response `201 Created`:**
```json
{
  "token": "...",
  "refreshToken": "...",
  "email": "...",
  "universityEmail": "generated@university.edu",
  "role": "Student",
  "userId": "ulid",
  "fullName": "...",
  "requiresPasswordChange": true,
  "generatedUniversityId": "S-20240001",
  "temporaryPassword": "TempPass123!",
  "generatedPassword": "..."
}
```

> The `temporaryPassword` and `generatedPassword` should be shown to the admin so they can hand it to the student.

---

### Register Doctor
**POST** `/api/auth/register/doctor`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",           // required
  "nationalId": "string",         // required
  "phone": "string",              // required, Egyptian format
  "departmentCode": "string",     // required
  "universityStaffId": "string | null"
}
```

**Response `201 Created`:** Same structure as Register Student response.

---

### Register Admin
**POST** `/api/auth/register/admin`
> Requires: SuperAdmin only

**Request Body:**
```json
{
  "fullName": "string",    // required
  "phone": "string",       // required, Egyptian format
  "nationalId": "string"   // required
}
```

**Response `201 Created`:** Same structure as above.

---

### Admin Reset Password
**POST** `/api/auth/admin/reset-password/{userId}`
> Requires: Admin or SuperAdmin

No body.

**Response `200 OK`:**
```json
{
  "temporaryPassword": "NewTempPass!",
  "message": "Password has been reset"
}
```

> The admin should give the temporary password to the user. User will be forced to change it on next login.

---

## 2. Data Models & Relationships

### Entity Relationship Overview

```
University
 └── College
      └── Department
           ├── Batch
           │    └── Group
           ├── Doctor ──────────────────────────┐
           │    └── SubjectDoctor (junction)     │
           ├── Subject ──────────────────────────┤
           │    └── SubjectOffering ─────────────┤
           │         ├── Semester               │
           │         ├── Doctor ────────────────┘
           │         ├── Batch
           │         ├── Group (optional)
           │         ├── Enrollment ←── Student
           │         └── Material (files)
           └── Student
                └── Enrollment → SubjectOffering
```

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Subject** | A course definition (e.g. "Data Structures", code "CS301") |
| **SubjectOffering** | A specific instance of a subject in a semester taught by a doctor |
| **Enrollment** | A student registered in a SubjectOffering |
| **Material** | A file uploaded to a SubjectOffering (accessible only to enrolled students) |
| **Doctor** | Instructor who teaches SubjectOfferings and uploads Materials |
| **Student** | Enrolled in SubjectOfferings, downloads Materials |

### ID Format
All IDs are **ULID** (Universally Unique Lexicographically Sortable Identifier) returned as strings.

---

## 3. Students API

### Get All Students (Paginated)
**GET** `/api/students`
> Requires: Admin | Doctor | Student

**Query Parameters:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `size` | int | 20 | Items per page |

**Response `200 OK`:**
```json
{
  "data": [
    {
      "id": "ulid",
      "code": "STU-001",
      "fullName": "Ahmed Mohamed",
      "email": "ahmed@gmail.com",
      "universityEmail": "ahmed.s2024@university.edu",
      "phone": "01012345678",
      "nationalId": "30012345678901",
      "universityStudentId": "20240001",
      "isActive": true,
      "universityId": "ulid",
      "batchId": "ulid",
      "groupId": "ulid"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "size": 20,
  "totalPages": 8,
  "hasNext": true,
  "hasPrev": false
}
```

---

### Search Students
**GET** `/api/students/search?q={query}`
> Requires: Admin | Doctor

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `q` | string | Search term (name, code, or email) |

**Response `200 OK`:** Array of `StudentDetailDto`:
```json
[
  {
    "id": "ulid",
    "code": "STU-001",
    "fullName": "Ahmed Mohamed",
    "email": "ahmed@gmail.com",
    "universityEmail": "ahmed.s2024@university.edu",
    "phone": "01012345678",
    "nationalId": "30012345678901",
    "universityStudentId": "20240001",
    "isActive": true,
    "universityId": "ulid",
    "collegeId": "ulid",
    "collegeName": "Faculty of Engineering",
    "departmentId": "ulid",
    "departmentName": "Computer Science",
    "batchId": "ulid",
    "batchName": "2024",
    "groupId": "ulid",
    "groupName": "Group A"
  }
]
```

---

### Filter Students
**GET** `/api/students/filter`
> Requires: Admin | Doctor

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `universityId` | ulid | Filter by university |
| `collegeId` | ulid | Filter by college |
| `departmentId` | ulid | Filter by department |
| `batchId` | ulid | Filter by batch |
| `groupId` | ulid | Filter by group |
| `isActive` | bool | Filter by active status |
| `search` | string | Text search |
| `page` | int | Page number (default 1) |
| `size` | int | Page size (default 20) |

**Response `200 OK`:** Same `PagedResult<StudentDetailDto>` as Get All.

---

### Get Student by Code
**GET** `/api/students/{code}`
> Requires: Admin | Doctor | Student (own record)

**Response `200 OK`:** Single `StudentDetailDto` object (same shape as search result above).

**Response `404 Not Found`:** Student not found.

---

### Get Students by Batch
**GET** `/api/students/by-batch/{batchId}`
> Requires: Admin | Doctor

**Response `200 OK`:** Array of `StudentDetailDto`.

---

### Create Student
**POST** `/api/students`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",               // required, 3-100 chars, Arabic or English
  "nationalId": "string",             // required
  "phone": "string",                  // required, format: 01XXXXXXXXX
  "batchCode": "string",              // required
  "groupCode": "string",              // required
  "collegeCode": "string | null",
  "departmentCode": "string | null",
  "universityStudentId": "string | null"
}
```

**Response `201 Created`:** `StudentDetailDto`.

---

### Update Student (Full)
**PUT** `/api/students/{code}`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",    // required
  "phone": "string",       // required
  "batchCode": "string",   // required
  "groupCode": "string"    // required
}
```

**Response `200 OK`:** Updated `StudentDetailDto`.

---

### Update Student (Partial)
**PATCH** `/api/students/{id}`
> Requires: Admin

**Request Body** (all fields optional):
```json
{
  "fullName": "string | null",
  "phone": "string | null",
  "email": "string | null",
  "batchCode": "string | null",
  "groupCode": "string | null",
  "isActive": "bool | null"
}
```

**Response `200 OK`:** Updated `StudentDetailDto`.

---

### Delete Student
**DELETE** `/api/students/{code}`
> Requires: Admin

No body.

**Response `200 OK`:** Success message. (Soft delete — data is retained, `isActive` becomes `false`)

---

### Bulk Upload Students (Excel)
**POST** `/api/students/import-excel`
> Requires: Admin
> Content-Type: `multipart/form-data`

**Form Data:**
| Field | Type | Description |
|-------|------|-------------|
| `file` | File | `.xlsx` Excel file |

**Response `200 OK`:**
```json
{
  "imported": 45,
  "failed": 2,
  "errors": ["Row 3: Invalid phone number", "Row 7: Batch not found"]
}
```

---

## 4. Doctors API

### Get All Doctors (Paginated)
**GET** `/api/doctors`
> Requires: Admin | Doctor | Student

**Query Parameters:** `page` (default 1), `size` (default 20)

**Response `200 OK`:**
```json
{
  "data": [
    {
      "id": "ulid",
      "code": "DOC-001",
      "fullName": "Dr. Mohamed Ali",
      "email": "moh.ali@gmail.com",
      "universityEmail": "m.ali@university.edu",
      "phone": "01098765432",
      "universityStaffId": "STAFF-001",
      "departmentId": "ulid"
    }
  ],
  "totalCount": 30,
  "page": 1,
  "size": 20,
  "totalPages": 2,
  "hasNext": true,
  "hasPrev": false
}
```

---

### Search Doctors
**GET** `/api/doctors/search?q={query}`
> Requires: Admin | Doctor

**Response `200 OK`:** Array of `DoctorDetailDto`:
```json
[
  {
    "id": "ulid",
    "code": "DOC-001",
    "fullName": "Dr. Mohamed Ali",
    "email": "moh.ali@gmail.com",
    "universityEmail": "m.ali@university.edu",
    "phone": "01098765432",
    "universityStaffId": "STAFF-001",
    "departmentId": "ulid",
    "departmentName": "Computer Science",
    "collegeId": "ulid",
    "collegeName": "Faculty of Engineering"
  }
]
```

---

### Filter Doctors
**GET** `/api/doctors/filter`
> Requires: Admin

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `collegeId` | ulid | Filter by college |
| `departmentId` | ulid | Filter by department |
| `isActive` | bool | Filter by active status |
| `search` | string | Text search |
| `page` | int | Page (default 1) |
| `size` | int | Size (default 20) |

**Response `200 OK`:** `PagedResult<DoctorDetailDto>`.

---

### Get Doctor by Code
**GET** `/api/doctors/{code}`
> Requires: Admin | Doctor | Student

**Response `200 OK`:** Single `DoctorDetailDto`.

---

### Get Doctor's Subjects
**GET** `/api/doctors/{code}/subjects`
> Requires: Admin | Doctor

**Response `200 OK`:** Array of `SubjectDto`:
```json
[
  {
    "id": "ulid",
    "name": "Data Structures",
    "code": "CS301",
    "creditHours": 3,
    "collegeId": "ulid",
    "collegeName": "Faculty of Engineering",
    "departmentId": "ulid",
    "departmentName": "Computer Science",
    "batchId": "ulid",
    "batchName": "2024",
    "doctorName": "Dr. Mohamed Ali"
  }
]
```

---

### Create Doctor
**POST** `/api/doctors`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",            // required
  "nationalId": "string",          // required
  "phone": "string",               // required, Egyptian format
  "departmentCode": "string"       // required
}
```

**Response `201 Created`:** `DoctorDetailDto`.

---

### Update Doctor (Full)
**PUT** `/api/doctors/{code}`
> Requires: Admin

**Request Body:**
```json
{
  "fullName": "string",    // required
  "phone": "string"        // required
}
```

**Response `200 OK`:** Updated `DoctorDetailDto`.

---

### Update Doctor (Partial)
**PATCH** `/api/doctors/{id}`
> Requires: Admin

**Request Body** (all optional):
```json
{
  "fullName": "string | null",
  "phone": "string | null",
  "departmentCode": "string | null"
}
```

**Response `200 OK`:** Updated `DoctorDetailDto`.

---

### Delete Doctor
**DELETE** `/api/doctors/{code}`
> Requires: Admin

**Response `200 OK`:** Success message. (Soft delete)

---

## 5. Subjects (Courses) API

### Search Subjects
**GET** `/api/subjects/search?q={query}`
> Requires: Any authenticated user

**Response `200 OK`:** Array of lightweight `SubjectSearchDto`:
```json
[
  {
    "id": "ulid-string",
    "name": "Data Structures",
    "code": "CS301"
  }
]
```

---

### Get Subject by Code
**GET** `/api/subjects/{code}`
> Requires: Any authenticated user

**Response `200 OK`:** Full `SubjectDto`:
```json
{
  "id": "ulid",
  "name": "Data Structures",
  "code": "CS301",
  "creditHours": 3,
  "collegeId": "ulid",
  "collegeName": "Faculty of Engineering",
  "departmentId": "ulid",
  "departmentName": "Computer Science",
  "batchId": "ulid",
  "batchName": "2024",
  "doctorName": "Dr. Mohamed Ali"
}
```

---

### Get Subjects by Batch
**GET** `/api/subjects/by-batch/{batchId}`
> Requires: Any authenticated user
> Cached for 5 minutes server-side.

**Response `200 OK`:** Array of `SubjectDto`.

---

### Get Subjects by Department
**GET** `/api/subjects/by-department/{departmentId}`
> Requires: Any authenticated user

**Response `200 OK`:** Array of `SubjectDto`.

---

### Get Subjects by College
**GET** `/api/subjects/by-college/{collegeId}`
> Requires: Any authenticated user

**Response `200 OK`:** Array of `SubjectDto`.

---

### Get My Subjects
**GET** `/api/subjects/my-subjects`
> Requires: Student OR Doctor (reads from JWT token automatically)

- **Student** → returns subjects they are enrolled in
- **Doctor** → returns subjects assigned to them

**Response `200 OK`:** Array of `SubjectDto`.

---

### Create Subject
**POST** `/api/subjects`
> Requires: Admin

**Request Body:**
```json
{
  "name": "string",               // required
  "code": "string",               // required, must be unique
  "creditHours": 3,               // required, integer
  "departmentCode": "string",     // required
  "collegeCode": "string | null",
  "batchCode": "string | null"
}
```

**Response `201 Created`:** `SubjectDto`.

---

### Update Subject
**PUT** `/api/subjects/{code}`
> Requires: Admin

**Request Body:**
```json
{
  "name": "string",    // required
  "code": "string"     // required
}
```

**Response `200 OK`:** Updated `SubjectDto`.

---

### Delete Subject
**DELETE** `/api/subjects/{code}`
> Requires: Admin

**Response `200 OK`:** Success message.

---

### Assign Doctor to Subject
**PUT** `/api/subjects/assign-doctor`
> Requires: Admin

**Request Body:**
```json
{
  "subjectCode": "string",    // required
  "doctorCode": "string"      // required
}
```

**Response `200 OK`:** Success message.

> This adds a Doctor to a Subject's instructor list (SubjectDoctor junction). A subject can have multiple doctors.

---

### Assign Teaching Assistant to Subject
**PUT** `/api/subjects/assign-assistant`
> Requires: Admin

**Request Body:**
```json
{
  "subjectCode": "string",        // required
  "assistantCode": "string"       // required
}
```

**Response `200 OK`:** Success message.

---

## 6. Subject Offerings API

> A **SubjectOffering** = a Subject being taught in a specific Semester by a specific Doctor for a specific Batch (and optionally Group). Think of it as a "course section".

### Get Offering by Code
**GET** `/api/subjectofferings/by-code/{code}`
> Requires: Any authenticated user

**Response `200 OK`:**
```json
{
  "id": "ulid",
  "subjectId": "ulid",
  "subjectCode": "CS301",
  "subjectName": "Data Structures",
  "creditHours": 3,
  "semesterId": "ulid",
  "semesterName": "Fall 2024",
  "doctorId": "ulid",
  "doctorName": "Dr. Mohamed Ali",
  "maxCapacity": 50,
  "departmentId": "ulid",
  "departmentName": "Computer Science",
  "batchId": "ulid",
  "batchName": "2024",
  "groupId": "ulid | null"
}
```

---

### Get Offerings by Semester
**GET** `/api/subjectofferings/by-semester/{semesterId}`
> Requires: Admin

**Response `200 OK`:** Array of `SubjectOfferingDto`.

---

### Get Doctor's Offerings
**GET** `/api/subjectofferings/my-offerings`
> Requires: Doctor

Returns all subject offerings assigned to the logged-in doctor.

**Response `200 OK`:** Array of `SubjectOfferingDto`.

---

### Get Student's Enrolled Offerings
**GET** `/api/subjectofferings/my-enrollments`
> Requires: Student

Returns all subject offerings the logged-in student is enrolled in.

**Response `200 OK`:** Array of `SubjectOfferingDto`.

---

### Create Subject Offering
**POST** `/api/subjectofferings`
> Requires: Admin

**Request Body:**
```json
{
  "subjectCode": "string",       // required
  "semesterId": "ulid-string",   // required
  "doctorCode": "string",        // required
  "departmentCode": "string",    // required
  "batchCode": "string",         // required
  "groupCode": "string | null",  // optional — if null, offering is for entire batch
  "maxCapacity": 50              // required, 1-1000
}
```

**Response `201 Created`:** `SubjectOfferingDto`.

**Validation Rules:**
- Subject, Semester, Doctor must all exist
- Department, Batch (and Group if provided) must be consistent
- No duplicate offering: same Subject + Semester combination is not allowed

---

## 7. Enrollments API

> Enrollments connect Students to SubjectOfferings.

### Enroll Student in Offering
**POST** `/api/enrollments/{offeringId}`
> Requires: Student (enrolls themselves) OR Admin

> **If the endpoint body requires studentId**, send:
```json
{
  "studentId": "ulid",             // required
  "subjectOfferingId": "ulid"      // required
}
```

> For student self-enrollment: the `offeringId` is in the URL, no body needed.

**Response `201 Created`:** `EnrollmentDto`:
```json
{
  "id": "ulid",
  "studentId": "ulid",
  "studentName": "Ahmed Mohamed",
  "subjectOfferingId": "ulid",
  "subjectCode": "CS301",
  "subjectName": "Data Structures",
  "departmentName": "Computer Science",
  "doctorName": "Dr. Mohamed Ali",
  "semesterName": "Fall 2024",
  "enrolledAt": "2024-09-01T10:30:00Z",
  "isActive": true
}
```

**Business Rules:**
- Student's department must match the SubjectOffering's department
- Student's batch must match the SubjectOffering's batch
- If offering has a GroupId, student's group must match
- Cannot enroll twice (if previously soft-deleted enrollment exists, it gets reactivated)

---

### Get My Enrollments
**GET** `/api/enrollments/my-enrollments`
> Requires: Student

Returns all enrollments for the logged-in student.

**Response `200 OK`:** Array of `EnrollmentDto`.

---

### Get Enrollments by Offering
**GET** `/api/enrollments/by-offering/{offeringId}`
> Requires: Doctor | Admin

Returns all students enrolled in a specific offering.

**Response `200 OK`:** Array of `EnrollmentDto`.

---

### Unenroll (Delete Enrollment)
**DELETE** `/api/enrollments/{id}`
> Requires: Admin

**Response `200 OK`:** Success message. (Soft delete — `isActive` becomes `false`)

---

### Reactivate Enrollment
**PUT** `/api/enrollments/{id}/reactivate`
> Requires: Admin | SuperAdmin

**Response `200 OK`:** Reactivated `EnrollmentDto`.

---

## 8. Materials API

> Materials are files (PDF, DOCX, videos, etc.) uploaded by Doctors to a SubjectOffering. Only enrolled students can access them.

### Upload Material
**POST** `/api/materials/upload`
> Requires: Doctor
> Content-Type: `multipart/form-data`

**Form Data:**
| Field | Type | Description |
|-------|------|-------------|
| `offeringId` | ulid | The SubjectOffering to attach file to |
| `file` | File | The file to upload |

**Allowed file types:**
- PDF (`application/pdf`)
- Word (`application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`)
- PowerPoint (`.ppt`, `.pptx`)
- Excel (`.xls`, `.xlsx`)
- Images (`image/jpeg`, `image/png`, `image/gif`)
- Video (`video/mp4`)
- Archive (`application/zip`)
- Text (`text/plain`)

**Max file size:** 500 MB

**Response `201 Created`:**
```json
{
  "id": "ulid",
  "fileName": "lecture1.pdf",
  "contentType": "application/pdf",
  "fileSize": 2048576,
  "uploadedAt": "2024-09-10T14:00:00Z",
  "fileUrl": "https://cdn.example.com/signed-url..."
}
```

**Business Rule:** Only the Doctor assigned to that SubjectOffering can upload materials to it.

---

### Delete Material
**DELETE** `/api/materials/{id}`
> Requires: Doctor (own uploads only)

**Response `200 OK`:** Success message.

---

### List Materials by Offering
**GET** `/api/materials/by-offering/{offeringId}`
> Requires: Student (must be enrolled) | Doctor | Admin

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `page` | int | Page number (default 1) |
| `size` | int | Items per page (default 20) |
| `search` | string | Filter by file name |

**Response `200 OK`:**
```json
{
  "data": [
    {
      "id": "ulid",
      "fileName": "lecture1.pdf",
      "contentType": "application/pdf",
      "fileSize": 2048576,
      "uploadedAt": "2024-09-10T14:00:00Z",
      "fileUrl": "https://cdn.example.com/signed-url..."
    }
  ],
  "totalCount": 12,
  "page": 1,
  "size": 20,
  "totalPages": 1,
  "hasNext": false,
  "hasPrev": false
}
```

**Access Control:**
- Students: must be actively enrolled in that SubjectOffering
- Doctors & Admins: no enrollment check required

---

### Download Material (Get Signed URL)
**GET** `/api/materials/download/{id}`
> Requires: Student (must be enrolled) | Doctor | Admin

**Response `200 OK`:**
```json
{
  "materialId": "ulid-string",
  "fileName": "lecture1.pdf",
  "fileUrl": "https://cdn.example.com/signed-url-valid-60min...",
  "subjectOfferingId": "ulid-string"
}
```

> The `fileUrl` is a **pre-signed URL** valid for **60 minutes**. Do NOT store this URL — it expires. Call this endpoint each time the user wants to download.

---

### Get Material Metadata
**GET** `/api/materials/{id}/metadata`
> Requires: Any authenticated user

**Response `200 OK`:**
```json
{
  "materialId": "ulid-string",
  "fileName": "lecture1.pdf",
  "fileUrl": "https://cdn.example.com/signed-url...",
  "subjectOfferingId": "ulid-string"
}
```

---

## 9. Common Patterns

### Pagination Response Shape
Every paginated endpoint returns:
```json
{
  "data": [...],
  "totalCount": 150,
  "page": 1,
  "size": 20,
  "totalPages": 8,
  "hasNext": true,
  "hasPrev": false
}
```

### Error Responses
| HTTP Status | Meaning |
|-------------|---------|
| `400 Bad Request` | Validation failed — check `errors` field in response |
| `401 Unauthorized` | No token or token expired |
| `403 Forbidden` | Token valid but role not allowed |
| `404 Not Found` | Resource doesn't exist |
| `409 Conflict` | Duplicate entry (already enrolled, code already used, etc.) |
| `500 Internal Server Error` | Server error |

**Error response shape:**
```json
{
  "message": "Human-readable error message",
  "errors": ["Detailed error 1", "Detailed error 2"]
}
```

### Code vs ID
The API uses two identifiers:
- **`id`** — Internal ULID (used for mutations like PATCH, DELETE, and relations)
- **`code`** — Human-readable code (used in URL params for GET, PUT, DELETE for most entities)

Use `code` in URL paths (e.g., `GET /api/students/STU-001`).
Use `id` (ULID) in request bodies when referencing related entities.

---

## 10. Roles & Permissions Matrix

| Endpoint | SuperAdmin | Admin | Doctor | TeachingAssistant | Student |
|----------|-----------|-------|--------|-------------------|---------|
| Login / Refresh / Logout | ✅ | ✅ | ✅ | ✅ | ✅ |
| Register Student/Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| Register Admin | ✅ | ❌ | ❌ | ❌ | ❌ |
| Reset Password | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Students** | | | | | |
| List / Search / Filter Students | ✅ | ✅ | ✅ | ❌ | ❌ |
| Get Student by Code | ✅ | ✅ | ✅ | ❌ | own only |
| Create / Update / Delete Student | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Doctors** | | | | | |
| List / Search Doctors | ✅ | ✅ | ✅ | ❌ | ✅ |
| Filter Doctors | ✅ | ✅ | ❌ | ❌ | ❌ |
| Create / Update / Delete Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Subjects** | | | | | |
| Search / Get Subjects | ✅ | ✅ | ✅ | ✅ | ✅ |
| My Subjects | ✅ | ✅ | ✅ (own) | ❌ | ✅ (enrolled) |
| Create / Update / Delete Subject | ✅ | ✅ | ❌ | ❌ | ❌ |
| Assign Doctor / Assistant | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Subject Offerings** | | | | | |
| Get Offering by Code | ✅ | ✅ | ✅ | ✅ | ✅ |
| Get by Semester | ✅ | ✅ | ❌ | ❌ | ❌ |
| My Offerings | ❌ | ❌ | ✅ | ❌ | ❌ |
| My Enrolled Offerings | ❌ | ❌ | ❌ | ❌ | ✅ |
| Create Offering | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Enrollments** | | | | | |
| Enroll Student | ✅ | ✅ | ❌ | ❌ | ✅ (self) |
| Get My Enrollments | ❌ | ❌ | ❌ | ❌ | ✅ |
| Get Enrollments by Offering | ✅ | ✅ | ✅ | ❌ | ❌ |
| Delete / Reactivate Enrollment | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Materials** | | | | | |
| Upload Material | ❌ | ❌ | ✅ (own offering) | ❌ | ❌ |
| Delete Material | ❌ | ❌ | ✅ (own) | ❌ | ❌ |
| List / Download Materials | ✅ | ✅ | ✅ | ❌ | ✅ (enrolled) |

---

## 11. Business Rules & Validations

### Phone Number
- Egyptian mobile format only: `01XXXXXXXXX`
- Starts with `010`, `011`, `012`, or `015`
- Exactly 11 digits

### Full Name
- 3 to 100 characters
- Arabic or English letters only (no numbers or special characters)

### Password
- Minimum 6 characters
- `requiresPasswordChange: true` on first login → force change before any other action

### Enrollment Rules
1. Student's **department** must match the SubjectOffering's department
2. Student's **batch** must match the SubjectOffering's batch
3. If the offering has a **group**, student's group must match
4. If a student was previously unenrolled (soft-deleted), enrolling again reactivates the record
5. A student cannot enroll in the same offering twice (409 Conflict)

### Material Access Rules
- Students: must have an **active enrollment** in the SubjectOffering to list or download materials
- Doctors: only the doctor **assigned to that specific offering** can upload/delete materials
- Admins: bypass all enrollment/assignment checks

### Subject Offering Rules
- Cannot create duplicate offering for the same Subject + Semester
- Doctor, Subject, Semester, Department, Batch must all exist
- GroupId is optional — if set, only students in that group can enroll

### Soft Deletes
- Deleting a Student, Doctor, or Enrollment does NOT permanently remove data
- Deleted records have `isActive: false` and a `deletedAt` timestamp
- Data is preserved for audit purposes

### File Download URLs
- `fileUrl` in material responses is a **pre-signed URL** from Cloudflare R2
- Valid for **60 minutes** only
- Never cache or store these URLs — always request a fresh one before download

---

*Report generated for Frontend Integration — University Management System*
*Backend: ASP.NET Core | Storage: Cloudflare R2 | Auth: JWT + Refresh Tokens | IDs: ULID*
