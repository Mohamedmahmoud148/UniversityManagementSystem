# 🎓 University ERP — Complete Frontend Integration Guide
> **For:** Frontend Developer  
> **Stack:** ASP.NET Core · PostgreSQL · JWT · Hangfire · Cloudflare R2  
> **Deployment:** Railway — live production  
> **Last Updated:** 2026-05-18

---

## ⚡ Quick Start

```
Base URL:  https://your-backend.railway.app
Auth:      Authorization: Bearer {jwt_token}   (header on every request)
Format:    Content-Type: application/json
IDs:       ULID strings (26 chars) — treat as string, not UUID
```

### All Responses Are Wrapped
```json
{
  "success": true,
  "message": "Done.",
  "data": { ... },
  "errors": null
}
```
Read `data` for payload. Check `success` for status. Show `message` in toasts.

---

## 🔐 1. AUTH

### POST `/api/auth/login`
**No auth required**
```json
// Request
{ "email": "super.admin@university.com", "password": "SuperSecretPass1!" }

// Response
{
  "token": "eyJhbGci...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-05-19T10:00:00Z",
  "role": "SuperAdmin",
  "userId": "01JXXX...",
  "profileId": "01JXXX...",
  "fullName": "Super Admin",
  "universityEmail": "super.admin@university.com"
}
```
**Store:** `token` in localStorage/cookie. Attach to every request as `Authorization: Bearer {token}`.

---

### POST `/api/auth/refresh-token`
```json
{ "token": "expired_jwt", "refreshToken": "abc123..." }
```

### POST `/api/auth/revoke-token` (logout)
```json
{ "refreshToken": "abc123..." }
```

### GET `/api/auth/me`
Returns current user claims. Use on app load to restore session.

### POST `/api/auth/register/student` `[Admin, SuperAdmin]`
```json
{
  "fullName": "Ahmed Mohamed",
  "nationalId": "30501151234567",
  "phone": "01012345678",
  "email": "ahmed@gmail.com",
  "batchCode": "AI2022",
  "groupCode": "G1",
  "departmentCode": "AI"
}
```

### POST `/api/auth/register/doctor` `[Admin, SuperAdmin]`
```json
{
  "fullName": "Dr. Sara Ali",
  "nationalId": "28001151234567",
  "phone": "01098765432",
  "email": "sara@gmail.com",
  "departmentCode": "AI"
}
```

### POST `/api/auth/admin/reset-password/{userId}` `[Admin, SuperAdmin]`
Returns new temporary password.

---

## 🏫 2. ACADEMIC STRUCTURE

> These endpoints build the dropdowns: University → College → Department → Batch → Group

### GET `/api/university/full-structure`
Returns the full nested tree in one call. Use this to populate all structure dropdowns at once.
```json
[{
  "id": "...", "name": "Beni Suef National University",
  "colleges": [{
    "id": "...", "name": "Faculty of Computers",
    "departments": [{
      "id": "...", "name": "Artificial Intelligence",
      "batches": [{
        "id": "...", "name": "Year 4",
        "groups": [{ "id": "...", "name": "Group 1" }]
      }]
    }]
  }]
}]
```

### GET `/api/university/structure`
Flat list of universities.

### GET `/api/colleges` `?page=1&pageSize=10`
### GET `/api/departments` `?page=1&pageSize=10`
### GET `/api/departments/by-college/{collegeId}`
### GET `/api/batches/by-department/{departmentId}`
### GET `/api/groups/by-batch/{batchId}`

### POST `/api/university` `[Admin, SuperAdmin]`
```json
{ "name": "Beni Suef National University" }
```

### POST `/api/colleges` `[Admin, SuperAdmin]`
```json
{ "name": "Faculty of Computers and Information", "universityId": "01JXXX..." }
```

### POST `/api/departments` `[Admin, SuperAdmin]`
```json
{ "name": "Artificial Intelligence", "collegeId": "01JXXX..." }
```

### POST `/api/batches` `[Admin, SuperAdmin]`
```json
{ "name": "Year 4", "departmentId": "01JXXX..." }
```

### POST `/api/groups` `[Admin, SuperAdmin]`
```json
{ "name": "Group 1", "batchId": "01JXXX..." }
```

---

## 🎓 3. ACADEMIC YEARS & SEMESTERS

### GET `/api/academic-years/by-college/{collegeId}`
Returns years like "First Year", "Second Year" etc.

### POST `/api/academic-years` `[Admin, SuperAdmin]`
```json
{ "name": "First Year", "order": 1, "collegeId": "01JXXX...", "isActive": true }
```

### POST `/api/academic-years/{yearId}/activate` `[Admin, SuperAdmin]`
Activates a year.

### POST `/api/academic-years/{yearId}/departments` `[Admin, SuperAdmin]`
```json
{ "departmentId": "01JXXX..." }
```

### GET `/api/semesters/by-academic-year/{academicYearId}` `[Admin, SuperAdmin]`

### POST `/api/semesters` `[Admin, SuperAdmin]`
```json
{
  "name": "Fall 2026",
  "academicYearId": "01JXXX...",
  "startDate": "2026-09-01T00:00:00Z",
  "endDate": "2027-01-31T00:00:00Z"
}
```

---

## 📚 4. SUBJECTS

### GET `/api/subjects/by-department/{departmentId}`
### GET `/api/subjects/by-batch/{batchId}`
### GET `/api/subjects/my-subjects` (for logged-in Doctor — their assigned subjects)
### GET `/api/subjects/{code}`
### GET `/api/subjects/search?name=data`

### POST `/api/subjects` `[Admin, SuperAdmin]`
```json
{
  "name": "Data Structures",
  "code": "CS301",
  "creditHours": 3,
  "departmentId": "01JXXX...",
  "batchId": "01JXXX..."
}
```

### PUT `/api/subjects/assign-doctor?subjectCode=CS301&doctorCode=DOC001` `[Admin, SuperAdmin]`
### PUT `/api/subjects/assign-assistant?subjectCode=CS301&assistantCode=TA001` `[Admin, SuperAdmin]`

---

## 📋 5. SUBJECT OFFERINGS

> An offering = subject + semester + doctor + batch. This is what students enroll into.

### POST `/api/subjectofferings` `[Admin, SuperAdmin]`
```json
{
  "subjectId": "01JXXX...",
  "semesterId": "01JXXX...",
  "doctorId": "01JXXX...",
  "departmentId": "01JXXX...",
  "batchId": "01JXXX...",
  "groupId": null,
  "maxCapacity": 120,
  "midtermMaxScore": 20,
  "midtermWeight": 0.2,
  "courseworkMaxScore": 20,
  "courseworkWeight": 0.2,
  "finalExamMaxScore": 50,
  "finalExamWeight": 0.5,
  "platformMaxScore": 10,
  "platformWeight": 0.1
}
```
**Note:** All weights must sum to exactly 1.0

### GET `/api/subjectofferings/my-offerings` `[Doctor]`
### GET `/api/subjectofferings/my-enrollments` `[Student]`
### GET `/api/subjectofferings/by-semester/{semesterId}` `[Admin, SuperAdmin]`
### GET `/api/subjectofferings/by-department/{departmentId}?semesterId=...` `[Admin, Doctor, SuperAdmin]`
### GET `/api/subjectofferings/by-batch/{batchId}?semesterId=...` `[Admin, Doctor, SuperAdmin]`

---

## 👨‍🎓 6. STUDENTS

### GET `/api/students/filter` `[Admin, Doctor, SuperAdmin]`
```
?page=1&size=20&departmentId=...&batchId=...&groupId=...&isActive=true&search=ahmed
```
Returns paginated `StudentDetailDto` with full hierarchy info.

### GET `/api/students/search?q=ahmed`
Quick search by name or code.

### GET `/api/students/by-offering/{offeringId}?page=1&size=20` `[Admin, Doctor, SuperAdmin]`
Students enrolled in a specific offering.

### GET `/api/students/struggling?threshold=2.0&departmentId=...` `[Admin, SuperAdmin]`
Students with GPA below threshold.

### POST `/api/students` `[Admin, SuperAdmin]`
Creates student + SystemUser in one call. Returns student with auto-generated university email and ID.

### GET `/api/students/import-excel/template` `[Admin, SuperAdmin]`
Downloads a pre-filled `.xlsx` template with correct column headers, sample rows, and a reference sheet listing all valid `BatchCode` and `GroupCode` values.

---

### POST `/api/students/import-excel` `[Admin, SuperAdmin]`
**Multipart form** — field name: `file`, must be `.xlsx`
Required columns: `FullName*`, `NationalId*`, `Phone*`, `BatchCode*`, `GroupCode*`
Optional columns: `CollegeCode`, `DepartmentCode`, `Email`, `Governorate`, `Gender`, `DateOfBirth`, `StudentType`, `Religion`
Headers are case-insensitive and support Arabic aliases (`الاسم`, `رقم قومي`, `تليفون`, `كود الدفعة`, `المجموعة`).

```json
// Response — full JSON result
{
  "totalRows": 150,
  "imported": 142,
  "skipped": 8,
  "validationErrors": 8,
  "temporaryPassword": "Univ@2026",
  "errors": ["Row 5: Batch 'CS2022X' not found — skipped."],
  "warnings": ["Row 23: Phone invalid — using placeholder."],
  "importedCredentials": [
    {
      "fullName": "Ahmed Ali Mohamed",
      "universityStudentId": "STU20260001",
      "universityEmail": "stu20260001@university.edu.eg",
      "temporaryPassword": "Univ@2026",
      "batchCode": "CS2024",
      "groupCode": "GRP-A",
      "department": "Computer Science"
    }
  ],
  "failedRows": [
    {
      "rowNumber": 5,
      "fullName": "Unknown Student",
      "nationalId": "30501011234567",
      "batchCode": "CS2022X",
      "groupCode": "GRP-A",
      "errorMessage": "Batch 'CS2022X' not found in system"
    }
  ]
}
```

> **Note:** `importedCredentials` contains one entry per successfully imported student with their generated `universityEmail` and `universityStudentId`. Display this table to the admin after import. `temporaryPassword` at the top level is the global password for all students (they must change it on first login).

---

### POST `/api/students/import-excel/download-credentials` `[Admin, SuperAdmin]`
**Same upload interface as above.** Instead of JSON, returns a downloadable `.xlsx` report file.

Response header `X-Import-Summary` contains quick counts (read before parsing the file):
```
X-Import-Summary: total=150;imported=142;failed=8;warnings=3;errors=8
```

The Excel file has **3 sheets**:
| Sheet | Content |
|---|---|
| `✓ Successful Students` | #, Full Name, University ID, **Academic Email**, Temp Password, Batch, Group, Department, Status |
| `✗ Failed Rows` | Row #, Full Name, National ID, Batch Code, Group Code, Status, **Error Message** |
| `📊 Summary` | Counts, warnings list, default password |

**Frontend implementation:**
```javascript
// Trigger download and show summary from header
const response = await fetch('/api/students/import-excel/download-credentials', {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}` },
  body: formData
});

// Read summary counts from header (available even before blob is read)
const summary = response.headers.get('X-Import-Summary');
// summary = "total=150;imported=142;failed=8;warnings=3;errors=8"
const stats = Object.fromEntries(summary.split(';').map(s => s.split('=')));
// stats.total, stats.imported, stats.failed, stats.warnings, stats.errors

// Download the report file
const blob = await response.blob();
const url = URL.createObjectURL(blob);
const a = document.createElement('a');
a.href = url;
a.download = `import_report_${Date.now()}.xlsx`;
a.click();
URL.revokeObjectURL(url);
```

---

## 👨‍⚕️ 7. DOCTORS

### GET `/api/doctors/filter?page=1&size=20&departmentId=...` `[Admin, SuperAdmin]`
### GET `/api/doctors/search?q=ahmed`
### GET `/api/doctors/{code}`
### GET `/api/doctors/{code}/subjects`
### POST `/api/doctors` `[Admin, SuperAdmin]`
```json
{
  "fullName": "Dr. Ahmed Hassan",
  "nationalId": "27001151234567",
  "phone": "01012345678",
  "email": "ahmed@gmail.com",
  "departmentId": "01JXXX..."
}
```

---

## 📝 8. SMART REGISTRATION `[Student]`

> This is the intelligent enrollment system. Always use these endpoints instead of the old `/api/enrollments`.

### STEP 1 — Load GPA status and limits
```
GET /api/registration/academic-status
```
```json
{
  "gpa": 3.42, "cgpa": 3.38, "lastSemesterGPA": 3.65,
  "standing": "Good", "standingColor": "green",
  "earnedHours": 87, "remainingHours": 58, "totalRequired": 145,
  "currentLevel": 3, "maxAllowedHours": 18,
  "hasWarning": false, "warningMessage": null
}
```

### STEP 2 — Load available offerings with eligibility
```
GET /api/registration/eligible-offerings?semesterId={id}
```
```json
[{
  "offeringId": "01JXXX...",
  "subjectName": "Data Structures", "subjectCode": "CS301",
  "creditHours": 3, "doctorName": "Dr. Ahmed",
  "maxCapacity": 120, "enrolledCount": 87, "waitlistCount": 3,
  "isFull": false,
  "isEligible": true,
  "blockers": [],
  "warnings": []
}, {
  "offeringId": "01JYYY...",
  "subjectName": "Advanced AI", "subjectCode": "CS501",
  "isEligible": false,
  "blockers": ["Prerequisite not completed: Operating Systems"],
  "warnings": []
}]
```

**Blocker messages to show in RED:**
- `"You are already enrolled in this offering."`
- `"You have already passed this subject."`
- `"Prerequisite not completed: {Name}"`
- `"Must PASS prerequisite: {Name} (currently: {Grade})"`
- `"Credit hours limit exceeded: {X} + {Y} = {Z} > max {max} (GPA: {gpa})"`
- `"Your account is suspended. Contact academic affairs."`

**Warning messages to show in YELLOW:**
- `"⚠️ Academic Warning: GPA {X}. Max {N} hours."`
- `"⚠️ Probation: GPA {X} < {threshold}. Max {N} hours."`
- `"Offering is full ({X}/{max}). You can join the waitlist (position {N})."`

### STEP 3 — Enroll
```
POST /api/registration/enroll/{offeringId}
```
```json
// Success enrolled
{ "success": true, "addedToWaitlist": false, "message": "Successfully enrolled in Data Structures.", "warnings": [] }

// Full — added to waitlist automatically
{ "success": false, "addedToWaitlist": true, "waitlistPosition": 4, "message": "Added to waitlist at position 4." }

// Blocked — 409 Conflict
{ "success": false, "errors": ["Prerequisite not completed: Operating Systems"] }
```

### Waitlist management
```
POST   /api/registration/waitlist/{offeringId}    → join
DELETE /api/registration/waitlist/{offeringId}    → leave
```

### Admin — prerequisites
```
GET    /api/registration/prerequisites/{subjectId}
POST   /api/registration/prerequisites
Body: { "subjectId": "...", "prerequisiteSubjectId": "...", "minimumGrade": 60 }
DELETE /api/registration/prerequisites/{id}
```

### Admin — academic policy
```
GET /api/registration/policy?departmentId=...
```
```json
{
  "defaultMaxHours": 18, "honorMaxHours": 21,
  "warningMaxHours": 12, "probationMaxHours": 9,
  "warningGpaThreshold": 2.0, "probationGpaThreshold": 1.5,
  "honorGpaThreshold": 3.5, "graduationMinGpa": 2.0
}
```

---

## 📊 9. ENROLLMENTS (Legacy — prefer Registration endpoints for Students)

### POST `/api/enrollments/auto-enroll` `[Student]`
Auto-enrolls student in ALL eligible offerings for their batch/group.
```json
{ "enrolled": 4, "alreadyHad": 0, "skipped": 0, "totalAvailable": 4, "enrolledSubjects": ["Data Structures", "Algorithms"] }
```

### POST `/api/enrollments/{offeringId}` `[Student]`
Enroll in a specific offering (no policy pipeline — use `/api/registration/enroll` instead for smart validation).

### GET `/api/enrollments/my-enrollments` `[Student]`
### GET `/api/enrollments/by-offering/{offeringId}` `[Doctor, Admin, SuperAdmin]`
### DELETE `/api/enrollments/{id}` `[Admin, SuperAdmin]`

---

## 📈 10. GRADES & GPA

### POST `/api/grades/import/{offeringId}` `[Doctor, Admin, SuperAdmin]`
**Multipart form** — Excel file
Required columns: `StudentId` (or `id`) — student's UniversityStudentId
Optional: `Midterm`, `Coursework`, `Final`
```json
{ "totalRows": 38, "imported": 36, "skipped": 2,
  "errors": ["Row 12: Grade is finalized — skipped."] }
```

### POST `/api/grades/calculate/{offeringId}` `[Doctor, SuperAdmin]`
Calculates weighted final scores for ALL enrolled students and **automatically persists GPA** to `StudentAcademicStatus`.
```
Response: { "processedCount": 38 }
```

### GET `/api/gpa/my-gpa` `[Student]`
```json
{ "studentId": "...", "studentName": "Ahmed", "gpa": 3.42, "totalCreditHours": 87, "totalSubjects": 29 }
```

### GET `/api/gpa/student/{studentId}` `[Admin, SuperAdmin]`
### POST `/api/gpa/student/{studentId}/recalculate` `[Admin, SuperAdmin]`

---

## 📝 11. REGULATIONS (Curriculum Plans)

### GET `/api/regulations/my-roadmap` `[Student]`
Returns the student's full academic roadmap — which subjects they need, which they've completed, what's remaining.
```json
{
  "regulationTitle": "AI Department Plan 2022",
  "semesters": [{
    "semesterNumber": 1,
    "subjects": [{
      "subjectName": "Programming Fundamentals",
      "creditHours": 3,
      "isRequired": true,
      "status": "Passed",
      "grade": "A"
    }]
  }],
  "totalRequired": 145,
  "totalCompleted": 87,
  "totalRemaining": 58
}
```

### GET `/api/regulations/active`
### GET `/api/regulations/by-department/{departmentId}`
### POST `/api/regulations` `[Admin, SuperAdmin]`
**Multipart form** — can attach a PDF file
```
Fields: title, type (Academic/Conduct/Exam/General), content, departmentId, file (optional PDF)
```

---

## 📖 12. EXAMS

### POST `/api/exams?subjectOfferingId={id}` `[Doctor, SuperAdmin]`
```json
{
  "title": "Midterm Exam", "type": "Midterm",
  "totalMarks": 20, "mode": "Structured",
  "startTime": "2026-11-01T10:00:00Z",
  "endTime": "2026-11-01T12:00:00Z",
  "questions": [{
    "questionText": "What is Big O?",
    "questionType": "Essay",
    "mark": 5
  }]
}
```
**Exam Types:** `Quiz`, `Midterm`, `Final`  
**Exam Modes:** `Structured`, `AI`, `File`  
**Exam Status:** `Draft` → `Published` → `Closed`

### POST `/api/exams/generate-ai` `[Doctor, SuperAdmin]`
AI generates questions automatically.
```json
{ "subjectOfferingId": "...", "examType": "Quiz", "totalMarks": 10, "questionsCount": 5 }
```

### GET `/api/exams/my-enrolled-exams` `[Student]`
### GET `/api/exams/{id}/my-variant` `[Student]`
For randomized exams — returns this student's specific questions.

### POST `/api/exams/{id}/submit` `[Student]`
```json
{
  "examId": "...",
  "answers": [{ "questionId": "...", "answer": "O(n log n)" }]
}
```

### GET `/api/exams/{id}/results` `[Doctor, SuperAdmin]`
### POST `/api/exams/{id}/auto-grade` `[Doctor, SuperAdmin]`
### POST `/api/exams/grade-submission` `[Doctor, SuperAdmin]`
```json
{ "submissionId": "...", "score": 18.5, "feedback": "Good work" }
```

---

## 📅 13. ATTENDANCE

### POST `/api/attendance/sessions` `[Doctor, TeachingAssistant, Admin, SuperAdmin]`
```json
{
  "subjectId": "...",
  "sessionDate": "2026-10-15T09:00:00Z",
  "startTime": "09:00:00",
  "endTime": "11:00:00",
  "qrCodeContent": "ATT-2026-101"
}
```

### POST `/api/attendance/check-in` `[Student]`
Student checks in using QR code.
```json
{ "qrCodeContent": "ATT-2026-101" }
```

### GET `/api/attendance/student/{studentId}/report?subjectId={id}` `[Doctor, TA, Admin, SuperAdmin]`
Returns attendance percentage and session-by-session history.

---

## 📄 14. MATERIALS

### POST `/api/materials/upload` `[Doctor, SuperAdmin]`
**Multipart form:**
```
file: (PDF/Word/etc)
subjectOfferingId: 01JXXX...
title: "Week 3 - Trees"
description: "Binary search trees lecture"
```

### GET `/api/materials/by-offering/{offeringId}?page=1&pageSize=10&search=trees` `[Student, Doctor, Admin, SuperAdmin]`
### GET `/api/materials/download/{id}` `[Student, Doctor, Admin, SuperAdmin]`
Returns a signed Cloudflare R2 URL — redirect the browser to this URL to download.

---

## 🔔 15. NOTIFICATIONS

### GET `/api/notification?unreadOnly=false`
### PUT `/api/notification/{id}/read`
### POST `/api/notification/send-to-my-students` `[Doctor, SuperAdmin]`
```json
{
  "title": "Exam Reminder",
  "message": "Midterm is on Nov 1st",
  "offeringId": "01JXXX..."
}
```

---

## 💬 16. AI CHAT

### POST `/api/chat/conversations`
```json
{ "title": "Academic Questions" }
```

### GET `/api/chat/conversations`
### GET `/api/chat/conversations/{id}/messages?page=1&pageSize=50`

### POST `/api/chat/messages`
```json
{ "conversationId": "01JXXX...", "content": "سجلني في المواد المتاحة" }
```
**The AI understands Arabic and English. It can:**
- Auto-enroll student in subjects
- Show their roadmap and remaining hours
- Create exams (for doctors)
- Answer academic questions using real DB data

### DELETE `/api/chat/conversations/{id}`

---

## 📁 17. FILES

### POST `/api/file/upload` (Staff files — lecture docs, regulations)
**Multipart form** — any file type
```json
// Response
{ "fileId": "...", "fileName": "regulation.pdf", "storageKey": "files/..." }
```

### POST `/api/studentfiles/upload` `[Student]`
**Multipart form** — student's own documents
### GET `/api/studentfiles/my`

---

## 📣 18. COMPLAINTS

### POST `/api/complaints` `[Student]`
```json
{
  "title": "Exam timing issue",
  "message": "The exam was rescheduled without notice...",
  "targetType": "doctor",
  "targetId": "01JXXX..."
}
```
**targetType values:** `doctor`, `department`, `administration`, `technical`, `subject`

### GET `/api/complaints/my-complaints` `[Student]`
### GET `/api/complaints/my-reports` `[Doctor]` — complaints about me
### GET `/api/complaints/all` `[Admin, SuperAdmin]`
### GET `/api/complaints/clusters?targetType=doctor&targetId=...` `[Admin, SuperAdmin, Doctor]`
AI-generated cluster showing patterns across similar complaints.

---

## 📅 19. SCHEDULE

### POST `/api/schedule` `[Admin, SuperAdmin]`
```json
{
  "subjectOfferingId": "...",
  "batchId": "...",
  "groupId": null,
  "dayOfWeek": 0,
  "startTime": "09:00:00",
  "endTime": "11:00:00",
  "type": "Lecture",
  "location": "Hall A-101",
  "weekType": "All"
}
```
**dayOfWeek:** 0=Sunday, 1=Monday, ... 6=Saturday  
**type:** `Lecture`, `Section`, `Lab`  
**weekType:** `All`, `Odd`, `Even`

### GET `/api/schedule/batch/{batchId}`
### GET `/api/schedule/batch/{batchId}/today`
### GET `/api/schedule/my-schedule` `[Doctor]`
### GET `/api/schedule/my-today` `[Doctor]`

---

## 🗑️ 20. INTELLIGENT DELETION `[Admin, SuperAdmin]`

### STEP 1 — Analyze impact (no data changed)
```
POST /api/deletion/analyze
```
```json
// Request
{ "entityName": "College", "entityId": "01JXXX..." }

// Response
{
  "displayName": "Faculty of Computers",
  "riskLevel": "Catastrophic", "riskLevelLabel": "CATASTROPHIC",
  "deleteType": "SoftDelete", "deleteTypeLabel": "Soft Delete",
  "canDelete": true, "isBlocked": false,
  "summary": { "counts": { "Department": 2, "Student": 80, "StudentGrade": 320 } },
  "warnings": ["☠️ CATASTROPHIC RISK — institution-wide impact"],
  "blockers": [],
  "confirmation": {
    "requiresTypedConfirmation": true,
    "typedConfirmationPhrase": "DELETE COLLEGE: FACULTY OF COMPUTERS",
    "requiresPasswordConfirmation": true,
    "requiresSecondAdminApproval": true,
    "confirmationSteps": 4
  }
}
```

### STEP 2 — Execute (after user confirms)
```
POST /api/deletion/execute
```
```json
{
  "entityName": "College",
  "entityId": "01JXXX...",
  "typedConfirmationPhrase": "DELETE COLLEGE: FACULTY OF COMPUTERS",
  "adminPassword": "SuperSecretPass1!"
}
```

**Risk Levels:**
| Level | Examples | Steps |
|---|---|---|
| Low | Notification, RefreshToken | 1 click |
| Medium | Group, AiMemory | Warning + confirm |
| High | Exam, Batch, Regulation | Typed phrase |
| Critical | Student, Doctor, Subject | Typed phrase + password |
| Catastrophic | University, College, Department, Semester | Typed phrase + password + 2nd admin |

**entityName values for deletion:**
`University`, `College`, `Department`, `AcademicYear`, `Semester`, `Batch`, `Group`, `Subject`, `Regulation`, `SubjectOffering`, `Student`, `Doctor`, `TeachingAssistant`, `Exam`, `AttendanceSession`, `StudentAttendance`, `Material`, `UploadedFile`, `Conversation`, `Complaint`, `AppNotification`, `RefreshToken`, `AiMemory`

---

## 📊 21. ANALYTICS & DASHBOARD `[Admin, SuperAdmin]`

### GET `/api/analytics/summary`
```json
{
  "totalStudents": 450, "totalDoctors": 28, "totalSubjects": 64,
  "totalEnrollments": 1820, "totalExams": 47, "totalComplaints": 12
}
```

### GET `/api/analytics/student-count-by-department`
### GET `/api/analytics/doctor-workload?departmentId=...`
### GET `/api/analytics/top-enrolled-subjects?top=10`
### GET `/api/analytics/offering-enrollment-stats?semesterId=...&page=1&size=20`
### GET `/api/dashboard` — overall system stats

---

## 🔍 22. AUDIT LOGS `[Admin, SuperAdmin]`

### GET `/api/auditlogs?page=1&pageSize=20&entity=Student&action=Delete`
```json
{
  "items": [{
    "actionType": "Delete",
    "entityName": "Student",
    "entityId": "01JXXX...",
    "oldValues": "{\"fullName\":\"Ahmed\"}",
    "newValues": null,
    "performedByUserId": "01JYYY...",
    "performedAt": "2026-05-18T10:30:00Z"
  }],
  "totalCount": 145
}
```

---

## 👥 23. ADMINS `[SuperAdmin, Admin]`

### GET `/api/admins`
### GET `/api/admins/{id}`
### PUT `/api/admins/{id}/activate` `[SuperAdmin]`
### PUT `/api/admins/{id}/deactivate` `[SuperAdmin]`
### DELETE `/api/admins/{id}` `[SuperAdmin]`

---

## 🔑 Role Reference

| Role | Value | Can Do |
|---|---|---|
| `SuperAdmin` | Can everything | System owner |
| `Admin` | Manage structure, users, analytics, import | Daily operations |
| `Doctor` | Manage own courses, exams, grades, materials | Teaching |
| `TeachingAssistant` | Attendance, view materials | Support |
| `Student` | Enroll, view grades, chat with AI, submit complaints | Learning |

**JWT Claims:**
- `nameid` / `UserId` → SystemUser ID
- `ProfileId` → Student/Doctor/Admin entity ID (use for role-specific calls)
- `role` → one of the above

---

## ⚠️ Common Errors

| HTTP Code | Meaning | What to Show |
|---|---|---|
| 400 | Bad request — check request body | Show `message` |
| 401 | Token expired or missing | Redirect to login |
| 403 | Wrong role | "Access denied" |
| 404 | Entity not found | "Not found" |
| 409 | Business logic blocked | Show `errors[]` in red |
| 429 | Rate limit (login/refresh have limits) | "Too many attempts, wait" |
| 500 | Server error | "Something went wrong, try again" |

---

## 🧪 Quick Test Accounts (seeded defaults)

| Role | Email | Password |
|---|---|---|
| SuperAdmin | `super.admin@university.com` | `SuperSecretPass1!` |
| Doctor | `ahmed@university.com` | `Pass123!` |
| Student | `ali@university.com` | `Pass123!` |

> ⚠️ These only exist if `SEED_DEMO_DATA=true` is set in Railway.
> If database is clean, create via `/api/auth/register/*` using SuperAdmin token.

---

## 🔄 Typical Page Flows

### Student Home Page
```
1. GET /api/auth/me                          → restore session
2. GET /api/registration/academic-status    → GPA + standing + max hours
3. GET /api/registration/my-enrollments-summary → current subjects
4. GET /api/notification?unreadOnly=true    → unread notifications
```

### Registration Page
```
1. GET /api/semesters/by-academic-year/{id}    → pick semester
2. GET /api/registration/academic-status       → GPA + limits
3. GET /api/registration/eligible-offerings?semesterId={id} → load cards
4. POST /api/registration/enroll/{offeringId}  → on click
```

### Doctor Dashboard
```
1. GET /api/subjectofferings/my-offerings      → my courses
2. GET /api/exams/my-exams                     → my exams
3. GET /api/schedule/my-today                  → today's sessions
```

### Admin Panel
```
1. GET /api/analytics/summary                  → stats
2. GET /api/students/filter?page=1&size=20     → student table
3. GET /api/auditlogs                          → audit trail
```

---

## 📎 Notes for Claude Code

When working with this project in Claude Code:

- All IDs are ULID strings — never parseInt them
- `code` fields are human-readable short codes (like "CS301") — used in URL params
- `id` fields are ULID (26 chars) — used in body and some URL params
- Controllers support both `/by-code/{code}` and `/{id}` for most entities
- Soft-delete means deleted records have `DeletedAt != null` — they won't appear in normal queries
- The registration pipeline (`/api/registration/*`) is smarter than plain `/api/enrollments/*` — prefer it for student enrollment
- GPA is auto-calculated and persisted after `POST /api/grades/calculate/{offeringId}`
- Files are stored on Cloudflare R2 — download links are signed URLs that expire
