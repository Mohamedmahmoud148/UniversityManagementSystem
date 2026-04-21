using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Entities;

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
        public DbSet<Admin> Admins { get; set; } = null!;
        public DbSet<Exam> Exams { get; set; } = null!;
        public DbSet<ExamQuestion> ExamQuestions { get; set; } = null!;
        public DbSet<ExamSubmission> ExamSubmissions { get; set; } = null!;
        public DbSet<StudentGrade> StudentGrades { get; set; } = null!;
        public DbSet<Material> Materials { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AiActionLog> AiActionLogs { get; set; } = null!;
        public DbSet<StudentFile> StudentFiles { get; set; } = null!;
        public DbSet<EnrollmentUpload> EnrollmentUploads { get; set; } = null!;
        public DbSet<Complaint> Complaints { get; set; } = null!;
        public DbSet<AcademicYearDepartment> AcademicYearDepartments { get; set; } = null!;

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

            modelBuilder.Entity<AiMemory>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

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
                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // FK → SubjectOffering (optional)
                entity.HasOne(c => c.SubjectOffering)
                      .WithMany()
                      .HasForeignKey(c => c.SubjectOfferingId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Index: date-range queries — most common dashboard filter
                entity.HasIndex(c => c.CreatedAt)
                      .HasDatabaseName("IX_Complaints_CreatedAt");

                // Index: filter by offering (student complaint list for a course)
                entity.HasIndex(c => c.SubjectOfferingId)
                      .HasDatabaseName("IX_Complaints_SubjectOfferingId");

                // Index: filter by doctor (doctor dashboard — all complaints in their offerings)
                entity.HasIndex(c => c.TargetDoctorId)
                      .HasDatabaseName("IX_Complaints_TargetDoctorId");

                // Index: filter by submitting user
                entity.HasIndex(c => c.UserId)
                      .HasDatabaseName("IX_Complaints_UserId");

                entity.Property(c => c.TargetType).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Status).HasMaxLength(30).IsRequired();
                entity.Property(c => c.Message).HasMaxLength(2000).IsRequired();
                entity.Property(c => c.TargetId).HasMaxLength(26);
            });
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
    }
}
