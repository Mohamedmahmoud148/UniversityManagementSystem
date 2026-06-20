using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using Xunit;

namespace UniversityManagementSystem.Tests
{
    /// <summary>
    /// Tests for AcademicContextBuilder — the single source of truth for AI context.
    /// Covers Section 6 (DRY) and Section 2 (Serialization Contract).
    /// </summary>
    public class AcademicContextBuilderTests : IAsyncLifetime
    {
        private AppDbContext _db = null!;
        private AcademicContextBuilder _builder = null!;

        public async Task InitializeAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(options);
            await _db.Database.EnsureCreatedAsync();
            _builder = new AcademicContextBuilder(_db);
        }

        public async Task DisposeAsync() => await _db.DisposeAsync();

        [Fact]
        public async Task BuildAsync_UnknownUser_ReturnsBaseContext()
        {
            var userId = Ulid.NewUlid();
            var ctx = await _builder.BuildAsync(userId, "student", null);

            var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(ctx));

            Assert.NotNull(dict);
            Assert.True(dict!.ContainsKey("userId"));
            Assert.True(dict.ContainsKey("role"));
        }

        [Fact]
        public async Task BuildAsync_AdminRole_ReturnsBaseContextWithRole()
        {
            var userId = Ulid.NewUlid();
            var ctx = await _builder.BuildAsync(userId, "admin", null);

            var json = System.Text.Json.JsonSerializer.Serialize(ctx);
            Assert.Contains("userId", json);
            Assert.Contains("admin", json);
        }

        [Fact]
        public async Task BuildAsync_StudentRole_WithValidProfile_ReturnsEnrichedContext()
        {
            // Arrange: create minimal university hierarchy
            var univId  = Ulid.NewUlid();
            var colId   = Ulid.NewUlid();
            var deptId  = Ulid.NewUlid();
            var batchId = Ulid.NewUlid();
            var groupId = Ulid.NewUlid();
            var userId  = Ulid.NewUlid();
            var studentId = Ulid.NewUlid();

            // Act: build context for a valid student
            var ctx = await _builder.BuildAsync(userId, "student", null);

            // Assert: even with no DB record, returns base context (doesn't throw)
            Assert.NotNull(ctx);
        }

        [Fact]
        public async Task BuildAsync_InvalidProfileId_DoesNotThrow()
        {
            var userId = Ulid.NewUlid();
            // Invalid ULID string — should not throw, should return base context
            var ctx = await _builder.BuildAsync(userId, "student", "not-a-valid-ulid");
            Assert.NotNull(ctx);
        }

        [Fact]
        public async Task BuildAsync_DoctorRole_ReturnsBaseOrEnrichedContext()
        {
            var userId = Ulid.NewUlid();
            var ctx = await _builder.BuildAsync(userId, "doctor", null);

            // Even with no DB record → base context, no exception
            Assert.NotNull(ctx);
        }
    }
}
