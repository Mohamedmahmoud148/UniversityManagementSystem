---
layout: default
title: "System Diagrams"
---

# System Diagrams

All architectural and flow diagrams for the University Management System.
Rendered automatically on GitHub and GitHub Pages (Mermaid).

---

## 1. High-Level System Architecture

```mermaid
graph TB
    subgraph Client["Client Layer"]
        FE[Frontend App]
    end

    subgraph Backend[".NET Backend — ASP.NET Core 9"]
        API[28 Controllers]
        SVC[33 Services]
        HF[Hangfire Jobs]
        SR[SignalR Hub]
    end

    subgraph AI["FastAPI — AI Brain"]
        PL[Planner Agent]
        EX[Executor]
        MOD[Modules]
        MR[Model Router]
    end

    subgraph Infra["Infrastructure"]
        PG[(PostgreSQL)]
        R2[(Cloudflare R2)]
        CL[Claude AI]
    end

    FE -->|REST + JWT| API
    FE -->|WebSocket| SR
    FE -->|AI Chat| AI

    API --> SVC
    SVC --> PG
    SVC --> R2
    HF --> PG
    HF --> SR

    AI -->|ai-tools APIs| API
    PL --> MR
    EX --> MOD
    MR --> CL
```

---

## 2. Clean Architecture Layers

```mermaid
graph TD
    subgraph Core["Core Layer (no dependencies)"]
        ENT[Entities]
        INT[Interfaces]
        DTO[DTOs]
    end

    subgraph Infra["Infrastructure Layer"]
        SVC[Services]
        DB[AppDbContext / EF Core]
        MIG[Migrations]
        JOBS[Hangfire Jobs]
    end

    subgraph Api["API Layer"]
        CTRL[Controllers]
        MW[Middleware]
        AUTH[JWT Auth]
    end

    Api -->|depends on| Core
    Infra -->|implements| Core
    Api -->|uses| Infra
```

---

## 3. Authentication & JWT Flow

```mermaid
sequenceDiagram
    participant U as User
    participant API as .NET API
    participant DB as PostgreSQL

    U->>API: POST /api/auth/login { email, password }
    API->>DB: Find SystemUser by email
    DB-->>API: SystemUser record
    API->>API: Verify BCrypt hash
    API->>API: Generate JWT (sub, role, ProfileId, ProfileType)
    API->>DB: Save RefreshToken
    API-->>U: { accessToken, refreshToken }

    Note over U,API: Every subsequent request
    U->>API: GET /api/... + Authorization: Bearer {token}
    API->>API: Validate JWT signature + expiry
    API->>API: Extract role → apply [Authorize(Roles="...")]
    API-->>U: 200 OK / 401 / 403
```

---

## 4. Role-Based Access Control

```mermaid
graph LR
    subgraph Roles
        SA[SuperAdmin]
        AD[Admin]
        DR[Doctor]
        ST[Student]
    end

    subgraph Access
        EVERYTHING[Full System Access]
        STRUCTURE[Manage Structure + Users + Analytics]
        COURSE[Courses + Exams + Grades]
        OWN[Own Data + AI Chat + Complaints]
    end

    SA --> EVERYTHING
    AD --> STRUCTURE
    DR --> COURSE
    ST --> OWN

    SA -.->|includes| AD
    AD -.->|includes| DR
```

---

## 5. AI Chat Flow

```mermaid
sequenceDiagram
    participant U as User
    participant FE as Frontend
    participant FA as FastAPI
    participant PL as Planner Agent
    participant EX as Executor
    participant BE as .NET Backend
    participant CL as Claude AI

    U->>FE: Types a message
    FE->>FA: POST /api/chat { message, role, history }
    FA->>PL: Plan the intent
    PL->>CL: Analyze message + API schema
    CL-->>PL: { intent, tool, params }

    alt Tool needed
        PL->>EX: Execute tool
        EX->>BE: GET/POST /api/...
        BE-->>EX: Data
        EX->>CL: Summarize result
        CL-->>EX: Human response
    else No tool needed
        PL->>CL: Generate direct response
        CL-->>PL: Response
    end

    FA-->>FE: { response, intent_executed }
    FE-->>U: Display answer
```

---

## 6. AI Planner Decision Tree

```mermaid
flowchart TD
    MSG[User Message] --> KW{Layer-2 Keyword\nEngine Match?}
    KW -->|Yes — high confidence| DIRECT[Execute Direct Module]
    KW -->|No| CLAUDE[Claude Planner]

    CLAUDE --> INTENT{Intent Type}
    INTENT -->|data_query| DYN[DynamicApiModule\nAuto-calls REST API]
    INTENT -->|generate_exam| EXAM[ExamGenerationModule]
    INTENT -->|academic_advice| ADV[AcademicAdvisorModule]
    INTENT -->|complaint| COMP[ComplaintModule]
    INTENT -->|file_upload| FILE[FileProcessorModule]
    INTENT -->|chat| CHAT[Direct Claude Response]

    DIRECT --> OUT[AgentOutput]
    DYN --> OUT
    EXAM --> OUT
    ADV --> OUT
    COMP --> OUT
    FILE --> OUT
    CHAT --> OUT
```

---

## 7. Randomized Exam Flow

```mermaid
sequenceDiagram
    participant DR as Doctor
    participant API as .NET API
    participant CL as Claude AI
    participant DB as PostgreSQL
    participant ST as Student

    DR->>API: POST /api/exams/generate-ai\n{ questionCount:30, questionsPerStudent:10, isRandomized:true }
    API->>CL: Generate 30 MCQ questions
    CL-->>API: [ {questionText, options, correctAnswer, mark} × 30 ]
    API->>DB: Save Exam + 30 ExamQuestions\n(IsRandomized=true, QuestionsPerStudent=10)
    API-->>DR: Exam in Draft status

    DR->>API: Publish exam

    ST->>API: GET /api/exams/{id}/my-variant
    API->>DB: StudentExamVariant exists?
    DB-->>API: No

    API->>API: Shuffle 30 IDs → pick 10
    API->>DB: INSERT StudentExamVariant\n{ ExamId, StudentId, QuestionIdsJson }
    API-->>ST: 10 questions (correctAnswer=null)

    ST->>API: POST /api/exams/{id}/submit\n{ answers: [...10 answers] }
    API->>DB: Save ExamSubmission

    DR->>API: POST /api/exams/{id}/auto-grade
    API->>DB: For each submission:\nlookup StudentExamVariant → grade only their 10 questions
    API-->>DR: { gradedCount: N }
```

---

## 8. Enrollment & Academic Roadmap Flow

```mermaid
flowchart TD
    ST[Student] --> REQ[Request Auto-Enrollment]
    REQ --> API[POST /api/enrollments/auto-enroll]
    API --> REG[Load Active Regulation]
    REG --> PLAN[Get Study Plan\nRequired Credits per Level]
    PLAN --> COMP[Calculate Completed Credits\nfrom StudentGrades]
    COMP --> ELIG{Eligible for\nnext level?}
    ELIG -->|Yes| AVAIL[Find Available Offerings\nfor required subjects]
    ELIG -->|No| BLOCK[Return: prerequisites not met]
    AVAIL --> ENROLL[Create Enrollment records]
    ENROLL --> NOTIFY[Push Notification to Student]
    NOTIFY --> DONE[Done ✓]
```

---

## 9. Notification System Flow

```mermaid
sequenceDiagram
    participant SRC as Source\n(Job / Doctor / System)
    participant NS as NotificationService
    participant DB as PostgreSQL
    participant SR as SignalR Hub
    participant FE as Frontend

    SRC->>NS: SendAsync(userId, title, message)
    NS->>DB: INSERT AppNotification
    NS->>SR: SendAsync("ReceiveNotification", payload)
    SR-->>FE: WebSocket push (instant)

    Note over FE: User sees toast notification

    FE->>NS: GET /api/notifications/my (on load)
    NS->>DB: SELECT unread notifications
    DB-->>NS: List
    NS-->>FE: Notification list with badge count

    FE->>NS: POST /api/notifications/{id}/read
    NS->>DB: UPDATE IsRead=true
```

---

## 10. Background Jobs Schedule

```mermaid
gantt
    title Background Jobs (Hangfire)
    dateFormat HH:mm
    axisFormat %H:%M

    section Daily (Midnight)
    AcademicRiskJob         : 00:00, 30m

    section Every 30 Minutes
    ExamReminderJob         : 00:00, 30m
    ExamReminderJob         : 00:30, 30m

    section Weekly
    ComplaintIntelligenceJob (Weekly) : 00:00, 1h

    section Monthly
    ComplaintIntelligenceJob (Monthly): 00:00, 1h
```

---

## 11. Database — Core Entity Relationships

```mermaid
erDiagram
    SystemUser ||--o| Student : "is"
    SystemUser ||--o| Doctor : "is"
    SystemUser ||--o| Admin : "is"

    Student }o--o{ SubjectOffering : "enrolled in"
    Doctor ||--o{ SubjectOffering : "teaches"

    SubjectOffering }o--|| Subject : "offers"
    SubjectOffering }o--|| Semester : "in"

    Subject }o--|| Department : "belongs to"
    Department }o--|| College : "belongs to"
    College }o--|| University : "belongs to"

    SubjectOffering ||--o{ Exam : "has"
    Exam ||--o{ ExamQuestion : "contains"
    Exam ||--o{ ExamSubmission : "receives"
    Exam ||--o{ StudentExamVariant : "assigns"

    Student ||--o{ StudentGrade : "has"
    StudentGrade }o--|| SubjectOffering : "for"

    Student ||--o{ Complaint : "submits"
    Complaint ||--o| ComplaintAnalysis : "analyzed by AI"
```

---

## 12. Complaint Intelligence Pipeline

```mermaid
flowchart LR
    C1[Complaint 1] --> AGG[Aggregator]
    C2[Complaint 2] --> AGG
    C3[Complaint N] --> AGG

    AGG --> CL[Claude AI\nAnalysis]
    CL --> ANL[ComplaintAnalysis\nsentiment + category + priority]
    ANL --> CLU[ComplaintCluster\nGrouped by topic]

    CLU --> REP[Weekly / Monthly\nIntelligence Report]
    REP --> NOTIF[Admin Notification]
```

---

## 13. File Upload Flow

```mermaid
sequenceDiagram
    participant U as User
    participant API as .NET API
    participant FS as FileService
    participant R2 as Cloudflare R2

    U->>API: POST /api/file/upload\nmultipart/form-data
    API->>FS: UploadFileStreamAsync(stream, fileName)
    FS->>R2: PUT object (UUID key)
    R2-->>FS: Public URL
    FS-->>API: { fileUrl, storageKey }
    API->>DB: Save UploadedFile record
    API-->>U: { fileId, fileUrl }
```

---

## 14. Deployment Architecture

```mermaid
graph TB
    subgraph Internet
        USER[Users / Browser]
        CF[Cloudflare CDN + R2]
    end

    subgraph Railway["Railway.app"]
        BE[.NET Backend\nASP.NET Core 9]
        FA[FastAPI\nAI Service]
    end

    subgraph DB["Supabase / Neon"]
        PG[(PostgreSQL)]
    end

    USER -->|HTTPS| BE
    USER -->|HTTPS| FA
    USER -->|Files| CF

    BE --> PG
    BE --> CF
    FA --> BE
    FA -->|Claude API| ANT[Anthropic API]
```
