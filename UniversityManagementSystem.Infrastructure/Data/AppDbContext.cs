using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Filter for Soft Delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "x");
                    var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
                    var nullConstant = System.Linq.Expressions.Expression.Constant(null);
                    var equality = System.Linq.Expressions.Expression.Equal(property, nullConstant);
                    var lambda = System.Linq.Expressions.Expression.Lambda(equality, parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }

            // Configure RefreshToken
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Many-to-Many Relationships

            // SubjectDoctor
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

            // SubjectAssistant
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

            // Enrollment (Old Config Removed)

            // Academic Hierarchy Relationships

            // University -> College
            modelBuilder.Entity<University>()
                 .HasMany(u => u.Colleges)
                 .WithOne(c => c.University)
                 .HasForeignKey(c => c.UniversityId)
                 .OnDelete(DeleteBehavior.Restrict);

            // College -> Department
            modelBuilder.Entity<College>()
                .HasMany(c => c.Departments)
                .WithOne(d => d.College)
                .HasForeignKey(d => d.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department -> Batch
            modelBuilder.Entity<Department>()
                .HasMany(d => d.Batches)
                .WithOne(b => b.Department)
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Batch -> Group (NEW)
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Batch)
                .WithMany(b => b.Groups)
                .HasForeignKey(g => g.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Batch -> Student (Existing, kept for compatibility if needed, but Student now has direct FK)
            // But Batch-Student is 1-to-many.
            modelBuilder.Entity<Batch>()
                .HasMany(b => b.Students)
                .WithOne(s => s.Batch)
                .HasForeignKey(s => s.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Student Hierarchy (Strict)
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

            // Student -> Group (NEW)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Group)
                .WithMany(g => g.Students)
                .HasForeignKey(s => s.GroupId)
                .OnDelete(DeleteBehavior.Restrict);


            // Indexes and Other Configs

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

            modelBuilder.Entity<AcademicYear>()
                .HasIndex(y => y.Name)
                .IsUnique();

            modelBuilder.Entity<Semester>()
                .HasIndex(s => new { s.Name, s.AcademicYearId })
                .IsUnique();

            // SubjectOffering Configuration
            modelBuilder.Entity<SubjectOffering>()
                .HasIndex(so => new { so.SubjectId, so.SemesterId })
                .IsUnique();

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Subject)
                .WithMany()
                .HasForeignKey(so => so.SubjectId)
                .OnDelete(DeleteBehavior.Restrict); // RESTRICT!

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Semester)
                .WithMany()
                .HasForeignKey(so => so.SemesterId)
                .OnDelete(DeleteBehavior.Restrict); // RESTRICT!

            modelBuilder.Entity<SubjectOffering>()
                .HasOne(so => so.Doctor)
                .WithMany()
                .HasForeignKey(so => so.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); // RESTRICT!

            // SubjectOffering New Hierarchy
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


            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.SubjectOfferingId })
                .IsUnique();

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.SubjectOffering)
                .WithMany()
                .HasForeignKey(e => e.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department -> Doctor
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.Department)
                .WithMany(dp => dp.Doctors)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department -> TA
            modelBuilder.Entity<TeachingAssistant>()
                .HasOne(ta => ta.Department)
                .WithMany(dp => dp.TeachingAssistants)
                .HasForeignKey(ta => ta.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Chat System
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

            // File Upload
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

            // Attendance
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

            // Notifications
            modelBuilder.Entity<AppNotification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 1. SystemUser -> Admin (One-to-One)
            modelBuilder.Entity<Admin>()
                .HasOne(a => a.SystemUser)
                .WithOne()
                .HasForeignKey<Admin>(a => a.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.SystemUserId)
                .IsUnique();

            // 2. SystemUser -> Student (One-to-One)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.SystemUser)
                .WithOne()
                .HasForeignKey<Student>(s => s.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.SystemUserId)
                .IsUnique();

            // 3. SystemUser -> Doctor (One-to-One)
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.SystemUser)
                .WithOne()
                .HasForeignKey<Doctor>(d => d.SystemUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Doctor>()
                .HasIndex(d => d.SystemUserId)
                .IsUnique();

            // 4. SystemUser -> TeachingAssistant (One-to-One)
            modelBuilder.Entity<TeachingAssistant>()
               .HasOne(t => t.SystemUser)
               .WithOne()
               .HasForeignKey<TeachingAssistant>(t => t.SystemUserId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeachingAssistant>()
                .HasIndex(t => t.SystemUserId)
                .IsUnique();

            // Exam
            modelBuilder.Entity<Exam>()
                .HasOne(e => e.SubjectOffering)
                .WithMany()
                .HasForeignKey(e => e.SubjectOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Exam>()
                .HasIndex(e => e.SubjectOfferingId);

            // ExamQuestion (Composition)
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

            // StudentGrade
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

            // Materials
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
        }
    }
}
