using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using Xunit;

namespace UniversityManagementSystem.Tests;

public class GradeServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly GradeService _sut;

    public GradeServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var auditMock  = new Mock<IAuditService>();
        var statusMock = new Mock<IAcademicStatusService>();
        _sut = new GradeService(_context, auditMock.Object, statusMock.Object);
    }

    private (SubjectOffering offering, Student student) SeedOfferingWithStudent(
        double midW = 0.25, double cwW = 0.25, double finalW = 0.25, double platW = 0.25)
    {
        var doctorId   = Ulid.NewUlid();
        var semesterId = Ulid.NewUlid();
        var deptId     = Ulid.NewUlid();
        var batchId    = Ulid.NewUlid();

        var subject = new Subject { Name = "Math", Code = "MATH101", CreditHours = 3 };
        _context.Subjects.Add(subject);
        _context.SaveChanges();

        var offering = new SubjectOffering(subject.Id, semesterId, doctorId, deptId, batchId, null, 30)
        {
            MidtermWeight   = midW,
            CourseworkWeight = cwW,
            FinalExamWeight  = finalW,
            PlatformWeight   = platW
        };
        _context.SubjectOfferings.Add(offering);

        var student = new Student
        {
            FullName = "Test Student",
            Email = "s@test.com",
            Phone = "01000000000",
            UniversityStudentId = "2024001",
            IsActive = true
        };
        _context.Students.Add(student);
        _context.Enrollments.Add(new Enrollment { StudentId = student.Id, SubjectOfferingId = offering.Id });
        _context.SaveChanges();

        return (offering, student);
    }

    [Theory]
    [InlineData(95, "A", 4.0)]
    [InlineData(85, "B", 3.0)]
    [InlineData(75, "C", 2.0)]
    [InlineData(65, "D", 1.0)]
    [InlineData(55, "F", 0.0)]
    public async Task CalculateGradesForOfferingAsync_CorrectGradeScale(double platformScore, string expectedLetter, double expectedPoints)
    {
        var (offering, student) = SeedOfferingWithStudent(platW: 1.0, midW: 0, cwW: 0, finalW: 0);

        var exam = new Exam { SubjectOfferingId = offering.Id, Title = "Test", TotalMarks = 100 };
        _context.Exams.Add(exam);
        _context.ExamSubmissions.Add(new ExamSubmission
        {
            ExamId = exam.Id,
            StudentId = student.Id,
            Score = platformScore
        });
        await _context.SaveChangesAsync();

        var count = await _sut.CalculateGradesForOfferingAsync(offering.Id, offering.DoctorId);

        count.Should().Be(1);
        var grade = _context.Set<StudentGrade>().First();
        grade.GradeLetter.Should().Be(expectedLetter);
        grade.GradePoints.Should().Be(expectedPoints);
    }

    [Fact]
    public async Task CalculateGradesForOfferingAsync_WeightsNotOne_ThrowsInvalidOperation()
    {
        var (offering, _) = SeedOfferingWithStudent(midW: 0.5, cwW: 0.5, finalW: 0.5, platW: 0.5);

        var act = async () => await _sut.CalculateGradesForOfferingAsync(offering.Id, offering.DoctorId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*weights*");
    }

    [Fact]
    public async Task CalculateGradesForOfferingAsync_WrongDoctor_ThrowsUnauthorized()
    {
        var (offering, _) = SeedOfferingWithStudent();

        var act = async () => await _sut.CalculateGradesForOfferingAsync(offering.Id, Ulid.NewUlid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CalculateStudentGpaAsync_WeightedByCredits()
    {
        var student = new Student
        {
            FullName = "GPA Student",
            Email = "gpa@test.com",
            Phone = "01000000000",
            UniversityStudentId = "GPA001",
            IsActive = true
        };
        _context.Students.Add(student);

        var s1 = new Subject { Name = "Math",    Code = "M1", CreditHours = 3 };
        var s2 = new Subject { Name = "Physics", Code = "P1", CreditHours = 2 };
        _context.Subjects.AddRange(s1, s2);
        _context.SaveChanges();

        var semId  = Ulid.NewUlid();
        var deptId = Ulid.NewUlid();
        var batId  = Ulid.NewUlid();
        var o1 = new SubjectOffering(s1.Id, semId, Ulid.NewUlid(), deptId, batId, null, 30);
        var o2 = new SubjectOffering(s2.Id, semId, Ulid.NewUlid(), deptId, batId, null, 30);
        _context.SubjectOfferings.AddRange(o1, o2);
        await _context.SaveChangesAsync();

        // A (4.0) in 3-credit + B (3.0) in 2-credit => GPA = (4*3 + 3*2)/(3+2) = 18/5 = 3.6
        _context.Set<StudentGrade>().AddRange(
            new StudentGrade { StudentId = student.Id, SubjectOfferingId = o1.Id, GradePoints = 4.0, IsFinalized = true },
            new StudentGrade { StudentId = student.Id, SubjectOfferingId = o2.Id, GradePoints = 3.0, IsFinalized = true }
        );
        await _context.SaveChangesAsync();

        var result = await _sut.CalculateStudentGpaAsync(student.Id);

        result.GPA.Should().Be(3.6);
        result.TotalCreditHours.Should().Be(5);
        result.TotalSubjects.Should().Be(2);
    }

    [Fact]
    public async Task CalculateStudentGpaAsync_NoGrades_ReturnsZeroGpa()
    {
        var student = new Student
        {
            FullName = "No Grades",
            Email = "nograde@test.com",
            Phone = "01000000000",
            UniversityStudentId = "NG001",
            IsActive = true
        };
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var result = await _sut.CalculateStudentGpaAsync(student.Id);

        result.GPA.Should().Be(0.0);
        result.TotalCreditHours.Should().Be(0);
    }

    public void Dispose() => _context.Dispose();
}
