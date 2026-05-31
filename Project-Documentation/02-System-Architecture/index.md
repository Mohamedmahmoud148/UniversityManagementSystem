# System Architecture

> **Last refreshed:** 2026-05-31 | **Pattern:** Clean Architecture (Onion)

---

## 1. Architectural Pattern

UniSys follows **Clean Architecture** (Onion Architecture). Inner layers define abstractions; outer layers provide implementations. The database and framework are swappable without changing business logic.

```
┌─────────────────────────────────────────────┐
│  Presentation  (Controllers, Hubs, Middleware)│  UniversityManagementSystem.Api
├─────────────────────────────────────────────┤
│  Infrastructure  (EF Core, Services, Jobs)   │  UniversityManagementSystem.Infrastructure
├─────────────────────────────────────────────┤
│  Core  (Entities, Interfaces, DTOs, Events)  │  UniversityManagementSystem.Core
└─────────────────────────────────────────────┘
```

Dependency rule: `Api` → `Infrastructure` → `Core`. `Core` has zero outward dependencies.

---

## 2. Component Map

| Component | Technology | Role |
|-----------|-----------|------|
| **REST API** | ASP.NET Core 9 | 35 controllers, JWT auth, rate limiting |
| **SignalR Hub** | ASP.NET Core SignalR | Real-time notification push |
| **Background Jobs** | Hangfire + PostgreSQL | 7 recurring jobs |
| **Message Consumer** | MassTransit + RabbitMQ | Event-driven notification delivery |
| **AI Gateway** | FastAPI 3.12 | Intent routing, 15 modules, RAG |
| **Primary Store** | PostgreSQL 16 | All business data, soft deletes |
| **Cache** | Redis 7 | Session, conversation memory, rate limit counters |
| **Message Bus** | RabbitMQ 3.13 | Async event delivery |
| **Vector Store** | ChromaDB 0.5 | RAG embeddings for materials + regulations |
| **File Storage** | Cloudflare R2 | All uploaded files (materials, exams, submissions) |
| **LLM** | OpenRouter → GPT-4o-mini | AI responses, exam generation, grading |
| **Embeddings** | OpenAI text-embedding-3-small | RAG indexing and query embedding |

---

## 3. Request Lifecycle

```
Browser / Mobile
      │ HTTPS + JWT Bearer
      ▼
ASP.NET Core Pipeline:
  [Rate Limiter] → [CORS] → [Auth Middleware] → [Controller Action]
      │
      ├─── Simple CRUD → EF Core → PostgreSQL
      ├─── File upload → IStorageService → Cloudflare R2
      ├─── Real-time → INotificationService → RabbitMQ → Consumer → SignalR
      └─── AI chat → IAiService → FastAPI /api/chat
                                       │
                                 PlannerAgent (LLM classify intent)
                                       │
                                 Layer-2 deterministic override
                                       │
                                 RBAC gate
                                       │
                                 Route to Module
                                       │
                           ┌───────────┴──────────────┐
                           │ Fetch backend data        │
                           │ (roadmap, grades, etc.)   │
                           │ + optional RAG search     │
                           └───────────────────────────┘
                                       │
                                 LLM generates response
                                       │
                                 AgentOutput → ChatService → DB + Frontend
```

---

## 4. Data Flow Principles

1. **Backend is source of truth** — the AI service never caches or mutates business data; it reads live APIs.
2. **Soft deletes everywhere** — `BaseEntity.DeletedAt` ensures no record is ever physically removed. All queries filter `WHERE DeletedAt IS NULL`.
3. **Event-driven notifications** — creating a notification saves to DB *and* publishes an event. The MassTransit consumer then pushes via SignalR. The two steps are decoupled.
4. **JWT-forwarded to AI** — every AI call includes the student's JWT so the AI service can call authenticated .NET endpoints on the student's behalf.
5. **Parallel data fetching** — every AI module uses `asyncio.gather()` to fetch all required data sources concurrently, minimising response latency.

---

## 5. Background Job Schedule

| Job | Cron | Purpose |
|-----|------|---------|
| `ExamReminderJob` | `*/30 * * * *` | 24h / 2h exam reminders to enrolled students |
| `AssignmentReminderJob` | `*/30 * * * *` | 24h / 2h deadline reminders (non-submitters only) |
| `AcademicRiskJob` | `0 6 * * *` | Daily GPA + attendance risk scoring |
| `RagIndexingJob` | Daily | Index unindexed materials into ChromaDB |
| `ComplaintIntelligenceJob` | Daily / Weekly / Monthly | AI intelligence reports for admin |

---

## 6. Security Architecture

| Mechanism | Detail |
|-----------|--------|
| **Authentication** | JWT Bearer tokens, issued by AuthController |
| **Authorisation** | `[Authorize(Roles="...")]` on every protected endpoint |
| **Rate Limiting** | Global: 1000 req/min; Login: 5 req/min; Sensitive: 10 req/min |
| **Input sanitisation** | `IAiInputSanitizer` strips injection patterns before AI calls |
| **Prompt injection guard** | `INJECTION_GUARD` prefix in every AI system prompt |
| **CORS** | Explicit allowlist from `ALLOWED_ORIGINS` env var |
| **Circuit breaker** | Backend client opens after 5 failures; resets after 30s |
| **Audit logs** | `AuditLogsController` tracks sensitive mutations |

---

## 7. All Diagrams

See [21-Diagrams](../21-Diagrams/index.md) for:
- Context Diagram (C1)
- System Overview (C2)
- Use Case Diagram
- Class Diagram
- Sequence Diagrams (material Q&A, assignment submission, AI grading)
- Activity Diagrams (student journey, AI flow, assignment lifecycle)
- Deployment Diagram
- ERD
