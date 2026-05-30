using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Api.Controllers;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;
using Xunit;

namespace UniversityManagementSystem.Tests;

public class AnalyticsControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AnalyticsController _analyticsController;
    private readonly DashboardController _dashboardController;

    public AnalyticsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _analyticsController = new AnalyticsController(_context);
        _dashboardController = new DashboardController(_context);
    }

    [Fact]
    public async Task StudentCountByDepartment_UsesBatchDepartment_WhenStudentDepartmentIsMissing()
    {
        var (_, department, batch, group) = await SeedAcademicStructureAsync();
        _context.Students.Add(new Student
        {
            FullName = "Legacy Import Student",
            Phone = "01000000000",
            UniversityStudentId = "STU-001",
            BatchId = batch.Id,
            GroupId = group.Id,
            DepartmentId = Ulid.Empty,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _analyticsController.StudentCountByDepartment();

        var stats = Extract<List<DepartmentCountDto>>(result);
        stats.Should().ContainSingle(s =>
            s.DepartmentId == department.Id.ToString() &&
            s.StudentCount == 1);
    }

    [Fact]
    public async Task StudentCountByBatch_CountsOnlyActiveStudents()
    {
        var (_, _, batch, group) = await SeedAcademicStructureAsync();
        _context.Students.AddRange(
            new Student
            {
                FullName = "Active Student",
                Phone = "01000000000",
                UniversityStudentId = "STU-001",
                BatchId = batch.Id,
                GroupId = group.Id,
                IsActive = true
            },
            new Student
            {
                FullName = "Inactive Student",
                Phone = "01000000001",
                UniversityStudentId = "STU-002",
                BatchId = batch.Id,
                GroupId = group.Id,
                IsActive = false
            });
        await _context.SaveChangesAsync();

        var result = await _analyticsController.StudentCountByBatch();

        var stats = Extract<List<BatchCountDto>>(result);
        stats.Should().ContainSingle(s =>
            s.BatchId == batch.Id.ToString() &&
            s.StudentCount == 1);
    }

    [Fact]
    public async Task AdminDashboard_CountsOnlyActiveStudents()
    {
        var (_, _, batch, group) = await SeedAcademicStructureAsync();
        _context.Students.AddRange(
            new Student
            {
                FullName = "Active Student",
                Phone = "01000000000",
                UniversityStudentId = "STU-001",
                BatchId = batch.Id,
                GroupId = group.Id,
                IsActive = true
            },
            new Student
            {
                FullName = "Inactive Student",
                Phone = "01000000001",
                UniversityStudentId = "STU-002",
                BatchId = batch.Id,
                GroupId = group.Id,
                IsActive = false
            });
        await _context.SaveChangesAsync();

        var result = await _dashboardController.AdminDashboard();

        var dashboard = Extract<AdminDashboardDto>(result);
        dashboard.TotalStudents.Should().Be(1);
    }

    private async Task<(College College, Department Department, Batch Batch, Group Group)> SeedAcademicStructureAsync()
    {
        var university = new University { Name = "Test University", Code = "UNI" };
        var college = new College { Name = "Computer Science", Code = "CS", UniversityId = university.Id };
        var department = new Department { Name = "AI", Code = "AI", CollegeId = college.Id };
        var batch = new Batch { Name = "AI 2026", Code = "AI2026", DepartmentId = department.Id };
        var group = new Group { Name = "Group 1", Code = "G1", BatchId = batch.Id };

        _context.Universities.Add(university);
        _context.Colleges.Add(college);
        _context.Departments.Add(department);
        _context.Batches.Add(batch);
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        return (college, department, batch, group);
    }

    private static T Extract<T>(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeAssignableTo<T>().Subject;
    }

    public void Dispose() => _context.Dispose();
}
