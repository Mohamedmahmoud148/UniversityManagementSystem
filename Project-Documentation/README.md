# 📚 University Management System — Master Documentation

> **AI-Powered University ERP Platform**  
> ASP.NET Core 9 · FastAPI · PostgreSQL · SignalR · Hangfire · Claude AI

---

## 📁 Documentation Map

| # | Section | Who Should Read |
|---|---------|----------------|
| [01](./01-Project-Overview/README.md) | **Project Overview** | Everyone — start here |
| [02](./02-System-Architecture/README.md) | **System Architecture** | All developers, committee |
| [03](./03-Backend-Architecture/README.md) | **Backend Architecture** | Backend developers |
| [04](./04-Database/README.md) | **Database — All Tables & Relations** | Backend devs, committee |
| [05](./05-AI-System/README.md) | **AI System — Deep Dive** | AI engineers, committee |
| [06](./06-API-Documentation/README.md) | **Complete API Reference** | Frontend + Backend devs |
| [07](./07-Authentication-and-Security/README.md) | **Authentication & Security** | All developers |
| [08](./08-Notifications-System/README.md) | **Notifications System** | Frontend devs, committee |
| [09](./09-Role-Based-System/README.md) | **Role-Based System** | All developers |
| [10](./10-Academic-Roadmap-System/README.md) | **Academic Roadmap** | All — key feature |
| [11](./11-Regulations-System/README.md) | **Regulations System** | Backend devs |
| [12](./12-Complaint-System/README.md) | **Complaint System + AI** | Committee |
| [13](./13-Exams-System/README.md) | **Exams System** | All developers |
| [14](./14-Analytics-System/README.md) | **Analytics System** | Admin features |
| [15](./15-Deployment-and-Infrastructure/README.md) | **Deployment & Infrastructure** | DevOps, committee |
| [16](./16-Project-Flow/README.md) | **Complete System Flows** | Everyone — visual guide |
| [17](./17-Frontend-Guide/README.md) | **Frontend Developer Guide** | Frontend developers |
| [18](./18-Developer-Onboarding/README.md) | **Developer Onboarding** | New developers |
| [19](./19-Discussion-Preparation/README.md) | **Discussion Preparation** | Committee presentation |
| [20](./20-AI-Tools-APIs/README.md) | **AI Tools APIs** | Backend + AI engineers |
| — | [**Randomized Exam Guide**](./RANDOMIZED_EXAM_FRONTEND_GUIDE.md) | Frontend developers |

---

## ⚡ Quick Reference

### User Roles
| Role | Can Do |
|------|--------|
| `SuperAdmin` | Everything + create admins + audit logs |
| `Admin` | Manage structure, users, analytics, complaints |
| `Doctor` | Manage courses, exams, grades, send notifications |
| `Student` | View own data, enroll, chat with AI, submit complaints |

### Key API Endpoints
```
POST   /api/auth/login                    → Get JWT token
GET    /api/regulations/my-roadmap        → Student's full academic plan
POST   /api/enrollments/auto-enroll       → AI auto-registration
POST   /api/notification/send-to-my-students → Doctor broadcasts to students
GET    /api/analytics/summary             → Admin dashboard stats
POST   /api/chat                          → AI conversation
GET    /health                            → System health check
WS     /hubs/notifications                → SignalR real-time connection
```

### AI Capabilities
| User Says | AI Does |
|-----------|---------|
| "كام ساعة خلصت؟" | Calls /api/regulations/my-roadmap → answers from real data |
| "سجلني في المواد" | Calls POST /api/enrollments/auto-enroll |
| "اعمل امتحان" | Calls POST /api/exams/generate-ai |
| "كام طالب في كل قسم؟" | Calls GET /api/analytics/student-count-by-department |

### Background Jobs
| Job | When | What |
|-----|------|------|
| AcademicRiskJob | Daily midnight | Alert students with GPA < 2.0 |
| ExamReminderJob | Every 30 min | Remind students of upcoming exams |
| ComplaintIntelligenceJob | Daily/Weekly/Monthly | AI complaint analysis reports |

---

## 🏗️ Architecture at a Glance

```
Browser/App
    │
    ├──── REST API ────────────► .NET Backend (ASP.NET Core 9)
    │                                 ├── 28 Controllers
    │                                 ├── 33 Services  
    │                                 ├── PostgreSQL (42 tables)
    │                                 ├── Hangfire (6 background jobs)
    │                                 └── Cloudflare R2 (files)
    │
    ├──── AI Chat ────────────► FastAPI (Python)
    │                                 ├── PlannerAgent (Claude LLM)
    │                                 ├── Layer-2 Keyword Engine
    │                                 └── DynamicApiModule (rule engine)
    │
    └──── WebSocket ──────────► SignalR Hub
                                      └── Real-time notifications
```

---

*Generated: 2026-05-16 | Version: 1.0*
