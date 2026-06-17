# 01 — Project Overview
## University Management System — React Frontend
### Enterprise Documentation | Graduation Project 2026

---

## Table of Contents

1. [Project Idea & Vision](#1-project-idea--vision)
2. [Business Goals](#2-business-goals)
3. [User Roles & Personas](#3-user-roles--personas)
4. [Core Modules](#4-core-modules)
5. [Features Summary](#5-features-summary)
6. [Technology Stack Overview](#6-technology-stack-overview)
7. [System Boundaries](#7-system-boundaries)
8. [Project Metrics](#8-project-metrics)

---

## 1. Project Idea & Vision

The University Management System (UniSys) is a full-stack, AI-powered academic platform designed to digitize and automate every phase of a student's university life — from enrollment to graduation.

The **React Frontend** is the web interface layer of this platform, deployed at `https://bsnu.web.app`. It is the primary point of interaction for five distinct user personas, each seeing a completely different application tailored to their responsibilities.

### Vision Statement

> Replace fragmented, paper-based, and siloed university processes with a single, intelligent web application that serves students, professors, assistants, and administrators in real time.

### What Problem Does It Solve?

| Before UniSys | After UniSys |
|--------------|-------------|
| Paper-based attendance sheets | Digital real-time attendance with live counters |
| Email attachments for quizzes | Structured quiz engine with auto-grading and timers |
| Manual exam question writing | AI generates quiz questions from a lecture PDF in 30 seconds |
| No insight into student attention | In-browser engagement tracking using webcam + MediaPipe AI |
| Excel spreadsheets for course management | Structured Firestore hierarchy with real-time sync |
| Manual bulk user creation | Excel upload → Firebase Functions → 100s of users in minutes |
| Static room timetables | Interactive campus building → floor → room → schedule management |

---

## 2. Business Goals

### Primary Goals

1. **Digitize Classroom Operations** — Replace all paper-based attendance, quiz, and material-sharing processes.
2. **Enable AI-Assisted Teaching** — Give professors an AI assistant grounded in their actual course materials.
3. **Improve Student Engagement** — Provide real-time engagement metrics so instructors know which students are paying attention.
4. **Streamline Administration** — Give admins a single interface to manage universities, buildings, and bulk user creation.
5. **Scale to Any University Size** — Firebase's serverless infrastructure means the system scales automatically with usage.

### Secondary Goals

1. Provide a foundation for integrating with the `.NET Backend` for grades, enrollment, and academic advisement.
2. Demonstrate production-ready engineering practices suitable for a real university deployment.
3. Serve as a portfolio and graduation project showcase.

---

## 3. User Roles & Personas

The application supports **five roles**. Each role has its own route tree, layout, sidebar, and feature set.

### 3.1 Student

**Who:** Enrolled university students.

**Primary Tasks:**
- View courses assigned to their college/year/department
- Take timed quizzes with automatic submission
- Download course materials (lecture PDFs)
- View quiz results and score breakdowns

**Route Base:** `/student/...`

**Authentication:** Firebase Auth + custom claim `role: "student"`

---

### 3.2 Professor

**Who:** Faculty members teaching courses.

**Primary Tasks:**
- Upload lecture PDFs to course materials
- Chat with an AI assistant about course content
- Create quizzes manually or using AI-powered generation from uploaded PDFs
- Monitor student quiz results and performance statistics

**Route Base:** `/prof/...` (modern) and `/professor/...` (legacy)

**Authentication:** Firebase Auth + custom claim `role: "professor"`

---

### 3.3 Teaching Assistant (TA)

**Who:** Graduate students or junior faculty assisting professors.

**Primary Tasks:**
- View courses they are assigned to assist
- Upload supplementary materials to course sections

**Route Base:** `/asst/...`

**Authentication:** Firebase Auth + custom claim `role: "assistant"`

---

### 3.4 Admin

**Who:** University administrators managing academic structure and infrastructure.

**Primary Tasks:**
- Create and manage the full academic hierarchy (colleges → years → departments → courses)
- Assign professors and TAs to course sections
- Manage campus buildings, floors, rooms, and room schedules
- Bulk-create student and staff accounts via Excel upload

**Route Base:** `/admin/...`

**Authentication:** Firebase Auth + custom claim `role: "admin"`

---

### 3.5 Super Admin

**Who:** The highest-privilege system administrator.

**Primary Tasks:**
- All Admin capabilities
- Create other Admin accounts
- Manage system-level user roles

**Route Base:** `/super_admin/...`

**Authentication:** Firebase Auth + custom claim `role: "super_admin"`

---

## 4. Core Modules

### Module 1: Authentication & Access Control
The identity foundation. Firebase Authentication handles login/signup. Custom claims on JWT tokens carry the user's role. Route guards (`RequireRole`, `ProtectedRoute`) enforce role-based access on every protected route.

### Module 2: Quiz Engine
A complete quiz lifecycle — from creation to grading. Professors create MCQ or True/False quizzes. Students take them in a timed, countdown interface that auto-submits. Results are calculated client-side and stored in Firestore.

### Module 3: AI Quiz Generation
When a professor uploads a lecture PDF, the frontend sends it to the FastAPI AI service. The AI analyzes the content and returns 10 structured questions automatically. This removes the single most time-consuming part of quiz creation.

### Module 4: Course Materials Management
Professors upload lecture PDFs which are stored in Firebase Storage. Students can browse and download materials. The same PDF files are indexed nightly by the `.NET` backend's RAG pipeline for AI Q&A.

### Module 5: AI Course Assistant (Professor)
Each professor gets a persistent AI chat per course. The chat is backed by Firebase Functions and the course's indexed materials. Responses are streamed back via Firestore `onSnapshot`.

### Module 6: Attendance Management
Instructors mark attendance per session — present, late, absent, or excused. Firebase Functions validate writes and update aggregated counters in real time. All connected clients see live updates via Firestore.

### Module 7: Engagement Tracking
During live sessions, the EngagementTracker component uses Google MediaPipe (running entirely in the student's browser) to detect face position from the webcam. Aggregated focus/distraction/away counts are sent to Firebase Functions every 10 seconds. No video is ever stored.

### Module 8: Academic Structure Management (Admin)
Full CRUD for the college → year → department → course hierarchy. Courses are stored in both the nested hierarchy and a flat `allCourses` mirror for efficient cross-department queries.

### Module 9: Campus Building Management (Admin)
Manage the physical campus: buildings → floors → rooms → time slots. Room scheduling uses Firestore transactions to prevent double-booking.

### Module 10: Bulk User Import (Admin/SuperAdmin)
Upload an Excel (.xlsx) file with user data. The `xlsx` library parses it client-side. The `bulkCreateUsers` Firebase Function creates Firebase Auth accounts, sets role custom claims, and writes Firestore profiles.

---

## 5. Features Summary

| Feature | Role | Backend | Status |
|---------|------|---------|--------|
| Email/password login | All | Firebase Auth | ✅ Live |
| Role-based routing | All | Firebase token claims | ✅ Live |
| Student quiz taking (timed) | Student | Firestore | ✅ Live |
| Quiz auto-submit on timer expiry | Student | Firestore | ✅ Live |
| Quiz result breakdown | Student | Firestore | ✅ Live |
| Student course list | Student | Firestore | ✅ Live |
| Course material download | Student | Firebase Storage | ✅ Live |
| Professor quiz creation | Professor | Firestore | ✅ Live |
| AI quiz generation from PDF | Professor | FastAPI | ✅ Live |
| Quiz results analytics | Professor | Firestore | ✅ Live |
| Course material upload | Professor | Firebase Storage | ✅ Live |
| AI chat per course | Professor | Firebase Functions + LLM | ✅ Live |
| Live attendance marking | Professor/TA | Firebase Functions | ✅ Live |
| Engagement tracking (MediaPipe) | Student | Firebase Functions | ✅ Live |
| Academic hierarchy management | Admin | Firestore | ✅ Live |
| Course-to-professor assignment | Admin | Firestore | ✅ Live |
| Campus buildings CRUD | Admin | Firestore | ✅ Live |
| Room schedule management | Admin | Firestore transactions | ✅ Live |
| Bulk user Excel import | Admin/SuperAdmin | Firebase Functions | ✅ Live |
| Admin account creation | SuperAdmin | Firebase Functions | ✅ Live |
| Grade/GPA display | Student | .NET Backend | 🔄 Planned |
| Academic roadmap | Student | .NET Backend | 🔄 Planned |
| AI academic advisor chat | Student | FastAPI | 🔄 Planned |
| Assignment submission | Student | .NET Backend | 🔄 Planned |

---

## 6. Technology Stack Overview

| Category | Technology | Version | Why Chosen |
|----------|-----------|---------|------------|
| **UI Framework** | React | 18 | Industry-standard, component model, ecosystem |
| **Routing** | React Router DOM | 7 | Declarative routing, nested routes, guards |
| **Component Library** | Material UI (MUI) | 5 | Complete design system, accessibility built-in |
| **Styling** | Tailwind CSS | 3 | Utility-first, works alongside MUI for custom layouts |
| **Charts** | ApexCharts | 4 | Feature-rich, responsive, easy integration |
| **Database** | Firebase Firestore | v9 | Real-time sync, offline support, serverless |
| **Authentication** | Firebase Auth | v9 | Custom claims, social login ready, scales automatically |
| **File Storage** | Firebase Storage | v9 | Direct browser uploads, signed URLs, CDN-backed |
| **Serverless** | Firebase Functions | v9 | Node.js, callable functions, no separate API server needed |
| **AI (Quiz)** | FastAPI service | — | Python ML ecosystem, PDF parsing + LLM |
| **AI (Chat)** | Firebase Function + LLM | — | Streamed via Firestore, grounded in course materials |
| **Computer Vision** | Google MediaPipe | 0.10.12 | Browser-native face detection, no video sent to server |
| **Excel** | xlsx | 0.18 | Client-side Excel parsing and generation |
| **PDF Export** | jsPDF | 3 | Client-side PDF generation for course schedules |
| **HTTP Client** | Axios | 1.10 | Interceptors, base URL config, request/response transform |
| **Deployment** | Firebase Hosting | — | Global CDN, HTTPS, one-command deploy |
| **Build Tool** | Create React App | 5 | Zero-config webpack setup |

---

## 7. System Boundaries

The React Frontend does **not** handle:
- Academic enrollment (managed by .NET Backend)
- Grade calculation and GPA (managed by .NET Backend)
- Academic roadmap and regulations (managed by .NET Backend)
- Assignment submission and AI grading (managed by .NET Backend)
- Proactive academic risk alerts (managed by .NET Backend background jobs)
- RAG indexing of materials (managed by FastAPI + Hangfire)

The React Frontend **does** handle:
- All classroom-level operations (quizzes, attendance, engagement, materials)
- Campus physical management (buildings, rooms, schedules)
- User management (creation, role assignment, bulk import)
- AI quiz generation (direct FastAPI call)
- AI course assistant (via Firebase Functions)

---

## 8. Project Metrics

| Metric | Value |
|--------|-------|
| React pages | 32+ |
| User roles | 5 |
| Route paths | 38+ |
| Firestore collections | 19+ |
| Firebase Functions called | 8 |
| Firebase API files | 15+ |
| Feature modules | 6 |
| Total npm dependencies | 28 production |
| Build size (estimated) | ~2.5 MB gzipped |
| Deployment | Firebase Hosting (`bsnu.web.app`) |
| Development server port | 3000 (CRA default) |
