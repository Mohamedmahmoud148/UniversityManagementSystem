using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Exceptions;

namespace UniversityManagementSystem.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<University> Universities { get; set; } = null!;
        public DbSet<College> Colleges { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Batch> Batches { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<SystemUser> SystemUsers { get; set; } = null!;
        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Doctor> Doctors { get; set; } = null!;
        public DbSet<TeachingAssistant> TeachingAssistants { get; set; } = null!;
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<SubjectDoctor> SubjectDoctors { get; set; } = null!;
        public DbSet<SubjectAssistant> SubjectAssistants { get; set; } = null!;
        public DbSet<Enrollment> Enrollments { get; set; } = null!;
        public DbSet<SubjectOffering> SubjectOfferings { get; set; } = null!;
        public DbSet<Conversation> Conversations { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<AiMemory> AiMemories { get; set; } = null!;
        public DbSet<UploadedFile> UploadedFiles { get; set; } = null!;
        public DbSet<AttendanceSession> AttendanceSessions { get; set; } = null!;
        public DbSet<StudentAttendance> StudentAttendances { get; set; } = null!;
        public DbSet<AppNotification> AppNotifications { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<Regulation> Regulations { get; set; } = null!;
        public DbSet<RegulationSubject> RegulationSubjects { get; set; } = null!;
        public DbSet<Admin> Admins { get; set; } = null!;
        public DbSet<Exam> Exams { get; set; } = null!;
        public DbSet<ExamQuestion> ExamQuestions { get; set; } = null!;
        public DbSet<ExamSubmission> ExamSubmissions { get; set; } = null!;
        public DbSet<StudentExamVariant> StudentExamVariants { get; set; } = null!;
        public DbSet<StudentGrade> StudentGrades { get; set; } = null!;
        public DbSet<Material> Materials { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AiActionLog> AiActionLogs { get; set; } = null!;
        public DbSet<StudentFile> StudentFiles { get; set; } = null!;
        public DbSet<EnrollmentUpload> EnrollmentUploads { get; set; } = null!;
        public DbSet<Complaint> Complaints { get; set; } = null!;
        public DbSet<ComplaintAnalysis> ComplaintAnalyses { get; set; } = null!;
        public DbSet<ComplaintCluster> ComplaintClusters { get; set; } = null!;
        public DbSet<AcademicYearDepartment> AcademicYearDepartments { get; set; } = null!;
        public DbSet<ScheduleEntry> ScheduleEntries { get; set; } = null!;
        public DbSet<AcademicYear> AcademicYears { get; set; } = null!;
        public DbSet<Semester> Semesters { get; set; } = null!;
        public DbSet<SubjectPrerequisite> SubjectPrerequisites { get; set; } = null!;
        public DbSet<AcademicPolicy> AcademicPolicies { get; set; } = null!;
        public DbSet<StudentAcademicStatus> StudentAcademicStatuses { get; set; } = null!;
        public DbSet<SubjectOfferingWaitlist> SubjectOfferingWaitlists { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // --------------------------------------------------------
            // Global ULID → string convention (EF Core 6+ design-time safe).
            // Registers typed converters so the migration tooling can discover
            // the mapping without running application code.
            // --------------------------------------------------------
            configurationBuilder.Properties<Ulid>()
                .HaveConversion<UlidToStringConverter>()
                .HaveMaxLength(26);

            configurationBuilder.Properties<Ulid?>()
                .HaveConversion<NullableUlidToStringConverter>()
                .HaveMaxLength(26);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Global soft-delete query filter for all BaseEntity types
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "x");
                    var property2 = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
                    var nullConstant = System.Linq.Expressions.Expression.Constant(null);
                    var equality = System.Linq.Expressions.Expression.Equal(property2, nullConstant);
                    var lambda = System.Linq.Expressions.Expression.Lambda(equality, parameter);
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // Code Column Unique Indexes
            // Ensures fast O(log n) lookup for all code-based API routes and
            // enforces uniqueness at the DB level.
            // ─────────────────────────────────────────────────────────────────
            modelBuilder.Entity<University>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Universities_Code");
            modelBuilder.Entity<College>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Colleges_Code");
            modelBuilder.Entity<Department>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Departments_Code");
            modelBuilder.Entity<Batch>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Batches_Code");
            modelBuilder.Entity<Group>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Groups_Code");
            modelBuilder.Entity<Subject>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Subjects_Code");
            modelBuilder.Entity<Student>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Students_Code");
            modelBuilder.Entity<Doctor>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Doctors_Code");
            modelBuilder.Entity<TeachingAssistant>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_TeachingAssistants_Code");
            modelBuilder.Entity<SystemUser>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_SystemUsers_Code");
            modelBuilder.Entity<Regulation>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Regulations_Code");
            modelBuilder.Entity<Exam>()
                .HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Exams_Code");

            // --------------------------------------------------------
            // Configure RefreshToken
            // --------------------------------------------------------
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => new { rt.UserId, rt.Token })
                .HasDatabaseName("IX_RefreshTokens_UserId_Token");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .HasDatabaseName("IX_RefreshTokens_Token");

            // --------------------------------------------------------
            // Many-to-Many: SubjectDoctor (composite PK)
            // --------------------------------------------------------
            modelBuilder.Entity<SubjectDoctor>()
                .HasKey(sd => new { sd.SubjectId, sd.DoctorId });

            modelBuilder.Entity<SubjectDoctor>()
                .HasOne(sd => sd.Subject)
                .WithMany(s => s.SubjectDoctors)
                .HasForeignKey(sd => sd.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectDoctor>()
                .HasOne(sd => sd.Doctor)
                .WithMany(d => d.SubjectDoctors)
                .HasForeignKey(sd => sd.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectDoctor>()
                .HasQueryFilter(sd => sd.Subject.DeletedAt == null && sd.Doctor.DeletedAt == null);

            // --------------------------------------------------------
            // Many-to-Many: SubjectAssistant (composite PK)
            // --------------------------------------------------------
            modelBuilder.Entity<SubjectAssistant>()
                .HasKey(sa => new { sa.SubjectId, sa.TeachingAssistantId });

            modelBuilder.Entity<SubjectAssistant>()
                .HasOne(sa => sa.Subject)
                .WithMany(s => s.SubjectAssistants)
                .HasForeignKey(sa => sa.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectAssistant>()
                .HasOne(sa => sa.TeachingAssistant)
                .WithMany(ta => ta.SubjectAssistants)
                .HasForeignKey(sa => sa.TeachingAssistantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectAssistant>()
                .HasQueryFilter(sa => sa.Subject.DeletedAt == null && sa.TeachingAssistant.DeletedAt == null);

            // --------------------------------------------------------
            // Academic Hierarchy
            // --------------------------------------------------------

            // University → College
            modelBuilder.Entity<University>()
                 .HasMany(u => u.Colleges)
                 .WithOne(c => c.University)
                 .HasForeignKey(c => c.UniversityId)
                 .OnDelete(DeleteBehavior.Restrict);

            // College → Department
            modelBuilder.Entity<College>()
                .HasMany(c => c.Departments)
                .WithOne(d => d.College)
                .HasForeignKey(d => d.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department → Batch
            modelBuilder.Entity<Department>()
                .HasMany(d => d.Batches)
                .WithOne(b => b.Department)
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Batch → Group
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Batch)
                .WithMany(b => b.Groups)
                .HasForeignKey(g => g.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Batch → Student
            modelBuilder.Entity<Batch>()
                .HasMany(b => b.Students)
                .WithOne(s => s.Batch)
                .HasForeignKey(s => s.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Batch → Regulation
            modelBuilder.Entity<Batch>()
                .HasOne(b => b.Regulation)
                .WithMany()
                .HasForeignKey(b => b.RegulationId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // Student Hierarchy
            // --------------------------------------------------------
            modelBuilder.Entity<Student>()
                 .HasOne(s => s.University)
                 .WithMany()
                 .HasForeignKey(s => s.UniversityId)
                 .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                 .HasOne(s => s.College)
                 .WithMany()
                 .HasForeignKey(s => s.CollegeId)
                 .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                 .HasOne(s => s.Department)
                 .WithMany()
                 .HasForeignKey(s => s.DepartmentId)
                 .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Group)
                .WithMany(g => g.Students)
                .HasForeignKey(s => s.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Regulation)
                .WithMany()
                .HasForeignKey(s => s.RegulationId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // SystemUser Indexes & Config
            // --------------------------------------------------------
            modelBuilder.Entity<SystemUser>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<SystemUser>()
                .Property(u => u.Email)
                .HasColumnType("citext");

            modelBuilder.Entity<SystemUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<SystemUser>()
                .Property(u => u.UniversityEmail)
                .HasColumnType("citext");

            modelBuilder.Entity<SystemUser>()
                .HasIndex(u => u.UniversityEmail)
                .IsUnique();

            modelBuilder.Entity<SystemUser>()
                .HasIndex(u => u.NationalId)
                .IsUnique();

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.UniversityStudentId)
                .IsUnique();

            modelBuilder.Entity<Doctor>()
                .HasIndex(d => d.UniversityStaffId)
                .IsUnique();

            modelBuilder.Entity<TeachingAssistant>()
                .HasIndex(ta => ta.UniversityStaffId)
                .IsUnique();

            // --------------------------------------------------------
            // AcademicYear & Semester
            // --------------------------------------------------------

            // FK: AcademicYear → College
            modelBuilder.Entity<AcademicYear>()
                .HasOne(y => y.College)
                .WithMany(c => c.AcademicYears)
                .HasForeignKey(y => y.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique: (CollegeId, Order) — no two years in the same college share the same order
            modelBuilder.Entity<AcademicYear>()
                .HasIndex(y => new { y.CollegeId, y.Order })
                .IsUnique()
                .HasDatabaseName("IX_AcademicYears_College_Order");

            // Name unique per college — two different colleges can both have "First Year"
            modelBuilder.Entity<AcademicYear>()
                .HasIndex(y => new { y.CollegeId, y.Name })
                .IsUnique()
                .HasDatabaseName("IX_AcademicYears_College_Name");

            modelBuilder.Entity<Semester>()
                .HasIndex(s => new { s.Name, s.AcademicYearId })
                .IsUnique();

            // --------------------------------------------------------
            // AcademicYearDepartment (junction / config table)
            // --------------------------------------------------------

            // Explicit PK — entity does NOT inherit BaseEntity so EF needs it declared
            modelBuilder.Entity<AcademicYearDepartment>()
                .HasKey(m => m.Id);

            // No soft-delete query filter — this is a hard-delete config table
            // (The global BaseEntity filter loop above only targets BaseEntity subtypes,
            //  so this entity is already excluded now that it no longer inherits BaseEntity.)

            // Unique mapping: each department can only be assigned once per year
            modelBuilder.Entity<AcademicYearDepartment>()
                .HasIndex(m => new { m.AcademicYearId, m.DepartmentId })
                .IsUnique()
                .HasDatabaseName("IX_AcademicYearDepartments_Year_Dept");

            // FK: mapping → AcademicYear (cascade: delete year → delete all its mappings)
            modelBuilder.Entity<AcademicYearDepartment>()
                .HasOne(m => m.AcademicYear)
                .WithMany(y => y.AcademicYearDepartments)
                .HasForeignKey(m => m.AcademicYearId)
                .OnDelete(DeleteBehavior.Cascade);

            // FK: mapping → Department (restrict: don't auto-delete dept if mapping exists)
            modelBuilder.Entity<AcademicYearDepartment>()
                .HasOne(m => m.Department)
                .WithMany()
                .HasForeignKey(m => m.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // SubjectOffering
            // --------------------------------------------------------
            modelBuilder.Entity<SubjectOffering>()
                .HasIndex(so => new { so.SubjectId, so.SemesterId })
                .IsUnique();

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Subject)
                .WithMany()
                .HasForeignKey(so => so.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Semester)
                .WithMany()
                .HasForeignKey(so => so.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Doctor)
                .WithMany()
                .HasForeignKey(so => so.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectOffering>()
               .HasOne(so => so.Department)
               .WithMany()
               .HasForeignKey(so => so.DepartmentId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Batch)
                .WithMany()
                .HasForeignKey(so => so.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Group)
                .WithMany(g => g.SubjectOfferings)
                .HasForeignKey(so => so.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // Enrollment
            // --------------------------------------------------------
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.SubjectOfferingId })
                .IsUnique();

            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => e.SubjectOfferingId)
                .HasDatabaseName("IX_Enrollments_SubjectOfferingId");

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.SubjectOffering)
                .WithMany()
                .HasForeignKey(e => e.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // Department → Doctor / TA
            // --------------------------------------------------------
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.Department)
                .WithMany(dp => dp.Doctors)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeachingAssistant>()
                .HasOne(ta => ta.Department)
                .WithMany(dp => dp.TeachingAssistants)
                .HasForeignKey(ta => ta.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // SystemUser → Role entities (One-to-One)
            // --------------------------------------------------------
            modelBuilder.Entity<Admin>()
                .HasOne(a => a.SystemUser)
                .WithOne()
                .HasForeignKey<Admin>(a => a.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.SystemUserId)
                .IsUnique();

            modelBuilder.Entity<Student>()
                .HasOne(s => s.SystemUser)
                .WithOne()
                .HasForeignKey<Student>(s => s.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.SystemUserId)
                .IsUnique();

            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.SystemUser)
                .WithOne()
                .HasForeignKey<Doctor>(d => d.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Doctor>()
                .HasIndex(d => d.SystemUserId)
                .IsUnique();

            modelBuilder.Entity<TeachingAssistant>()
               .HasOne(t => t.SystemUser)
               .WithOne()
               .HasForeignKey<TeachingAssistant>(t => t.SystemUserId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeachingAssistant>()
                .HasIndex(t => t.SystemUserId)
                .IsUnique();

            // --------------------------------------------------------
            // Chat System
            // --------------------------------------------------------
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.ConversationId)
                .HasDatabaseName("IX_ChatMessages_ConversationId");

            modelBuilder.Entity<AiMemory>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AiMemory>()
                .HasIndex(m => m.UserId)
                .HasDatabaseName("IX_AiMemories_UserId");

            // --------------------------------------------------------
            // File Upload
            // --------------------------------------------------------
            modelBuilder.Entity<UploadedFile>()
                .HasOne(f => f.UploadedBy)
                .WithMany()
                .HasForeignKey(f => f.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UploadedFile>()
                .HasOne(f => f.Subject)
                .WithMany()
                .HasForeignKey(f => f.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UploadedFile>()
                .HasOne(f => f.SubjectOffering)
                .WithMany()
                .HasForeignKey(f => f.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UploadedFile>()
                .HasOne(f => f.UploadedByDoctor)
                .WithMany()
                .HasForeignKey(f => f.UploadedByDoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UploadedFile>()
                .HasIndex(f => f.SubjectOfferingId);

            modelBuilder.Entity<UploadedFile>()
                .HasIndex(f => f.UploadedByDoctorId);

            // --------------------------------------------------------
            // Attendance
            // --------------------------------------------------------
            modelBuilder.Entity<AttendanceSession>()
                .HasOne(s => s.Subject)
                .WithMany()
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceSession>()
                .HasOne(s => s.Doctor)
                .WithMany()
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceSession>()
                .HasOne(s => s.TeachingAssistant)
                .WithMany()
                .HasForeignKey(s => s.TeachingAssistantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentAttendance>()
                .HasOne(sa => sa.AttendanceSession)
                .WithMany(s => s.Attendances)
                .HasForeignKey(sa => sa.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentAttendance>()
                .HasOne(sa => sa.Student)
                .WithMany()
                .HasForeignKey(sa => sa.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentAttendance>()
                .HasIndex(sa => new { sa.AttendanceSessionId, sa.StudentId })
                .IsUnique();

            // --------------------------------------------------------
            // Notifications
            // --------------------------------------------------------
            modelBuilder.Entity<AppNotification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // --------------------------------------------------------
            // Exam
            // --------------------------------------------------------
            modelBuilder.Entity<Exam>()
                .Property(e => e.Mode)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Exam>()
                .Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Exam>()
                .HasOne(e => e.SubjectOffering)
                .WithMany()
                .HasForeignKey(e => e.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Exam>()
                .HasOne(e => e.CreatedByDoctor)
                .WithMany()
                .HasForeignKey(e => e.CreatedByDoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Exam>()
                .HasIndex(e => e.SubjectOfferingId);

            modelBuilder.Entity<Exam>()
                .HasIndex(e => e.CreatedByDoctorId);

            // ExamQuestion
            modelBuilder.Entity<ExamQuestion>()
                .HasOne(q => q.Exam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            // ExamSubmission
            modelBuilder.Entity<ExamSubmission>()
                .HasOne(s => s.Exam)
                .WithMany(e => e.Submissions)
                .HasForeignKey(s => s.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSubmission>()
                .HasOne(s => s.Student)
                .WithMany()
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSubmission>()
                .HasIndex(s => new { s.ExamId, s.StudentId })
                .IsUnique();

            modelBuilder.Entity<ExamSubmission>()
                .HasIndex(s => s.StudentId)
                .HasDatabaseName("IX_ExamSubmissions_StudentId");

            // StudentExamVariant
            modelBuilder.Entity<StudentExamVariant>()
                .HasOne(v => v.Exam)
                .WithMany(e => e.StudentVariants)
                .HasForeignKey(v => v.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentExamVariant>()
                .HasOne(v => v.Student)
                .WithMany()
                .HasForeignKey(v => v.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentExamVariant>()
                .HasIndex(v => new { v.ExamId, v.StudentId })
                .IsUnique()
                .HasDatabaseName("IX_StudentExamVariants_ExamId_StudentId");

            // --------------------------------------------------------
            // StudentGrade
            // --------------------------------------------------------
            modelBuilder.Entity<StudentGrade>()
                .HasOne(g => g.Student)
                .WithMany()
                .HasForeignKey(g => g.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentGrade>()
                .HasOne(g => g.SubjectOffering)
                .WithMany()
                .HasForeignKey(g => g.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentGrade>()
                .HasIndex(g => new { g.StudentId, g.SubjectOfferingId })
                .IsUnique();

            // --------------------------------------------------------
            // Materials
            // --------------------------------------------------------
            modelBuilder.Entity<Material>()
                .HasOne(m => m.SubjectOffering)
                .WithMany()
                .HasForeignKey(m => m.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Material>()
                .HasOne(m => m.UploadedByDoctor)
                .WithMany()
                .HasForeignKey(m => m.UploadedByDoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Material>()
                .HasIndex(m => m.SubjectOfferingId);

            // --------------------------------------------------------
            // AiActionLog
            // --------------------------------------------------------
            modelBuilder.Entity<AiActionLog>(entity =>
            {
                entity.ToTable("AiActionLogs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ToolName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ParametersJson).HasColumnType("text");
                entity.Property(e => e.Success).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Timestamp);
            });

            // --------------------------------------------------------
            // AuditLog (standalone, not BaseEntity)
            // --------------------------------------------------------
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Ulid → string conversion and MaxLength(26) are applied globally via ConfigureConventions.
            });

            // --------------------------------------------------------
            // Complaint
            // --------------------------------------------------------
            modelBuilder.Entity<Complaint>(entity =>
            {
                // FK → Student's SystemUser
                entity.HasOne(c => c.Student)
                      .WithMany()
                      .HasForeignKey(c => c.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.CreatedAt)
                      .HasDatabaseName("IX_Complaints_CreatedAt");

                entity.HasIndex(c => c.TargetType)
                      .HasDatabaseName("IX_Complaints_TargetType");

                entity.HasIndex(c => c.TargetId)
                      .HasDatabaseName("IX_Complaints_TargetId");

                entity.HasIndex(c => c.StudentId)
                      .HasDatabaseName("IX_Complaints_StudentId");

                entity.Property(c => c.Title).HasMaxLength(200).IsRequired();
                entity.Property(c => c.TargetType).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Status).HasMaxLength(30).IsRequired();
                entity.Property(c => c.Priority).HasMaxLength(30).IsRequired();
                entity.Property(c => c.Message).HasMaxLength(2000).IsRequired();
                entity.Property(c => c.TargetId).HasMaxLength(100);
            });

            // --------------------------------------------------------
            // ComplaintAnalysis
            // --------------------------------------------------------
            modelBuilder.Entity<ComplaintAnalysis>(entity =>
            {
                entity.HasOne(ca => ca.Complaint)
                      .WithOne(c => c.Analysis)
                      .HasForeignKey<ComplaintAnalysis>(ca => ca.ComplaintId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(ca => ca.DuplicateGroupId)
                      .HasDatabaseName("IX_ComplaintAnalyses_DuplicateGroupId");
                
                entity.Property(ca => ca.Category).HasMaxLength(100);
                entity.Property(ca => ca.Severity).HasMaxLength(50);
            });

            // --------------------------------------------------------
            // ComplaintCluster
            // --------------------------------------------------------
            modelBuilder.Entity<ComplaintCluster>(entity =>
            {
                entity.HasIndex(cc => cc.TargetType)
                      .HasDatabaseName("IX_ComplaintClusters_TargetType");

                entity.HasIndex(cc => cc.TargetId)
                      .HasDatabaseName("IX_ComplaintClusters_TargetId");

                entity.Property(cc => cc.TargetType).HasMaxLength(50);
                entity.Property(cc => cc.TargetId).HasMaxLength(100);
                entity.Property(cc => cc.Topic).HasMaxLength(255);
            });

            // --------------------------------------------------------
            // Regulation
            // --------------------------------------------------------
            modelBuilder.Entity<Regulation>()
                .HasOne(r => r.Department)
                .WithMany()
                .HasForeignKey(r => r.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RegulationSubject>()
                .HasOne(rs => rs.Regulation)
                .WithMany(r => r.RegulationSubjects)
                .HasForeignKey(rs => rs.RegulationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RegulationSubject>()
                .HasOne(rs => rs.Subject)
                .WithMany()
                .HasForeignKey(rs => rs.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RegulationSubject>()
                .HasIndex(rs => new { rs.RegulationId, rs.SubjectId })
                .IsUnique();

            // --------------------------------------------------------
            // ScheduleEntry
            // --------------------------------------------------------
            modelBuilder.Entity<ScheduleEntry>()
                .HasOne(se => se.SubjectOffering)
                .WithMany()
                .HasForeignKey(se => se.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScheduleEntry>()
                .HasOne(se => se.Batch)
                .WithMany()
                .HasForeignKey(se => se.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScheduleEntry>()
                .HasOne(se => se.Group)
                .WithMany()
                .HasForeignKey(se => se.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Fast lookup: all slots for a batch on a given day
            modelBuilder.Entity<ScheduleEntry>()
                .HasIndex(se => new { se.BatchId, se.DayOfWeek })
                .HasDatabaseName("IX_ScheduleEntries_Batch_Day");

            modelBuilder.Entity<ScheduleEntry>()
                .HasIndex(se => se.SubjectOfferingId)
                .HasDatabaseName("IX_ScheduleEntries_OfferingId");

            modelBuilder.Entity<ScheduleEntry>()
                .Property(se => se.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<ScheduleEntry>()
                .Property(se => se.WeekType)
                .HasConversion<string>()
                .HasMaxLength(10);

            // --------------------------------------------------------
            // SubjectPrerequisite
            // --------------------------------------------------------
            modelBuilder.Entity<SubjectPrerequisite>()
                .HasOne(sp => sp.Subject)
                .WithMany()
                .HasForeignKey(sp => sp.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubjectPrerequisite>()
                .HasOne(sp => sp.PrerequisiteSubject)
                .WithMany()
                .HasForeignKey(sp => sp.PrerequisiteSubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubjectPrerequisite>()
                .HasIndex(sp => new { sp.SubjectId, sp.PrerequisiteSubjectId })
                .IsUnique()
                .HasDatabaseName("IX_SubjectPrerequisites_Subject_Prereq");

            modelBuilder.Entity<SubjectPrerequisite>()
                .HasIndex(sp => sp.SubjectId)
                .HasDatabaseName("IX_SubjectPrerequisites_SubjectId");

            // --------------------------------------------------------
            // AcademicPolicy
            // --------------------------------------------------------
            modelBuilder.Entity<AcademicPolicy>()
                .HasOne(ap => ap.Department)
                .WithMany()
                .HasForeignKey(ap => ap.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // --------------------------------------------------------
            // StudentAcademicStatus (1-to-1 with Student)
            // --------------------------------------------------------
            modelBuilder.Entity<StudentAcademicStatus>()
                .HasOne(s => s.Student)
                .WithOne()
                .HasForeignKey<StudentAcademicStatus>(s => s.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentAcademicStatus>()
                .HasIndex(s => s.StudentId)
                .IsUnique()
                .HasDatabaseName("IX_StudentAcademicStatuses_StudentId");

            // --------------------------------------------------------
            // SubjectOfferingWaitlist
            // --------------------------------------------------------
            modelBuilder.Entity<SubjectOfferingWaitlist>()
                .HasOne(w => w.Student)
                .WithMany()
                .HasForeignKey(w => w.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubjectOfferingWaitlist>()
                .HasOne(w => w.Offering)
                .WithMany()
                .HasForeignKey(w => w.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubjectOfferingWaitlist>()
                .HasIndex(w => new { w.StudentId, w.SubjectOfferingId })
                .IsUnique()
                .HasDatabaseName("IX_SubjectOfferingWaitlist_Student_Offering");

            modelBuilder.Entity<SubjectOfferingWaitlist>()
                .HasIndex(w => w.SubjectOfferingId)
                .HasDatabaseName("IX_SubjectOfferingWaitlist_OfferingId");
        }

        // ── Soft Delete Intercept ────────────────────────────────────────────
        // Converts any EntityState.Deleted for a BaseEntity into a soft delete
        // by setting DeletedAt = UtcNow and flipping the state to Modified.
        // This means no service code ever needs to know about soft deletes.
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>()
                         .Where(e => e.State == EntityState.Deleted))
            {
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        // ── Cascade Soft Delete ──────────────────────────────────────────────
        // Recursively soft-deletes all children of the given entity before
        // soft-deleting the entity itself. Bypasses query filters so that
        // already-soft-deleted children are not double-processed.
        public async Task CascadeDeleteAsync(BaseEntity entity)
        {
            var now = DateTime.UtcNow;
            await CascadeChildrenAsync(entity, now);
            entity.DeletedAt = now;
            Entry(entity).State = EntityState.Modified;
            await SaveChangesAsync();
        }

        private async Task CascadeChildrenAsync(BaseEntity entity, DateTime now)
        {
            switch (entity)
            {
                case University u:
                {
                    var children = await Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id && c.DeletedAt == null).ToListAsync();
                    foreach (var child in children) { await CascadeChildrenAsync(child, now); child.DeletedAt = now; }
                    break;
                }
                case College c:
                {
                    var children = await Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == c.Id && d.DeletedAt == null).ToListAsync();
                    foreach (var child in children) { await CascadeChildrenAsync(child, now); child.DeletedAt = now; }
                    break;
                }
                case Department dept:
                {
                    // Cascade into Batches only — Doctors and TAs are NOT deleted with the department
                    var batches = await Batches.IgnoreQueryFilters()
                        .Where(b => b.DepartmentId == dept.Id && b.DeletedAt == null).ToListAsync();
                    foreach (var b in batches) { await CascadeChildrenAsync(b, now); b.DeletedAt = now; }
                    break;
                }
                case Batch batch:
                {
                    // Cascade into Groups and SubjectOfferings — Students are NOT deleted with the batch
                    var groups = await Groups.IgnoreQueryFilters()
                        .Where(g => g.BatchId == batch.Id && g.DeletedAt == null).ToListAsync();
                    foreach (var g in groups) { await CascadeChildrenAsync(g, now); g.DeletedAt = now; }

                    var offerings = await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.DeletedAt == null).ToListAsync();
                    foreach (var so in offerings) { await CascadeChildrenAsync(so, now); so.DeletedAt = now; }
                    break;
                }
                case Group group:
                {
                    // Cascade into SubjectOfferings only — Students are NOT deleted with the group
                    var offerings = await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id && so.DeletedAt == null).ToListAsync();
                    foreach (var so in offerings) { await CascadeChildrenAsync(so, now); so.DeletedAt = now; }
                    break;
                }
                case SubjectOffering offering:
                {
                    var enrollments = await Enrollments.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id && e.DeletedAt == null).ToListAsync();
                    foreach (var e in enrollments) e.DeletedAt = now;

                    var materials = await Materials.IgnoreQueryFilters()
                        .Where(m => m.SubjectOfferingId == offering.Id && m.DeletedAt == null).ToListAsync();
                    foreach (var m in materials) m.DeletedAt = now;

                    var grades = await StudentGrades.IgnoreQueryFilters()
                        .Where(g => g.SubjectOfferingId == offering.Id && g.DeletedAt == null).ToListAsync();
                    foreach (var g in grades) g.DeletedAt = now;

                    var files = await UploadedFiles.IgnoreQueryFilters()
                        .Where(f => f.SubjectOfferingId == offering.Id && f.DeletedAt == null).ToListAsync();
                    foreach (var f in files) f.DeletedAt = now;

                    var exams = await Exams.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id && e.DeletedAt == null).ToListAsync();
                    foreach (var e in exams) { await CascadeChildrenAsync(e, now); e.DeletedAt = now; }
                    break;
                }
                case Exam exam:
                {
                    var submissions = await ExamSubmissions.IgnoreQueryFilters()
                        .Where(s => s.ExamId == exam.Id && s.DeletedAt == null).ToListAsync();
                    foreach (var s in submissions) s.DeletedAt = now;
                    break;
                }
            }
        }

        // ── Hard Cascade Delete ──────────────────────────────────────────────
        // Permanently removes the entity and all its academic children from the DB.
        // BLOCKS with DomainException if any students, doctors, or TAs are still
        // linked — they must be reassigned first.
        // Uses ExecuteDeleteAsync which bypasses EF tracking and fires real SQL
        // DELETE — completely independent of the soft-delete SaveChangesAsync hook.
        public async Task HardCascadeDeleteAsync(BaseEntity entity)
        {
            await EnsureNoLinkedUsersAsync(entity);
            await HardCascadeChildrenAsync(entity);

            switch (entity)
            {
                case University u:
                    await Universities.IgnoreQueryFilters().Where(x => x.Id == u.Id).ExecuteDeleteAsync();
                    break;
                case College c:
                    await Colleges.IgnoreQueryFilters().Where(x => x.Id == c.Id).ExecuteDeleteAsync();
                    break;
                case Department d:
                    await Departments.IgnoreQueryFilters().Where(x => x.Id == d.Id).ExecuteDeleteAsync();
                    break;
                case Batch b:
                    await Batches.IgnoreQueryFilters().Where(x => x.Id == b.Id).ExecuteDeleteAsync();
                    break;
                case Group g:
                    await Groups.IgnoreQueryFilters().Where(x => x.Id == g.Id).ExecuteDeleteAsync();
                    break;
                case SubjectOffering so:
                    await SubjectOfferings.IgnoreQueryFilters().Where(x => x.Id == so.Id).ExecuteDeleteAsync();
                    break;
            }
        }

        // Throws DomainException if students/doctors/TAs reference this entity.
        // Non-nullable FKs on Student mean we cannot null them out — user must
        // reassign them before the structure can be deleted.
        private async Task EnsureNoLinkedUsersAsync(BaseEntity entity)
        {
            switch (entity)
            {
                case Group group:
                {
                    var count = await Students.IgnoreQueryFilters()
                        .CountAsync(s => s.GroupId == group.Id);
                    if (count > 0)
                        throw new DomainException(
                            $"Cannot delete Group: {count} student(s) are assigned to it. Reassign them first.");
                    break;
                }
                case Batch batch:
                {
                    var count = await Students.IgnoreQueryFilters()
                        .CountAsync(s => s.BatchId == batch.Id);
                    if (count > 0)
                        throw new DomainException(
                            $"Cannot delete Batch: {count} student(s) belong to it. Reassign them first.");
                    break;
                }
                case Department dept:
                {
                    var students = await Students.IgnoreQueryFilters().CountAsync(s => s.DepartmentId == dept.Id);
                    var doctors  = await Doctors.IgnoreQueryFilters().CountAsync(d => d.DepartmentId == dept.Id);
                    var tas      = await TeachingAssistants.IgnoreQueryFilters().CountAsync(t => t.DepartmentId == dept.Id);
                    if (students + doctors + tas > 0)
                        throw new DomainException(
                            $"Cannot delete Department: {students} student(s), {doctors} doctor(s), {tas} TA(s) are linked. Reassign them first.");
                    break;
                }
                case College college:
                {
                    var depts = await Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == college.Id).ToListAsync();
                    foreach (var d in depts) await EnsureNoLinkedUsersAsync(d);
                    break;
                }
                case University u:
                {
                    var colleges = await Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id).ToListAsync();
                    foreach (var c in colleges) await EnsureNoLinkedUsersAsync(c);
                    break;
                }
            }
        }

        // Deletes all academic children bottom-up before the parent is removed.
        // Order matters — children with Restrict FKs must go before their parents.
        private async Task HardCascadeChildrenAsync(BaseEntity entity)
        {
            switch (entity)
            {
                case University u:
                {
                    var colleges = await Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id).ToListAsync();
                    foreach (var c in colleges) await HardCascadeChildrenAsync(c);
                    await Colleges.IgnoreQueryFilters()
                        .Where(c => c.UniversityId == u.Id).ExecuteDeleteAsync();
                    break;
                }
                case College college:
                {
                    // Departments first — cascades Batches → Groups → SubjectOfferings
                    var depts = await Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == college.Id).ToListAsync();
                    foreach (var d in depts) await HardCascadeChildrenAsync(d);
                    await Departments.IgnoreQueryFilters()
                        .Where(d => d.CollegeId == college.Id).ExecuteDeleteAsync();

                    // SubjectOfferings are now gone, so Semesters can be deleted.
                    // AcademicYearDepartments cascade at DB level when AcademicYear is deleted.
                    var yearIds = await Set<AcademicYear>().IgnoreQueryFilters()
                        .Where(y => y.CollegeId == college.Id)
                        .Select(y => y.Id).ToListAsync();
                    if (yearIds.Count > 0)
                    {
                        await Set<Semester>().IgnoreQueryFilters()
                            .Where(s => yearIds.Contains(s.AcademicYearId)).ExecuteDeleteAsync();
                        await Set<AcademicYear>().IgnoreQueryFilters()
                            .Where(y => y.CollegeId == college.Id).ExecuteDeleteAsync();
                    }
                    break;
                }
                case Department dept:
                {
                    // Junction table (no soft-delete, no IgnoreQueryFilters needed)
                    await AcademicYearDepartments
                        .Where(m => m.DepartmentId == dept.Id).ExecuteDeleteAsync();

                    // Regulations: delete subjects first, then the regulation itself
                    var regIds = await Regulations.IgnoreQueryFilters()
                        .Where(r => r.DepartmentId == dept.Id).Select(r => r.Id).ToListAsync();
                    if (regIds.Count > 0)
                    {
                        await RegulationSubjects.IgnoreQueryFilters()
                            .Where(rs => regIds.Contains(rs.RegulationId)).ExecuteDeleteAsync();
                        await Regulations.IgnoreQueryFilters()
                            .Where(r => r.DepartmentId == dept.Id).ExecuteDeleteAsync();
                    }

                    var batches = await Batches.IgnoreQueryFilters()
                        .Where(b => b.DepartmentId == dept.Id).ToListAsync();
                    foreach (var b in batches) await HardCascadeChildrenAsync(b);
                    await Batches.IgnoreQueryFilters()
                        .Where(b => b.DepartmentId == dept.Id).ExecuteDeleteAsync();
                    break;
                }
                case Batch batch:
                {
                    var groups = await Groups.IgnoreQueryFilters()
                        .Where(g => g.BatchId == batch.Id).ToListAsync();
                    foreach (var g in groups) await HardCascadeChildrenAsync(g);
                    await Groups.IgnoreQueryFilters()
                        .Where(g => g.BatchId == batch.Id).ExecuteDeleteAsync();

                    // Batch-level offerings (GroupId IS NULL) — group-level ones
                    // were already deleted by the group cascade above.
                    var offerings = await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.GroupId == null).ToListAsync();
                    foreach (var so in offerings) await HardCascadeChildrenAsync(so);
                    await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.BatchId == batch.Id && so.GroupId == null).ExecuteDeleteAsync();
                    break;
                }
                case Group group:
                {
                    var offerings = await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id).ToListAsync();
                    foreach (var so in offerings) await HardCascadeChildrenAsync(so);
                    await SubjectOfferings.IgnoreQueryFilters()
                        .Where(so => so.GroupId == group.Id).ExecuteDeleteAsync();
                    break;
                }
                case SubjectOffering offering:
                {
                    // ExamSubmissions have Restrict FK → must delete before Exams
                    var examIds = await Exams.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id)
                        .Select(e => e.Id).ToListAsync();
                    if (examIds.Count > 0)
                    {
                        await ExamSubmissions.IgnoreQueryFilters()
                            .Where(s => examIds.Contains(s.ExamId)).ExecuteDeleteAsync();
                        // DB CASCADE removes ExamQuestions + StudentExamVariants automatically
                        await Exams.IgnoreQueryFilters()
                            .Where(e => e.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    }

                    await Enrollments.IgnoreQueryFilters()
                        .Where(e => e.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await Materials.IgnoreQueryFilters()
                        .Where(m => m.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await StudentGrades.IgnoreQueryFilters()
                        .Where(g => g.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    await UploadedFiles.IgnoreQueryFilters()
                        .Where(f => f.SubjectOfferingId == offering.Id).ExecuteDeleteAsync();
                    // DB CASCADE removes ScheduleEntries automatically when SubjectOffering is deleted
                    break;
                }
            }
        }
    }
}
