# Deployment & Infrastructure

> **Last refreshed:** 2026-05-31 | **Platform:** Railway PaaS

---

## 1. Production Services (Railway)

| Service | Type | Notes |
|---------|------|-------|
| .NET Backend | Web Service | Auto-deploys from GitHub main branch |
| FastAPI AI | Web Service | Python 3.12 |
| PostgreSQL 16 | Managed DB | Persistent, Railway-managed |
| Redis 7 | Plugin | Conversation memory, cache |
| RabbitMQ 3.13 | Plugin | Async event bus |
| ChromaDB 0.5 | Web Service | Persistent volume for vectors |

**Frontend:** Hosted separately on Vercel/CDN at `bsnu.web.app`

**File Storage:** Cloudflare R2 (not on Railway)

---

## 2. Required Environment Variables

### .NET Backend

```env
# Database
ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...

# Redis
REDIS_URL=redis://...

# RabbitMQ
RABBITMQ_URL=amqp://user:pass@host:5672/vhost

# JWT
JWT_SECRET=<256-bit secret>
JWT_ISSUER=UniversityManagementSystem
JWT_AUDIENCE=UniversityManagementSystem

# FastAPI AI Service
AI_SERVICE_URL=http://fastapi-service:8000

# Cloudflare R2
R2_ACCESS_KEY_ID=...
R2_SECRET_ACCESS_KEY=...
R2_BUCKET_NAME=...
R2_ENDPOINT_URL=https://<account>.r2.cloudflarestorage.com
R2_PUBLIC_URL=https://pub-<hash>.r2.dev

# Hangfire
HANGFIRE_DASHBOARD_USER=admin
HANGFIRE_DASHBOARD_PASS=...

# CORS
ALLOWED_ORIGINS=https://bsnu.web.app,https://yourdomain.com
```

### FastAPI AI Service

```env
# LLM
OPENROUTER_API_KEY=sk-or-...
OPENROUTER_FALLBACK_MODEL_1=openai/gpt-4o-mini
OPENROUTER_TIMEOUT_SECONDS=45

# Embeddings
OPENAI_API_KEY=sk-...
EMBEDDING_MODEL=text-embedding-3-small

# Backend
BACKEND_BASE_URL=https://universitymanagementsystem-production-e58e.up.railway.app

# Redis
REDIS_URL=redis://...

# Rate limiting
RATE_LIMIT_RPM=30

# Timeouts
BACKEND_TIMEOUT_SECONDS=30
REQUEST_TIMEOUT_SECONDS=60

# ChromaDB
CHROMA_HOST=localhost
CHROMA_PORT=8001
```

---

## 3. Deployment Flow

```
Developer pushes to GitHub main branch
    │
Railway detects push → trigger build
    │
.NET: dotnet publish → Docker container
FastAPI: pip install + uvicorn startup
    │
Railway deploys new containers (zero-downtime rolling update)
    │
.NET startup: MigrateAsync() → DB schema up-to-date
FastAPI startup: index regulations → ready
```

---

## 4. Health Checks

```
GET /health           → .NET health check endpoint
GET /health/ai        → FastAPI health check
GET /api/rag/stats    → ChromaDB collection stats
```

Railway uses health check endpoints to determine deployment success.

---

## 5. Resilience Configuration

### .NET Circuit Breaker (Polly)
- Opens after 5 AI service failures in 15 seconds
- Resets after 15 seconds
- Returns friendly fallback during open state

### FastAPI Circuit Breaker (ToolExecutionClient)
- Opens after 5 consecutive backend call failures
- Resets after 30 seconds

### Redis Fallback
- If Redis unavailable: `IDistributedCache` falls back to in-memory cache
- Conversation memory: graceful degradation (AI still works, just loses history)

---

## 6. Scaling Considerations

| Bottleneck | Current | Scale Strategy |
|-----------|---------|---------------|
| .NET API | Single instance | Horizontal scale (stateless, Redis session) |
| FastAPI AI | Single instance | Horizontal scale (stateless, Redis memory) |
| PostgreSQL | Railway managed | Read replicas for analytics |
| ChromaDB | Single instance | Volume-backed; scale vertically |
| RabbitMQ | Railway plugin | Scale via Railway addons |

---

## 7. Monitoring

- **Logging:** Serilog structured logs → Railway log viewer
- **Hangfire Dashboard:** `/hangfire` (protected by basic auth) — view job history, failures, retries
- **RabbitMQ Management:** `http://host:15672` — queue depth, consumer status

---

## 8. Backup Strategy

- PostgreSQL: Railway automatic daily backups (point-in-time recovery)
- ChromaDB: Railway persistent volume (survives redeploys)
- R2 Files: Cloudflare R2 is redundantly stored across data centers

---

## 9. Local Development Setup

```bash
# Clone repository
git clone <repo>

# .NET Backend (requires .NET 9 SDK)
cd UniversityManagementSystem
cp appsettings.example.json appsettings.Development.json
# Fill in local PostgreSQL, Redis, RabbitMQ connection strings
dotnet run --project UniversityManagementSystem.Api

# FastAPI AI Service (requires Python 3.12)
cd fastApi
pip install -r requirements.txt
cp .env.example .env
# Fill in OPENROUTER_API_KEY, OPENAI_API_KEY, BACKEND_BASE_URL
uvicorn app.main:app --reload --port 8000

# Optional: run infrastructure locally with Docker
docker-compose up -d  # PostgreSQL + Redis + RabbitMQ
```
