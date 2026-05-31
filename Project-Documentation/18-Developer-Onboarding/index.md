# Developer Onboarding Guide

> **Last refreshed:** 2026-05-31

---

## 1. Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 9.0 | Backend development |
| Python | 3.12 | AI service development |
| PostgreSQL | 16 | Primary database |
| Redis | 7 | Cache + memory |
| RabbitMQ | 3.13 | Message bus |
| Node.js | 18+ | Frontend (if working on UI) |
| Docker | Any | Easy infra spin-up |

---

## 2. Repository Structure

```
/
├── UniversityManagementSystem.sln    ← .NET solution
├── UniversityManagementSystem.Api/   ← Main API project
├── UniversityManagementSystem.Core/  ← Domain models + interfaces
├── UniversityManagementSystem.Infrastructure/ ← EF Core, services, jobs
├── UniversityManagementSystem.Tests/ ← Unit tests
├── fastApi/                          ← Python AI service
│   ├── app/
│   │   ├── agents/     (planner, executor, schemas)
│   │   ├── modules/    (15 AI modules)
│   │   ├── core/       (config, rbac, logging)
│   │   ├── prompts/    (.md prompt files)
│   │   ├── services/   (backend_client, vector_store, embeddings)
│   │   └── main.py
│   └── requirements.txt
└── Project-Documentation/            ← This docs folder
```

---

## 3. Local Setup

### Step 1: Infrastructure (Docker)

```bash
docker run -d --name postgres -e POSTGRES_PASSWORD=dev -p 5432:5432 postgres:16
docker run -d --name redis -p 6379:6379 redis:7
docker run -d --name rabbitmq -e RABBITMQ_DEFAULT_USER=dev \
  -e RABBITMQ_DEFAULT_PASS=dev -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### Step 2: .NET Backend

```bash
# Copy and fill config
cp appsettings.Development.json.example appsettings.Development.json

# Key settings to fill:
# - ConnectionStrings__DefaultConnection
# - JWT_SECRET (any 32+ char string)
# - AI_SERVICE_URL=http://localhost:8000
# - R2_* (Cloudflare R2 credentials, or use local storage mock)

dotnet restore
dotnet run --project UniversityManagementSystem.Api
# → Runs on https://localhost:7000
# → DB migrations apply automatically on startup
# → Hangfire dashboard at https://localhost:7000/hangfire
```

### Step 3: FastAPI AI Service

```bash
cd fastApi
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt

cp .env.example .env
# Fill: OPENROUTER_API_KEY, OPENAI_API_KEY, BACKEND_BASE_URL=http://localhost:7000

uvicorn app.main:app --reload --port 8000
# → Runs on http://localhost:8000
# → API docs at http://localhost:8000/docs
```

---

## 4. Key Files to Understand First

| File | Why |
|------|-----|
| `UniversityManagementSystem.Api/Program.cs` | Service registrations, middleware pipeline, Hangfire jobs |
| `UniversityManagementSystem.Core/Entities/BaseEntity.cs` | All entities inherit this — ULID keys, soft deletes |
| `UniversityManagementSystem.Infrastructure/Services/ChatService.cs` | How AI context is built + sent to FastAPI |
| `fastApi/app/agents/planner.py` | Intent classification + Layer-2 keyword overrides |
| `fastApi/app/agents/executor.py` | Module routing + RBAC enforcement |
| `fastApi/app/core/rbac.py` | AI permission matrix |
| `fastApi/app/modules/academic_advisor.py` | Reference implementation of a module |
| `fastApi/app/modules/study_plan.py` | Most complex module — 4 parallel data fetches |

---

## 5. Adding a New API Endpoint

1. Add entity to `Core/Entities/` (inheriting `BaseEntity`)
2. Add `DbSet<YourEntity>` to `AppDbContext`
3. Run `dotnet ef migrations add <Name>`
4. Add interface to `Core/Interfaces/`
5. Add service implementation to `Infrastructure/Services/`
6. Register in `Program.cs` DI container
7. Add controller in `Api/Controllers/`
8. Add DTOs to `Core/DTOs/`

---

## 6. Adding a New AI Intent

1. Add to `Intent` enum in `fastApi/app/schemas/intents.py`
2. Add to `VALID_INTENTS` set in `fastApi/app/agents/planner.py`
3. Add keyword detector function (`_detect_*`) in `planner.py`
4. Add Layer-2 override in `PlannerAgent.run()` in `planner.py`
5. Add description to system prompt fallback in `planner.py`
6. Create module in `fastApi/app/modules/<intent>.py`
7. Register in `_MODULE_CLASS_MAP` in `fastApi/app/agents/executor.py`
8. Add permissions in `fastApi/app/core/rbac.py`

---

## 7. Database Migrations

```bash
# Add migration
dotnet ef migrations add <MigrationName> \
  --project UniversityManagementSystem.Infrastructure \
  --startup-project UniversityManagementSystem.Api

# Apply migration
dotnet ef database update \
  --project UniversityManagementSystem.Infrastructure \
  --startup-project UniversityManagementSystem.Api

# Migrations also auto-apply on startup via MigrateAsync()
```

---

## 8. Testing

```bash
# .NET tests
dotnet test

# FastAPI manual test (requires running backend)
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"اعمللي خطة مذاكرة","user_id":"test","context":{"role":"student"}}'
```

---

## 9. Common Issues

| Issue | Solution |
|-------|---------|
| DB migration fails | Check `ConnectionStrings__DefaultConnection` in appsettings |
| FastAPI can't reach backend | Set `BACKEND_BASE_URL` in `.env` |
| RAG returns empty results | Ensure `OPENAI_API_KEY` is set; trigger manual index via `/api/rag/trigger-index` |
| Redis connection refused | Ensure Redis is running; check `REDIS_URL` |
| RabbitMQ events not delivered | Ensure RabbitMQ running; check `RABBITMQ_URL` |
| JWT validation fails | Ensure `JWT_SECRET` matches between .NET and any token-decoding clients |
