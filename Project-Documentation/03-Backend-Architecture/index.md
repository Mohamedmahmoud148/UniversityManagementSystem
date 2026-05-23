---
layout: default
title: "🔧 Backend Architecture"
---

# 🔧 Backend Architecture — Deep Dive

## Project Structure

```
UniversityManagementSystem.Core/
├── Entities/                     ← 42 domain entities (pure C# classes)
│   ├── BaseEntity.cs             ← Id (ULID), Code, CreatedAt, DeletedAt
│   ├── SystemUser.cs             ← All users (Student/Doctor/Admin)
│   ├── Student.cs                ← Student academic profile
│   ├── Doctor.cs                 ← Doctor (professor) profile
│   ├── Admin.cs                  ← Admin profile
│   ├── University.cs             ← Top-level institution
│   ├── College.cs                ← Faculty (Engineering, Science...)
│   ├── Department.cs             ← CS, Electronics, Civil...
│   ├── Batch.cs                  ← Class year (2022, 2023...)
│   ├── Group.cs                  ← Section within batch (A, B, C)
│   ├── AcademicYear.cs           ← Academic year (2023-2024)
│   ├── Semester.cs               ← Spring/Fall 2024
│   ├── Subject.cs                ← Course definition
│   ├── SubjectOffering.cs ⭐     ← Course instance (most important!)
│   ├── SubjectDoctor.cs          ← Junction: subject ↔ doctor
│   ├── SubjectAssistant.cs       ← Teaching assistants
│   ├── Enrollment.cs             ← Student enrolled in offering
│   ├── StudentGrade.cs           ← Computed grade record
│   ├── Regulation.cs             ← Academic curriculum
│   ├── RegulationSubject.cs      ← Subject in regulation
│   ├── Exam.cs                   ← Exam instance
│   ├── ExamQuestion.cs           ← Questions for exam
│   ├── ExamSubmission.cs         ← Student's answers
│   ├── AttendanceSession.cs      ← Lecture session
│   ├── StudentAttendance.cs      ← Student attendance record
│   ├── Complaint.cs              ← Student complaint
│   ├── ComplaintAnalysis.cs      ← AI analysis of complaint
│   ├── ComplaintCluster.cs       ← Grouped complaints
│   ├── AppNotification.cs        ← In-app notification
│   ├── Conversation.cs           ← AI chat thread
│   ├── ChatMessage.cs            ← Individual message
│   ├── AiMemory.cs               ← Persistent AI user facts
│   ├── AuditLog.cs               ← Immutable action log
│   ├── RefreshToken.cs           ← JWT refresh token
│   ├── Material.cs               ← Course material/file
│   ├── ScheduleEntry.cs          ← Class schedule slot
│   ├── UploadedFile.cs           ← File metadata (R2)
│   ├── StudentFile.cs            ← Student personal documents
│   └── EnrollmentUpload.cs       ← Bulk enrollment job
│
├── DTOs/                         ← Data Transfer Objects (request/response shapes)
├── Interfaces/                   ← Service contracts (33 interfaces)
├── Exceptions/                   ← DomainException (business rule violations)
└── Events/                       ← Domain events (AttendanceRecordedEvent)
```

---

## Controllers (API Endpoints)

| Controller | Endpoints | Auth |
|------------|---------|------|
| `AuthController` | login, refresh, logout, change-password | Public/Any |
| `StudentsController` | CRUD, bulk-upload, by-offering, struggling | Admin/Student |
| `DoctorsController` | CRUD, bulk-upload, by-offering, by-subject | Admin/Doctor |
| `AdminsController` | CRUD | SuperAdmin |
| `EnrollmentsController` | CRUD, my-enrollments, auto-enroll | Admin/Student |
| `SubjectOfferingsController` | CRUD, by-dept, by-doctor, by-batch | Admin/Doctor |
| `SubjectsController` | CRUD | Admin |
| `RegulationsController` | CRUD, my-roadmap, by-department | Admin/Student |
| `AnalyticsController` | summary, dept-count, workload, top-subjects | Admin |
| `GradesController` | my-grades, submit, calculate, offering-grades | Doctor/Student |
| `GpaController` | my-gpa, student-gpa, recalculate | Doctor/Student/Admin |
| `ExamsController` | CRUD, generate-ai, submit, auto-grade | Doctor/Student |
| `AttendanceController` | sessions, check-in, my-attendance | Doctor/Student |
| `ComplaintsController` | CRUD, my-complaints, resolve | Student/Admin |
| `NotificationController` | get, read, send, send-to-students | Any/Admin/Doctor |
| `MaterialsController` | list, upload, download | Doctor/Student |
| `ScheduleController` | my-schedule, offering-schedule | Any |
| `ChatController` | chat, history, conversations | Any |
| `SemestersController` | CRUD | Admin |
| `StructureControllers` | colleges, departments, batches, groups | Admin |
| `AcademicYearsController` | CRUD | Admin |
| `FileController` | upload, download (signed URL) | Any |
| `StudentFilesController` | upload, list, download | Student/Admin |
| `DashboardController` | admin, student, doctor dashboards | Role-based |
| `AuditLogsController` | list, filter | SuperAdmin |
| `AiController` | internal AI service proxy | Internal |
| `AiToolsController` | AI-specific action endpoints | AI/Doctor |
| `DevController` | debug/dev utilities | Dev only |

---

## Service Layer — All 33 Services

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IAuthService` | `AuthService` | Login, refresh, password |
| `IStudentService` | `StudentService` | Student CRUD + bulk |
| `IDoctorService` | `DoctorService` | Doctor CRUD |
| `IAdminService` | `AdminService` | Admin management |
| `IEnrollmentService` | `EnrollmentService` | Enrollment logic + auto-enroll |
| `ISubjectOfferingService` | `SubjectOfferingService` | Offering management |
| `IGradeService` | `GradeService` | Grade calculation |
| `IRegulationService` | `RegulationService` | Regulation CRUD + roadmap |
| `IComplaintService` | `ComplaintService` | Complaint workflow |
| `INotificationService` | `NotificationService` | Notification + real-time |
| `IExamService` | `ExamService` | Exam CRUD + AI generation |
| `IAttendanceService` | `AttendanceService` | Sessions + check-in |
| `IChatService` | `ChatService` | AI chat + history |
| `IMaterialService` | `MaterialService` | Upload + retrieval |
| `IScheduleService` | `ScheduleService` | Schedule management |
| `ISemesterService` | `SemesterService` | Semester CRUD |
| `IStructureServices` | (multiple) | University/College/Dept/Batch/Group |
| `IAcademicYearService` | `AcademicYearService` | Academic year |
| `IAuditService` | `AuditService` | Audit log writing |
| `IFileService` | `FileService` | File metadata |
| `IStorageService` | `R2StorageService` | Cloudflare R2 operations |
| `IUserContextService` | `UserContextService` | JWT claim extraction |
| `IIdentityProvisioningService` | `IdentityProvisioningService` | Create SystemUser accounts |
| `ISystemUserResolver` | `SystemUserResolver` | Resolve user from JWT |
| `ISmartStringGenerator` | `SmartStringGenerator` | Auto-generate codes/emails |
| `IAiService` | `AiService` | HTTP client to FastAPI |
| `IExcelService` | `ExcelService` | Excel parsing |
| `IExcelImportService` | `ExcelImportService` | Import from Excel |
| `IStudentFileService` | `StudentFileService` | Student personal files |
| `IEnrollmentUploadService` | `EnrollmentUploadService` | Bulk enrollment |
| `IRealtimeNotifier` | `SignalRNotifier` | WebSocket push |
| `IGenericRepository<T>` | `GenericRepository<T>` | Generic CRUD |

---

## Background Jobs — Complete List

| Job | Schedule | Purpose |
|-----|---------|---------|
| `AcademicRiskJob` | Daily midnight | Detect low-GPA students → notify |
| `ExamReminderJob` | Every 30 min | Send exam reminders 24h + 2h before |
| `ComplaintIntelligenceJob` (daily) | Daily | Daily complaint analysis report |
| `ComplaintIntelligenceJob` (weekly) | Weekly | Weekly trend analysis |
| `ComplaintIntelligenceJob` (monthly) | Monthly | Monthly department report |
| `BulkUploadJob` | On-demand | Process bulk student upload Excel |

---

## Middleware Chain

```csharp
// Applied in order:
app.UseMiddleware<ExceptionMiddleware>();     // Global exception handler
app.UseSwagger();                            // API documentation
app.UseSwaggerUI();                          // Swagger UI
app.UseSerilogRequestLogging();             // HTTP request logging
app.UseCors();                              // CORS headers
app.UseHttpsRedirection();                  // Force HTTPS
app.UseAuthentication();                    // JWT validation
app.UseAuthorization();                     // Role checking
app.UseRateLimiter();                       // Request throttling
app.UseHangfireDashboard("/hangfire");      // Job dashboard
app.MapControllers();                       // Route to controllers
app.MapHub<NotificationHub>("/hubs/notifications"); // SignalR
app.MapHealthChecks("/health");             // Health endpoint
```

---

## Response Wrapping

All API responses are wrapped in a standard envelope by `ResponseWrapperFilter`:

```json
// Success response (200):
{
  "success": true,
  "data": { ... },
  "message": null
}

// Error response (400/404/500):
{
  "success": false,
  "data": null,
  "message": "Specific error description",
  "correlationId": "abc-123"
}
```

This gives frontend a consistent shape to handle — always check `success` first.

---

## GradeService — Core Algorithm

```csharp
FinalScore = 
  (midtermScore / MidtermMaxScore) * MidtermWeight * 100 +
  (courseworkScore / CourseworkMaxScore) * CourseworkWeight * 100 +
  (finalExamScore / FinalExamMaxScore) * FinalExamWeight * 100 +
  (platformScore / PlatformMaxScore) * PlatformWeight * 100;

// Default weights: Midterm 20%, Coursework 20%, Final 50%, Platform 10%
// Weights MUST sum to 1.0 — validated before calculation
// GPA computed per credit hour weight for accuracy
```

**GPA Formula:**
```
GPA = Σ(GradePoints_i × CreditHours_i) / Σ(CreditHours_i)
```
