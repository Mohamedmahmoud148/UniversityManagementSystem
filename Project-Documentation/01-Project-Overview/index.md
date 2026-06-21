# UniSys — University Management System
## Project Overview

> **Last refreshed:** 2026-05-31 | **Source of truth:** live codebase

---

## 1. What is UniSys?

UniSys is a full-stack, AI-powered university management platform built for real academic institutions. It replaces paper-based and siloed legacy systems with a single integrated environment covering every stage of a student's academic journey — from enrolment to graduation.

Three user roles share one unified backend:

| Role | Primary Responsibilities |
|------|-------------------------|
| **Student** | Course registration, grades, materials, assignments, exams, AI advisor |
| **Doctor (Instructor)** | Offerings, attendance, AI exam generation, grading, notifications |
| **Admin / SuperAdmin** | University structure, analytics, complaints, regulations, bulk import |

---

## 2. Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend API | ASP.NET Core | 9.0 |
| AI Orchestration | FastAPI (Python) | 3.12 |
| Primary Database | PostgreSQL | 16 |
| Cache | Redis | 7 |
| Message Bus | RabbitMQ | 3.13 |
| Vector Store | ChromaDB | 0.5 |
| LLM Gateway | OpenRouter | — |
| File Storage | Cloudflare R2 | — |
| Background Jobs | Hangfire | 1.8 |
| Real-time Push | SignalR | — |
| ORM | Entity Framework Core | 9 |
| Authentication | JWT Bearer | — |
| Deployment | Railway (PaaS) | — |
| Frontend | React 18 + TypeScript + MUI + Tailwind | — |
| Frontend Deployment | Firebase Hosting (CDN only) | — |

---

## 3. Architecture at a Glance

```
┌─────────────────────────────────────────┐
│     React Frontend (bsnu.web.app)        │
└────────────────┬────────────────────────┘
                 │ HTTPS + JWT
┌────────────────▼────────────────────────┐
│      .NET 9 Backend API (Railway)        │
│  35 Controllers · SignalR · Hangfire     │
│  EF Core · MassTransit · Rate Limiter   │
└───┬──────────────────┬───────────────────┘
    │                  │ HTTP (internal)
    │              ┌───▼─────────────────┐
    │              │ FastAPI AI Service   │
    │              │ 15 modules · 17 intents│
    │              │ RAG · Memory · Planner│
    │              └───┬─────────────────┘
    │                  │
    ▼                  ▼
PostgreSQL          ChromaDB (vectors)
Redis               OpenRouter (LLM)
RabbitMQ            Cloudflare R2 (files)
```

---

## 4. Core Feature Set

### Academic Structure
- University → College → Department → Batch → Group hierarchy
- Subject catalogue with credit hours and prerequisites
- Subject Offerings (semester instances: Doctor + Students + Grading config)
- Academic years and semester lifecycle

### Student Lifecycle
- Registration with document upload and validation
- Course enrolment (manual + AI-assisted auto-enrol)
- Grade tracking (letter grades, GPA, credit hours)
- Credit-hour-weighted GPA, updated in real time
- Daily academic risk scoring (attendance + GPA)
- Graduation readiness: roadmap vs regulation comparison

### Materials & Learning Content
- Upload any academic file type up to **500 MB** (PDF, DOCX, PPTX, XLSX, ZIP, images, video)
- Stored in Cloudflare R2 with 60-minute signed download URLs
- Automatic RAG indexing into ChromaDB (on upload + daily Hangfire job)
- Students query AI for answers grounded in indexed course materials

### Assignments
- Deadline enforcement, late-submission flag, per-assignment rubrics
- Text or file submission (up to 100 MB)
- Manual grading + optional AI grading (essay scoring via OpenRouter)
- Automated 24h/2h deadline reminders — only to students who haven't submitted

### Examinations
- Multi-type questions: MCQ, True/False, Short Answer, Essay
- Per-student randomization (different question order per student)
- AI-generated exams from doctor prompt or course material
- Live proctoring metadata (tab-switch, focus-loss events)
- Auto-grading for objective questions; essay grading via AI

### AI Academic Advisor (Headline Feature)
- **17 intents** handled by **15 specialized FastAPI modules**
- All answers grounded in student's actual data: roadmap, grades, attendance, assignments, exam schedule, regulations
- **Study Plan Generator**: personalized weekly/daily schedule based on GPA, weak subjects, deadlines
- Bilingual: Arabic (Egyptian dialect) + English
- Deterministic keyword overrides prevent LLM misclassification
- Conversation memory persisted in Redis
- Academic regulation Q&A via RAG over indexed PDF regulations

### Notifications & Real-time
- In-app notifications via SignalR WebSocket hub
- Event-driven delivery: RabbitMQ → MassTransit → SignalR push
- Automated: exam reminders (30 min), assignment deadline (30 min), academic risk alerts
- Doctor broadcast to all enrolled students

### Complaints & Intelligence
- Students submit complaints with priority (Normal / High / Critical) and target type
- AI generates daily / weekly / monthly intelligence reports (Hangfire)
- Admin analytics dashboard with complaint trends

### Analytics & Reporting
- Admin dashboard: 10 KPIs (students, doctors, offerings, enrolments, colleges, departments, batches, avg GPA, pass rate, at-risk count)
- Department comparison, grade distribution, attendance trends, course performance
- Student personal performance dashboard
- Doctor course analytics with per-offering stats

---

## 5. Project Metrics

| Metric | Count |
|--------|-------|
| Backend Controllers | 35 |
| API Endpoints | 120+ |
| AI Modules (FastAPI) | 15 |
| AI Intents | 17 |
| Recurring Background Jobs | 7 |
| Database Entities | 30+ |
| Supported File Types | 14 MIME types |
| Max File Upload Size | 500 MB (materials) / 100 MB (submissions) |

---

## 6. What Makes UniSys Different

| Differentiator | Detail |
|---------------|--------|
| **AI-first architecture** | AI advisor reads live student data before every answer — not a generic chatbot |
| **RAG-grounded accuracy** | Regulations and materials answered from indexed documents, not LLM memory |
| **Personalized study plans** | Concrete weekly schedule based on GPA, deadlines, and weak subjects |
| **Arabic-native** | Egyptian dialect support with deterministic keyword overrides |
| **Production-grade** | Circuit breakers, rate limiting, soft deletes, audit logs, distributed caching |
| **Scalable background pipeline** | 7 Hangfire recurring jobs handling reminders, risk analysis, RAG indexing |

---

## 7. Documentation Map

| Section | Contents |
|---------|----------|
| [02 System Architecture](../02-System-Architecture/index.md) | C4 diagrams, context/component maps |
| [03 Backend Architecture](../03-Backend-Architecture/index.md) | Clean Architecture, service layer, patterns |
| [04 Database](../04-Database/index.md) | ERD, entity relationships, migration strategy |
| [05 AI System](../05-AI-System/index.md) | Intents, modules, RAG pipeline, memory system |
| [06 API Documentation](../06-API-Documentation/index.md) | All endpoints with request/response shapes |
| [07 Authentication & Security](../07-Authentication-and-Security/index.md) | JWT, RBAC, rate limiting |
| [08 Notifications](../08-Notifications-System/index.md) | SignalR hub, MassTransit, Hangfire reminders |
| [13 Exams System](../13-Exams-System/index.md) | Exam lifecycle, randomization, proctoring, grading |
| [14 Analytics](../14-Analytics-System/index.md) | Dashboard KPIs, comparison endpoints |
| [15 Deployment](../15-Deployment-and-Infrastructure/index.md) | Railway, environment variables, CI/CD |
| [21 Diagrams](../21-Diagrams/index.md) | All Mermaid source diagrams |
| [27 Assignments](../27-Assignments-System/index.md) | Assignment lifecycle, AI grading |
