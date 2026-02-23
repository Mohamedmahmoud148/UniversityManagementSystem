using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Ensure database is created/migrated
            await context.Database.MigrateAsync();

            // Seed SuperAdmins
            // Seed SuperAdmins
            if (!await context.SystemUsers.AnyAsync(u => u.Role == UserRole.SuperAdmin))
            {
                Console.WriteLine("Seeding SuperAdmin...");
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
                    Console.WriteLine("SuperAdmin seeded successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error seeding SuperAdmin: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine("SuperAdmin already exists.");
            }
                // Seed Academic Hierarchy
                if (!await context.Universities.AnyAsync())
                {
                    Console.WriteLine("Seeding Academic Hierarchy...");
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
                    // Student entity check:
                    // Inherits from BaseEntity? Yes.
                    // Has UniversityEmail? Let's check Student.cs.
                    // It had "UniversityStudentsId" and "Email".
                    // Let's quickly check Student.cs to be sure about properties.
                    
                    context.Students.Add(student);
                    await context.SaveChangesAsync();

                    Console.WriteLine("Academic Hierarchy and Users seeded successfully.");
                }
        }
    }
}
