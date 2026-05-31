# Project Flow

> **Last refreshed:** 2026-05-31

Complete system flows for key operations. See [21-Diagrams](../21-Diagrams/index.md) for Mermaid versions.

---

## 1. Authentication Flow

```
1. User opens app → no JWT → redirected to login
2. POST /api/auth/login { email, password }
3. Backend validates credentials (BCrypt hash comparison)
4. On success: generate JWT with claims {userId, role, ProfileId}
5. Return { token, refreshToken, role, userId }
6. Frontend stores token in localStorage/memory
7. All subsequent requests: Authorization: Bearer <token>
8. Token expiry → POST /api/auth/refresh → new token
```

---

## 2. Student Enrolment Flow

```
Admin creates student (systemUser + student record)
    │
Admin assigns: Department, Batch, Group, Regulation
    │
Student logs in → sees available subject offerings
    │
Option A: Manual enrolment
  → Student selects offerings → POST /api/enrollments/auto-enrol
  → Backend finds all open offerings for student's batch/dept/group
  → Creates Enrollment records

Option B: Admin enrols manually
  → POST /api/enrollments { studentId, subjectOfferingId }
```

---

## 3. AI Chat Flow (Detailed)

```
Student types: "اعمللي خطة مذاكرة للأسبوع ده"
    │
POST /api/chat { message, conversationId }
    │
ChatController → ChatService.SendMessageAsync()
    │
ChatService:
  1. Fetch last 10 messages from DB (history)
  2. Build academic_context:
     - Resolve student record → studentId, studentName
     - Get department, batch names
     - Fetch active enrollments → enrolledOfferingIds, enrolledSubjects
     - Inject: today (YYYY-MM-DD), dayOfWeek
  3. Save user message to DB
  4. POST FastAPI /api/chat {message, history, academic_context, role, userId, jwt}

FastAPI PlannerAgent:
  1. Build messages[] with history + system prompt
  2. Call GPT-4o-mini → classify intent
  3. Layer-2: _detect_study_plan matches "خطة مذاكرة" → override to study_plan
  4. RBAC gate: Student allowed study_plan ✅
  5. Route to StudyPlanModule

StudyPlanModule (4 parallel fetches):
  - GET /api/Regulations/my-roadmap → GPA, subjects, mustRetake
  - GET /api/ai-tools/student-overview/{userId} → grades, exams
  - GET /api/analytics/student/{userId}/performance → attendance per subject
  - GET /api/assignments/offering/{id}×N → upcoming deadlines

Build context block:
  - Today + day, GPA, enrolled subjects
  - Per-subject attendance + performance
  - Upcoming assignments sorted by urgency (🔴🟡🟢)

Call GPT-4o-mini (max_tokens=2500):
  → Generates weekly schedule + priorities + tips + motivation

Return AgentOutput { response, module: "StudyPlanModule", gpa, upcomingAssignments }

ChatService:
  - Save AI response to DB
  - Return to frontend with suggestions

Frontend renders: formatted study plan + action suggestions
```

---

## 4. Material Upload + RAG Indexing Flow

```
Doctor uploads file (POST /api/materials/upload, multipart)
    │
MaterialService:
  1. Validate MIME type (14 types allowed)
  2. Validate file size (≤ 500 MB)
  3. StorageService.UploadAsync() → Cloudflare R2 → returns StorageKey
  4. Create UploadedFile record (DB)
  5. Create Material record with StorageKey + SubjectOfferingId
  6. Fire-and-forget: IAiService.IndexMaterialAsync(material.Id, auth)

Background RAG indexing (non-blocking):
    │
POST /api/rag/index (FastAPI)
  1. Fetch file from R2 using StorageKey
  2. Extract text: PDF → pdfplumber, DOCX → python-docx
  3. Chunk text (~500 tokens, 50-token overlap)
  4. OpenAI text-embedding-3-small → float[] per chunk
  5. ChromaDB.upsert(materialId_chunkN → embedding + content + metadata)
  6. Material.ValidationStatus = Indexed

RagIndexingJob (daily Hangfire):
  → Finds materials with ValidationStatus != Indexed
  → Retriggers indexing for any missed files
```

---

## 5. Assignment Submission + AI Grading Flow

```
Student submits: POST /api/assignments/{id}/submit
    │
AssignmentService:
  1. Fetch assignment → check deadline → set IsLate flag
  2. If file: upload to R2 → store StorageKey + fileUrl
  3. CREATE AssignmentSubmission { text, fileUrl, isLate, status=Submitted }
  4. Notify doctor via NotificationService

Doctor triggers AI grading: POST /api/assignments/submissions/{id}/ai-grade
    │
AiService.GradeEssayAsync(text, title, description, rubric, maxGrade)
    │
POST /api/ai/grade-submission (FastAPI)
  → GPT-4o-mini with structured JSON output
  → Returns: { score, feedback, strengths, weaknesses, confidence }
    │
UPDATE AssignmentSubmission:
  { grade, aiFeedback, strengths, weaknesses, isAiGraded=true, status=Graded }
```

---

## 6. Exam Randomization Flow

```
Doctor creates exam with IsRandomized=true, QuestionsPerStudent=15
    │
Students take exam: GET /api/exams/{id} (each student gets unique question set)
    │
ExamService.GetStudentQuestions(examId, studentId):
  1. Seed = hash(examId + studentId)
  2. Shuffle master question list using seeded random
  3. Take first QuestionsPerStudent questions
  4. For MCQ: shuffle answer options using same seed
  5. Return personalized question set

Student submits answers:
  POST /api/exams/{id}/submit { answers: [{questionId, answer}] }
    │
Auto-grade objective questions against CorrectAnswer
Essay questions → status = PendingGrading
Doctor or AI grades essays
```

---

## 7. Notification Flow

```
Source event (exam reminder / assignment deadline / risk alert / doctor broadcast)
    │
NotificationService.SendNotificationAsync(userId, title, message)
    │
    ├─ Step 1: INSERT AppNotification (PostgreSQL) — immediate persistence
    └─ Step 2: Publish NotificationCreatedEvent (RabbitMQ)
                    │
              MassTransit NotificationConsumer
                    │
              SignalR: hub.Clients.Group(userId).SendAsync("ReceiveNotification", dto)
                    │
              Browser receives real-time push → toast notification shown
```
