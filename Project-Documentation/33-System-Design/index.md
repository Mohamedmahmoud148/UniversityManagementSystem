# System Design Documentation — University Management System

## Overview

This folder contains the complete system design documentation for the University Management System graduation project. The system is composed of three tightly integrated components: a .NET backend for academic records, a FastAPI AI service for intelligent student assistance, and a React frontend for the user interface.

The documentation is organized into nine documents, each covering a distinct aspect of the system architecture. Together, they provide a comprehensive technical reference suitable for graduation committee review and future development handover.

---

## Document Index

| # | Document | Description |
|---|----------|-------------|
| 01 | [High-Level Architecture](01_HIGH_LEVEL_ARCHITECTURE.md) | System-wide overview, three-tier model, component interactions, external services |
| 02 | [Database Design](02_DATABASE_DESIGN.md) | ER diagram, all major tables, relationships, indexes, GPA schema, soft-delete strategy |
| 03 | [API Design](03_API_DESIGN.md) | REST principles, JWT lifecycle, endpoint groups, request/response conventions, error handling |
| 04 | [AI System Design](04_AI_SYSTEM_DESIGN.md) | Orchestrator, RAG pipeline, Redis memory, modules, intent classification, LLM strategy |
| 05 | [Frontend Architecture](05_FRONTEND_ARCHITECTURE.md) | Component hierarchy, JWT auth, SignalR real-time, state management, exam engine, proctoring hook |
| 06 | [Security Design](06_SECURITY_DESIGN.md) | Authentication, RBAC at 4 levels, data protection, CORS |
| 07 | [Data Flow Diagrams](07_DATA_FLOW_DIAGRAMS.md) | Mermaid sequence diagrams for 7 key system flows |
| 08 | [Scalability and Deployment](08_SCALABILITY_AND_DEPLOYMENT.md) | Railway, Firebase, ChromaDB, Redis, connection pooling, Hangfire, recommendations |
| 09 | [Design Decisions](09_DESIGN_DECISIONS.md) | Rationale for every major architectural choice |

---

## System at a Glance

### Components

```
University Management System
├── .NET Backend (ASP.NET Core 9)
│   ├── 35 REST Controllers, 120+ endpoints
│   ├── PostgreSQL database (50+ tables)
│   ├── JWT Bearer authentication
│   ├── SignalR real-time notifications
│   ├── Hangfire background jobs (8 jobs)
│   └── Cloudflare R2 file storage
│
├── FastAPI AI Service (Python 3.12)
│   ├── 15 modules, 17 intents
│   ├── Claude LLM via OpenRouter
│   ├── ChromaDB vector database (RAG)
│   └── Redis conversation memory
│
└── React Frontend (React 18)
    ├── JWT Auth (from .NET backend)
    ├── SignalR (real-time notifications)
    ├── Axios (REST calls to .NET + FastAPI)
    ├── Firebase Hosting (CDN deployment only)
    └── Material UI + Tailwind CSS
```

### Roles

| Role | Primary Access |
|------|---------------|
| Student | Courses, grades, quizzes, AI chat, schedule, roadmap |
| Professor | Subject management, assignments, exams, grading, engagement |
| Assistant | Attendance, announcements, lecture upload |
| Admin | User management, announcements, system data |
| SuperAdmin | Full system access, regulations, system configuration |

### Technology Stack Summary

| Layer | Technology |
|-------|-----------|
| Web API | ASP.NET Core 9 + EF Core 9 |
| Database | PostgreSQL 16 |
| AI Service | FastAPI + Python 3.12 |
| LLM | Claude (claude-sonnet) via OpenRouter |
| Vector DB | ChromaDB |
| Cache / Memory | Redis |
| Frontend | React 18 + TypeScript |
| Realtime | SignalR (.NET) |
| Auth | JWT Bearer — issued by .NET backend |
| File Storage | Cloudflare R2 |
| Deployment (backend) | Railway PaaS (.NET + FastAPI) |
| Deployment (frontend) | Firebase Hosting (CDN only) |

---

## How to Read This Documentation

1. Start with **01_HIGH_LEVEL_ARCHITECTURE.md** for a mental model of the whole system.
2. Read **02_DATABASE_DESIGN.md** to understand the data model driving the .NET backend.
3. Read **04_AI_SYSTEM_DESIGN.md** to understand how the AI service works end-to-end.
4. Use **07_DATA_FLOW_DIAGRAMS.md** to trace specific user journeys through the system.
5. Consult **09_DESIGN_DECISIONS.md** for the "why" behind each major choice.
