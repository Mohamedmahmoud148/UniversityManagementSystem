# System Diagrams — Mermaid Source Files

> **Last refreshed:** 2026-05-31 | Generated from live codebase

All diagrams use [Mermaid](https://mermaid.js.org/) syntax. Paste into any Mermaid renderer (mermaid.live, GitHub, Notion, VS Code extension).

---

## Diagram 1 — Context Diagram (C1)

Shows the system in its environment: who uses it and what external services it depends on.

```mermaid
C4Context
  title UniSys — System Context

  Person(student, "Student", "Enrols, studies, submits assignments, takes exams, talks to AI advisor")
  Person(doctor, "Doctor / Instructor", "Manages courses, attendance, exams, grades, uploads materials")
  Person(admin, "Admin / SuperAdmin", "Manages university structure, regulations, analytics, complaints")

  System(unisys, "UniSys Platform", "Full-stack university management system with integrated AI advisor")

  System_Ext(openrouter, "OpenRouter", "LLM gateway — routes to GPT-4o-mini and fallbacks")
  System_Ext(r2, "Cloudflare R2", "Object storage for materials, exam files, student submissions")
  System_Ext(railway, "Railway PaaS", "Hosts all production services")

  Rel(student, unisys, "Uses", "HTTPS / JWT")
  Rel(doctor, unisys, "Uses", "HTTPS / JWT")
  Rel(admin, unisys, "Uses", "HTTPS / JWT")
  Rel(unisys, openrouter, "LLM calls", "HTTPS")
  Rel(unisys, r2, "Store / retrieve files", "S3-compatible API")
  Rel(unisys, railway, "Deployed on")
```

---

## Diagram 2 — System Overview (C2 — Container Level)

The big picture: every major container and how they connect.

```mermaid
graph TB
  subgraph Users
    S[👩‍🎓 Student]
    D[👨‍🏫 Doctor]
    A[🔧 Admin]
  end

  subgraph Frontend["Frontend — React (bsnu.web.app)"]
    UI[React SPA]
  end

  subgraph Backend[".NET 9 Backend API — Railway"]
    API[ASP.NET Core 9<br/>35 Controllers]
    HF[Hangfire<br/>7 Recurring Jobs]
    SR[SignalR Hub<br/>Real-time Push]
    MT[MassTransit<br/>Message Consumer]
  end

  subgraph AIService["FastAPI AI Service — Railway"]
    AGENT[React Agent<br/>Planner + Executor]
    PLAN[PlannerAgent<br/>17 Intents]
    EXEC[PlanExecutor<br/>15 Modules]
    RAG[RAG Pipeline<br/>ChromaDB]
    MEM[Memory Store<br/>Redis]
  end

  subgraph Storage["Data & Messaging"]
    PG[(PostgreSQL 16)]
    RD[(Redis 7)]
    RMQ[(RabbitMQ 3.13)]
    CH[(ChromaDB 0.5)]
    R2[(Cloudflare R2<br/>File Storage)]
  end

  subgraph External["External Services"]
    OR[OpenRouter<br/>LLM Gateway]
    OAIE[OpenAI Embeddings<br/>text-embedding-3-small]
  end

  S & D & A -->|HTTPS + JWT| UI
  UI -->|REST API + WebSocket| API
  API -->|SQL via EF Core| PG
  API -->|Cache + Session| RD
  API -->|Publish Events| RMQ
  API -->|Upload / Download| R2
  API -->|HTTP Internal| AGENT
  HF -->|Schedule| API
  MT -->|Consume| RMQ
  MT -->|Push| SR
  AGENT --> PLAN
  PLAN --> EXEC
  EXEC --> RAG
  RAG -->|Embed Queries| OAIE
  RAG -->|Semantic Search| CH
  EXEC -->|Fetch Student Data| API
  MEM -->|Conversation History| RD
  AGENT -->|LLM Calls| OR
```

---

## Diagram 3 — Use Case Diagram

```mermaid
graph LR
  subgraph Student["👩‍🎓 Student"]
    S1[Register & Enrol]
    S2[Browse Course Materials]
    S3[Download Material Files]
    S4[Submit Assignments]
    S5[Take Exams]
    S6[View Grades & GPA]
    S7[Chat with AI Advisor]
    S8[Get Study Plan]
    S9[Ask Regulation Q&A]
    S10[Submit Complaint]
    S11[View Notifications]
    S12[Track Academic Roadmap]
  end

  subgraph Doctor["👨‍🏫 Doctor"]
    D1[Manage Subject Offering]
    D2[Upload Course Materials]
    D3[Create Assignments]
    D4[Grade Submissions]
    D5[Create & Publish Exams]
    D6[Record Attendance]
    D7[View Student Analytics]
    D8[Generate AI Exam]
    D9[Send Notifications]
    D10[Chat with AI Assistant]
  end

  subgraph Admin["🔧 Admin / SuperAdmin"]
    A1[Manage University Structure]
    A2[Upload Regulations]
    A3[View Complaint Reports]
    A4[Access Analytics Dashboard]
    A5[Bulk Import Students]
    A6[Manage Regulations]
    A7[View Academic Risk]
    A8[Send Broadcast Notifications]
    A9[Manage All Users]
  end

  subgraph AI["🤖 AI System"]
    AI1[Detect Intent]
    AI2[Fetch Student Context]
    AI3[Search Regulations RAG]
    AI4[Search Materials RAG]
    AI5[Generate Study Plan]
    AI6[Grade Essay]
    AI7[Generate Exam Questions]
    AI8[Analyse Complaints]
  end

  S7 --> AI1
  S8 --> AI5
  S9 --> AI3
  D8 --> AI7
  D4 --> AI6
  A3 --> AI8
```

---

## Diagram 4 — Domain Class Diagram (Simplified ERD)

```mermaid
classDiagram
  class BaseEntity {
    +Ulid Id
    +string Code
    +DateTime CreatedAt
    +DateTime? DeletedAt
  }

  class SystemUser {
    +string FullName
    +string Email
    +string PasswordHash
    +UserRole Role
    +bool IsActive
  }

  class Student {
    +string FullName
    +string UniversityStudentId
    +StudentType Type
    +Ulid DepartmentId
    +Ulid BatchId
    +Ulid? RegulationId
    +float? GPA
    +bool IsActive
  }

  class Doctor {
    +string FullName
    +string UniversityStaffId
    +Ulid DepartmentId
  }

  class Subject {
    +string Name
    +string Code
    +int CreditHours
    +Ulid DepartmentId
  }

  class SubjectOffering {
    +Ulid SubjectId
    +Ulid DoctorId
    +Ulid SemesterId
    +int MaxCapacity
    +float MidtermWeight
    +float CourseworkWeight
    +float FinalWeight
  }

  class Enrollment {
    +Ulid StudentId
    +Ulid SubjectOfferingId
    +DateTime EnrolledAt
    +bool IsActive
  }

  class Material {
    +string Title
    +string StorageKey
    +string ContentType
    +long FileSize
    +Ulid SubjectOfferingId
    +Ulid UploadedByDoctorId
  }

  class Assignment {
    +string Title
    +string Description
    +DateTime Deadline
    +int MaxGrade
    +bool AllowLateSubmission
    +bool AiGradingEnabled
    +string? GradingRubric
  }

  class AssignmentSubmission {
    +string? TextAnswer
    +string? FileUrl
    +DateTime SubmittedAt
    +bool IsLate
    +SubmissionStatus Status
    +float? Grade
    +string? Feedback
    +string? AiFeedback
  }

  class Exam {
    +string Title
    +ExamType Type
    +ExamStatus Status
    +DateTime StartTime
    +DateTime EndTime
    +int TotalMarks
    +bool IsRandomized
  }

  class ExamSubmission {
    +float Score
    +bool IsGraded
    +string AnswersJson
    +DateTime SubmittedAt
  }

  class AttendanceSession {
    +DateTime SessionDate
    +Ulid SubjectId
  }

  class StudentAttendance {
    +bool IsPresent
    +Ulid StudentId
    +Ulid AttendanceSessionId
  }

  class Regulation {
    +string Title
    +string? Content
    +RegulationType Type
    +bool IsActive
    +Ulid? FileId
  }

  class RegulationSubject {
    +Ulid RegulationId
    +Ulid SubjectId
    +int Semester
    +bool IsRequired
  }

  class Complaint {
    +string Title
    +string Message
    +ComplaintStatus Status
    +ComplaintPriority Priority
    +TargetType TargetType
  }

  class AppNotification {
    +Ulid UserId
    +string Title
    +string Message
    +bool IsRead
    +string? ActionUrl
  }

  class College {
    +string Name
    +string Code
  }

  class Department {
    +string Name
    +Ulid CollegeId
  }

  class Batch {
    +string Name
    +Ulid DepartmentId
  }

  BaseEntity <|-- SystemUser
  BaseEntity <|-- Student
  BaseEntity <|-- Doctor
  BaseEntity <|-- Subject
  BaseEntity <|-- SubjectOffering
  BaseEntity <|-- Enrollment
  BaseEntity <|-- Material
  BaseEntity <|-- Assignment
  BaseEntity <|-- AssignmentSubmission
  BaseEntity <|-- Exam
  BaseEntity <|-- ExamSubmission
  BaseEntity <|-- AttendanceSession
  BaseEntity <|-- StudentAttendance
  BaseEntity <|-- Regulation
  BaseEntity <|-- RegulationSubject
  BaseEntity <|-- Complaint
  BaseEntity <|-- AppNotification
  BaseEntity <|-- College
  BaseEntity <|-- Department
  BaseEntity <|-- Batch

  SystemUser "1" --> "0..1" Student
  SystemUser "1" --> "0..1" Doctor
  Department "1" --> "many" Student
  Batch "1" --> "many" Student
  Student "1" --> "many" Enrollment
  Enrollment "many" --> "1" SubjectOffering
  SubjectOffering "1" --> "many" Material
  SubjectOffering "1" --> "many" Assignment
  SubjectOffering "1" --> "many" Exam
  Assignment "1" --> "many" AssignmentSubmission
  Exam "1" --> "many" ExamSubmission
  Student "1" --> "many" Complaint
  Regulation "1" --> "many" RegulationSubject
  Student "many" --> "0..1" Regulation
  College "1" --> "many" Department
  Department "1" --> "many" Batch
```

---

## Diagram 5A — Sequence: Student Asks AI About Course Material

```mermaid
sequenceDiagram
  actor Student
  participant Frontend
  participant .NET API
  participant ChatService
  participant FastAPI
  participant PlannerAgent
  participant MaterialQAModule
  participant ChromaDB
  participant OpenRouter

  Student->>Frontend: "اشرحلي محاضرة الـ binary trees"
  Frontend->>.NET API: POST /api/chat {message, conversationId}
  .NET API->>ChatService: SendMessageAsync()
  ChatService->>ChatService: Fetch history (last 10 msgs)
  ChatService->>ChatService: Build academic_context (studentId, offeringIds, today)
  ChatService->>FastAPI: POST /api/chat {message, history, academic_context, jwt}

  FastAPI->>PlannerAgent: Classify intent
  Note over PlannerAgent: Layer-2 override detects<br/>"محاضرة" → material_qa
  PlannerAgent-->>FastAPI: intent = material_qa

  FastAPI->>MaterialQAModule: run(agent_input)
  MaterialQAModule->>MaterialQAModule: Extract offeringId from context
  MaterialQAModule->>ChromaDB: Embed query + semantic search (top-5 chunks)
  ChromaDB-->>MaterialQAModule: Relevant material chunks + metadata

  MaterialQAModule->>OpenRouter: Generate grounded answer
  Note over OpenRouter: GPT-4o-mini with strict<br/>grounding prompt
  OpenRouter-->>MaterialQAModule: Answer citing source chunks

  MaterialQAModule-->>FastAPI: AgentOutput {response, sources}
  FastAPI-->>.NET API: {response, intent, module}
  .NET API->>ChatService: Save AI response to DB
  ChatService-->>Frontend: {message, suggestions}
  Frontend-->>Student: Answer + source references
```

---

## Diagram 5B — Sequence: Assignment Submission Flow

```mermaid
sequenceDiagram
  actor Student
  participant Frontend
  participant .NET API
  participant AssignmentService
  participant StorageService
  participant Cloudflare R2
  participant PostgreSQL
  participant NotificationService
  participant RabbitMQ
  participant SignalR

  Student->>Frontend: Submit assignment (text + optional file)
  Frontend->>.NET API: POST /api/assignments/{id}/submit (multipart/form-data)
  .NET API->>AssignmentService: SubmitAsync(assignmentId, studentId, text, file)

  AssignmentService->>AssignmentService: Check deadline → set IsLate flag
  AssignmentService->>AssignmentService: Check if already submitted

  alt File attached
    AssignmentService->>StorageService: UploadFileStreamAsync()
    StorageService->>Cloudflare R2: PUT file to assignments/ folder
    Cloudflare R2-->>StorageService: StorageKey
    StorageService-->>AssignmentService: fileUrl, storageKey
  end

  AssignmentService->>PostgreSQL: INSERT AssignmentSubmission {studentId, assignmentId, text, fileUrl, isLate, status=Submitted}
  PostgreSQL-->>AssignmentService: Submission record

  AssignmentService->>NotificationService: Notify doctor of new submission
  NotificationService->>PostgreSQL: INSERT AppNotification
  NotificationService->>RabbitMQ: Publish NotificationCreatedEvent
  RabbitMQ->>SignalR: Push to doctor's group

  AssignmentService-->>.NET API: SubmissionDto
  .NET API-->>Frontend: {submissionId, message: "submitted"}
  Frontend-->>Student: ✅ Submission confirmed
```

---

## Diagram 5C — Sequence: AI Grading Flow

```mermaid
sequenceDiagram
  actor Doctor
  participant Frontend
  participant .NET API
  participant AssignmentService
  participant AiService
  participant FastAPI
  participant OpenRouter
  participant PostgreSQL

  Doctor->>Frontend: Click "AI Grade" on submission
  Frontend->>.NET API: POST /api/assignments/submissions/{id}/ai-grade
  .NET API->>AssignmentService: TriggerAiGradingAsync(submissionId)

  AssignmentService->>PostgreSQL: Fetch submission + assignment (title, description, rubric, maxGrade)
  PostgreSQL-->>AssignmentService: Submission with textAnswer

  AssignmentService->>AiService: GradeEssayAsync(submissionText, title, description, rubric, maxGrade)
  AiService->>FastAPI: POST /api/ai/grade-submission
  FastAPI->>OpenRouter: Grade with structured JSON prompt
  Note over OpenRouter: Returns {score, feedback,<br/>strengths, weaknesses, confidence}
  OpenRouter-->>FastAPI: GradingResponse (JSON)
  FastAPI-->>AiService: GradingResult

  AiService-->>AssignmentService: {score, feedback, strengths, weaknesses}
  AssignmentService->>PostgreSQL: UPDATE submission {grade, aiFeedback, strengths, weaknesses, isAiGraded=true, status=Graded}
  PostgreSQL-->>AssignmentService: Updated

  AssignmentService-->>.NET API: GradingResultDto
  .NET API-->>Frontend: Grade result
  Frontend-->>Doctor: Score + AI feedback displayed
```

---

## Diagram 6A — Activity: Complete Student Journey

```mermaid
flowchart TD
  A([Student Created by Admin]) --> B[Login with JWT]
  B --> C{Profile complete?}
  C -- No --> D[Complete profile / upload docs]
  C -- Yes --> E[Browse Subject Offerings]
  D --> E
  E --> F{AI Auto-enrol or Manual?}
  F -- AI --> G[AI calls POST /api/enrollments/auto-enrol]
  F -- Manual --> H[Student selects courses]
  G & H --> I[Enrolled in SubjectOfferings]
  I --> J[Access Course Materials]
  J --> K[Download files / Ask AI about material]
  K --> L[Submit Assignments]
  L --> M{Deadline passed?}
  M -- Yes, AllowLate=true --> N[Submit as late]
  M -- No --> O[Submit on time]
  N & O --> P[Doctor reviews / AI grades]
  P --> Q[Take Randomized Exams]
  Q --> R[Auto-graded MCQ + AI essay grading]
  R --> S[View Grades & GPA]
  S --> T{GPA < 2.0?}
  T -- Yes --> U[Academic risk alert sent]
  U --> V[AI study plan generated]
  T -- No --> W[Track Roadmap Progress]
  V --> W
  W --> X{All requirements met?}
  X -- No --> E
  X -- Yes --> Y([Graduation Eligible ✓])
```

---

## Diagram 6B — Activity: AI Conversation Flow

```mermaid
flowchart TD
  A([User Sends Message]) --> B[ChatService: build academic_context]
  B --> C[Inject: studentId, today, offeringIds, history]
  C --> D[POST /api/chat to FastAPI]
  D --> E[PlannerAgent: classify intent]
  E --> F{Deterministic override?}
  F -- Yes → regulation keyword --> G[intent = regulation]
  F -- Yes → exam creation keyword --> H[intent = generate_exam]
  F -- Yes → study plan keyword --> I[intent = study_plan]
  F -- Yes → assignment keyword --> J[intent = assignment_query]
  F -- Yes → material keyword --> K[intent = material_qa]
  F -- No → LLM classification --> L[LLM classifies intent]
  G & H & I & J & K & L --> M[RBAC gate: role allowed?]
  M -- Denied --> N[Return permission error]
  M -- Allowed --> O[Route to Module]
  O --> P{Module type}
  P -- StudyPlanModule --> Q[Fetch: roadmap + grades + attendance + assignments parallel]
  P -- MaterialQAModule --> R[Embed query → ChromaDB search → ground answer]
  P -- AcademicAdvisorModule --> S[Fetch: roadmap + overview + RAG regulation chunks]
  P -- DynamicApiModule --> T[Discover endpoint → call backend → summarize]
  P -- AssignmentQueryModule --> U[Fetch assignments + submission status per offering]
  Q & R & S & T & U --> V[LLM generates response]
  V --> W[AgentOutput: response + suggestions]
  W --> X[Save to conversation history]
  X --> Y([Response to Student])
```

---

## Diagram 6C — Activity: Assignment Lifecycle

```mermaid
flowchart TD
  A([Doctor Creates Assignment]) --> B{Rubric provided?}
  B -- Yes --> C[Store JSON rubric for AI grading]
  B -- No --> D[Default rubric]
  C & D --> E[Assignment Published to Students]
  E --> F[AssignmentReminderJob: every 30 min]
  F --> G{Deadline in 24h?}
  G -- Yes → check submitted --> H{Student submitted?}
  H -- No --> I[Send 24h reminder notification]
  H -- Yes --> J[Skip — no noise]
  G -- No → check 2h --> K{Deadline in 2h?}
  K -- Yes --> L[Send urgent 2h reminder]
  K -- No --> M[Wait next cycle]
  I & L & J --> N[Student Submits]
  N --> O{File submission?}
  O -- Yes --> P[Upload to Cloudflare R2]
  P --> Q[Store StorageKey + URL]
  O -- No --> R[Store text answer only]
  Q & R --> S[Mark IsLate if past deadline]
  S --> T[Status = Submitted]
  T --> U{AiGradingEnabled?}
  U -- Yes --> V[POST /api/ai/grade-submission]
  V --> W[OpenRouter grades essay]
  W --> X[Store: grade, aiFeedback, strengths, weaknesses]
  U -- No --> Y[Doctor grades manually]
  Y --> Z[Store: grade, feedback]
  X & Z --> AA[Status = Graded]
  AA --> AB[Student views grade + feedback]
  AB --> AC([Assignment Complete])
```

---

## Diagram 7 — Deployment Diagram (Railway)

```mermaid
graph TB
  subgraph Railway["Railway PaaS — Production"]
    subgraph Services["Application Services"]
      NET[".NET 9 Backend\nPort 443 (public)\n/api/* routes\nSignalR /hubs/*"]
      FPY["FastAPI AI Service\nPort 8000 (internal)\n/api/chat, /api/rag\n/api/ai_grading"]
    end

    subgraph Data["Managed Data Services"]
      PG["PostgreSQL 16\nPrimary DB\nHangfire storage\nEF Core migrations"]
      RD["Redis 7\nConversation memory\nDistributed cache\nRate limiting"]
      RMQ["RabbitMQ 3.13\nNotification events\nAttendance events\nMassTransit bus"]
    end

    subgraph Volumes["Persistent Volumes"]
      CH["ChromaDB 0.5\nVector embeddings\nRAG chunks\nPersistent volume"]
    end
  end

  subgraph External["External (Not on Railway)"]
    R2["Cloudflare R2\nMaterial files\nExam files\nSubmission files"]
    OR["OpenRouter\nGPT-4o-mini\nFallback models"]
    OAI["OpenAI API\ntext-embedding-3-small\nRAG embeddings"]
  end

  NET <-->|"HTTP (Railway internal)"| FPY
  NET -->|"TCP 5432"| PG
  NET -->|"TCP 6379"| RD
  NET -->|"TCP 5672"| RMQ
  FPY -->|"TCP 6379"| RD
  FPY -->|"TCP 5672"| RMQ
  FPY -->|"HTTP localhost"| CH
  NET -->|"S3-compatible HTTPS"| R2
  FPY -->|"HTTPS"| OR
  FPY -->|"HTTPS"| OAI

  Internet["Internet (Frontend / Users)"] -->|"HTTPS"| NET
```

---

## Diagram 8 — ERD (Key Relationships)

```mermaid
erDiagram
  College ||--o{ Department : "has"
  Department ||--o{ Batch : "has"
  Department ||--o{ Student : "enrolled in"
  Batch ||--o{ Student : "belongs to"

  SystemUser ||--o| Student : "profile"
  SystemUser ||--o| Doctor : "profile"

  Student }o--|| Regulation : "follows"
  Regulation ||--o{ RegulationSubject : "contains"
  RegulationSubject }o--|| Subject : "defines"

  Subject ||--o{ SubjectOffering : "offered as"
  Doctor ||--o{ SubjectOffering : "teaches"
  SubjectOffering ||--o{ Enrollment : "has"
  Student ||--o{ Enrollment : "enrolled"

  SubjectOffering ||--o{ Material : "has"
  SubjectOffering ||--o{ Assignment : "has"
  SubjectOffering ||--o{ Exam : "has"
  SubjectOffering ||--o{ AttendanceSession : "tracks"

  Assignment ||--o{ AssignmentSubmission : "receives"
  Student ||--o{ AssignmentSubmission : "submits"

  Exam ||--o{ ExamSubmission : "receives"
  Student ||--o{ ExamSubmission : "submits"

  AttendanceSession ||--o{ StudentAttendance : "records"
  Student ||--o{ StudentAttendance : "has"

  Student ||--o{ StudentGrade : "earns"
  SubjectOffering ||--o{ StudentGrade : "produces"

  Student ||--o{ Complaint : "files"
  SystemUser ||--o{ AppNotification : "receives"

  Material ||--o{ MaterialChunk : "indexed as"
```
