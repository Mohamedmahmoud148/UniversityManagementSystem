using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using Xunit;

namespace UniversityManagementSystem.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"]         = "TestSecretKeyThatIsLongEnoughForHS256!",
                ["JwtSettings:Issuer"]            = "TestIssuer",
                ["JwtSettings:Audience"]          = "TestAudience",
                ["JwtSettings:ExpirationInHours"] = "1"
            })
            .Build();

        _sut = new AuthService(_context, config);
    }

    private SystemUser CreateActiveUser(string email, string password, int failedCount = 0, DateTime? lockoutEnd = null)
    {
        var user = new SystemUser
        {
            Id = Ulid.NewUlid(),
            FullName = "Test User",
            Email = email,
            NationalId = "12345678901234",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Student,
            IsActive = true,
            AccessFailedCount = failedCount,
            LockoutEnd = lockoutEnd
        };
        _context.SystemUsers.Add(user);
        _context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        CreateActiveUser("student@test.com", "correct-pass");

        var result = await _sut.LoginAsync(new UserLoginDto
        {
            Email = "student@test.com",
            Password = "correct-pass"
        });

        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        CreateActiveUser("student2@test.com", "correct-pass");

        var result = await _sut.LoginAsync(new UserLoginDto
        {
            Email = "student2@test.com",
            Password = "wrong-pass"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsFailedCount()
    {
        var user = CreateActiveUser("student3@test.com", "correct-pass");

        await _sut.LoginAsync(new UserLoginDto { Email = "student3@test.com", Password = "wrong" });

        var updated = await _context.SystemUsers.FindAsync(user.Id);
        updated!.AccessFailedCount.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_FiveFailedAttempts_LocksAccount()
    {
        var user = CreateActiveUser("student4@test.com", "correct-pass", failedCount: 4);

        await _sut.LoginAsync(new UserLoginDto { Email = "student4@test.com", Password = "wrong" });

        var updated = await _context.SystemUsers.FindAsync(user.Id);
        updated!.LockoutEnd.Should().NotBeNull();
        updated.LockoutEnd.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsException()
    {
        CreateActiveUser("student5@test.com", "correct-pass",
            lockoutEnd: DateTime.UtcNow.AddMinutes(10));

        var act = async () => await _sut.LoginAsync(new UserLoginDto
        {
            Email = "student5@test.com",
            Password = "correct-pass"
        });

        await act.Should().ThrowAsync<Exception>().WithMessage("*locked*");
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        var result = await _sut.LoginAsync(new UserLoginDto
        {
            Email = "nobody@test.com",
            Password = "any"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_ReturnsTrue()
    {
        var user = CreateActiveUser("chpass@test.com", "old-pass");

        var result = await _sut.ChangePasswordAsync(user.Id, "old-pass", "NewPass123!");

        result.Should().BeTrue();
        var updated = await _context.SystemUsers.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("NewPass123!", updated!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFalse()
    {
        var user = CreateActiveUser("chpass2@test.com", "old-pass");

        var result = await _sut.ChangePasswordAsync(user.Id, "wrong-old", "NewPass123!");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_ClearsMustChangePassword()
    {
        var user = CreateActiveUser("chpass3@test.com", "old-pass");
        user.MustChangePassword = true;
        await _context.SaveChangesAsync();

        await _sut.ChangePasswordAsync(user.Id, "old-pass", "NewPass123!");

        var updated = await _context.SystemUsers.FindAsync(user.Id);
        updated!.MustChangePassword.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
