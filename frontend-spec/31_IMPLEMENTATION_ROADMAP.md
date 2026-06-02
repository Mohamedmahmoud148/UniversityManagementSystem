# 31 — Implementation Roadmap

## Phase 1 — Foundation & Auth (Week 1-2)
**Goal**: Working app with login, navigation, and core layout

### Pages
- `/auth/login` — Login form
- `/unauthorized` — 403 page
- `/not-found` — 404 page

### Components
- Navbar + Sidebar
- RootLayout
- ProtectedRoute guard
- Toast notification system
- Loading spinner
- Error boundary

### APIs
- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `GET /api/auth/me`
- `POST /api/auth/logout`

### Dependencies
- React Router v6 router setup
- Zustand auth store
- Axios with interceptors
- Tailwind CSS + Shadcn/UI base setup
- i18n setup (Arabic RTL + English)

**Priority**: Critical  
**Complexity**: Medium  
**Effort**: 3-4 days

---

## Phase 2 — Student Core Features (Week 3-4)
**Goal**: Students can see their dashboard, grades, schedule

### Pages
- `/student/dashboard`
- `/student/grades`
- `/student/attendance`
- `/student/schedule`
- `/student/roadmap`
- `/profile`

### Components
- MetricCard, ProgressBar
- DataTable
- GradeTable
- AttendanceReport
- AcademicRoadmap (semester accordion)
- NotificationBell

### APIs
- `GET /api/analytics/dashboard/student`
- `GET /api/grades/my-grades`
- `GET /api/gpa/my-gpa`
- `GET /api/attendance/student/{id}/report`
- `GET /api/schedule/batch/{batchId}`
- `GET /api/Regulations/my-roadmap`
- `GET /api/notification`

**Priority**: Critical  
**Complexity**: Medium  
**Effort**: 5-6 days

---

## Phase 3 — Doctor Core Features (Week 5-6)
**Goal**: Doctors can manage exams, view enrollments

### Pages
- `/doctor/dashboard` (basic version without Teaching Intelligence)
- `/doctor/exams` (list + create + results)
- `/doctor/assignments`
- `/doctor/materials`
- `/doctor/attendance`

### Components
- ExamCard, ExamForm
- AssignmentCard
- MaterialUploader
- AttendanceQR (QR code display)
- StudentTable (basic)

### APIs
- `GET /api/analytics/dashboard/doctor`
- `GET /api/exams/my-exams`
- `POST /api/exams`
- `GET /api/exams/{id}/results`
- `GET /api/assignments/offering/{id}`
- `POST /api/assignments`
- `POST /api/attendance/sessions`
- `GET /api/materials/by-offering/{id}`
- `POST /api/materials/upload`

**Priority**: High  
**Complexity**: Medium  
**Effort**: 6-7 days

---

## Phase 4 — Admin Features (Week 7-8)
**Goal**: Admins can manage the university structure and users

### Pages
- `/admin/dashboard`
- `/admin/structure` (CRUD for all entities)
- `/admin/students` (list + import + edit)
- `/admin/doctors`
- `/admin/enrollments`
- `/admin/analytics`

### Components
- StructureTree / Tab management
- ImportWizard (Excel upload)
- AdminUserForm
- AnalyticsCharts (bar, pie, line)
- PaginatedTable with filters

### APIs
- All structure CRUD endpoints
- Student/Doctor CRUD
- Enrollment management
- Analytics endpoints

**Priority**: High  
**Complexity**: High  
**Effort**: 8-10 days

---

## Phase 5 — Exam Taking + Assignments (Week 9-10)
**Goal**: Full exam experience for students

### Pages
- `/student/exams/{id}` — Pre-exam screen
- `/student/exams/{id}/take` — Exam UI (fullscreen)
- `/student/exams/{id}/result` — Post-exam result
- `/student/assignments/{id}` — Submit assignment
- `/doctor/exams/{id}/analytics` — Exam analytics

### Components
- ExamTimer
- QuestionCard (MCQ + Essay + TrueFalse)
- QuestionNavigator
- AssignmentSubmitForm (file + text)
- ExamAnalyticsPanel
- ProctoringMonitor (frontend event tracking)

### APIs
- `GET /api/exams/{id}` (student view — no answers)
- `POST /api/exams/{id}/submit`
- `POST /api/exams/{id}/save-progress`
- `GET /api/exams/{id}/my-submission`
- `POST /api/assignments/{id}/submit`
- `GET /api/exams/{id}/analytics`
- `POST /api/proctoring/event`

**Priority**: High  
**Complexity**: Very High  
**Effort**: 8-10 days

---

## Phase 6 — AI Chat Platform (Week 11)
**Goal**: Full AI chat experience for all roles

### Pages
- `/chat` — Full AI chat interface

### Components
- ConversationSidebar
- MessageList (virtualized)
- MessageBubble (User + AI)
- AITypingIndicator
- SuggestionChips
- ChatInput

### APIs
- `POST /api/Chat/conversations`
- `GET /api/Chat/conversations`
- `GET /api/Chat/conversations/{id}/messages`
- `POST /api/Chat/messages`
- `DELETE /api/Chat/conversations/{id}`

**Priority**: High  
**Complexity**: High  
**Effort**: 5-6 days

---

## Phase 7 — AI Companion + Teaching Intelligence (Week 12-14)
**Goal**: Advanced AI features — the platform's showcase

### Sub-Phase 7a: AI Companion (Student)
#### Pages
- `/student/companion/*` (all companion sub-pages)

#### Components
- CompanionDashboard
- AcademicCoachChat
- StudyPartnerQuiz
- FlashcardDeck + FlashcardReview
- ProgressReport
- InsightsFeed
- StreakBadge, EngagementScore

#### APIs
- All `/api/companion/*` endpoints

**Effort**: 7-8 days

### Sub-Phase 7b: Teaching Intelligence (Doctor)
#### Pages
- `/doctor/dashboard` (full Teaching Intelligence version)
- `/doctor/analytics/:offeringId` (full version with Topics tab)
- `/doctor/students/:offeringId` (filterable intelligent table)
- `/doctor/students/:offeringId/:studentId` (individual student profile)
- `/doctor/alerts`

#### Components
- TeachingDashboard (complete version)
- OfferingHealthCard
- RiskStudentTable (sortable, filterable)
- StudentRiskProfile
- TopicAnalyticsPanel
- ClassComparisonChart
- ExcelExport (SheetJS integration)
- AlertsFeed

#### APIs
- All `/api/teaching-intelligence/*` endpoints
- `GET /api/teaching-intelligence/offerings/{id}/export`

**Effort**: 8-10 days

---

## Phase Summary Table

| Phase | Name | Pages | Effort | Priority | Complexity |
|-------|------|-------|--------|----------|------------|
| 1 | Foundation & Auth | 3 | 3-4 days | Critical | Medium |
| 2 | Student Core | 6 | 5-6 days | Critical | Medium |
| 3 | Doctor Core | 5 | 6-7 days | High | Medium |
| 4 | Admin Features | 6 | 8-10 days | High | High |
| 5 | Exam + Assignment | 5 | 8-10 days | High | Very High |
| 6 | AI Chat | 1 | 5-6 days | High | High |
| 7a | AI Companion | 8 | 7-8 days | Medium | High |
| 7b | Teaching Intelligence | 5 | 8-10 days | Medium | Very High |
| **Total** | | **39** | **50-61 days** | | |

---

## Critical Path Dependencies

```
Phase 1 (Auth) → All other phases
Phase 2 (Student Core) → Phase 5 (Exam Taking), Phase 7a (Companion)
Phase 3 (Doctor Core) → Phase 5 (Exam Analytics), Phase 7b (Teaching Intel)
Phase 4 (Admin) → Standalone (no blocking dependencies)
Phase 6 (AI Chat) → Phase 7a (Companion uses chat), Phase 7b (Teaching chat)
```

---

## MVP Definition (Phases 1-3 + 6)
If resources are limited, the MVP is:
- Auth + basic navigation
- Student dashboard + grades + schedule
- Doctor exam management
- AI chat (the core differentiator)

This delivers a working platform in ~20-25 days.
