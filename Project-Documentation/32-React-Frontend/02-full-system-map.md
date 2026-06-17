---
render_with_liquid: false
---

# Full System Map — Three Components Together

> How the React Frontend, .NET Backend, and FastAPI AI Service fit together as one complete platform.

---

## Complete Architecture

```
╔══════════════════════════════════════════════════════════════════╗
║                    UNIVERSITY MANAGEMENT SYSTEM                   ║
║                    mشروع التخرج — Graduation Project              ║
╚══════════════════════════════════════════════════════════════════╝

┌─────────────────────────────────────────────────────────────────┐
│                    REACT FRONTEND                                │
│                    bsnu.web.app                                 │
│                    React 18 + Firebase + MUI + Tailwind         │
│                                                                 │
│  Student Portal  │  Professor Portal  │  Admin Portal           │
└──────┬──────────────────┬────────────────────┬──────────────────┘
       │                  │                    │
       │ Firebase SDK      │ REST (Axios)        │ Firebase Callable
       ▼                  ▼                    ▼
┌──────────────┐  ┌──────────────────┐  ┌────────────────────────┐
│  FIREBASE    │  │  .NET BACKEND    │  │   FASTAPI AI SERVICE   │
│              │  │  ASP.NET Core 9  │  │   Python 3.12          │
│ Firestore    │  │  Railway PaaS    │  │                        │
│ Auth         │  │  35 Controllers  │  │  15 AI Modules         │
│ Storage      │  │  120+ Endpoints  │  │  17 Intent Handlers    │
│ Functions    │  │  PostgreSQL      │  │  RAG + Memory          │
│              │  │  Redis + RabbitMQ│  │  Claude LLM            │
└──────────────┘  │  Hangfire Jobs   │  │  ChromaDB vectors      │
                  │  SignalR Hub     │  │  OpenRouter gateway    │
                  └──────────────────┘  └────────────────────────┘
```

---

## Who Owns What Data

| Data Type | Owned By | Why |
|-----------|---------|-----|
| User authentication tokens | Firebase Auth | Real-time auth, custom role claims |
| Quiz definitions & submissions | Firebase Firestore | Real-time updates for live quizzes |
| Attendance records | Firebase Firestore | Live session marking with onSnapshot |
| Engagement metrics | Firebase Firestore | Streamed from MediaPipe → Functions |
| Course materials (files) | Firebase Storage | Direct browser upload, signed URLs |
| AI chat conversations (professor) | Firebase Firestore | Streamed responses via onSnapshot |
| Campus buildings & rooms | Firebase Firestore | Admin CRUD, not academic-critical |
| Students, doctors, admins | PostgreSQL (.NET) | Relational academic data |
| Enrollments | PostgreSQL (.NET) | Complex enrollment rules + EF Core |
| Grades & GPA | PostgreSQL (.NET) | Weighted calculations, grade history |
| Academic regulations (لائحة) | PostgreSQL (.NET) | Document + subject relationships |
| Academic roadmap | PostgreSQL (.NET) | Journey-based semester tracking |
| Assignments & submissions | PostgreSQL (.NET) | Deadline enforcement, file storage |
| Notifications | PostgreSQL + SignalR | Event-driven real-time push |
| AI advisor conversations | Redis (.NET) | Session memory, ephemeral |
| Course material embeddings | ChromaDB (FastAPI) | Semantic vector search for RAG |

---

## User Journeys Across All Three Systems

### Student — Complete Academic Journey

```
1. SIGN IN
   Firebase Auth → custom claim "student" → routed to /student

2. VIEW COURSES
   Firebase Firestore (courseAssignments) → list of enrolled courses

3. TAKE QUIZ
   Firebase Firestore (quizzes) → countdown timer
   On submit → Firebase Firestore (quizSubmissions) stored

4. VIEW LECTURE MATERIALS
   Firebase Firestore (materials metadata)
   Firebase Storage (PDF download URL)

5. CHECK ACADEMIC GRADES (planned)
   .NET Backend → GET /api/grades/my-grades
   JWT token from .NET auth

6. VIEW ACADEMIC ROADMAP (planned)
   .NET Backend → GET /api/regulations/my-roadmap
   Full semester journey with GPA + activities

7. CHAT WITH AI ADVISOR (planned)
   FastAPI → POST /chat
   AI reads roadmap + grades + attendance from .NET
   Returns Arabic-language academic advice
```

### Professor — Teaching Journey

```
1. SIGN IN
   Firebase Auth → custom claim "professor" → /prof

2. UPLOAD LECTURE MATERIALS
   Firebase Storage (PDF upload)
   Firestore (metadata record created)
   FastAPI RAG indexing job runs daily (ChromaDB)

3. CREATE AI QUIZ FROM LECTURE
   Browser POSTs PDF to FastAPI /api/generate-quiz
   Returns 10 auto-generated questions
   Professor edits & publishes → Firestore

4. TAKE ATTENDANCE IN SESSION
   Firestore (sessions collection)
   Firebase Function setAttendance → writes records

5. MONITOR ENGAGEMENT
   EngagementTracker (MediaPipe in browser)
   Firebase Function pushEngagement → Firestore aggregates

6. CHAT WITH AI ABOUT COURSE
   Firebase Function courseAiAssistant
   Grounded in uploaded lecture materials

7. GENERATE AI EXAM (planned)
   .NET Backend → POST /api/exams/generate-ai
   Creates exam with randomized questions per student
```

### Admin — Management Journey

```
1. SIGN IN
   Firebase Auth → custom claim "admin" → /admin

2. MANAGE ACADEMIC STRUCTURE
   Firebase Firestore (colleges → years → departments → courses)

3. ASSIGN PROFESSORS TO COURSES
   Firebase Firestore (courseAssignments)

4. BULK IMPORT USERS FROM EXCEL
   xlsx.js parses file client-side
   Firebase Function bulkCreateUsers creates Auth + Firestore records

5. MANAGE CAMPUS BUILDINGS
   Firebase Firestore (campusBuildings hierarchy)

6. VIEW ANALYTICS (planned integration)
   .NET Backend → GET /api/analytics/dashboard/admin
   10 KPIs: students, doctors, GPA, at-risk count

7. HANDLE COMPLAINTS (planned integration)
   .NET Backend → GET /api/complaints
   AI-generated intelligence reports
```

---

## Integration Points Between Systems

### Currently Active Integrations

| From | To | Integration | Feature |
|------|----|------------|---------|
| React Frontend | Firebase | Firebase SDK | Auth, Firestore, Storage, Functions |
| React Frontend | FastAPI | HTTP POST (fetch) | AI quiz generation from PDF |
| .NET Backend | FastAPI | HTTP (internal) | AI academic advisor, study plan |
| .NET Backend | FastAPI | HTTP (internal) | RAG search over materials |
| FastAPI | .NET Backend | HTTP | Roadmap data, enrollment data, grades |

### Planned Integrations

| From | To | Integration | Feature |
|------|----|------------|---------|
| React Frontend | .NET Backend | Axios REST | Grades, GPA, roadmap display |
| React Frontend | FastAPI | WebSocket/REST | AI advisor chat in UI |
| React Frontend | .NET Backend | REST | Assignment submission from UI |

---

## Key Numbers Across All Three Systems

| Metric | Value |
|--------|-------|
| **Frontend** | |
| React pages | 30+ |
| User roles supported | 5 |
| Firebase collections | 15+ |
| Firebase Functions | 4 |
| **Backend (.NET)** | |
| API controllers | 35 |
| API endpoints | 120+ |
| Database entities | 30+ |
| Background jobs (Hangfire) | 8 |
| Database tables | 50+ |
| **AI Service (FastAPI)** | |
| AI modules | 15 |
| AI intents | 17 |
| LLM: Claude (via OpenRouter) | 1 |
| Vector store collections | Per-material |
| **Overall** | |
| Total lines of code (est.) | 80,000+ |
| Supported languages | Arabic + English |
| Live deployment | Firebase Hosting + Railway |
