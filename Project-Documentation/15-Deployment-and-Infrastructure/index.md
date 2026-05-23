---
layout: default
title: "🚀 Deployment & Infrastructure Guide"
---

# 🚀 Deployment & Infrastructure Guide

## Platform: Railway

This project is deployed on **Railway** — a modern Platform-as-a-Service (PaaS) that auto-deploys from GitHub.

### Services on Railway

| Service | Type | Description |
|---------|------|-------------|
| `.NET Backend` | Web Service | ASP.NET Core 9 API |
| `FastAPI AI` | Web Service | Python AI orchestration |
| `PostgreSQL` | Managed DB | Primary database |
| `Redis` | Managed Cache | Session/cache layer |
| `RabbitMQ` | Managed MQ | Message bus |

### External Services

| Service | Provider | Purpose |
|---------|---------|---------|
| File Storage | Cloudflare R2 | PDF/Excel/document storage |
| LLM | Anthropic Claude | AI reasoning |
| CDN | Cloudflare | File delivery |

---

## Auto-Deploy Flow

```
Developer pushes to main branch
        │
        ▼
Railway detects push via webhook
        │
        ▼
Railway runs Dockerfile or dotnet publish
        │
        ▼
New container deployed (zero-downtime rolling deploy)
        │
        ▼
Health check: GET /health
        │
        ├── Passes → old container shut down
        └── Fails → rollback to previous version
```

---

## Health Check Endpoint

```
GET /health

Response:
{
  "status": "Healthy",
  "checks": [
    { "component": "postgresql", "status": "Healthy" },
    { "component": "redis", "status": "Healthy" },
    { "component": "hangfire", "status": "Healthy" }
  ],
  "totalDuration": "00:00:00.123"
}
```

If any check fails: Status = "Degraded" or "Unhealthy"

---

## Database Migration Strategy

Migrations run **automatically on startup**:

```csharp
// Program.cs
db.Database.Migrate();  // Applies all pending migrations
```

Additionally, idempotent SQL runs for legacy column fixes:
```csharp
db.Database.ExecuteSqlRaw(@"
    ALTER TABLE ""SystemUsers""
    ADD COLUMN IF NOT EXISTS ""Code"" text;
");
```

This means: **no manual migration steps** needed in production. Just deploy and the DB updates itself.

---

## Environment Variables — Production Checklist

```
# Required for production:
CONNECTION_STRING=Host=railway-postgres;...
JWT_SECRET=<min-32-char-secret>
AI_SERVICE_URL=https://your-ai-service.railway.app
R2_ACCOUNT_ID=<cloudflare-account>
R2_ACCESS_KEY=<r2-key>
R2_SECRET_KEY=<r2-secret>
R2_BUCKET_NAME=university-files
DEFAULT_PASSWORD=<student-default-pass>
ANTHROPIC_API_KEY=<claude-key>
REDIS_URL=<railway-redis-url>
RABBITMQ_URL=<railway-rabbitmq-url>
```

---

## File Storage Architecture (Cloudflare R2)

```
User uploads file
        │
        ▼
POST /api/file/upload (multipart/form-data)
        │
        ▼
R2StorageService.UploadAsync():
  1. Generate unique StorageKey: "{entityType}/{ulid}/{filename}"
  2. Upload to Cloudflare R2 bucket
  3. Save UploadedFile record to DB (StorageKey, FileName, ContentType)
  4. Return { fileId, storageKey }
        │
        ▼
User downloads file
        │
        ▼
GET /api/file/{fileId}
        │
        ▼
R2StorageService.GetSignedUrlAsync():
  1. Look up StorageKey from DB
  2. Generate presigned URL (60-minute expiry)
  3. Return URL to client
  4. Client downloads directly from Cloudflare (not through backend)
```

**Why presigned URLs?**
- File download bypasses the backend server → no bandwidth cost
- URL expires after 60 minutes → security
- Cloudflare CDN serves the file → global performance

---

## Serilog Structured Logging

Every request is logged with:
```json
{
  "timestamp": "2026-05-16T08:00:00Z",
  "level": "Information",
  "message": "HTTP GET /api/students responded 200 in 45ms",
  "properties": {
    "correlationId": "abc-123",
    "userId": "01HXYZ...",
    "requestPath": "/api/students",
    "statusCode": 200,
    "elapsed": 45
  }
}
```

Correlation IDs link all log entries from one request across services.

---

## Rate Limiting Configuration

```csharp
options.AddFixedWindowLimiter("default", opt =>
{
    opt.PermitLimit = 100;        // Max 100 requests
    opt.Window = TimeSpan.FromMinutes(1);
    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    opt.QueueLimit = 10;          // Allow 10 to queue
});
```

AI chat endpoint may need a separate, lower rate limit (AI calls are expensive).

---

## Hangfire Dashboard

**URL:** `/hangfire`  
**Access:** Admin and SuperAdmin only (custom authorization filter)  
**Features:**
- See all scheduled recurring jobs
- See job history (success/failure)
- Retry failed jobs manually
- Enqueue jobs on-demand
- Monitor job processing times
