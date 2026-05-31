using System.Collections;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Api.Controllers;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;
using Xunit;

namespace UniversityManagementSystem.Tests;

public class AuditLogsControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuditLogsController _controller;

    public AuditLogsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _controller = new AuditLogsController(_context);
    }

    [Fact]
    public async Task GetAll_ReturnsLegacyAndCurrentPaginationFields()
    {
        _context.AuditLogs.AddRange(
            new AuditLog
            {
                ActionType = "Create",
                EntityName = "Student",
                EntityId = Ulid.NewUlid().ToString(),
                PerformedByUserId = Ulid.NewUlid(),
                PerformedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new AuditLog
            {
                ActionType = "Delete",
                EntityName = "Student",
                EntityId = Ulid.NewUlid().ToString(),
                PerformedByUserId = null,
                PerformedAt = DateTime.UtcNow
            });
        await _context.SaveChangesAsync();

        var result = await _controller.GetAll(page: 1, pageSize: 20);

        var payload = result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        GetProperty<int>(payload, "page").Should().Be(1);
        GetProperty<int>(payload, "pageSize").Should().Be(20);
        GetProperty<int>(payload, "size").Should().Be(20);
        GetProperty<int>(payload, "total").Should().Be(2);
        GetProperty<int>(payload, "totalCount").Should().Be(2);
        GetProperty<IEnumerable>(payload, "data").Cast<object>().Should().HaveCount(2);
        GetProperty<IEnumerable>(payload, "items").Cast<object>().Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_LeavesMissingPerformedByUserIdAsNull()
    {
        _context.AuditLogs.Add(new AuditLog
        {
            ActionType = "Delete",
            EntityName = "Student",
            EntityId = Ulid.NewUlid().ToString(),
            PerformedByUserId = null,
            PerformedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _controller.GetAll(page: 1, pageSize: 20);

        var payload = result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        var firstItem = GetProperty<IEnumerable>(payload, "items").Cast<object>().Single();
        GetPropertyValue(firstItem, "PerformedByUserId").Should().BeNull();
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        var value = GetPropertyValue(instance, propertyName);
        return value.Should().BeAssignableTo<T>().Subject;
    }

    private static object? GetPropertyValue(object instance, string propertyName)
        => instance.GetType().GetProperty(propertyName)?.GetValue(instance);

    public void Dispose() => _context.Dispose();
}
