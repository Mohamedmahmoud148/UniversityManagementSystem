---
layout: default
title: "🚀 Developer Onboarding Guide"
---

# 🚀 Developer Onboarding Guide

## Local Development Setup

### Prerequisites
- .NET 9 SDK
- Python 3.11+
- PostgreSQL 15+
- Redis
- Node.js 18+ (for frontend)

### Backend Setup
```bash
# 1. Clone and navigate
cd UniversityManagementSystem

# 2. Set environment variables (create .env or use user-secrets)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=university;Username=postgres;Password=yourpass"
dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key-min-32-chars"

# 3. Run migrations
cd UniversityManagementSystem.Api
dotnet ef database update

# 4. Run the API
dotnet run
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
# Hangfire: http://localhost:5000/hangfire
```

### AI Service Setup
```bash
cd fastApi
pip install -r requirements.txt

# Set environment variables
export ANTHROPIC_API_KEY="sk-ant-..."
export BACKEND_URL="http://localhost:5000"

# Run
uvicorn app.main:app --reload --port 8000
```

### Running Tests
```bash
cd UniversityManagementSystem
dotnet test
# Expected: 22/22 passing
```

---

## Environment Variables

### .NET Backend
| Variable | Purpose | Example |
|----------|---------|---------|
| `CONNECTION_STRING` | PostgreSQL connection | `Host=...;Database=university` |
| `DATABASE_URL` | Alternative to above | Railway auto-sets this |
| `JWT_SECRET` | JWT signing key | Min 32 chars |
| `JWT_ISSUER` | JWT issuer | `university-management-system` |
| `REDIS_URL` | Redis connection | `localhost:6379` |
| `RABBITMQ_URL` | RabbitMQ | `amqp://guest:guest@localhost` |
| `AI_SERVICE_URL` | FastAPI URL | `https://ai.railway.app` |
| `R2_ACCOUNT_ID` | Cloudflare R2 | From Cloudflare dashboard |
| `R2_ACCESS_KEY` | R2 access key | |
| `R2_SECRET_KEY` | R2 secret key | |
| `R2_BUCKET_NAME` | R2 bucket | `university-files` |
| `DEFAULT_PASSWORD` | Default student password | Overrides appsettings |

### FastAPI AI Service
| Variable | Purpose |
|----------|---------|
| `ANTHROPIC_API_KEY` | Claude API key |
| `BACKEND_URL` | .NET backend URL |
| `SECRET_KEY` | Internal API security |

---

## Project Conventions

### Adding a New Entity
1. Create entity in `Core/Entities/` extending `BaseEntity`
2. Add DbSet to `AppDbContext`
3. Add navigation properties
4. Create migration: `dotnet ef migrations add YourMigrationName`
5. Create DTOs in `Core/DTOs/`
6. Create interface in `Core/Interfaces/`
7. Implement service in `Infrastructure/Services/`
8. Register service in `Program.cs`
9. Create controller in `Api/Controllers/`

### Naming Conventions
```csharp
// Entities: PascalCase noun
public class SubjectOffering { }

// DTOs: [Purpose][Entity]Dto
public class CreateStudentDto { }
public class StudentSummaryDto { }

// Interfaces: I prefix
public interface IStudentService { }

// Services: implements interface name without I
public class StudentService : IStudentService { }

// Controllers: [Entity]Controller
public class StudentsController : ControllerBase { }
```

### ID Pattern
```csharp
// Always use ULID for primary keys
public Ulid Id { get; set; } = Ulid.NewUlid();

// Always use ULID for FK parameters in routes
[HttpGet("{id}")]
public async Task<IActionResult> Get(string id)
{
    if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
    // use uid
}
```

---

## Hangfire Job Registration Pattern

```csharp
// 1. Define interface in Core/Interfaces/
public interface IMyJob { Task RunAsync(); }

// 2. Implement in Infrastructure/Jobs/
public class MyJob : IMyJob
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync() { ... }
}

// 3. Register in Program.cs
builder.Services.AddScoped<IMyJob, MyJob>();

// 4. Schedule in Program.cs (after app.Build())
using (var scope = app.Services.CreateScope())
{
    var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    manager.AddOrUpdate<IMyJob>("job-name", j => j.RunAsync(), Cron.Daily);
}
```

---

## Common Patterns

### Reading Current User from JWT
```csharp
// SystemUser.Id (for notifications, audit)
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

// Profile Id (Student/Doctor/Admin.Id)
var profileId = User.FindFirst("ProfileId")?.Value;

// Role
var role = User.FindFirst(ClaimTypes.Role)?.Value;
// Or: User.IsInRole("Student")
```

### Standard PagedResult Response
```csharp
return Ok(new PagedResult<MyDto>
{
    Data = items,
    TotalCount = total,
    Page = page,
    Size = size
});
```

### Batch Loading (Avoid N+1)
```csharp
// WRONG
foreach (var offering in offerings)
    offering.EnrolledCount = _context.Enrollments.Count(e => e.SubjectOfferingId == offering.Id);

// RIGHT — one query for all
var counts = await _context.Enrollments
    .Where(e => offeringIds.Contains(e.SubjectOfferingId) && e.IsActive)
    .GroupBy(e => e.SubjectOfferingId)
    .Select(g => new { g.Key, Count = g.Count() })
    .ToDictionaryAsync(x => x.Key, x => x.Count);
```
