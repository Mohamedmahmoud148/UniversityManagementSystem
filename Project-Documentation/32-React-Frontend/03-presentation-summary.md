---
render_with_liquid: false
---

# Project Presentation Summary
## University Management System — Graduation Project

> **For:** Documentation committee & project supervisor  
> **Date:** June 2026

---

## What We Built

A **complete, AI-powered university management platform** — built as a graduation project — that covers the full academic lifecycle from student enrollment to graduation. The system consists of three integrated components working together as one product.

---

## The Three Components

### 1. React Frontend (Web Application)
**Built by:** Feras Hatem  
**Tech:** React 18, Firebase, Material UI, Tailwind CSS  
**Live at:** `https://bsnu.web.app`

The web interface that students, professors, and admins use daily. Supports 5 user roles with completely separate interfaces per role.

**What it does:**
- Students take timed quizzes (auto-submit when timer ends), view course materials
- Professors upload lectures, create quizzes, track attendance, chat with AI
- AI generates quiz questions automatically from a lecture PDF upload
- Real-time engagement tracking using in-browser webcam (MediaPipe face detection — no video stored)
- Admins manage the full university structure and import users in bulk from Excel

---

### 2. .NET Backend (Core Academic Engine)
**Tech:** ASP.NET Core 9, PostgreSQL, Redis, RabbitMQ, Hangfire, SignalR  
**Deployed on:** Railway PaaS

The main academic database and business logic engine. Handles everything requiring strict relational data integrity.

**What it does:**
- Complete academic hierarchy (university → college → department → batch → group)
- Student enrollment, grades, GPA calculation (credit-hour weighted)
- Academic roadmap tracking — real semesters, passed/failed subjects, retake detection
- Exam system with per-student question randomization and AI-generated exams
- Assignments with AI auto-grading
- 8 background jobs (daily risk scoring, RAG indexing, exam reminders)
- Real-time push notifications via SignalR

---

### 3. FastAPI AI Service (Intelligent Advisor)
**Tech:** Python 3.12, FastAPI, Claude LLM, ChromaDB, Redis  

The AI brain of the platform. Every answer is grounded in the student's actual data — not a generic chatbot.

**What it does:**
- 17 AI intents handled by 15 specialized modules
- Answers academic questions in Egyptian Arabic: "كام ساعة خلصت؟", "ايه المواد اللي لازم أعيدها؟"
- Reads live student data before every answer (roadmap, grades, attendance, enrollment)
- Generates personalized weekly study plans based on GPA, deadlines, and weak subjects
- RAG (Retrieval-Augmented Generation): answers questions from lecture PDFs and regulation documents
- Generates quiz questions and exam questions from course materials

---

## Key Features to Highlight for the Committee

### 1. AI That Knows the Student
Unlike generic AI chatbots, our AI advisor pulls the student's real data before answering:
- Reads their actual grades, GPA, and enrollment history
- Knows which subjects they failed and which to retake
- Generates a concrete weekly study schedule based on their specific weak points
- Answers regulation questions from the actual indexed regulation documents (not guessing)

### 2. Quiz Engine with AI Generation
Professors can generate a full 10-question quiz from a lecture PDF in 30 seconds. Students take quizzes in a live countdown interface that auto-submits when time runs out — just like a real exam.

### 3. Real-time Engagement Tracking
Using Google MediaPipe (runs entirely in the browser — no server needed), the system tracks whether students are focused, distracted, or away during live sessions. Data is anonymized (only counts, no video). This gives instructors actionable data about class engagement.

### 4. Academic Roadmap
A student can see their complete academic journey visualized — every semester they attended, what they passed/failed, their GPA per semester, cumulative GPA progress, and what subjects they need to graduate. The roadmap is rebuilt from real enrollment data, not a static template.

### 5. Production-Grade Engineering
- Soft-delete everywhere (nothing is ever permanently lost)
- PostgreSQL filtered unique indexes (deleted records don't conflict with re-creation)
- Credit-hour weighted GPA calculation (not simple average)
- Distributed caching with Redis
- Event-driven architecture with RabbitMQ
- Rate limiting and circuit breakers for AI services

---

## Numbers

| Component | Metric |
|-----------|--------|
| Frontend pages | 30+ |
| User roles | 5 (Student, Professor, TA, Admin, SuperAdmin) |
| Backend API endpoints | 120+ |
| Database tables | 50+ |
| AI intents | 17 |
| Background jobs | 8 |
| Languages supported | Arabic (Egyptian dialect) + English |

---

## Technology Choices — Why We Chose This Stack

| Choice | Why |
|--------|-----|
| Firebase for classroom layer | Real-time updates (quizzes, attendance) without building WebSocket infrastructure |
| .NET 9 for academic core | Strong typing, EF Core migrations, production-grade ORM for complex relational data |
| FastAPI for AI | Python's ML ecosystem — LangChain, ChromaDB, sentence-transformers |
| Claude LLM | Best Arabic language quality, instruction-following, reasoning |
| PostgreSQL | ACID compliance, filtered indexes, complex queries |
| Railway deployment | Zero-config PaaS, auto-deploys from git |

---

## What Makes This Project Different From Other Graduation Projects

1. **Three integrated systems** — most graduation projects are a single monolith. We built three specialized services that communicate with each other.

2. **AI that reads live data** — not a chatbot that ignores your actual academic situation. Every AI answer starts with a real API call to fetch the student's current state.

3. **Real-time features** — live quiz taking with countdown, live attendance marking, real-time engagement tracking. Not a simple CRUD app.

4. **Production-ready code** — soft deletes, audit logs, background jobs, rate limiting, circuit breakers. Code patterns used in real enterprise applications.

5. **Bilingual Arabic-first AI** — Egyptian dialect support with deterministic keyword overrides to prevent LLM misclassification of Arabic slang.
