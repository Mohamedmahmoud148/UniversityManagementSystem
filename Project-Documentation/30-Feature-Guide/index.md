# Feature Guide — "I want to do X, how?"

> A practical reference for frontend developers. Each section answers one real task with the exact API calls needed, in order.

---

## Table of Contents

### Student Features
1. [Login and get my profile](#1-login-and-get-my-profile)
2. [View my weekly schedule](#2-view-my-weekly-schedule)
3. [View today's classes](#3-view-todays-classes)
4. [Enroll in subjects](#4-enroll-in-subjects)
5. [View my grades and GPA](#5-view-my-grades-and-gpa)
6. [View my academic roadmap](#6-view-my-academic-roadmap)
7. [Take an exam](#7-take-an-exam)
8. [Submit an assignment](#8-submit-an-assignment)
9. [View course materials](#9-view-course-materials)
10. [Chat with AI assistant](#10-chat-with-ai-assistant)
11. [Submit a complaint](#11-submit-a-complaint)
12. [Check in to a class (attendance)](#12-check-in-to-a-class-attendance)

### Doctor Features
13. [View my offerings and students](#13-view-my-offerings-and-students)
14. [View my weekly schedule](#14-view-my-schedule-doctor)
15. [Create and manage exams](#15-create-and-manage-exams)
16. [AI-generate an exam from topics](#16-ai-generate-an-exam)
17. [Grade student submissions](#17-grade-student-submissions)
18. [Upload course materials](#18-upload-course-materials)
19. [Create and grade assignments](#19-create-and-grade-assignments)
20. [Send notification to my students](#20-send-notification-to-my-students)
21. [View complaint reports](#21-view-complaint-reports)
22. [Open attendance session](#22-open-attendance-session)

### Admin Features
23. [Create a student account](#23-create-a-student-account)
24. [Bulk import students from Excel](#24-bulk-import-students-from-excel)
25. [Manage academic structure](#25-manage-academic-structure)
26. [View analytics and dashboards](#26-view-analytics-and-dashboards)
27. [Safely delete an entity](#27-safely-delete-an-entity)
28. [View audit logs](#28-view-audit-logs)

---

## Student Features

### 1. Login and get my profile

```
POST /api/auth/login
→ store accessToken, refreshToken, role
→ if mustChangePassword == true → redirect to change password screen

GET /api/auth/me
→ get userId, fullName, role, email
```

When token expires (401 response):
```
POST /api/auth/refresh-token
Body: { "refreshToken": "..." }
→ get new accessToken, replace old one
```

---

### 2. View my weekly schedule

```
Step 1: GET /api/students/me
→ extract batchId from response

Step 2: GET /api/schedule/batch/{batchId}
→ full weekly schedule (all days, all slots)
```

**Schedule slot shape:**
```json
{
  "dayOfWeek": 1,
  "dayName": "Monday",
  "startTime": "09:00",
  "endTime": "11:00",
  "subjectName": "Database Systems",
  "doctorName": "Dr. Ahmed",
  "hall": "Hall B-201",
  "isNow": false
}
```

---

### 3. View today's classes

```
Step 1: GET /api/students/me → get batchId
Step 2: GET /api/schedule/batch/{batchId}/today
```

Each slot has `isNow: true/false` — use it to highlight the currently running class.

---

### 4. Enroll in subjects

**Auto-enroll (recommended):**
```
POST /api/enrollments/auto-enroll
→ enrolls in ALL available offerings for student's batch/group/dept
Response: { enrolled: 5, alreadyEnrolled: 2, skipped: 1 }
```

**Manual enroll in one subject:**
```
POST /api/enrollments/
Body: { "offeringId": "01ARZ..." }
```

**View current enrollments:**
```
GET /api/enrollments/my-enrollments
or
GET /api/subjectofferings/my-enrollments
```

---

### 5. View my grades and GPA

```
GET /api/grades/my-grades
→ list of grades per subject (midterm, coursework, final, platform, total, letter)

GET /api/gpa/my-gpa
→ { gpa: 3.4, totalCreditHours: 48, gradePoints: ... }
```

---

### 6. View my academic roadmap

```
GET /api/regulations/my-roadmap
→ full academic status: passed subjects, remaining, credit hours, GPA
```

Use this to show progress bars, "X credit hours remaining", graduation estimate.

---

### 7. Take an exam

```
Step 1: GET /api/exams/my-enrolled-exams
→ list upcoming/active exams

Step 2: GET /api/exams/{id}/session
→ starts timed session, returns questions + time limit

Step 3 (optional): GET /api/exams/{id}/my-variant
→ if exam is randomized, get your specific question order

Step 4 (mid-exam auto-save): POST /api/exams/{id}/save-progress
Body: { "answers": [...] }

Step 5: POST /api/exams/{id}/submit
Body: { "answers": [...] }
→ final submission, cannot be changed after
```

**Proctoring events (send while exam is open):**
```
POST /api/proctoring/event
Body: { "examId": "...", "eventType": "TabSwitch", "timestamp": "..." }
```

---

### 8. Submit an assignment

```
Step 1: GET /api/assignments/offering/{offeringId}
→ list assignments for the subject

Step 2: GET /api/assignments/{id}/my-submission
→ check if already submitted

Step 3: POST /api/assignments/{id}/submit
Content-Type: multipart/form-data
Fields: textAnswer (string), file (optional)
```

**View grade after submission:**
```
GET /api/assignments/{id}/my-submission
→ includes grade, aiFeedback, strengths, weaknesses
```

---

### 9. View course materials

```
GET /api/materials/by-offering/{offeringId}
→ list of materials (name, type, uploadedAt)

GET /api/materials/download/{id}
→ returns signed download URL (valid 60 min)
```

**AI semantic search over materials:**
```
POST /api/rag/search
Body: { "query": "explain foreign keys", "offeringId": "..." }
→ returns relevant excerpts from uploaded PDFs
```

---

### 10. Chat with AI assistant

```
Step 1: POST /api/chat/conversations
Body: { "title": "Academic Questions" }
→ get conversationId

Step 2: POST /api/chat/messages
Body: { "conversationId": "...", "content": "كام ساعة خلصت؟" }
→ AI replies in Arabic/English based on query

Step 3 (history): GET /api/chat/conversations/{id}/messages
```

**The AI can answer:**
- Academic roadmap questions
- Schedule queries
- Grade questions
- Enrollment help
- General university info

---

### 11. Submit a complaint

```
GET /api/complaints/doctor-options
→ list of doctors to complain about

POST /api/complaints/
Body: {
  "targetType": "Doctor",
  "targetId": "01ARZ...",
  "category": "Grading",
  "description": "..."
}
→ 202 Accepted (AI analysis runs in background)
```

**View own complaints:**
```
GET /api/complaints/my-complaints
→ includes AI-generated summary and priority
```

---

### 12. Check in to a class (attendance)

```
POST /api/attendance/check-in
Body: { "sessionId": "01ARZ..." }
```

The `sessionId` is provided by the doctor (shown as QR or code in class).

---

## Doctor Features

### 13. View my offerings and students

```
GET /api/subjectofferings/my-offerings
→ all offerings assigned to this doctor

GET /api/students/by-offering/{offeringId}
→ students enrolled in a specific offering
```

---

### 14. View my schedule (Doctor)

```
GET /api/schedule/my-schedule
→ full weekly schedule across all batches

GET /api/schedule/my-today
→ today's classes only
```

---

### 15. Create and manage exams

```
POST /api/exams/
Body: {
  "title": "Midterm Exam",
  "offeringId": "01ARZ...",
  "startTime": "2026-06-01T09:00:00Z",
  "durationMinutes": 90,
  "questions": [
    {
      "text": "What is normalization?",
      "type": "MCQ",
      "choices": ["A", "B", "C", "D"],
      "correctAnswer": "A",
      "points": 5
    }
  ]
}

GET /api/exams/my-exams
→ all exams I created

GET /api/exams/{id}/results
→ all student submissions with scores

GET /api/exams/{id}/analytics
→ pass rate, average score, difficulty per question
```

---

### 16. AI-generate an exam

```
POST /api/exams/generate-ai
Body: {
  "offeringId": "01ARZ...",
  "topics": ["Normalization", "SQL Joins", "Indexing"],
  "questionCount": 20,
  "difficulty": "Medium",
  "questionTypes": ["MCQ", "TrueFalse"]
}
→ returns ready exam with questions, you review before publishing

Alternative — generate from PDF:
POST /api/exams/preview-questions-from-pdf
Content-Type: multipart/form-data
Fields: file (PDF), questionCount, difficulty
→ extract questions from lecture PDF
```

---

### 17. Grade student submissions

**Auto-grade all (AI):**
```
POST /api/exams/{id}/auto-grade
→ AI grades all ungraded submissions

POST /api/assignments/submissions/{id}/ai-grade
→ AI grades one assignment submission
```

**Manual grade:**
```
POST /api/assignments/submissions/{id}/grade
Body: { "grade": 18, "feedback": "Good work but missing..." }

POST /api/exams/grade-submission
Body: { "submissionId": "...", "answers": [...] }
```

---

### 18. Upload course materials

```
POST /api/materials/upload
Content-Type: multipart/form-data
Fields: file, offeringId, title, description

→ file stored in Cloudflare R2
→ auto-indexed for RAG search (background job)
```

---

### 19. Create and grade assignments

```
POST /api/assignments/
Body: {
  "title": "ERD Design",
  "offeringId": "01ARZ...",
  "deadline": "2026-06-01T23:59:00Z",
  "maxGrade": 20,
  "aiGradingEnabled": true,
  "gradingRubric": "Evaluate normalization (40%), relationships (30%), naming (30%)"
}

GET /api/assignments/{id}/submissions
→ all student submissions

POST /api/assignments/submissions/{id}/ai-grade
→ AI grades with rubric → returns score, feedback, strengths, weaknesses
```

---

### 20. Send notification to my students

```
POST /api/notification/send-to-my-students
Body: {
  "title": "Exam Reminder",
  "body": "Midterm is tomorrow at 9 AM, Hall B-201",
  "type": "Exam"
}
→ sent to ALL students in ALL your offerings simultaneously
→ real-time push via SignalR
```

---

### 21. View complaint reports

```
GET /api/complaints/my-reports
→ complaints filed against me

GET /api/complaints/clusters?targetType=Doctor&targetId={myId}
→ AI-clustered patterns in complaints (categories, frequency)
```

---

### 22. Open attendance session

```
POST /api/attendance/sessions
Body: {
  "offeringId": "01ARZ...",
  "date": "2026-05-24",
  "startTime": "09:00",
  "endTime": "11:00"
}
→ returns sessionId → share with students as QR or code

GET /api/attendance/student/{studentId}/report?subjectId=...
→ attendance percentage per student
```

---

## Admin Features

### 23. Create a student account

```
POST /api/students/
Body: {
  "fullName": "Ahmed Ali Mohamed",
  "phone": "01012345678",
  "nationalId": "30501011234567",
  "batchCode": "BATCH-2024",
  "groupCode": "GRP-A",
  "universityStudentId": "202400001",
  "email": "ahmed@gmail.com"
}
→ auto-generates university email and password
→ returns credentials
```

---

### 24. Bulk import students from Excel

```
Step 1: GET /api/students/import-excel/template
→ download the official template

Step 2: Fill template, then:
POST /api/students/import-excel/download-credentials
Content-Type: multipart/form-data
Fields: file (.xlsx)
→ imports all students + returns Excel with all credentials

Alternative (background processing):
POST /api/students/bulk-upload-direct   ← fast, direct
POST /api/students/bulk-upload-ai       ← AI-assisted, handles messy data
```

---

### 25. Manage academic structure

Order of creation matters:
```
1. POST /api/universities/
2. POST /api/colleges/        (needs universityId)
3. POST /api/departments/     (needs collegeId)
4. POST /api/subjects/        (needs departmentId)
5. POST /api/academicyears/
6. POST /api/semesters/       (needs academicYearId)
7. POST /api/batches/         (needs departmentId + semesterId)
8. POST /api/groups/          (needs batchId)
9. POST /api/subjectofferings/ (needs subjectId + batchId + doctorId + semesterId)
```

---

### 26. View analytics and dashboards

```
GET /api/dashboard/admin
→ KPIs: total students, active enrollments, upcoming exams, at-risk count

GET /api/analytics/summary
→ system-wide numbers

GET /api/analytics/student-count-by-department
GET /api/analytics/doctor-workload?departmentId=&collegeId=
GET /api/analytics/top-enrolled-subjects?top=10

GET /api/risk/dashboard
→ all academically at-risk students with risk scores
```

---

### 27. Safely delete an entity

**Always analyze impact before deleting:**
```
Step 1: POST /api/deletion/analyze
Body: { "entityType": "Batch", "entityId": "01ARZ..." }
→ returns: what will be cascade-deleted, counts, warnings

Step 2 (only if impact is acceptable):
POST /api/deletion/execute
Body: { "entityType": "Batch", "entityId": "01ARZ...", "confirmed": true }
```

---

### 28. View audit logs

```
GET /api/auditlogs/
→ paginated: who did what, when, on which entity
```
Only accessible by SuperAdmin.

---

## Common Patterns

### Handling token expiry
```javascript
// On 401 response:
const refresh = await fetch('/api/auth/refresh-token', {
  method: 'POST',
  body: JSON.stringify({ refreshToken: stored_refresh_token })
});
const { accessToken } = await refresh.json();
// retry original request with new token
```

### Pagination
```
?page=1&size=20     ← default
?page=2&size=50     ← next page
Response: { data: [...], totalCount: 150, page: 2, size: 50 }
```

### Real-Time Notifications (SignalR)
```javascript
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/notifications', {
    accessTokenFactory: () => accessToken
  })
  .build();

connection.on('ReceiveNotification', (notification) => {
  // show toast, update unread count
});

await connection.start();
```
