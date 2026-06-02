# 00 — Project Overview

## Platform Name
**University Management System — AI Academic Companion Platform**

## What This Document Is
This is a machine-readable frontend specification package. It contains every detail a Claude Code instance or frontend engineer needs to build the complete platform UI without any additional clarification.

---

## Platform Mission
Transform a traditional University Management System into an AI-powered Academic Companion Platform that:
- Helps **students** learn, track progress, and stay motivated
- Helps **doctors** monitor performance, identify risks, and improve teaching
- Helps **admins** manage the university system with AI-powered insights

---

## Platform Scale

| Metric | Value |
|--------|-------|
| Backend | .NET 8 REST API |
| AI Service | FastAPI (Python) |
| Total API Endpoints | 300+ |
| User Roles | 5 (SuperAdmin, Admin, Doctor, TeachingAssistant, Student) |
| Total Pages | 65+ |
| Total Reusable Components | 80+ |
| Real-time Features | SignalR notifications |
| Languages | Arabic (RTL) + English (LTR) |

---

## Technology Stack (Frontend)

| Layer | Technology |
|-------|-----------|
| Framework | React 18 + TypeScript |
| Routing | React Router v6 |
| State Management | Zustand + React Query (TanStack) |
| UI Library | Shadcn/UI + Radix UI primitives |
| Styling | Tailwind CSS |
| Charts | Recharts |
| Forms | React Hook Form + Zod |
| HTTP | Axios |
| Real-time | Socket.io or native WebSocket (SignalR) |
| i18n | react-i18next |
| Animations | Framer Motion |
| Icons | Lucide React |
| Date handling | date-fns |
| Excel export | SheetJS (xlsx) |
| File upload | react-dropzone |

---

## Backend Services

| Service | URL | Purpose |
|---------|-----|---------|
| .NET API | `VITE_API_BASE_URL` | All core CRUD, auth, analytics |
| FastAPI AI | `VITE_AI_SERVICE_URL` | Chat, AI companion, teaching intelligence |
| SignalR Hub | `/hubs/notifications` | Real-time notifications |

---

## Environment Variables Required

```env
VITE_API_BASE_URL=https://api.university.edu
VITE_AI_SERVICE_URL=https://ai.university.edu
VITE_APP_NAME=University AI Platform
VITE_DEFAULT_LOCALE=ar
```

---

## Authentication Model

- **JWT Bearer tokens** — stored in `localStorage` as `access_token`
- **Refresh tokens** — stored in `localStorage` as `refresh_token`
- Token auto-refresh on 401 response via Axios interceptor
- Role encoded in JWT claims: `role` claim = `Student | Doctor | Admin | SuperAdmin | TeachingAssistant`
- On login → decode JWT → store `userProfile` in Zustand store

---

## Localization Requirements

- **Default**: Arabic (RTL) — `ar`
- **Secondary**: English (LTR) — `en`
- Language toggle in all navbar/settings
- RTL support via Tailwind `dir` attribute on `<html>` element
- All date/number formatting uses `Intl` API with locale

---

## Core User Flows Summary

1. **Student**: Login → Dashboard → AI Chat → Companion → Exams → Materials → Profile
2. **Doctor**: Login → Teaching Intelligence Dashboard → Class Analytics → Exam Management → AI Assistant
3. **Admin**: Login → System Dashboard → User Management → Analytics → Settings
4. **SuperAdmin**: Login → Full system control + all admin capabilities

---

## Key Feature Modules

| Module | Pages | Roles |
|--------|-------|-------|
| Authentication | 3 | All |
| Student Dashboard | 12 | Student |
| Doctor Intelligence | 10 | Doctor |
| Admin Management | 15 | Admin, SuperAdmin |
| AI Chat | 1 (full screen) | All |
| AI Companion | 8 | Student, Doctor |
| Teaching Intelligence | 7 | Doctor, SuperAdmin |
| Exam Platform | 8 | Student, Doctor, Admin |
| Assignments | 5 | Student, Doctor |
| Attendance | 4 | Student, Doctor, Admin |
| Materials | 4 | Student, Doctor, Admin |
| Complaints | 4 | Student, Doctor, Admin |
| Notifications | 2 | All |
| Profile & Settings | 3 | All |

---

## Development Conventions

- **File naming**: `PascalCase` for components, `camelCase` for utilities
- **Folder structure**: Feature-based (`src/features/exams/`, `src/features/companion/`)
- **API calls**: All in `src/api/` folder, one file per controller
- **Types**: All TypeScript interfaces in `src/types/` folder
- **i18n keys**: `snake_case` — e.g., `student.dashboard.welcome`
- **Responsive breakpoints**: `sm:640px`, `md:768px`, `lg:1024px`, `xl:1280px`

---

## Platform Vision: Premium SaaS Quality

The UI quality should match:
- **Duolingo** — engagement, streaks, gamification
- **Linear** — speed, keyboard shortcuts, clean data tables
- **Notion** — structured content, progressive disclosure
- **Stripe** — typography, spacing, metric cards
- **GitHub** — code-quality analytics, status indicators
- **Vercel** — dark/light theme, deployment-style dashboards
