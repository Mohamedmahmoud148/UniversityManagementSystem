---
layout: default
title: "🏗️ System Architecture"
---

# 🏗️ System Architecture — Deep Dive

## Clean Architecture Overview

This project follows **Clean Architecture** (also known as Onion Architecture or Hexagonal Architecture). This is a professional software design pattern used by enterprise systems at Microsoft, Google, and major SaaS companies.

### Why Clean Architecture?

> **Simple Explanation:** Imagine layers of an onion. The inner layers don't know anything about the outer layers. The database can be swapped, the framework can be changed — and the core business rules remain untouched.

```
┌─────────────────────────────────────────────────┐
│              Presentation Layer                  │
│          (Controllers, Middleware, Hubs)         │
│               UniversityManagementSystem.Api     │
├─────────────────────────────────────────────────┤
│             Infrastructure Layer                  │
│     (EF Core, Services, Jobs, Storage, Consumers)│
│        UniversityManagementSystem.Infrastructure  │
├─────────────────────────────────────────────────┤
│                  Core Layer                       │
│      (Entities, Interfaces, DTOs, Exceptions)    │
│           UniversityManagementSystem.Core         │
└─────────────────────────────────────────────────┘
```

### Dependency Rules
- **Core** → knows nothing about Infrastructure or Api
- **Infrastructure** → knows Core, NOT Api  
- **Api** → knows both Core and Infrastructure
- **Tests** → knows all layers (for testing)

---

## The Three-Service Architecture

This system is deployed as **three separate services** on Railway:

```
┌──────────────────────────────────────────────────────────────────┐
│                        INTERNET                                   │
└────────────────┬────────────────────────────────────────────────┘
                 │
        ┌────────▼────────┐
        │   Frontend App   │  (React/Next.js — separate deployment)
        │   Browser Client │
        └────────┬─────────┘
                 │
         ┌───────┴────────────┐
         │                    │
         ▼                    ▼
┌────────────────┐   ┌──────────────────────┐
│  .NET Backend  │   │  FastAPI AI Service   │
│  (Railway)     │   │  (Railway)            │
│                │   │                       │
│  ASP.NET Core  │   │  Python + Claude LLM  │
│  Port: 8080    │   │  Port: 8000           │
└───────┬────────┘   └──────────┬───────────┘
        │                       │
        │    Calls Backend      │
        │◄──────────────────────┘
        │
   ┌────┴──────────────────────────────────┐
   │           Railway Platform            │
   │                                       │
   │  ┌─────────┐  ┌───────┐  ┌────────┐  │
   │  │PostgreSQL│  │ Redis │  │RabbitMQ│  │
   │  └─────────┘  └───────┘  └────────┘  │
   └───────────────────────────────────────┘
        │
   ┌────▼──────────────────────────┐
   │      Cloudflare R2            │
   │  (File Storage — S3 compat)   │
   └───────────────────────────────┘
```

---

## Layered Request Flow

### Standard REST Request Flow
```
HTTP Request arrives
        │
        ▼
[Rate Limiter] — blocks if too many requests from same IP
        │
        ▼
[CORS Middleware] — validates origin
        │
        ▼
[Authentication Middleware] — validates JWT token
        │
        ▼
[Authorization] — checks role claims
        │
        ▼
[Controller Action] — parses request, calls service
        │
        ▼
[Service Layer] — business logic, validation
        │
        ▼
[Repository / DbContext] — database query via EF Core
        │
        ▼
[PostgreSQL] — data persisted/retrieved
        │
        ▼
[Response] — DTO mapped, JSON returned
        │
        ▼
[ResponseWrapperFilter] — wraps in ApiResponse<T>
        │
        ▼
HTTP Response to client
```

### AI Chat Request Flow
```
Student: "كام ساعة خلصت من اللائحة؟"
        │
        ▼
[ChatController] POST /api/chat
        │
        ▼
[ChatService] — saves message, calls AI service
        │
        ▼
[FastAPI /chat endpoint]
        │
        ▼
[PlannerAgent] — classifies: backend_api_query (regulation roadmap)
        │
        ▼
[_detect_backend_query] — Layer-2 override confirms: "لائحة" keyword
        │
        ▼
[DynamicApiModule] — Rule Q1 matches: GET /api/Regulations/my-roadmap
        │
        ▼
[.NET Backend] — validates JWT, queries DB, returns AcademicRoadmapDto
        │
        ▼
[Claude LLM] — interprets JSON data, generates human Arabic response
        │
        ▼
[ChatService] — saves AI response to Conversation history
        │
        ▼
Student receives: "أكملت 45 ساعة من أصل 120 ساعة، معدلك الحالي 2.8..."
```

---

## Key Architectural Decisions

### Decision 1: ULID Instead of UUID/int

**What:** All primary keys use ULID (Universally Unique Lexicographically Sortable Identifier)

**Why:**
- ULIDs are **sortable by creation time** (first 10 chars = timestamp)
- No integer overflow risk unlike int IDs
- No UUID collision risk
- URL-safe (no special characters)
- Database-friendly (stored as string, indexed efficiently)

```
ULID Example: 01HXYZ2ABC3DEF4GHI5JKL6MNO
              ├─────────────┤├────────────┤
              Timestamp (ms)  Randomness
```

### Decision 2: Soft Deletes

**What:** Nothing is ever truly deleted. Instead, `DeletedAt` is set to the current timestamp.

**Why:**
- Audit trail preserved
- Data recovery possible
- Reporting on historical data
- EF Core global query filter automatically hides soft-deleted records

```csharp
// BaseEntity has:
public DateTime? DeletedAt { get; set; }

// EF Core global filter:
modelBuilder.Entity<Student>().HasQueryFilter(s => s.DeletedAt == null);
```

### Decision 3: AI Cannot Modify Critical Data

**What:** The AI is blocked from calling DELETE, PUT, PATCH endpoints.

**Why:**
- Prevents accidental data destruction by LLM hallucination
- AI reasoning is probabilistic; data mutation must be deterministic
- A student's grade or profile can only be changed by intentional human action

```python
# api_discovery.py
_BLOCKED_METHODS = {"delete", "put", "patch"}
```

### Decision 4: Two-Layer AI Intent Detection

**What:** LLM classifies intent, then a deterministic rule engine validates/overrides.

**Why:**
- LLMs can misclassify (e.g., "tell me about the exam" vs "create an exam")
- Critical operations (exam creation, enrollment) need 100% accuracy
- Layer-2 keyword matching catches LLM errors

```
Layer 1: Claude LLM → "I think this is general_chat"
Layer 2: Keyword engine → "رسبت في" found → OVERRIDE to backend_api_query
Result: Correct routing to GET /api/Regulations/my-roadmap
```

### Decision 5: Hangfire for Background Jobs

**What:** All scheduled/background tasks use Hangfire with PostgreSQL persistence.

**Why:**
- Jobs survive server restarts (persisted to DB)
- Automatic retry with backoff on failure
- Admin dashboard at `/hangfire` to monitor jobs
- Cron scheduling for recurring jobs

### Decision 6: Infrastructure as IRealtimeNotifier Interface

**What:** The `IRealtimeNotifier` interface lives in Core; `SignalRNotifier` lives in Api.

**Why:**
- Infrastructure cannot depend on Api (Clean Architecture rule)
- Core defines the contract; Api provides the SignalR implementation
- Infrastructure's `NotificationService` calls `IRealtimeNotifier` without knowing about SignalR
- This allows swapping SignalR for Firebase, WebPush, etc. without touching Infrastructure

---

## Domain Events Flow (MassTransit)

```
AttendanceService records attendance
        │
        ▼
publishes AttendanceRecordedEvent via MassTransit
        │
        ▼
RabbitMQ message broker routes event
        │
        ▼
AttendanceConsumer receives event
        │
        ▼
Processes: GPA recalculation, notifications, etc.
```

---

## Caching Strategy

| Layer | What's Cached | How Long |
|-------|-------------|---------|
| Redis | JWT revocation lists | Token lifetime |
| Redis | Health check results | Short TTL |
| FastAPI in-memory | OpenAPI schema from backend | Until service restart |

---

## Error Handling Architecture

```
Any Exception thrown anywhere
        │
        ▼
[GlobalExceptionMiddleware] catches it
        │
        ├── DomainException → 400 Bad Request
        ├── KeyNotFoundException → 404 Not Found
        ├── UnauthorizedAccessException → 403 Forbidden
        └── Any other → 500 Internal Server Error
        │
        ▼
[Serilog] logs structured error with CorrelationId
        │
        ▼
JSON error response returned to client:
{
  "success": false,
  "message": "Error description",
  "correlationId": "abc-123"
}
```
