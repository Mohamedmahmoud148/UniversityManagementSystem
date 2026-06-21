# Complaint Intelligence Platform — Architecture

> **Version:** 2.0 (Enterprise Enhancement) | **Date:** 2026-06-21

---

## 1. System Overview

The Complaint Intelligence Platform transforms a basic complaint submission form into an AI-powered enterprise management system. Every complaint submitted by a student is automatically analyzed, categorized, clustered with similar complaints, and surfaced to the right person through smart notifications and dashboards.

```
Student Submits Complaint
         │
         ▼
  [.NET API] Saves to PostgreSQL
         │
         ├──► Doctor Notified (if targetType = Doctor)
         │
         ▼
  [Hangfire Background Job]
         │
         ▼
  [FastAPI AI Service]
    ├── Sentiment Analysis    (-1.0 to +1.0)
    ├── Category Detection    (Grading / Attendance / Technical / ...)
    ├── Severity Assessment   (low / medium / high / critical)
    ├── AI Summary            (concise 1-2 sentence summary)
    ├── Suggested Action      (recommended next step)
    └── Cluster Assignment    (DuplicateGroupId → ComplaintCluster)
         │
         ▼
  [Cluster Engine]
    ├── Existing cluster? → increment ComplaintCount, update metrics
    ├── New cluster?      → create ComplaintCluster record
    └── Count ≥ 3?        → log trending alert
         │
         ▼
  [Periodic Reports — Hangfire CRON]
    ├── Daily   (every 24h) → Admins notified
    ├── Weekly  (every 7d)  → Admins notified
    └── Monthly (every 30d) → Admins notified
```

---

## 2. Entity Model

```
Complaint (1) ──────────── (1) ComplaintAnalysis
     │                              │
     │                              └── SentimentScore, Category,
     │                                  Severity, AiSummary,
     │                                  DuplicateGroupId → ClusterId
     │
     └── StudentId (FK → SystemUser)
         TargetType, TargetId
         Status, Priority, ResolutionNote

ComplaintCluster (1) ──── (many) ClusterReply
ComplaintCluster (1) ──── (many) ClusterStatusHistory
```

### ComplaintCluster Fields
| Field | Type | Description |
|---|---|---|
| `Id` | ULID | Primary key |
| `Topic` | string | AI-detected topic name |
| `TargetType` | string | Doctor / Subject / Administration |
| `TargetId` | string | ULID of the target entity |
| `ComplaintCount` | int | Total complaints in this cluster |
| `CriticalCount` | int | Count with priority = Critical |
| `AiSummary` | string | AI-generated cluster summary |
| `AiRecommendations` | string (JSON) | List of recommended actions |
| `Status` | string | Open / Investigating / Resolved / Archived |
| `TrendDirection` | string | Increasing / Stable / Decreasing |
| `AverageSentiment` | double | Mean sentiment across cluster |
| `FirstComplaintAt` | DateTime | When the first complaint arrived |
| `LastUpdated` | DateTime | Last modification timestamp |
| `ResolvedAt` | DateTime? | When cluster was resolved |

---

## 3. New Endpoints (v2.0)

| Method | Route | Role | Description |
|---|---|---|---|
| PUT | `/api/complaints/clusters/{id}/reply` | Admin, Doctor, SuperAdmin | Reply to all students in a cluster |
| GET | `/api/complaints/clusters/{id}` | Admin, Doctor, SuperAdmin | Get single cluster with full details |
| PATCH | `/api/complaints/clusters/{id}/status` | Admin, SuperAdmin | Update cluster workflow status |
| GET | `/api/complaints/dashboard` | Admin, SuperAdmin | Full intelligence dashboard |

---

## 4. Cluster Reply Flow

```
Admin/Doctor calls PUT /clusters/{id}/reply
            │
            ├── Load cluster from DB
            ├── Find all ComplaintAnalyses WHERE DuplicateGroupId = clusterId
            ├── Load all related Complaints
            ├── For each complaint:
            │     ├── Set ResolutionNote = message
            │     ├── Set Status = "Resolved"
            │     └── Set ResolvedAt = now
            ├── For each unique StudentId:
            │     └── SendNotificationAsync (individualized)
            ├── Create ClusterReply record (reply history)
            ├── Update Cluster: Status = Resolved, ResolvedAt = now
            └── Return { clusterId, affectedStudents, notificationsSent }
```

---

## 5. Cluster Status Machine

```
Open ──────────► Investigating ──────────► Resolved ──────────► Archived
 │                     │                      │
 └─────────────────────┘                      │
 (Admin can jump states manually)             │
                                   (Can re-open if needed)
```

Every status transition is logged in `ClusterStatusHistory` with the user who made the change and an optional reason.

---

## 6. Periodic Notification Content

| Report | Trigger | Recipients | Content |
|---|---|---|---|
| Daily | Hangfire CRON daily | All Admins | Total, critical, pending, top category |
| Weekly | Hangfire CRON weekly | All Admins | Last 7 days summary |
| Monthly | Hangfire CRON monthly | All Admins | Last 30 days summary |

---

## 7. Privacy Rules

| Role | What they see |
|---|---|
| Student | Own complaints only. StudentId visible. |
| Doctor | Complaints targeting them. `studentId` = "HIDDEN". No PII. |
| Admin | All complaints. Full student profile (name, email, code). |
| SuperAdmin | Same as Admin. |

---

## 8. Tech Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 9 |
| ORM | EF Core 9 + PostgreSQL 16 |
| IDs | ULID (NUlid library) |
| Background Jobs | Hangfire |
| AI Analysis | FastAPI → Claude (OpenRouter) |
| Notifications | MassTransit + RabbitMQ → SignalR |
| Audit | AuditLog entity (append-only, never soft-deleted) |
