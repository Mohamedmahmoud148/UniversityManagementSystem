# 33 — Frontend-Backend Mapping

## Page → API Mapping

| Page | Primary APIs | Secondary APIs |
|------|-------------|---------------|
| `/auth/login` | `POST /api/auth/login` | `GET /api/auth/me` |
| `/student/dashboard` | `GET /api/analytics/dashboard/student` | `GET /api/gpa/my-gpa`, `GET /api/exams/my-enrolled-exams`, `GET /api/companion/dashboard` |
| `/student/grades` | `GET /api/grades/my-grades`, `GET /api/gpa/my-gpa` | — |
| `/student/attendance` | `GET /api/attendance/student/{id}/report` | — |
| `/student/exams` | `GET /api/exams/my-enrolled-exams` | — |
| `/student/exams/{id}/take` | `GET /api/exams/{id}`, `POST /api/exams/{id}/save-progress` | `POST /api/proctoring/event` |
| `/student/exams/{id}` submit | `POST /api/exams/{id}/submit` | — |
| `/student/assignments` | `GET /api/enrollments/my-enrollments` + per offering assignments | — |
| `/student/assignments/{id}` | `GET /api/assignments/{id}`, `GET /api/assignments/{id}/my-submission` | `POST /api/assignments/{id}/submit` |
| `/student/materials` | `GET /api/enrollments/my-enrollments` + per offering materials | — |
| `/student/roadmap` | `GET /api/Regulations/my-roadmap` | — |
| `/student/companion` | `GET /api/companion/dashboard` | `GET /api/companion/insights`, `GET /api/companion/flashcards/due` |
| `/student/companion/coach` | `POST /api/Chat/messages` (intent: academic_coach) | `GET /api/companion/dashboard` |
| `/student/companion/quiz` | `POST /api/companion/sessions/start`, `POST /api/Chat/messages` | `POST /api/companion/sessions/{id}/complete` |
| `/student/companion/flashcards` | `GET /api/companion/flashcards` | `GET /api/companion/flashcards/due` |
| `/student/complaints` | `GET /api/complaints/my-complaints` | `POST /api/complaints` |
| `/doctor/dashboard` | `GET /api/teaching-intelligence/dashboard` | `GET /api/teaching-intelligence/alerts` |
| `/doctor/analytics/{id}` | `GET /api/teaching-intelligence/offerings/{id}/analytics` | `GET /api/teaching-intelligence/offerings/{id}/topics` |
| `/doctor/students/{id}` | `GET /api/teaching-intelligence/offerings/{id}/students` | — |
| `/doctor/students/{id}/{sid}` | `GET /api/teaching-intelligence/offerings/{id}/students/{sid}` | — |
| `/doctor/exams` | `GET /api/exams/my-exams` | `GET /api/subjectofferings/my-offerings` |
| `/doctor/exams/create` | `POST /api/exams`, `POST /api/exams/generate-ai` | — |
| `/doctor/exams/{id}/results` | `GET /api/exams/{id}/results` | `GET /api/exams/{id}/analytics` |
| `/doctor/assignments` | `GET /api/subjectofferings/my-offerings` + assignments per offering | — |
| `/doctor/assignments/{id}` | `GET /api/assignments/{id}/submissions` | `POST /api/assignments/submissions/{id}/grade` |
| `/doctor/materials` | `GET /api/subjectofferings/my-offerings` + materials | `POST /api/materials/upload` |
| `/doctor/attendance` | `GET /api/subjectofferings/my-offerings` | `POST /api/attendance/sessions` |
| `/admin/dashboard` | `GET /api/analytics/dashboard/admin`, `GET /api/analytics/summary` | `GET /api/risk/at-risk-students` |
| `/admin/students` | `GET /api/students/filter` | `POST /api/students/bulk-upload-ai` |
| `/admin/structure` | All structure CRUD endpoints | — |
| `/admin/analytics` | All `/api/analytics/*` endpoints | `GET /api/risk/dashboard` |
| `/notifications` | `GET /api/notification` | `PUT /api/notification/{id}/read` |
| `/chat` | `GET /api/Chat/conversations`, `POST /api/Chat/messages` | `POST /api/Chat/conversations` |
| `/profile` | `GET /api/auth/me` | `POST /api/auth/change-password` |

---

## DTO → Component Mapping

| DTO | Component |
|-----|-----------|
| `StudentGpaDto` | `GPACard`, student dashboard |
| `AttendanceReportDto` | `AttendanceReport` component |
| `ExamDto` | `ExamCard`, `ExamForm`, `ExamTaking` |
| `AssignmentDto` + `SubmissionDto` | `AssignmentCard`, submit form |
| `TeachingDashboardDto` | `TeachingDashboard` page |
| `StudentIntelligenceDto` | `StudentIntelligenceRow`, `StudentRiskProfile` |
| `DoctorOfferingSummaryDto` | `OfferingHealthCard` |
| `WeakTopicDto` | `WeakTopicRow` |
| `AiInsightDto` | `InsightCard` |
| `FlashcardDto` | `FlashcardReview` |
| `FlashcardDeckDto` | `FlashcardDeck` |
| `LearningSessionDto` | `SessionCard`, results screen |
| `AcademicRoadmapDto` | `AcademicRoadmap`, semester view |
| `ComplaintDto` | `ComplaintCard`, complaint list |
| `NotificationDto` | `NotificationItem`, bell dropdown |
| `StudentExcelRowDto` | Excel export via SheetJS |
| `AdminDashboardDto` | Admin dashboard metric cards |
| `CompanionDashboardDto` | Companion dashboard |
