# Database Architecture

> **Last refreshed:** 2026-05-31 | **Engine:** PostgreSQL 16 | **ORM:** EF Core 9

---

## 1. Design Decisions

| Decision | Rationale |
|----------|-----------|
| **ULID primary keys** | Time-sortable, URL-safe, no collision risk |
| **Soft deletes** | `BaseEntity.DeletedAt` — data never destroyed; all queries filter `WHERE DeletedAt IS NULL` |
| **Code-first migrations** | Schema in C# source; `MigrateAsync()` at startup |
| **No lazy loading** | All navigations via `.Include()` or projection — zero N+1 |
| **Hangfire in same DB** | Job state co-located with business data |
| **Projection on analytics** | Dashboard queries `.Select(x => new Dto{})` — no full entity loads |

---

## 2. Entity Catalogue (30+)

### Identity & Structure

| Entity | Key Fields |
|--------|-----------|
| `SystemUser` | FullName (3-100), Email, PasswordHash, UserRole, IsActive |
| `Student` | FullName, UniversityStudentId, StudentType, Gender, DepartmentId, BatchId, RegulationId |
| `Doctor` | FullName, UniversityStaffId, Phone, Email, DepartmentId |
| `College` | Name, Code |
| `Department` | Name, Code, CollegeId |
| `Batch` | Name, DepartmentId |
| `AcademicYear` | Name, StartDate, EndDate |
| `Semester` | Name, AcademicYearId, IsActive |

### Academic Content

| Entity | Key Fields |
|--------|-----------|
| `Subject` | Name, Code, CreditHours, DepartmentId |
| `SubjectOffering` | SubjectId, DoctorId, SemesterId, MaxCapacity, MidtermWeight=20%, FinalWeight=50% |
| `Enrollment` | StudentId, SubjectOfferingId, EnrolledAt, IsActive |
| `StudentGrade` | StudentId, SubjectOfferingId, GradePoints, GradeLetter, FinalScore, IsFinalized |

### Materials & RAG

| Entity | Key Fields |
|--------|-----------|
| `Material` | Title, StorageKey, ContentType, FileSize, SubjectOfferingId, UploadedByDoctorId |
| `MaterialChunk` | MaterialId, ChunkIndex, Content, Embedding (JSON), TokenCount |
| `UploadedFile` | FileName, StorageKey, ContentType, FileSizeBytes, ValidationStatus |

### Assignments

| Entity | Key Fields |
|--------|-----------|
| `Assignment` | Title, Description, Deadline, MaxGrade=100, AllowLateSubmission, AiGradingEnabled, GradingRubric (JSON) |
| `AssignmentSubmission` | AssignmentId, StudentId, TextAnswer?, FileUrl?, IsLate, Status, Grade?, AiFeedback?, IsAiGraded |

### Examinations

| Entity | Key Fields |
|--------|-----------|
| `Exam` | Title, ExamType, ExamStatus, StartTime, EndTime, TotalMarks, IsRandomized, QuestionsPerStudent |
| `ExamQuestion` | ExamId, QuestionText, QuestionType, Options (JSON), CorrectAnswer, Points |
| `ExamSubmission` | ExamId, StudentId, AnswersJson, Score, IsGraded, SubmittedAt |
| `ExamProctoringEvent` | ExamSubmissionId, EventType, Timestamp |

### Attendance

| Entity | Key Fields |
|--------|-----------|
| `AttendanceSession` | SubjectId, SessionDate, DoctorId |
| `StudentAttendance` | StudentId, AttendanceSessionId, IsPresent |

### Regulations

| Entity | Key Fields |
|--------|-----------|
| `Regulation` | Title, Content?, Type (Academic/Conduct/Exam/General), IsActive, FileId? |
| `RegulationSubject` | RegulationId, SubjectId, Semester (1-8), IsRequired |

### Communication

| Entity | Key Fields |
|--------|-----------|
| `AppNotification` | UserId, Title, Message, IsRead, ActionUrl? |
| `Complaint` | StudentId, Title, Message (max 2000), Status, Priority (Normal/High/Critical), TargetType |
| `ChatConversation` | UserId, Title |
| `ChatMessage` | ConversationId, Sender, Content |

---

## 3. Key Relationships

```
College → (N) Department → (N) Batch → (N) Student
SystemUser → (0..1) Student
SystemUser → (0..1) Doctor
Student → (0..1) Regulation
Regulation → (N) RegulationSubject → Subject

Subject → (N) SubjectOffering
SubjectOffering → (N) Enrollment ← Student
SubjectOffering → (N) Material → (N) MaterialChunk
SubjectOffering → (N) Assignment → (N) AssignmentSubmission ← Student
SubjectOffering → (N) Exam → (N) ExamSubmission ← Student
SubjectOffering → (N) AttendanceSession → (N) StudentAttendance
```

---

## 4. GPA Calculation

```sql
SELECT ROUND(AVG(grade_points)::numeric, 2)
FROM student_grades
WHERE student_id = $1
  AND is_finalized = TRUE
  AND deleted_at IS NULL
  AND grade_points > 0
```

---

## 5. Full ERD

See [21-Diagrams — ERD](../21-Diagrams/index.md#diagram-8--erd-key-relationships).
