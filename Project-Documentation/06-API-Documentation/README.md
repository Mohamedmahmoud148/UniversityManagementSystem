# 📡 Complete API Reference

**Base URL:** `https://your-backend.railway.app`  
**Auth:** Bearer JWT token in `Authorization: Bearer {token}` header  
**Content-Type:** `application/json` (unless noted)  
**ID Format:** ULID strings for all IDs

---

## Authentication APIs (`/api/Auth`)

### POST /api/auth/login
**Purpose:** Authenticate user, receive JWT + refresh token  
**Auth Required:** No  

**Request:**
```json
{
  "email": "student@university.edu",
  "password": "MyPassword123"
}
```

**Response (200):**
```json
{
  "token": "eyJhbGci...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-05-16T12:00:00Z",
  "role": "Student",
  "userId": "01HXYZ...",
  "profileId": "01HABC...",
  "mustChangePassword": false
}
```

**Errors:**
- `401` — Invalid credentials
- `423` — Account locked (too many failed attempts)
- `403` — Account deactivated

**Business Rules:**
- 5 failed attempts → 15-minute lockout
- `mustChangePassword: true` → frontend must redirect to change-password screen

---

### POST /api/auth/refresh
**Purpose:** Get new JWT using refresh token (silent re-auth)  
**Auth Required:** No  

**Request:**
```json
{
  "refreshToken": "abc123..."
}
```

**Response:** Same as login response

---

### POST /api/auth/change-password
**Auth Required:** Yes (any role)

**Request:**
```json
{
  "currentPassword": "OldPass",
  "newPassword": "NewPass123"
}
```

**Response (200):** `{ "message": "Password changed successfully." }`

---

### POST /api/auth/logout
**Auth Required:** Yes  
**Body:** `{ "refreshToken": "abc123" }`  
**Response:** 204 No Content

---

## Student APIs (`/api/Students`)

### GET /api/students
**Auth:** Admin, SuperAdmin  
**Query Params:** `?departmentId=&batchId=&groupId=&search=&page=1&size=20`

**Response:**
```json
{
  "data": [
    {
      "id": "01HXYZ...",
      "code": "STU001",
      "fullName": "Ahmed Mohamed",
      "universityStudentId": "CS2022001",
      "email": "ahmed@gmail.com",
      "batchName": "Batch 2022",
      "departmentName": "Computer Science",
      "collegeName": "Engineering"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "size": 20
}
```

---

### GET /api/students/{id}
**Auth:** Admin, SuperAdmin, Doctor (for their offering's students)  
**Response:** Single StudentSummaryDto

---

### GET /api/students/me
**Auth:** Student  
**Response:** Current student's full profile

---

### GET /api/students/by-offering/{offeringId}
**Auth:** Doctor (owns the offering), Admin  
**Purpose:** All students enrolled in a specific offering  
**Query:** `?page=1&size=20`

---

### GET /api/students/struggling
**Auth:** Admin, SuperAdmin  
**Purpose:** Students with GPA below threshold  
**Query:** `?threshold=2.0&departmentId=&batchId=&page=1&size=20`

**Response:**
```json
{
  "data": [
    {
      "studentId": "01H...",
      "fullName": "...",
      "avgGradePoints": 1.75,
      "failedSubjectsCount": 2
    }
  ]
}
```

---

### POST /api/students
**Auth:** Admin, SuperAdmin  
**Purpose:** Create single student  

**Request:**
```json
{
  "fullName": "Ahmed Mohamed",
  "email": "ahmed@gmail.com",
  "phone": "01012345678",
  "nationalId": "12345678901234",
  "universityId": "01H...",
  "collegeId": "01H...",
  "departmentId": "01H...",
  "batchId": "01H...",
  "groupId": "01H..."
}
```

**Auto-Generated:** universityStudentId, SystemUser account, default password, university email

---

### POST /api/students/bulk-upload-direct
**Auth:** Admin  
**Content-Type:** multipart/form-data  
**Body:** `file: Excel file`  
**Response:** `{ "successCount": 45, "errorCount": 3, "errors": [...] }`

---

### PUT /api/students/{id}
**Auth:** Admin, SuperAdmin

---

### DELETE /api/students/{id}
**Auth:** Admin, SuperAdmin  
**Effect:** Soft delete (sets DeletedAt)

---

## Doctor APIs (`/api/Doctors`)

### GET /api/doctors
**Auth:** Admin, SuperAdmin  
**Query:** `?departmentId=&search=&page=1&size=20`

---

### GET /api/doctors/me
**Auth:** Doctor  
**Response:** Doctor's profile with assigned subjects

---

### GET /api/doctors/by-offering/{offeringId}
**Auth:** Admin, Doctor  
**Response:** The doctor assigned to a specific offering

---

### GET /api/doctors/by-subject/{subjectId}
**Auth:** Admin  
**Response:** All doctors who teach a subject (paginated)

---

### POST /api/doctors
**Auth:** Admin, SuperAdmin  
**Creates:** Doctor profile + SystemUser account

---

## Enrollment APIs (`/api/Enrollments`)

### GET /api/enrollments
**Auth:** Admin, Doctor  
**Query:** `?studentId=&offeringId=&page=1&size=20`

---

### GET /api/enrollments/my-enrollments
**Auth:** Student  
**Response:** All active enrollments for current student

```json
[
  {
    "enrollmentId": "01H...",
    "subjectName": "Data Structures",
    "subjectCode": "CS301",
    "doctorName": "Dr. Ahmed",
    "semesterName": "Spring 2024",
    "isActive": true
  }
]
```

---

### POST /api/enrollments
**Auth:** Admin  
**Purpose:** Manually enroll a student  
**Body:** `{ "studentId": "...", "subjectOfferingId": "..." }`

---

### POST /api/enrollments/auto-enroll
**Auth:** Student  
**Purpose:** AI-powered: enroll student in ALL available courses for their dept+batch+group  
**Body:** (empty — uses JWT identity)

**Response:**
```json
{
  "enrolled": 5,
  "alreadyHad": 2,
  "skipped": 1,
  "totalAvailable": 8,
  "enrolledSubjects": ["Data Structures", "Algorithms", "..."],
  "errors": []
}
```

**Business Logic:**
1. Identify student from JWT `ProfileId` claim
2. Find all SubjectOfferings for student's department + batch + group
3. Filter out already-enrolled (active or soft-deleted reactivation)
4. Create new Enrollment records for remaining
5. Return summary

---

### DELETE /api/enrollments/{id}
**Auth:** Admin  
**Effect:** Soft delete

---

## Grades APIs (`/api/Grades`)

### GET /api/grades/offering/{offeringId}
**Auth:** Doctor (owns offering), Admin  
**Response:** All grades for all students in this offering

---

### GET /api/grades/my-grades
**Auth:** Student  
**Response:** All student's finalized grades across all subjects

```json
[
  {
    "subjectName": "Data Structures",
    "subjectCode": "CS301",
    "finalScore": 85.5,
    "gradeLetter": "B",
    "gradePoints": 3.0,
    "isFinalized": true,
    "semester": "Spring 2024"
  }
]
```

---

### POST /api/grades/calculate/{offeringId}
**Auth:** Doctor (owns offering)  
**Purpose:** Compute weighted grades for all enrolled students  
**Body:** Weights configuration (optional — uses offering defaults)

---

### POST /api/grades/submit/{offeringId}
**Auth:** Doctor  
**Purpose:** Submit manual score components  
**Body:** Array of `{ studentId, midterm, coursework, finalExam, platform }`

---

## GPA APIs (`/api/Gpa`)

### GET /api/gpa/my-gpa
**Auth:** Student  
**Response:**
```json
{
  "studentId": "01H...",
  "studentName": "Ahmed Mohamed",
  "gpa": 3.15,
  "totalCreditHours": 45,
  "earnedCreditHours": 42,
  "grades": [ ... ]
}
```

---

### GET /api/gpa/student/{studentId}
**Auth:** Admin, Doctor  
**Response:** Any student's GPA

---

### POST /api/gpa/student/{studentId}
**Auth:** Admin  
**Purpose:** Force-recalculate GPA

---

## Regulations APIs (`/api/Regulations`)

### GET /api/regulations
**Auth:** Any authenticated  
**Response:** All regulations (paginated)

---

### GET /api/regulations/{id}
**Auth:** Any authenticated  
**Response:** Full regulation with subjects list

---

### GET /api/regulations/my-roadmap ⭐
**Auth:** Student  
**Purpose:** Student's complete personalized academic roadmap  
**Response:**
```json
{
  "regulationId": "01H...",
  "regulationTitle": "CS Department Plan 2022-2026",
  "departmentName": "Computer Science",
  "collegeName": "Engineering",
  "batchName": "Batch 2022",
  "totalSemesters": 8,
  "totalCreditHours": 120,
  "completedCreditHours": 45,
  "remainingCreditHours": 75,
  "totalSubjects": 40,
  "passedSubjects": 15,
  "failedSubjects": 2,
  "currentlyEnrolled": 5,
  "currentGpa": 2.85,
  "semesters": [
    {
      "semesterNumber": 1,
      "status": "completed",
      "totalSubjects": 5,
      "passedSubjects": 5,
      "failedSubjects": 0,
      "enrolledSubjects": 0,
      "totalCreditHours": 15,
      "earnedCreditHours": 15,
      "subjects": [
        {
          "subjectId": "01H...",
          "subjectName": "Programming 1",
          "subjectCode": "CS101",
          "creditHours": 3,
          "isRequired": true,
          "status": "passed",
          "gradeLetter": "A",
          "gradePoints": 4.0,
          "finalScore": 92
        }
      ]
    }
  ],
  "recommendedNext": [ ... ],
  "mustRetake": [ ... ]
}
```

---

### GET /api/regulations/student/{studentId}
**Auth:** Admin, SuperAdmin  
**Response:** Another student's roadmap

---

### GET /api/regulations/by-department/{departmentId}
**Auth:** Any authenticated

---

### POST /api/regulations
**Auth:** Admin, SuperAdmin  
**Content-Type:** multipart/form-data  
**Body:**
```
Title: string
Content: string (optional)
Type: 0|1|2|3
File: PDF/Word (optional)
DepartmentId: ULID (optional)
SubjectsJson: '[{"subjectId":"...","semester":1,"isRequired":true}]'
```

---

## Analytics APIs (`/api/Analytics`)

All require: `Admin` or `SuperAdmin`

### GET /api/analytics/summary
```json
{
  "totalStudents": 1250,
  "totalDoctors": 85,
  "totalOfferings": 340,
  "totalEnrollments": 4800,
  "totalColleges": 9,
  "totalDepartments": 42,
  "totalBatches": 168,
  "topDepartments": [...],
  "topSubjects": [...]
}
```

### GET /api/analytics/student-count-by-department
```json
[
  {
    "departmentId": "01H...",
    "departmentName": "Computer Science",
    "collegeName": "Engineering",
    "studentCount": 450,
    "doctorCount": 28
  }
]
```

### GET /api/analytics/student-count-by-batch

### GET /api/analytics/doctor-workload
**Query:** `?departmentId=&collegeId=`

### GET /api/analytics/top-enrolled-subjects
**Query:** `?top=10`

### GET /api/analytics/offering-enrollment-stats
**Query:** `?departmentId=&batchId=&doctorId=&semesterId=&page=1&size=20`

---

## Notifications APIs (`/api/Notification`)

### GET /api/notification
**Auth:** Any authenticated  
**Query:** `?unreadOnly=false`  
**Response:** User's notifications sorted newest-first

---

### PUT /api/notification/{id}/read
**Auth:** Any authenticated (own notifications only)  
**Response:** 204 No Content

---

### POST /api/notification
**Auth:** Admin, SuperAdmin  
**Purpose:** Send notification to specific user or broadcast  
**Body:**
```json
{
  "userId": "01H...",  // null for broadcast
  "title": "Important Update",
  "message": "The exam schedule has changed",
  "actionUrl": "/exams"
}
```

---

### POST /api/notification/send-to-my-students ⭐
**Auth:** Doctor  
**Purpose:** Doctor sends notification to all their students  
**Query:** `?offeringId=01H...` (optional — scope to one course)  
**Body:**
```json
{
  "title": "Assignment Due Tomorrow",
  "message": "Please submit your assignment by 11:59 PM",
  "actionUrl": "/materials"
}
```

**Response:**
```json
{
  "sentTo": 145,
  "message": "Notification sent to 145 students."
}
```

**Also triggers SignalR push in real-time.**

---

## Exams APIs (`/api/Exams`)

### GET /api/exams/my-exams
**Auth:** Student  
**Response:** All exams for student's enrolled offerings

### GET /api/exams/offering/{offeringId}
**Auth:** Doctor, Admin

### POST /api/exams
**Auth:** Doctor  
**Body:** `{ title, type, totalMarks, startTime, endTime, mode, subjectOfferingId }`

### POST /api/exams/generate-ai
**Auth:** Doctor  
**Purpose:** AI generates exam questions  
**Body:** `{ subjectOfferingId, questionCount, questionType, examType }`

### POST /api/exams/{id}/submit
**Auth:** Student  
**Body:** `{ answers: [ { questionId, answer } ] }`

### POST /api/exams/{id}/auto-grade
**Auth:** Doctor  
**Purpose:** AI grades all submissions for this exam

---

## Complaints APIs (`/api/Complaints`)

### GET /api/complaints
**Auth:** Admin, SuperAdmin  
**Query:** `?status=&priority=&targetType=&page=1&size=20`

### GET /api/complaints/my-complaints
**Auth:** Student

### POST /api/complaints
**Auth:** Student  
**Body:**
```json
{
  "title": "Unfair grading",
  "message": "The midterm grade doesn't reflect my performance...",
  "targetType": "doctor",
  "targetId": "01H..."
}
```
**After save:** AI Background Job runs → analyzes sentiment, risk, category

### PUT /api/complaints/{id}/resolve
**Auth:** Admin  
**Body:** `{ "resolutionNote": "Issue investigated and resolved." }`

---

## Attendance APIs (`/api/Attendance`)

### POST /api/attendance/sessions
**Auth:** Doctor  
**Body:** `{ subjectOfferingId, sessionDate, topic }`

### POST /api/attendance/check-in
**Auth:** Student  
**Body:** `{ sessionId }`

### GET /api/attendance/offering/{offeringId}
**Auth:** Doctor

### GET /api/attendance/my-attendance
**Auth:** Student

---

## Materials APIs (`/api/Materials`)

### GET /api/materials/offering/{offeringId}
**Auth:** Enrolled student, Doctor, Admin

### POST /api/materials/upload
**Auth:** Doctor  
**Content-Type:** multipart/form-data  
**Body:** `file: File, subjectOfferingId: ULID, title: string, description?: string`

---

## SubjectOfferings APIs (`/api/SubjectOfferings`)

### GET /api/subjectofferings/by-department/{departmentId}
**Auth:** Admin, Doctor

### GET /api/subjectofferings/by-doctor/{doctorId}
**Auth:** Doctor (own only), Admin

### GET /api/subjectofferings/by-batch/{batchId}
**Auth:** Admin

### POST /api/subjectofferings
**Auth:** Admin  
**Body:**
```json
{
  "subjectId": "01H...",
  "semesterId": "01H...",
  "doctorId": "01H...",
  "departmentId": "01H...",
  "batchId": "01H...",
  "groupId": "01H...",
  "maxCapacity": 60
}
```

---

## Chat APIs (`/api/Chat`)

### POST /api/chat
**Auth:** Any authenticated  
**Body:**
```json
{
  "message": "كام ساعة خلصت من اللائحة؟",
  "conversationId": "01H..."  // optional — creates new if null
}
```

**Response:**
```json
{
  "reply": "أنهيت 45 ساعة من أصل 120 ساعة في لائحتك...",
  "conversationId": "01H...",
  "intent": "backend_api_query"
}
```

### GET /api/chat/history/{conversationId}
**Auth:** Any (own conversations only)

### GET /api/chat/conversations
**Auth:** Any — list user's conversations

---

## Schedule APIs (`/api/Schedule`)

### GET /api/schedule/my-schedule
**Auth:** Student  
**Response:** Student's weekly schedule

### GET /api/schedule/offering/{offeringId}
**Auth:** Doctor, Admin

### POST /api/schedule
**Auth:** Admin  
**Body:** `{ subjectOfferingId, dayOfWeek, startTime, endTime, room }`

---

## File APIs (`/api/File`)

### POST /api/file/upload
**Auth:** Any  
**Content-Type:** multipart/form-data  
**Body:** `file: File`  
**Response:** `{ fileId: "01H...", fileUrl: "https://..." }`

### GET /api/file/{fileId}
**Auth:** Any authenticated  
**Response:** 60-minute signed download URL

---

## Dashboard APIs (`/api/Dashboard`)

### GET /api/dashboard/admin
**Auth:** Admin, SuperAdmin  
**Response:** Aggregated KPIs

### GET /api/dashboard/student
**Auth:** Student  
**Response:** Student-specific dashboard data

### GET /api/dashboard/doctor
**Auth:** Doctor  
**Response:** Doctor-specific dashboard data

---

## Structure APIs (`/api/[Colleges|Departments|Batches|Groups|Subjects]`)

All follow the same pattern:
- `GET /` — list (paginated, Admin)
- `GET /{id}` — single
- `POST /` — create (Admin)
- `PUT /{id}` — update (Admin)
- `DELETE /{id}` — soft delete (Admin)

---

## Semesters APIs (`/api/Semesters`)

### GET /api/semesters
### GET /api/semesters/{id}
### POST /api/semesters — `{ name, academicYearId, departmentId, startDate, endDate }`

---

## Audit Logs (`/api/AuditLogs`)

### GET /api/auditlogs
**Auth:** SuperAdmin only  
**Query:** `?entityType=&action=&userId=&from=&to=&page=1&size=20`

---

## SignalR Hub

**Endpoint:** `wss://your-backend.railway.app/hubs/notifications`  
**Auth:** JWT Bearer token required  

**Events Received by Client:**
```javascript
connection.on("ReceiveNotification", (data) => {
  // data = { title, message, actionUrl, createdAt }
});
```

**Connection setup:**
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("jwt_token")
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```
