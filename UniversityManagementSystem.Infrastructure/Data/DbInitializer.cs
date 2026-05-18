using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Infrastructure.Data
{
    public static class DbInitializer
    {
        private static readonly (string Name, int Order)[] DefaultAcademicYears =
        {
            ("First Year", 1),
            ("Second Year", 2),
            ("Third Year", 3),
            ("Fourth Year", 4)
        };

        /// <summary>
        /// Controls whether the demo academic hierarchy (University, College, Department,
        /// Batch, Groups, Doctor, Student) is seeded on startup.
        /// Set environment variable SEED_DEMO_DATA=true to enable.
        /// SuperAdmin is always seeded regardless of this flag (needed for login).
        /// </summary>
        private static bool SeedDemoDataEnabled =>
            string.Equals(
                Environment.GetEnvironmentVariable("SEED_DEMO_DATA"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// When true, ALL seeding (including SuperAdmin) is completely disabled.
        /// Set DISABLE_SEEDING=true in production/demo Railway environment variable.
        /// </summary>
        private static bool SeedingDisabled =>
            string.Equals(
                Environment.GetEnvironmentVariable("DISABLE_SEEDING"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("DbInitializer");

            // ── DISABLE_SEEDING guard — set env var to completely stop all seeding ──
            if (SeedingDisabled)
            {
                logger.LogInformation("DbInitializer: DISABLE_SEEDING=true — all seeding skipped.");
                return;
            }

            // Ensure database is created/migrated
            await context.Database.MigrateAsync();

            // Seed SuperAdmins — always runs unless DISABLE_SEEDING=true
            if (!await context.SystemUsers.AnyAsync(u => u.Role == UserRole.SuperAdmin))
            {
                logger.LogInformation("Seeding SuperAdmin...");
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var superAdmin = new SystemUser
                    {
                        FullName = "Super Admin",
                        Email = "super.admin@university.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperSecretPass1!"),
                        Role = UserRole.SuperAdmin,
                        IsActive = true
                    };

                    context.SystemUsers.Add(superAdmin);
                    await context.SaveChangesAsync();

                    // Create Admin profile for consistency
                    var adminProfile = new Admin
                    {
                        FullName = superAdmin.FullName,
                        Email = superAdmin.Email,
                        SystemUserId = superAdmin.Id
                    };
                    context.Admins.Add(adminProfile);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    logger.LogInformation("SuperAdmin seeded successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    logger.LogError(ex, "Error seeding SuperAdmin.");
                    throw;
                }
            }
            else
            {
                logger.LogInformation("SuperAdmin already exists.");
            }

            // Seed Academic Hierarchy
            // Only runs when SEED_DEMO_DATA=true AND universities table is empty.
            // Default (no env var set): skipped — prevents surprise data after manual deletion.
            if (SeedDemoDataEnabled && !await context.Universities.AnyAsync())
            {
                logger.LogInformation("Seeding Academic Hierarchy...");
                var university = new University { Name = "Beni Suef National University" };
                context.Universities.Add(university);
                await context.SaveChangesAsync();

                var college = new College { Name = "Faculty of Computers and Information", UniversityId = university.Id };
                context.Colleges.Add(college);
                await context.SaveChangesAsync();

                var department = new Department { Name = "Artificial Intelligence", CollegeId = college.Id };
                context.Departments.Add(department);
                await context.SaveChangesAsync();

                var batch = new Batch { Name = "Year 4", DepartmentId = department.Id };
                context.Batches.Add(batch);
                await context.SaveChangesAsync();

                var groups = new[]
                {
                    new Group { Name = "Group 1", BatchId = batch.Id },
                    new Group { Name = "Group 2", BatchId = batch.Id },
                    new Group { Name = "Group 3", BatchId = batch.Id },
                    new Group { Name = "Group 4", BatchId = batch.Id }
                };
                context.Groups.AddRange(groups);
                await context.SaveChangesAsync();

                // Seed Doctor
                var doctorUser = new SystemUser
                {
                    FullName = "Dr. Ahmed",
                    Email = "ahmed@university.com",
                    UniversityEmail = "dr.ahmed@uni.edu.eg",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass123!"),
                    Role = UserRole.Doctor,
                    IsActive = true,
                    NationalId = "12345678901234"
                };
                context.SystemUsers.Add(doctorUser);
                await context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    FullName = doctorUser.FullName,
                    Email = doctorUser.Email,
                    Phone = "01012345678",
                    UniversityStaffId = "DOC-001",
                    DepartmentId = department.Id,
                    SystemUserId = doctorUser.Id
                };
                context.Doctors.Add(doctor);
                await context.SaveChangesAsync();

                // Seed Student
                var studentUser = new SystemUser
                {
                    FullName = "Student Ali",
                    Email = "ali@university.com",
                    UniversityEmail = "ali.student@uni.edu.eg",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass123!"),
                    Role = UserRole.Student,
                    IsActive = true,
                    NationalId = "12345678901235"
                };
                context.SystemUsers.Add(studentUser);
                await context.SaveChangesAsync();

                var student = new Student
                {
                    FullName = studentUser.FullName,
                    Email = studentUser.Email,
                    Phone = "01087654321",
                    UniversityStudentId = "STU-001",
                    UniversityId = university.Id,
                    CollegeId = college.Id,
                    DepartmentId = department.Id,
                    BatchId = batch.Id,
                    GroupId = groups[0].Id,
                    SystemUserId = studentUser.Id,
                    IsActive = true
                };

                context.Students.Add(student);
                await context.SaveChangesAsync();

                logger.LogInformation("Academic Hierarchy and Users seeded successfully.");
            }

            // SeedAcademicYearsAsync also only runs when SEED_DEMO_DATA=true.
            // Without this guard it ran on EVERY startup and recreated deleted AcademicYears.
            if (SeedDemoDataEnabled)
                await SeedAcademicYearsAsync(context, logger);
        }

        private static async Task SeedAcademicYearsAsync(AppDbContext context, ILogger logger)
        {
            logger.LogInformation("Seeding AcademicYears...");

            var colleges = await context.Colleges
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            if (colleges.Count == 0)
            {
                logger.LogInformation("Seeding completed successfully. No colleges found for AcademicYears seeding.");
                return;
            }

            var insertedCount = 0;
            var skippedCount = 0;

            foreach (var college in colleges)
            {
                var hasAcademicYears = await context.Set<AcademicYear>()
                    .IgnoreQueryFilters()
                    .AnyAsync(y => y.CollegeId == college.Id);

                if (hasAcademicYears)
                {
                    logger.LogInformation(
                        "AcademicYears already exist for college {CollegeId} ({CollegeName}). Checking for missing default years.",
                        college.Id,
                        college.Name);
                }

                foreach (var seedYear in DefaultAcademicYears)
                {
                    var orderExists = await context.Set<AcademicYear>()
                        .IgnoreQueryFilters()
                        .AnyAsync(y => y.CollegeId == college.Id && y.Order == seedYear.Order);

                    if (orderExists)
                    {
                        skippedCount++;
                        logger.LogInformation(
                            "Skipping duplicates: AcademicYear Order={Order} already exists for college {CollegeId} ({CollegeName}).",
                            seedYear.Order,
                            college.Id,
                            college.Name);
                        continue;
                    }

                    var nameExists = await context.Set<AcademicYear>()
                        .IgnoreQueryFilters()
                        .AnyAsync(y => y.CollegeId == college.Id && y.Name == seedYear.Name);

                    if (nameExists)
                    {
                        skippedCount++;
                        logger.LogInformation(
                            "Skipping duplicates: AcademicYear Name={Name} already exists for college {CollegeId} ({CollegeName}).",
                            seedYear.Name,
                            college.Id,
                            college.Name);
                        continue;
                    }

                    var academicYear = new AcademicYear(
                        seedYear.Name,
                        isActive: false,
                        seedYear.Order,
                        college.Id);

                    context.Set<AcademicYear>().Add(academicYear);

                    try
                    {
                        await context.SaveChangesAsync();
                        insertedCount++;
                    }
                    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                    {
                        context.Entry(academicYear).State = EntityState.Detached;
                        skippedCount++;

                        logger.LogWarning(
                            ex,
                            "Skipping duplicates: concurrent AcademicYear seed conflict for college {CollegeId} ({CollegeName}), Order={Order}.",
                            college.Id,
                            college.Name,
                            seedYear.Order);
                    }
                }
            }

            if (insertedCount == 0)
            {
                logger.LogInformation("AcademicYears already exist. No new records inserted.");
            }

            logger.LogInformation(
                "Seeding completed successfully. AcademicYears inserted: {InsertedCount}; duplicates skipped: {SkippedCount}.",
                insertedCount,
                skippedCount);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
        }
    }
}
