# Backend Architecture

> **Last refreshed:** 2026-05-31 | **Framework:** ASP.NET Core 9

---

## 1. Solution Structure

```
UniversityManagementSystem/
├── UniversityManagementSystem.Api/            ← Presentation layer
│   ├── Controllers/   (35 controllers)
│   ├── Hubs/          (NotificationHub — SignalR)
│   ├── Middleware/    (exception, request logging)
│   ├── Converters/    (ULID JSON converter)
│   └── Program.cs     (DI, pipeline, Hangfire jobs)
├── UniversityManagementSystem.Infrastructure/ ← Infrastructure layer
│   ├── Data/          (AppDbContext, GenericRepository)
│   ├── Services/      (all service implementations)
│   ├── Jobs/          (Hangfire job classes)
│   └── Consumers/     (MassTransit event consumers)
├── UniversityManagementSystem.Core/           ← Domain layer (zero outward deps)
│   ├── Entities/      (30+ EF Core entity classes)
│   ├── DTOs/          (request/response shapes)
│   ├── Interfaces/    (service contracts)
│   └── Events/        (domain events for MassTransit)
└── UniversityManagementSystem.Tests/
```

---

## 2. All 35 Controllers

| Controller | Route Prefix | Roles | Purpose |
|-----------|-------------|-------|---------|
| AuthController | /api/auth | Public/Admin | Login, register, refresh token |
| StudentsController | /api/students | Admin | Student CRUD |
| DoctorsController | /api/doctors | Admin | Doctor CRUD |
| AdminsController | /api/admins | Admin | Admin management |
| StructureControllers | /api/colleges, /api/departments, /api/batches | Admin | University hierarchy |
| SubjectsController | /api/subjects | Admin | Subject catalogue |
| SubjectOfferingsController | /api/subjectofferings | Admin, Doctor | Offering management |
| EnrollmentsController | /api/enrollments | Auth | Enrol, auto-enrol, withdraw |
| MaterialsController | /api/materials | Doctor, Student | Upload, download, metadata |
| AssignmentsController | /api/assignments | Doctor, Student | Create, submit, grade |
| ExamsController | /api/exams | Doctor, Student | Lifecycle, publish, results |
| SubmissionsController | /api/submissions | Doctor | Exam submission management |
| GradesController | /api/grades | Doctor, Admin | Grade entry, finalization |
| GpaController | /api/gpa | Auth | GPA queries |
| AttendanceController | /api/attendance | Doctor | Sessions, records, reports |
| RegulationsController | /api/regulations | Auth | CRUD, my-roadmap |
| ComplaintsController | /api/complaints | Auth | Submit, manage complaints |
| NotificationController | /api/notification | Auth | Get, read, send |
| ChatController | /api/chat | Auth | AI conversation |
| AiController | /api/ai | Doctor | Grading, generation tools |
| AiToolsController | /api/ai-tools | Auth | Student overview, GPA, schedule |
| AnalyticsController | /api/analytics | Admin, Doctor | Stats, distributions |
| DashboardController | /api/analytics/dashboard | Auth | Role-specific dashboards |
| RiskController | /api/risk | Admin | Academic risk scoring |
| ScheduleController | /api/schedule | Auth | Timetable |
| SemestersController | /api/semesters | Admin | Semester lifecycle |
| AcademicYearsController | /api/academicyears | Admin | Year management |
| RegistrationController | /api/registration | Admin | Bulk student import |
| RagController | /api/rag | Admin | RAG status, manual trigger |
| FileController | /api/files | Auth | Generic file upload |
| StudentFilesController | /api/studentfiles | Auth | Student documents |
| ProctoringController | /api/proctoring | Student | Exam proctoring events |
| AuditLogsController | /api/auditlogs | Admin | Audit trail |
| DeletionController | /api/deletion | Admin | Soft-delete management |
| DevController | /api/dev | Dev | Development utilities |

---

## 3. Key Service Interfaces (Core/Interfaces/)

```
IAuthService          – Login, JWT generation, password ops
IChatService          – AI conversation, academic_context building
IAiService            – FastAPI HTTP gateway (chat, grading, indexing)
IRagService           – ChromaDB index + semantic search
IMaterialService      – Material upload, metadata, fire-and-forget RAG
IAssignmentService    – Assignment CRUD, submit, AI/manual grade
IExamService          – Exam lifecycle, question randomization
INotificationService  – Create DB record + publish to RabbitMQ
IRegulationService    – Regulation CRUD, by-code lookup, slug generation
IStorageService       – Cloudflare R2 upload/download/signed-URL
IFileService          – UploadedFile entity lifecycle
IGradeService         – StudentGrade creation, finalization, GPA calc
IRiskService          – Attendance + GPA risk scoring
IComplaintService     – Complaint CRUD + AI intelligence
IUserContextService   – Extract userId/profileId/role from JWT claims
```

---

## 4. Entity Framework Core Patterns

- **DB:** PostgreSQL 16 via `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Primary keys:** ULID (NUlid) — time-sortable, URL-safe, collision-free
- **Soft deletes:** Every entity has `BaseEntity.DeletedAt`; all queries filter `WHERE DeletedAt IS NULL`
- **Migrations:** Code-first, applied at startup via `context.Database.MigrateAsync()`
- **Read optimization:** `AsNoTracking()` on every read-only query
- **Projection:** Dashboard/analytics queries project directly to DTOs — no full entity loads

```csharp
public abstract class BaseEntity {
    public Ulid Id { get; set; }
    public string Code { get; set; }        // Human-readable slug
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; } // null = active record
}
```

---

## 5. Background Jobs (Hangfire)

All jobs stored in PostgreSQL. Attributes: `[DisableConcurrentExecution]` + `[AutomaticRetry]`.

| Job Class | Schedule | Smart Logic |
|-----------|----------|------------|
| `ExamReminderJob` | `*/30 * * * *` | 24h + 2h reminders to enrolled students |
| `AssignmentReminderJob` | `*/30 * * * *` | 24h + 2h reminders — skips students who already submitted |
| `AcademicRiskJob` | `0 6 * * *` | Daily GPA + attendance risk for all active students |
| `RagIndexingJob` | Daily | Indexes unindexed Material files into ChromaDB |
| `ComplaintIntelligenceJob` | Daily / Weekly / Monthly | AI-generated admin intelligence reports |

---

## 6. Notification Pipeline

```
NotificationService.SendNotificationAsync(userId, title, message)
  Step 1: INSERT AppNotification → PostgreSQL (guaranteed persistence)
  Step 2: Publish NotificationCreatedEvent → RabbitMQ

NotificationConsumer (MassTransit):
  Step 3: Consume event
  Step 4: IRealtimeNotifier.PushToUserAsync(userId, notification)
  Step 5: SignalR Hub → push to user's connected clients

Fallback: If SignalR fails, DB record already exists → client fetches on next poll
```

---

## 7. Rate Limiting

| Policy | Limit | Window | Scope |
|--------|-------|--------|-------|
| GlobalPolicy | 1000 requests | 1 minute | All endpoints |
| LoginPolicy | 5 requests | 1 minute | /api/auth/login per IP |
| SensitiveAuthPolicy | 10 requests | 1 minute | Password change, sensitive ops |

---

## 8. Caching

`IDistributedCache` backed by Redis (in-memory fallback if Redis unavailable).

| Cached Data | TTL | Invalidation |
|------------|-----|-------------|
| Regulations list | 5 minutes | On create/update/delete |
| Rate limit counters | Sliding window | Auto-expire |
| Chat session state | Session lifetime | On conversation delete |
