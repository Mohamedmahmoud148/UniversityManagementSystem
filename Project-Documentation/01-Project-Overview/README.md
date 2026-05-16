# 🎓 University Management System — Project Overview

> **Version:** 1.0 | **Platform:** ASP.NET Core 9 + FastAPI + PostgreSQL  
> **Type:** AI-Powered University ERP Platform | **Scale:** Enterprise-Grade Graduation Project

---

## 📖 What Is This Project? (Simple Explanation)

Imagine a **smart university system** that:

- Lets **students** register for courses, view their grades, check their academic plan, and chat with an AI advisor at 2am when no one else is available.
- Lets **doctors (professors)** manage their courses, post exams, record attendance, send notifications to their students, and get AI-generated exam questions.
- Lets **admins** see analytics, manage the entire university structure, handle complaints, and monitor everything from a dashboard.
- Has an **AI assistant** that understands Arabic and English, knows each student's personal academic situation, and gives personalized academic advice — like a real academic advisor but available 24/7.

This is not a simple CRUD system. This is an **enterprise-grade platform** with real-time notifications, background automation, AI integration, role-based security, and academic intelligence.

---

## 🏗️ What Makes This Special?

| Feature | What It Does | Why It's Special |
|---------|-------------|-----------------|
| **AI Academic Advisor** | Answers any academic question personally | Knows your GPA, your courses, your regulations |
| **Academic Roadmap** | Shows every student their full 4-year plan | Tracks passed/failed/enrolled for each semester |
| **Auto-Enrollment** | AI can register students for available courses | One command, zero paperwork |
| **Real-Time Notifications** | SignalR push to browser the moment something happens | Zero refresh needed |
| **Exam Reminder Automation** | Sends reminders 24h and 2h before exams automatically | Never miss an exam |
| **Academic Risk Alerts** | Detects students in danger of failing | Proactive intervention |
| **Complaint Intelligence** | AI clusters and analyzes complaint patterns | Management sees trends not just complaints |
| **Multi-Regulation Support** | 9 colleges, different regulations per department | Complex academic rules handled correctly |
| **Audit Trail** | Every action is logged | Accountability and transparency |
| **Background AI Jobs** | Hangfire + AI = automated analysis while you sleep | System improves itself |

---

## 🏛️ University Structure Supported

```
University
└── College (e.g., Engineering, Science, Medicine...)
    └── Department (e.g., Computer Science, Electronics...)
        └── Batch (e.g., 2022, 2023, 2024...)
            └── Group (e.g., Group A, Group B...)
                └── Student
```

Each **Subject Offering** is the intersection of:
- A Subject (the course content)
- A Doctor (the professor teaching it)
- A Semester (when it runs)
- A Department + Batch + Group (who can take it)

---

## 🧩 Technology Stack

### Backend
| Technology | Purpose | Why Chosen |
|------------|---------|------------|
| **ASP.NET Core 9** | Main API framework | Performance, maturity, .NET ecosystem |
| **Entity Framework Core** | ORM / Database access | Type-safe queries, migrations |
| **PostgreSQL** | Primary database | ACID compliance, JSON support, scalability |
| **Hangfire** | Background jobs scheduler | Reliable, retryable background tasks |
| **SignalR** | Real-time WebSocket communication | Push notifications without polling |
| **MassTransit + RabbitMQ** | Message bus / event-driven | Decoupled async processing |
| **Serilog** | Structured logging | Searchable, structured log entries |
| **JWT + Refresh Tokens** | Authentication | Stateless, scalable auth |
| **Cloudflare R2** | File storage (S3-compatible) | Cost-effective object storage |
| **Redis** | Caching | Fast session/cache layer |
| **ULID** | Unique IDs | Sortable, URL-safe, no UUID collision risk |

### AI Layer
| Technology | Purpose |
|------------|---------|
| **FastAPI (Python)** | AI orchestration service |
| **Claude (Anthropic)** | Primary LLM for reasoning |
| **PlannerAgent** | Classifies user intent |
| **DynamicApiModule** | Routes AI to correct backend API |
| **Rule Engine** | Deterministic overrides for critical operations |

### Frontend (Expected)
| Technology | Purpose |
|------------|---------|
| **React / Next.js** | UI framework |
| **SignalR Client** | Real-time notifications |
| **JWT Storage** | Auth token management |

---

## 📊 System Scale

| Metric | Value |
|--------|-------|
| Number of API Controllers | 28 |
| Number of API Endpoints | 80+ |
| Number of Database Entities | 42 |
| Number of Background Jobs | 6 |
| Number of User Roles | 4 (SuperAdmin, Admin, Doctor, Student) |
| Number of Supported Colleges | Up to 9+ |
| AI Intents Handled | 12 classified intents |
| Languages Supported | Arabic + English |

---

## 🎯 Who Uses This System?

### 👨‍🎓 Student
- Login → View dashboard
- See enrolled courses and grades
- Chat with AI advisor
- View academic roadmap (4-year plan)
- Submit complaints
- Register for courses (manual or AI auto-enroll)
- Receive notifications
- Take online exams

### 👨‍🏫 Doctor (Professor)
- Login → View my courses
- Create and publish exams (manual or AI-generated)
- Record attendance
- Upload course materials
- View students enrolled in my courses
- Send notifications to my students
- Grade submissions

### 👨‍💼 Admin
- Manage entire university structure
- Create batches, departments, colleges
- Assign regulations to batches
- View analytics and reports
- Handle complaints
- See audit logs
- Manage all users

### 👑 SuperAdmin
- All Admin permissions
- Create other admins
- System-level configuration
- Full audit access

---

## 🗺️ High-Level System Flow

```
User Opens App
    │
    ▼
Frontend (React)
    │
    ├─── REST API Call ──────────► .NET Backend (ASP.NET Core 9)
    │                                    │
    │                                    ├── Auth: JWT validation
    │                                    ├── Business Logic: Service Layer
    │                                    ├── Data: PostgreSQL via EF Core
    │                                    ├── Files: Cloudflare R2
    │                                    └── Background: Hangfire Jobs
    │
    ├─── AI Chat ────────────────► FastAPI AI Service (Python)
    │                                    │
    │                                    ├── PlannerAgent: classify intent
    │                                    ├── DynamicApiModule: hit backend API
    │                                    ├── Claude LLM: generate response
    │                                    └── Return human-readable answer
    │
    └─── Real-Time ──────────────► SignalR Hub (/hubs/notifications)
                                         │
                                         └── Push to connected browser
```

---

## 📁 Repository Structure

```
UniversityManagementSystem/
├── UniversityManagementSystem.Core/          # Domain layer (entities, interfaces, DTOs)
│   ├── Entities/                             # 42 domain entities
│   ├── Interfaces/                           # Service contracts
│   ├── DTOs/                                 # Data transfer objects
│   ├── Exceptions/                           # Domain exceptions
│   └── Events/                              # Domain events
│
├── UniversityManagementSystem.Infrastructure/ # Data + External services
│   ├── Data/                                 # DbContext, repositories, migrations
│   ├── Services/                             # Service implementations
│   ├── Jobs/                                 # Hangfire background jobs
│   ├── Storage/                              # R2 file storage
│   └── Consumers/                           # MassTransit message consumers
│
├── UniversityManagementSystem.Api/           # Presentation layer
│   ├── Controllers/                          # 28 API controllers
│   ├── Middleware/                           # Exception handler, correlation ID
│   ├── Filters/                              # Response wrapper, Hangfire auth
│   ├── Hubs/                                 # SignalR notification hub
│   └── Services/                            # API-layer services (SignalRNotifier)
│
├── UniversityManagementSystem.Tests/         # Test suite (xUnit + FluentAssertions)
│
└── Project-Documentation/                   # This documentation
```

```
FastAPI AI Service (separate Railway service)
├── app/
│   ├── agents/
│   │   ├── planner.py          # Intent classifier
│   │   ├── base_agent.py       # Base agent class
│   │   └── schemas.py          # Pydantic models
│   ├── core/
│   │   ├── api_discovery.py    # Backend API discovery + RBAC
│   │   └── config.py           # Settings
│   └── modules/
│       └── dynamic_api.py      # Rule engine for AI-to-API routing
```
