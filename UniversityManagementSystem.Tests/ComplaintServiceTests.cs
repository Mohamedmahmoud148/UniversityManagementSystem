using FluentAssertions;
using Hangfire;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using Xunit;

namespace UniversityManagementSystem.Tests;

public class ComplaintServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IBackgroundJobClient> _jobClientMock;
    private readonly ComplaintService _sut;

    public ComplaintServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _jobClientMock = new Mock<IBackgroundJobClient>();
        _sut = new ComplaintService(_context, _jobClientMock.Object);
    }

    [Fact]
    public async Task CreateComplaintAsync_ValidInput_PersistsComplaint()
    {
        var studentId = Ulid.NewUlid();
        var dto = new CreateComplaintDto
        {
            Title = "Missing grade",
            Message = "My grade for midterm is missing.",
            TargetType = "Grade",
            TargetId = Ulid.NewUlid().ToString()
        };

        var result = await _sut.CreateComplaintAsync(studentId, dto);

        result.Should().NotBeNull();
        result.Title.Should().Be("Missing grade");
        result.Status.Should().Be("Pending");
        _context.Complaints.Count().Should().Be(1);
    }

    [Fact]
    public async Task CreateComplaintAsync_EnqueuesBackgroundJob()
    {
        var studentId = Ulid.NewUlid();
        var dto = new CreateComplaintDto
        {
            Title = "Test complaint",
            Message = "Test message.",
            TargetType = "Doctor",
            TargetId = Ulid.NewUlid().ToString()
        };

        await _sut.CreateComplaintAsync(studentId, dto);

        _jobClientMock.Verify(
            x => x.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<IState>()),
            Times.Once);
    }

    [Fact]
    public async Task GetComplaintByIdAsync_ExistingId_ReturnsComplaint()
    {
        var studentId = Ulid.NewUlid();
        var complaint = new Complaint
        {
            StudentId = studentId,
            Title = "Test",
            Message = "Body",
            TargetType = "General",
            TargetId = string.Empty,
            Status = "Pending",
            Priority = "Normal",
            CreatedAt = DateTime.UtcNow
        };
        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();

        var result = await _sut.GetComplaintByIdAsync(complaint.Id, studentId, "Student");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test");
    }

    [Fact]
    public async Task GetComplaintByIdAsync_WrongStudent_ThrowsUnauthorized()
    {
        var ownerStudentId = Ulid.NewUlid();
        var otherStudentId = Ulid.NewUlid();
        var complaint = new Complaint
        {
            StudentId = ownerStudentId,
            Title = "Private",
            Message = "Body",
            TargetType = "General",
            TargetId = string.Empty,
            Status = "Pending",
            Priority = "Normal",
            CreatedAt = DateTime.UtcNow
        };
        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();

        var act = async () => await _sut.GetComplaintByIdAsync(complaint.Id, otherStudentId, "Student");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    public void Dispose() => _context.Dispose();
}
