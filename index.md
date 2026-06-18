---
layout: default
title: University Management System
---

# University Management System

> **AI-Powered University ERP Platform**
> ASP.NET Core 9 · FastAPI · PostgreSQL · SignalR · Hangfire · Claude AI

[![Railway](https://img.shields.io/badge/Backend-Railway-blueviolet)](https://universitymanagementsystem-production-e58e.up.railway.app)
[![GitHub Pages](https://img.shields.io/badge/Docs-GitHub%20Pages-blue)](https://mohamedmahmoud148.github.io/UniversityManagementSystem/)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![FastAPI](https://img.shields.io/badge/AI-FastAPI-green)](https://fastapi.tiangolo.com/)

---

## 📁 Documentation

| # | Section | Description |
|---|---------|-------------|
| 01 | [Project Overview](Project-Documentation/01-Project-Overview/) | What the system does and who it's for |
| 02 | [System Architecture](Project-Documentation/02-System-Architecture/) | High-level architecture diagram |
| 03 | [Backend Architecture](Project-Documentation/03-Backend-Architecture/) | Clean Architecture layers |
| 04 | [Database](Project-Documentation/04-Database/) | All 50+ tables, relations, migrations |
| 05 | [AI System](Project-Documentation/05-AI-System/) | RAG, intents, planner, modules, grading |
| 06 | [API Reference](Project-Documentation/06-API-Documentation/) | Complete endpoint reference |
| 07 | [Authentication & Security](Project-Documentation/07-Authentication-and-Security/) | JWT, RBAC, roles |
| 08 | [Notifications](Project-Documentation/08-Notifications-System/) | SignalR real-time notifications |
| 09 | [Role-Based System](Project-Documentation/09-Role-Based-System/) | Permissions per role |
| 10 | [Academic Roadmap](Project-Documentation/10-Academic-Roadmap-System/) | Credit hours, GPA, graduation tracking |
| 11 | [Regulations System](Project-Documentation/11-Regulations-System/) | Study plans and regulations |
| 12 | [Complaint System](Project-Documentation/12-Complaint-System/) | AI-powered complaint analysis |
| 13 | [Exams System](Project-Documentation/13-Exams-System/) | Randomized exams, auto-grading |
| 14 | [Analytics System](Project-Documentation/14-Analytics-System/) | Admin dashboards and reports |
| 15 | [Deployment & Infrastructure](Project-Documentation/15-Deployment-and-Infrastructure/) | Cloudflare, Railway, R2 |
| 16 | [Project Flow](Project-Documentation/16-Project-Flow/) | End-to-end system flows |
| 17 | [Frontend Guide](Project-Documentation/17-Frontend-Guide/) | Frontend integration guide |
| 18 | [Developer Onboarding](Project-Documentation/18-Developer-Onboarding/) | Setup and getting started |
| 19 | [Discussion Preparation](Project-Documentation/19-Discussion-Preparation/) | Committee presentation guide |
| 20 | [AI Tools APIs](Project-Documentation/20-AI-Tools-APIs/) | Internal AI tool endpoints |
| 21 | [System Diagrams](Project-Documentation/21-Diagrams/) | Architecture, flows, ERD diagrams |
| 22 | [Deletion Framework](Project-Documentation/22-Deletion-Framework/) | Intelligent cascaded deletion |
| 23 | [Academic Architecture](Project-Documentation/23-Academic-Architecture-Redesign/) | Enterprise academic redesign |
| 24 | [Frontend Handoff](Project-Documentation/24-Frontend-Handoff/) | Registration & GPA system handoff |
| 25 | [Database Seed](Project-Documentation/25-Database-Seed-Investigation/) | Seed data investigation |
| 26 | [RAG System](Project-Documentation/26-RAG-System/) | 🆕 Retrieval-Augmented Generation pipeline |
| 27 | [Assignments System](Project-Documentation/27-Assignments-System/) | 🆕 Assignments + AI auto-grading |
| 28 | [Proactive Alerts](Project-Documentation/28-Proactive-Alerts/) | 🆕 Academic risk scoring + notifications |
| 29 | [API Reference — Full Endpoint List](Project-Documentation/29-API-Reference/) | 🆕 All 33 controllers, roles, request/response |
| 30 | [Feature Guide — "How to do X"](Project-Documentation/30-Feature-Guide/) | 🆕 Step-by-step guide for every feature |
| 31 | [What Makes Us Different](Project-Documentation/31-What-Makes-Us-Different/) | 🆕 Comparison vs typical university systems |
| 32 | [React Frontend — Complete Guide](Project-Documentation/32-React-Frontend/) | 🆕 React 18 + Firebase + AI — full frontend docs |

---

## ⚡ Quick Reference

### User Roles

| Role | Permissions |
|------|-------------|
| `SuperAdmin` | Everything + create admins + audit logs |
| `Admin` | Manage structure, users, analytics, complaints |
| `Doctor` | Courses, exams, grades, assignments, attendance |
| `Student` | View own data, enroll, AI chat, submit assignments |

### Key API Endpoints

```
POST   /api/auth/login                          → Get JWT token
GET    /api/regulations/my-roadmap              → Student academic plan
POST   /api/enrollments/auto-enroll             → AI auto-registration
POST   /api/chat                                → AI conversation (13 intents)
POST   /api/rag/index/{materialId}              → Index material for RAG
POST   /api/rag/search                          → Semantic search in materials
POST   /api/assignments                         → Create assignment (Doctor)
POST   /api/assignments/{id}/submit             → Submit assignment (Student)
POST   /api/assignments/submissions/{id}/ai-grade → AI grade submission
GET    /api/analytics/dashboard/admin           → Admin KPI dashboard
GET    /api/analytics/dashboard/student         → Student performance
GET    /api/risk/dashboard                      → At-risk students overview
POST   /api/proctoring/event                    → Record exam proctoring event
WS     /hubs/notifications                      → SignalR real-time connection
```

### AI Capabilities (13 Intents)

| Student Says | AI Does |
|-------------|---------|
| "اشرحلي الـ recursion من المحاضرة" | RAG search → answers from course material only |
| "كام ساعة معتمدة خلصت؟" | Calls `/api/regulations/my-roadmap` → real data |
| "سجلني في المواد" | Calls `POST /api/enrollments/auto-enroll` |
| "اعمل امتحان" | Generates exam via LLM → saves to database |
| "من المادة دي اشرحلي الفصل الأول" | `material_qa` → grounded answer from PDF |

### Background Jobs

| Job | Schedule | Purpose |
|-----|----------|---------|
| `AcademicRiskJob` | Daily 6:00 AM | Risk scoring per student (attendance + grades) |
| `RagIndexingJob` | Daily | Index all un-indexed course materials |
| `ExamReminderJob` | Every 30 min | Remind students of upcoming exams |
| `ComplaintIntelligenceJob` | Daily/Weekly | AI complaint clustering & analysis |

---

## 🏗️ Architecture

```
Browser / Mobile App
        │
        ├── REST API ──────────► .NET Backend (ASP.NET Core 9)
        │                              ├── 34 Controllers
        │                              ├── 40+ Services
        │                              ├── PostgreSQL (50+ tables)
        │                              ├── Hangfire (8 background jobs)
        │                              └── Cloudflare R2 (file storage)
        │
        ├── AI Chat ───────────► FastAPI (Python)
        │                              ├── PlannerAgent (13 intents)
        │                              ├── RAG Pipeline (ChromaDB)
        │                              ├── MaterialQAModule
        │                              ├── ExamGenerationModule
        │                              ├── AI Auto-Grading
        │                              └── Advanced Memory (Redis)
        │
        └── WebSocket ─────────► SignalR Hub
                                       └── Proactive AI alerts
```

---

*Last updated: 2026-05-23 · Version: 2.0*
