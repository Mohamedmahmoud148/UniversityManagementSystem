---
layout: default
title: "🌊 Complete System Flows"
---

# 🌊 Complete System Flows — Step by Step

## Flow 1: User Login

```
[Frontend]                    [.NET Backend]                [Database]
    │                              │                             │
    ├── POST /api/auth/login ──────►│                             │
    │   { email, password }        │                             │
    │                              ├── Find SystemUser by email ─►│
    │                              │◄── Return user ─────────────┤
    │                              │                             │
    │                              ├── BCrypt.Verify(password)   │
    │                              │   (fails → increment failed │
    │                              │    count → check lockout)   │
    │                              │                             │
    │                              ├── Reset failed count ───────►│
    │                              ├── Generate JWT (60 min) ─── │
    │                              ├── Generate RefreshToken ────►│
    │                              │   (save to RefreshTokens)   │
    │                              │                             │
    │◄── 200 { token, refreshToken,│                             │
    │    role, profileId,          │                             │
    │    mustChangePassword }      │                             │
    │                              │                             │
    ├── if mustChangePassword:     │                             │
    │   redirect to /change-pass   │                             │
    │                              │                             │
    ├── Store token in memory/     │                             │
    │   localStorage               │                             │
    │                              │                             │
    └── Connect SignalR ──────────►│ /hubs/notifications        │
        (with JWT token)           │ Hub adds user to group      │
```

---

## Flow 2: Student AI Chat — Academic Question

```
Student types: "كام ساعة خلصت من اللائحة؟"
    │
    ▼
[Frontend]
  POST /api/chat
  Headers: Authorization: Bearer {jwt}
  Body: { message: "كام ساعة خلصت...", conversationId: "01H..." }
    │
    ▼
[ChatController]
  1. Extract userId from JWT
  2. Save message to ChatMessages table
  3. Fetch conversation history (last 10 messages)
  4. Build academic context (userId, role, profileId, deptId)
  5. POST to FastAPI AI service /chat
    │
    ▼
[FastAPI — PlannerAgent]
  1. Build messages array with system prompt + history
  2. Call Claude API: "Classify this intent"
  3. Claude returns: intent = "general_chat" ← WRONG
    │
    ▼
[FastAPI — Layer 2: _detect_backend_query()]
  1. Check Arabic keywords in message
  2. "خلصت" + "لائحة" detected → OVERRIDE intent
  3. Force: intent = "backend_api_query"
    │
    ▼
[FastAPI — DynamicApiModule]
  1. Match intent + keywords → Rule Q1
  2. GET /api/Regulations/my-roadmap
  3. Headers: Authorization: Bearer {student_jwt}
    │
    ▼
[.NET RegulationsController]
  1. Extract student ProfileId from JWT
  2. Load regulation + subjects
  3. Load student grades (finalized)
  4. Load student enrollments
  5. Compute full roadmap
  6. Return AcademicRoadmapDto
    │
    ▼
[FastAPI]
  1. Receives JSON roadmap
  2. Builds prompt: "Here is the student's roadmap: {...}"
  3. Sends to Claude: "Answer the question: كام ساعة خلصت؟"
  4. Claude reads completedCreditHours: 45, total: 120
  5. Claude generates Arabic response
    │
    ▼
[Response]: "أنهيت 45 ساعة معتمدة من أصل 120 ساعة في لائحتك.
             تبقى لك 75 ساعة، أي ما يعادل 5 فصول دراسية تقريباً.
             معدلك التراكمي الحالي هو 2.85 من 4.0."
    │
    ▼
[ChatController]
  1. Save AI response to ChatMessages
  2. Return { reply: "...", conversationId: "..." }
    │
    ▼
[Frontend]
  Display response in chat UI
```

---

## Flow 3: Doctor Creates AI-Generated Exam

```
Doctor types: "اعمل امتحان كويز لمادة Data Structures 10 أسئلة MCQ"
    │
    ▼
[PlannerAgent]
  Layer 1 LLM: intent = "generate_exam" ✅
  ExamParams: { questionCount: 10, type: Quiz, questionType: MCQ }
  Missing: subjectOfferingId
    │
    ▼
[PlannerAgent checks academic_context]
  If subjectOfferingId in context → auto-fill
  Else → ask user: "أي مادة تقصد؟"
    │
    ▼
[DynamicApiModule]
  POST /api/exams/generate-ai
  Body: { 
    subjectOfferingId: "01H...",
    questionCount: 10,
    examType: "Quiz",
    questionType: "MCQ"
  }
    │
    ▼
[.NET ExamService]
  1. Verify doctor owns the offering
  2. Load subject name, course description
  3. Build Claude prompt:
     "Generate 10 MCQ questions for 'Data Structures'.
      Format: [{question, options:[A,B,C,D], correctAnswer, marks}]"
  4. Call Claude API
  5. Parse JSON response
  6. Create Exam entity (Status = Draft)
  7. Create ExamQuestion records (10 rows)
  8. Return { examId, title, questions }
    │
    ▼
[AI Response]: "تم إنشاء الكويز 'Data Structures Quiz' بنجاح!
               يحتوي على 10 أسئلة اختيار من متعدد.
               يمكنك مراجعة الأسئلة وتعديلها من لوحة التحكم ثم نشرها."
```

---

## Flow 4: Auto-Enrollment

```
Student types: "سجلني في كل المواد المتاحة"
    │
    ▼
[Layer 2 keyword match]: "سجلني" → intent: backend_api_query, Rule F3
    │
    ▼
[DynamicApiModule → Rule F3]
  POST /api/enrollments/auto-enroll
  Headers: { Authorization: Bearer {student_jwt} }
    │
    ▼
[EnrollmentsController]
  1. Extract ProfileId from JWT → studentId
  2. Load Student (with DepartmentId, BatchId, GroupId)
    │
    ▼
[EnrollmentService.AutoEnrollAsync()]
  1. Find all SubjectOfferings WHERE:
     - DepartmentId = student.DepartmentId
     - BatchId = student.BatchId
     - (GroupId = student.GroupId OR GroupId IS NULL)
  2. Load existing enrollments (IgnoreQueryFilters to see soft-deleted)
  3. For each offering:
     - Already active → AlreadyHad++
     - Soft-deleted → Reactivate (IsActive=true, DeletedAt=null) → Enrolled++
     - New → Create Enrollment record → Enrolled++
     - Error → Errors[]
  4. SaveChangesAsync()
  5. Return AutoEnrollResultDto
    │
    ▼
[AI Response]: "تم تسجيلك بنجاح في 5 مواد:
               • Data Structures (CS301)
               • Algorithms (CS302)  
               • Database Systems (CS303)
               • Computer Networks (CS304)
               • Software Engineering (CS305)
               كانت لديك 2 مواد مسجلة مسبقاً."
```

---

## Flow 5: Grade Calculation Flow

```
Doctor finishes semester → clicks "Calculate Grades"
    │
    ▼
POST /api/grades/calculate/{offeringId}
    │
    ▼
[GradeService.CalculateGradesForOfferingAsync()]
  1. Verify doctor owns offering
  2. Load offering (with weight config: midterm 20%, coursework 20%, final 50%, platform 10%)
  3. Validate weights sum to 1.0 (throws if not)
  4. Load all active enrollments for offering
  5. For each student:
     a. Load submitted scores (midterm, coursework, finalExam, platform)
     b. FinalScore = (midterm/maxMidterm × midtermWeight × 100)
                   + (coursework/maxCoursework × courseworkWeight × 100)
                   + (finalExam/maxFinal × finalWeight × 100)
                   + (platform/maxPlatform × platformWeight × 100)
     c. GradeLetter:
        FinalScore >= 90 → A (4.0)
        FinalScore >= 80 → B (3.0)
        FinalScore >= 70 → C (2.0)
        FinalScore >= 60 → D (1.0)
        else             → F (0.0)
     d. Upsert StudentGrade record
  6. AuditLog: "Grade calculation for offering X"
  7. Return summary: { graded: N, passing: M, failing: K }
    │
    ▼
[GPA auto-recalculates on next query via /api/gpa/my-gpa]
    │
    ▼
[If AcademicRiskJob runs tonight]:
  Detects students with GPA < 2.0 → sends notification
```

---

## Flow 6: Complaint Submission + AI Analysis

```
Student submits complaint about unfair grading
    │
    ▼
POST /api/complaints
Body: {
  title: "Unfair Midterm Grading",
  message: "I believe my midterm was graded incorrectly...",
  targetType: "doctor",
  targetId: "01H..."
}
    │
    ▼
[ComplaintController]
  1. Extract studentId from JWT
  2. Validate message length (max 2000 chars)
  3. Create Complaint entity:
     Status = "Pending", Priority = "Normal"
  4. Save to database
  5. Enqueue Hangfire background job: AiBackgroundJob
  6. Return 201 Created immediately (don't wait for AI)
    │
    ├─────────────────────────────────────────────────────────┐
    │                                                         │
    ▼ (async, background)                                     │
[AiBackgroundJob] (Hangfire)                              [Frontend receives 201]
  1. Load complaint text                                   [Student sees success message]
  2. Send to Claude:
     "Analyze this complaint. Return JSON:
      { sentiment, category, riskScore, summary }"
  3. Claude returns:
     { sentiment: "negative",
       category: "academic_fairness",
       riskScore: 0.72,
       summary: "Student disputes midterm grade..." }
  4. Save ComplaintAnalysis to DB
  5. If riskScore > 0.7:
     Update Complaint.Priority = "High"
     Notify admin: "High-risk complaint received"
    │
    ▼
[Next morning — DailyComplaintReport]
  "5 new complaints yesterday.
   Pattern detected: 3 complaints about CS301 midterm grading.
   Recommendation: Review CS301 grading policy."
```

---

## Flow 7: Exam Reminder Flow (Automated)

```
[Hangfire — every 30 minutes]
    │
    ▼
ExamReminderJob.RunAsync()
  1. now = 2026-05-20 08:00 UTC
  2. Query: Published exams where 08:00 < StartTime <= 08:00+24h
  3. Found: "CS301 Final Exam" starts 2026-05-21 10:00 UTC
    │
    ▼
  4. is2hWindow = (StartTime <= now + 2h)? → No → window = "يوم"
  5. Get enrolled students: 145 students in CS301 offering
  6. For each of 145 students:
     - SendNotificationAsync(
         title: "تذكير بامتحان — CS301 Final Exam",
         message: "امتحان 'CS301 Final Exam' يبدأ خلال يوم. الوقت: 10:00 UTC"
       )
     - Also fires SignalR push to online students
    │
    ▼
  7. Run again in 30 minutes: 08:30
     if StartTime <= 10:30 (08:30 + 2h)? → Yes → window = "ساعتين"
     Second reminder: "امتحانك بعد ساعتين!"
```

---

## Flow 8: Real-Time Notification Push

```
Event: Doctor posts "POST /api/notification/send-to-my-students"
    │
    ▼
[NotificationService.SendToOfferingStudentsAsync()]
    │
    ├── 1. DB: INSERT 145 rows into AppNotifications
    │
    └── 2. SignalR: Task.WhenAll(145 push tasks)
                │
                └── For each student online:
                    Hub.Clients.Group(studentUserId)
                      .SendAsync("ReceiveNotification", {
                        title: "Assignment Due",
                        message: "Submit by Friday",
                        actionUrl: "/materials",
                        createdAt: "2026-05-16T..."
                      })
                    
                    [Student's browser receives WebSocket message]
                    → Toast popup appears
                    → Bell counter +1
                    → Notification added to dropdown
```

---

## Flow 9: Student Registration Lifecycle

```
Admin creates student account
    │
    ▼
POST /api/students
  { fullName, email, nationalId, departmentId, batchId, groupId }
    │
    ▼
[StudentService.CreateStudentAsync()]
  1. Generate universityStudentId (auto-increment per dept+batch)
  2. Generate universityEmail: "firstname.lastname@university.edu"
  3. Generate default password: from UniversitySettings
  4. Create SystemUser: { email, passwordHash, role: Student, MustChangePassword: true }
  5. Create Student: { linked to SystemUser, departmentId, batchId, groupId }
  6. AuditLog: "Student created"
  7. Send welcome notification
    │
    ▼
Admin assigns Regulation to student's batch
  PATCH /api/students/{id} { regulationId: "01H..." }
    │
    ▼
Student receives credentials (via email in real system)
    │
    ▼
Student first login:
  → mustChangePassword: true
  → Forced to change password
  → Can now access system
    │
    ▼
Student uses AI auto-enroll:
  → Enrolled in all current semester offerings
    │
    ▼
Student attends classes, takes exams
    │
    ▼
Doctor calculates grades
    │
    ▼
Student checks roadmap:
  → Sees passed/failed/enrolled subjects
  → GPA calculated
  → Recommendations for next semester
```
