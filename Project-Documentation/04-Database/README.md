# 🗄️ Database Architecture — Complete Reference

## Overview

**Database:** PostgreSQL (hosted on Railway)  
**ORM:** Entity Framework Core 9  
**ID Format:** ULID (all primary keys)  
**Soft Deletes:** All entities via `DeletedAt` timestamp  
**Migrations:** EF Core code-first migrations (31 total as of 2026-05-23)  
**Tables:** 50+ (including 6 new tables from the Phase 1–6 AI upgrade)

---

## Complete Entity Relationship Map

```
University (1)
    │
    ├── College (many)
    │       │
    │       └── Department (many)
    │               │
    │               ├── Batch (many)           ← class year (2022, 2023...)
    │               │       │
    │               │       └── Group (many)   ← subgroup within batch
    │               │               │
    │               │               └── Student (many)
    │               │                       │
    │               │                       ├── Enrollment (many) ──► SubjectOffering
    │               │                       ├── StudentGrade (many) ──► SubjectOffering
    │               │                       ├── StudentAttendance (many) ──► AttendanceSession
    │               │                       ├── ExamSubmission (many) ──► Exam
    │               │                       └── Complaint (many)
    │               │
    │               ├── Doctor (many)
    │               │       │
    │               │       ├── SubjectDoctor (many) ──► Subject (junction)
    │               │       └── SubjectOffering (many) [as teacher]
    │               │
    │               └── Regulation (many)     ← academic plan / curriculum
    │                       │
    │                       └── RegulationSubject (many) ──► Subject
    │
    └── AcademicYear (many)
            │
            └── AcademicYearDepartment (many) ──► Department (junction)
                    │
                    └── Semester (many)
                            │
                            └── SubjectOffering (many)
                                    │
                                    ├── Exam (many)
                                    │       │
                                    │       ├── ExamQuestion (many)
                                    │       └── ExamSubmission (many)
                                    │
                                    ├── Enrollment (many)
                                    ├── StudentGrade (many)
                                    ├── AttendanceSession (many)
                                    │       │
                                    │       └── StudentAttendance (many)
                                    ├── Material (many)
                                    └── ScheduleEntry (many)

SystemUser (1) ──► Student (1:1)
SystemUser (1) ──► Doctor (1:1)
SystemUser (1) ──► Admin (1:1)

SystemUser (1) ──► AppNotification (many)
SystemUser (1) ──► Conversation (many)
                        │
                        └── ChatMessage (many)

SystemUser (1) ──► AiMemory (many)
SystemUser (1) ──► RefreshToken (many)
SystemUser (1) ──► Complaint (many)

Complaint (1) ──► ComplaintAnalysis (1:1)
ComplaintAnalysis (many) ──► ComplaintCluster

Material (1) ──► MaterialChunk (many)  [CASCADE delete — Phase 1 RAG]

RagSearchLog (standalone log)          [Phase 1 RAG]

Student (1) ──► AcademicRiskScore (many) ──► SubjectOffering  [Phase 2 Alerts]

SubjectOffering (1) ──► Assignment (many) ──► Doctor  [Phase 3 Assignments]
Assignment (1) ──► AssignmentSubmission (many) ──► Student
  └── UNIQUE constraint: (AssignmentId, StudentId)

ExamSubmission (1) ──► ExamProctoringLog (1:1) ──► Student  [Phase 6 Proctoring]
```

---

## Entity Details — Every Table

### `SystemUsers` Table
**Purpose:** Central authentication table. All users regardless of role.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID (text) | Primary key |
| `Code` | text | Auto-generated system code |
| `FullName` | text | 3-100 chars, Arabic/English only |
| `Email` | text | Personal email (login) |
| `UniversityEmail` | text | Official university email |
| `NationalId` | text | Egyptian national ID |
| `PasswordHash` | text | BCrypt hashed password |
| `Role` | int | enum: SuperAdmin=0, Admin=1, Doctor=2, Student=3 |
| `IsActive` | bool | Can user login? |
| `AccessFailedCount` | int | Failed login attempts (lockout after 5) |
| `LockoutEnd` | datetime? | When lockout expires |
| `MustChangePassword` | bool | Forces password change on next login |
| `CreatedByUserId` | ULID? | Who created this account |
| `CreatedAt` | datetime | Record creation time |
| `DeletedAt` | datetime? | Soft delete timestamp |

**Business Rules:**
- After 5 failed logins → account locked for 15 minutes
- First login → `MustChangePassword = true` → forced redirect to change password
- FullName regex: `^[؀-ۿa-zA-Z\s]{3,100}$` (Arabic + English only)

---

### `Students` Table
**Purpose:** Student academic profile. Linked 1:1 to SystemUser.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `FullName` | text | Student display name |
| `UniversityStudentId` | text | e.g., "CS2022001" |
| `Phone` | text | Validated Egyptian mobile (01[0125]XXXXXXXX) |
| `Email` | text | Personal email |
| `UniversityId` | ULID | FK → Universities |
| `CollegeId` | ULID | FK → Colleges |
| `DepartmentId` | ULID | FK → Departments |
| `BatchId` | ULID | FK → Batches |
| `GroupId` | ULID | FK → Groups |
| `SystemUserId` | ULID | FK → SystemUsers (1:1) |
| `RegulationId` | ULID? | FK → Regulations (nullable — may not be assigned) |
| `IsActive` | bool | Active student? |

---

### `Doctors` Table
**Purpose:** Doctor (professor) profile. Linked 1:1 to SystemUser.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `FullName` | text | |
| `UniversityStaffId` | text | Staff number |
| `Phone` | text | Validated Egyptian mobile |
| `Email` | text | |
| `DepartmentId` | ULID | FK → Departments |
| `SystemUserId` | ULID | FK → SystemUsers (1:1) |

---

### `Admins` Table
**Purpose:** Admin profile. Linked 1:1 to SystemUser.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `FullName` | text | |
| `SystemUserId` | ULID | FK → SystemUsers |

---

### `Universities` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | University name |
| `Code` | text | Short code |

---

### `Colleges` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | College name |
| `UniversityId` | ULID | FK → Universities |
| `DeletedAt` | datetime? | Soft delete |

---

### `Departments` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | |
| `CollegeId` | ULID | FK → Colleges |

---

### `Batches` Table
**Purpose:** Class year within a department (e.g., "2022 intake").

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | e.g., "Batch 2022" |
| `Code` | text | e.g., "CS2022" |
| `DepartmentId` | ULID | FK → Departments |

---

### `Groups` Table
**Purpose:** Sub-division of a batch (e.g., Group A, Group B).

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | e.g., "Group A" |
| `BatchId` | ULID | FK → Batches |

---

### `AcademicYears` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | e.g., "2023-2024" |
| `StartDate` | datetime | |
| `EndDate` | datetime | |

---

### `AcademicYearDepartments` Table (Junction)
**Purpose:** Links academic years to departments (a department may run in multiple academic years).

| Column | Type | Description |
|--------|------|-------------|
| `AcademicYearId` | ULID | FK |
| `DepartmentId` | ULID | FK |

---

### `Semesters` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | e.g., "Spring 2024" |
| `AcademicYearId` | ULID | FK → AcademicYears |
| `DepartmentId` | ULID | FK → Departments |
| `StartDate` | datetime | |
| `EndDate` | datetime | |

---

### `Subjects` Table
| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Name` | text | e.g., "Data Structures" |
| `Code` | text | e.g., "CS301" |
| `CreditHours` | int | Academic credit weight |
| `DepartmentId` | ULID | FK → Departments |

---

### `SubjectDoctors` Table (Junction)
**Purpose:** Many-to-many: a subject can have multiple assigned doctors; a doctor can teach multiple subjects.

| Column | Type | Description |
|--------|------|-------------|
| `SubjectId` | ULID | FK |
| `DoctorId` | ULID | FK |

---

### `SubjectOfferings` Table ⭐ (Central Table)
**Purpose:** The actual instance of a subject being taught. Connects everything.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Code` | text | Auto-generated unique code |
| `SubjectId` | ULID | What subject |
| `SemesterId` | ULID | When (semester) |
| `DoctorId` | ULID | Who teaches it |
| `DepartmentId` | ULID | Which department |
| `BatchId` | ULID | Which batch year |
| `GroupId` | ULID? | Which group (null = all groups) |
| `MaxCapacity` | int | Max students |
| `MidtermMaxScore` | float | Max midterm marks (default 20) |
| `MidtermWeight` | float | Weight in final grade (default 0.2) |
| `CourseworkMaxScore` | float | Max coursework marks (default 20) |
| `CourseworkWeight` | float | Weight (default 0.2) |
| `FinalExamMaxScore` | float | Max final marks (default 50) |
| `FinalExamWeight` | float | Weight (default 0.5) |
| `PlatformMaxScore` | float | Online platform score (default 10) |
| `PlatformWeight` | float | Weight (default 0.1) |

> **Note:** Weights must sum to 1.0. Validated in GradeService.

---

### `Enrollments` Table
**Purpose:** Student enrolled in a SubjectOffering.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → Students |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |
| `IsActive` | bool | Active enrollment |
| `DeletedAt` | datetime? | Soft delete |
| `CreatedAt` | datetime | Enrollment date |

---

### `StudentGrades` Table
**Purpose:** Final computed grade for a student in a subject offering.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → Students |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |
| `FinalScore` | float | 0-100 weighted total |
| `GradeLetter` | text | A, B, C, D, F |
| `GradePoints` | float | 4.0 scale (A=4, B=3, C=2, D=1, F=0) |
| `MidtermScore` | float? | Raw midterm |
| `CourseworkScore` | float? | Raw coursework |
| `FinalExamScore` | float? | Raw final exam |
| `PlatformScore` | float? | Online platform score |
| `IsFinalized` | bool | Grade locked/published |
| `CalculatedAt` | datetime | When grade was computed |

**Grade Scale:**
| Score | Letter | Points |
|-------|--------|--------|
| 90-100 | A | 4.0 |
| 80-89 | B | 3.0 |
| 70-79 | C | 2.0 |
| 60-69 | D | 1.0 |
| < 60 | F | 0.0 |

---

### `Regulations` Table
**Purpose:** Academic curriculum / study plan for a department.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Title` | text | e.g., "CS Department Plan 2022-2026" |
| `Content` | text? | Text description (optional) |
| `Type` | int | enum: Academic=0, Conduct=1, Exam=2, General=3 |
| `IsActive` | bool | Currently in effect? |
| `FileId` | ULID? | Optional PDF attachment |
| `DepartmentId` | ULID? | Scope to department (null = university-wide) |

---

### `RegulationSubjects` Table
**Purpose:** Lists all subjects in a regulation with their semester and required status.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `RegulationId` | ULID | FK → Regulations |
| `SubjectId` | ULID | FK → Subjects |
| `Semester` | int | Semester number (1-8) |
| `IsRequired` | bool | Required vs elective |

---

### `Exams` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Title` | text | Exam title |
| `Type` | int | enum: Quiz=0, Midterm=1, Final=2 |
| `TotalMarks` | int | |
| `StartTime` | datetime | |
| `EndTime` | datetime | |
| `Mode` | int | enum: Structured, Essay, Mixed |
| `Status` | int | enum: Draft, Published, Closed |
| `FilePath` | text? | If file-based exam |
| `CreatedByDoctorId` | ULID | FK → Doctors |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |

---

### `ExamQuestions` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `ExamId` | ULID | FK → Exams |
| `QuestionText` | text | |
| `QuestionType` | int | MCQ, TrueFalse, Essay |
| `Options` | text? | JSON array for MCQ options |
| `CorrectAnswer` | text? | |
| `Marks` | int | |

---

### `ExamSubmissions` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `ExamId` | ULID | FK → Exams |
| `StudentId` | ULID | FK → Students |
| `SubmittedAt` | datetime | |
| `AutoGradedScore` | float? | AI-graded score |
| `ManualGradedScore` | float? | Doctor override |
| `Answers` | text | JSON of student answers |

---

### `Complaints` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → SystemUsers (the complainant) |
| `Title` | text | |
| `Message` | text | Body (max 2000 chars) |
| `TargetType` | text | "doctor"\|"department"\|"administration"\|"technical"\|"subject" |
| `TargetId` | text | ID of the target entity |
| `Status` | text | "Pending"\|"UnderReview"\|"Resolved"\|"Dismissed" |
| `Priority` | text | "Normal"\|"High"\|"Critical" |
| `ResolutionNote` | text? | Admin/doctor response |

---

### `ComplaintAnalyses` Table
**Purpose:** AI analysis of a complaint (sentiment, category, risk).

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `ComplaintId` | ULID | FK → Complaints (1:1) |
| `Sentiment` | text | positive/negative/neutral |
| `Category` | text | AI-classified category |
| `RiskScore` | float | 0-1 severity |
| `Summary` | text | AI-generated summary |

---

### `ComplaintClusters` Table
**Purpose:** Groups similar complaints for pattern detection.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Label` | text | Cluster label |
| `Description` | text | |
| `ComplaintCount` | int | |

---

### `AppNotifications` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `UserId` | ULID | FK → SystemUsers |
| `Title` | text | |
| `Message` | text | |
| `IsRead` | bool | |
| `ActionUrl` | text? | Deep link in frontend |
| `CreatedAt` | datetime | |
| `DeletedAt` | datetime? | Soft delete |

---

### `Conversations` Table
**Purpose:** AI chat conversation threads.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `UserId` | ULID | FK → SystemUsers |
| `Title` | text? | |
| `CreatedAt` | datetime | |

---

### `ChatMessages` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `ConversationId` | ULID | FK → Conversations |
| `Role` | text | "user" or "assistant" |
| `Content` | text | Message text |
| `CreatedAt` | datetime | |

---

### `AiMemory` Table
**Purpose:** Persistent AI memory per user — facts the AI remembers across sessions.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `UserId` | ULID | FK → SystemUsers |
| `Key` | text | Memory key |
| `Value` | text | Memory value |
| `CreatedAt` | datetime | |

---

### `AttendanceSessions` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |
| `SessionDate` | datetime | |
| `Topic` | text? | What was covered |

---

### `StudentAttendances` Table (Junction)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `SessionId` | ULID | FK → AttendanceSessions |
| `StudentId` | ULID | FK → Students |
| `IsPresent` | bool | |
| `CheckInTime` | datetime? | |

---

### `Materials` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Title` | text | Display title |
| `Description` | text? | |
| `FileName` | text | Original file name |
| `StorageKey` | text | R2 storage key |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |
| `UploadedByDoctorId` | ULID | FK → Doctors |

---

### `ScheduleEntries` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings |
| `DayOfWeek` | int | 0=Sunday ... 6=Saturday |
| `StartTime` | time | |
| `EndTime` | time | |
| `Room` | text? | |

---

### `UploadedFiles` Table
**Purpose:** Tracks all files uploaded to Cloudflare R2.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `FileName` | text | |
| `ContentType` | text | MIME type |
| `StorageKey` | text | R2 key |
| `UploadedByUserId` | ULID | FK → SystemUsers |

---

### `StudentFiles` Table
**Purpose:** Personal documents per student.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → Students |
| `FileName` | text | |
| `StorageKey` | text | |
| `FileType` | text | |

---

### `AuditLogs` Table
**Purpose:** Immutable audit trail of every significant action.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Action` | text | "Create"\|"Update"\|"Delete"\|"Login" |
| `EntityType` | text | Which table was affected |
| `EntityId` | text | Which record |
| `OldValues` | text? | JSON of before-state |
| `NewValues` | text? | JSON of after-state |
| `PerformedByUserId` | ULID | Who did it |
| `Timestamp` | datetime | |

---

### `RefreshTokens` Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Token` | text | Unique token value |
| `UserId` | ULID | FK → SystemUsers |
| `ExpiresAt` | datetime | |
| `IsRevoked` | bool | |
| `CreatedAt` | datetime | |

---

### `EnrollmentUploads` Table
**Purpose:** Tracks bulk enrollment upload jobs.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `UploadedByUserId` | ULID | |
| `Status` | text | pending/processing/done/failed |
| `FileName` | text | |
| `TotalRows` | int | |
| `SuccessRows` | int | |
| `ErrorRows` | int | |

---

### `AiActionLogs` Table
**Purpose:** Logs every AI action for debugging and transparency.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `UserId` | ULID | |
| `Intent` | text | Classified intent |
| `ApiCalled` | text | Which backend API was called |
| `Success` | bool | |
| `DurationMs` | int | |

---

### `MaterialChunks` Table
**Purpose:** Stores text chunks extracted from course materials for RAG (Retrieval-Augmented Generation). Added in migration `AddRagPipeline`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `MaterialId` | ULID | FK → Materials (CASCADE delete) |
| `ChunkIndex` | int | Position of this chunk within the material (0-based) |
| `Content` | text | Raw text content of the chunk (approx. 500 tokens) |
| `Embedding` | text | JSON-serialized float array (1536-dimensional OpenAI embedding) |
| `TokenCount` | int | Actual token count of this chunk |

---

### `RagSearchLogs` Table
**Purpose:** Audit log of student RAG search queries. Added in migration `AddRagPipeline`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | Who searched |
| `Query` | text | The search question |
| `RetrievedChunkIds` | text | JSON array of chunk IDs returned |
| `ResponseSummary` | text? | AI-generated answer summary |
| `CreatedAt` | datetime | When the search occurred |

---

### `AcademicRiskScores` Table
**Purpose:** Per-student risk assessment per subject offering. Upserted daily by `AcademicRiskJob`. Added in migration `AddAcademicRiskScoring`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → Students (RESTRICT) |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings (RESTRICT) |
| `AttendancePercent` | float | Calculated attendance percentage |
| `AverageGrade` | float | Average grade score for this offering |
| `RiskLevel` | int | enum: Low=0, Medium=1, High=2, Critical=3 |
| `AiRecommendation` | text? | Arabic bilingual recommendation text |
| `AnalyzedAt` | datetime | When this assessment was computed |

**Risk Level Formula:**
- `Critical`: attendance < 50% OR average grade < 40
- `High`: attendance < 65% OR average grade < 55
- `Medium`: attendance < 75% OR average grade < 65
- `Low`: all metrics within acceptable range

---

### `Assignments` Table
**Purpose:** Assignments created by doctors for a subject offering. Added in migration `AddAssignmentsSystem`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Title` | text | Assignment title |
| `Description` | text | Full assignment instructions |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings (RESTRICT) |
| `DoctorId` | ULID | FK → Doctors (RESTRICT) |
| `Deadline` | datetime | Submission deadline |
| `MaxGrade` | float | Maximum achievable grade |
| `AllowLateSubmission` | bool | Whether late submissions are accepted |
| `AiGradingEnabled` | bool | Whether AI auto-grading is enabled |
| `GradingRubric` | text? | Rubric text sent to the AI grader |
| `CreatedAt` | datetime | |

---

### `AssignmentSubmissions` Table
**Purpose:** Student submissions for assignments, with AI and human grading fields. Added in migration `AddAssignmentsSystem`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `AssignmentId` | ULID | FK → Assignments (CASCADE) |
| `StudentId` | ULID | FK → Students (RESTRICT) |
| `TextAnswer` | text? | Written answer text |
| `FileUrl` | text? | Submitted file URL (R2) |
| `StorageKey` | text? | R2 storage key |
| `SubmittedAt` | datetime | Submission timestamp |
| `IsLate` | bool | Whether submitted after deadline |
| `Status` | int | enum: Submitted=0, UnderReview=1, Graded=2, Rejected=3 |
| `Grade` | float? | Final grade (human-confirmed) |
| `Feedback` | text? | Doctor feedback text |
| `AiFeedback` | text? | AI-generated feedback |
| `Strengths` | text? | AI-identified strengths (JSON array) |
| `Weaknesses` | text? | AI-identified weaknesses (JSON array) |
| `IsAiGraded` | bool | Whether AI grading was applied |
| `IsHumanReviewed` | bool | Whether doctor has reviewed the AI grade |
| `ReviewedByDoctorId` | ULID? | FK → Doctors who reviewed |

**Constraint:** UNIQUE on (`AssignmentId`, `StudentId`) — one submission per student per assignment.

---

### `ExamProctoringLogs` Table
**Purpose:** Records browser behavior events during online exam sessions. Added in migration `AddProctoringAndAnalytics`.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `ExamSubmissionId` | ULID | FK → ExamSubmissions (RESTRICT) |
| `StudentId` | ULID | FK → Students (RESTRICT) |
| `ExamId` | ULID | FK → Exams |
| `TabSwitchCount` | int | Number of times student switched tabs |
| `FullscreenExitCount` | int | Number of times student exited fullscreen |
| `SuspiciousActivityCount` | int | Total suspicious event count |
| `EventsJson` | text | Full JSON log of all proctoring events |
| `Status` | int | enum: Clean=0, Suspicious=1, Flagged=2 |
| `DoctorNote` | text? | Doctor's note when manually flagging |

**Auto-flag rule:** `ProctoringService.RecordEventAsync()` automatically sets `Status = Flagged` if `TabSwitchCount > 5`.

---

## Deletion Architecture

All delete operations are governed by the **Intelligent Deletion Framework** (`/api/deletion/*`).
Full documentation: [22-Deletion-Framework](../22-Deletion-Framework/README.md)

### Delete Type per Entity Category

| Category | Entities | Delete Strategy |
|---|---|---|
| **Never delete** | University, College, Department, Semester, AcademicYear | SoftDelete only — data preserved forever |
| **Soft delete only** | Student, Doctor, TA, Subject, SubjectOffering, Regulation, Batch | `DeletedAt = now` + `IsActive = false` |
| **Immutable** | StudentGrade (finalized), AuditLog, Exam (published) | Hard block — no delete ever |
| **Hard delete OK** | AppNotification, RefreshToken, ChatMessage, AiMemory, ScheduleEntry | Permanent removal safe |
| **Restricted** | Enrollment, ExamSubmission | Cannot delete while active |

### Risk Levels at a Glance

```
🟢 Low          → AppNotification, RefreshToken, ChatMessage
🟡 Medium       → Group, AiMemory, Material, ScheduleEntry
🟠 High         → Exam, Batch, Regulation, AttendanceSession
🔴 Critical     → Student, Doctor, Subject, SubjectOffering, Enrollment
🟣 Catastrophic → University, College, Department, Semester, AcademicYear
```

### Deletion Order (always leaf-first)
When resetting or cascading, always delete in this direction:
```
ComplaintAnalysis → Exam submissions → Grades → Enrollments
→ SubjectOfferings → Students → Doctors → SystemUsers
→ Groups → Batches → Subjects → Departments → Colleges → University
```

---

## Database Performance Considerations

### Indexes (Critical for Query Speed)
```sql
-- Students
CREATE INDEX idx_students_department ON "Students"("DepartmentId");
CREATE INDEX idx_students_batch ON "Students"("BatchId");
CREATE INDEX idx_students_systemuser ON "Students"("SystemUserId");

-- Enrollments
CREATE INDEX idx_enrollments_student ON "Enrollments"("StudentId");
CREATE INDEX idx_enrollments_offering ON "Enrollments"("SubjectOfferingId");

-- StudentGrades
CREATE INDEX idx_grades_student ON "StudentGrades"("StudentId");
CREATE INDEX idx_grades_offering ON "StudentGrades"("SubjectOfferingId");

-- AppNotifications
CREATE INDEX idx_notifications_user ON "AppNotifications"("UserId");

-- Complaints
CREATE INDEX idx_complaints_student ON "Complaints"("StudentId");
```

### Global Query Filters (Auto-applied soft delete)
```csharp
modelBuilder.Entity<Student>().HasQueryFilter(s => s.DeletedAt == null);
modelBuilder.Entity<Doctor>().HasQueryFilter(d => d.DeletedAt == null);
// etc. for all soft-deletable entities
```

### AsNoTracking Pattern
All read-only queries use `.AsNoTracking()` to avoid EF Core change tracking overhead:
```csharp
var students = await _context.Students
    .AsNoTracking()  // 30-40% faster for read-only
    .Where(s => s.DepartmentId == deptId)
    .Select(s => new StudentSummaryDto { ... })
    .ToListAsync();
```

### N+1 Prevention
Always use `.Include()` or separate batch queries:
```csharp
// WRONG — causes N+1 queries:
foreach (var student in students)
    student.Grades = GetGrades(student.Id); // 1 query per student!

// RIGHT — batch load:
var gradeDict = await _context.StudentGrades
    .Where(g => studentIds.Contains(g.StudentId))
    .ToDictionaryAsync(g => g.StudentId);
```
