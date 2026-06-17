# 23 — System Diagrams
## Visual Architecture Using Mermaid Diagrams

All diagrams use Mermaid syntax. Render them at https://mermaid.live or in any Markdown viewer that supports Mermaid.

---

## Diagram 1: Overall System Architecture

```mermaid
graph TB
    subgraph CLIENT["Browser (React 18 + CRA)"]
        UI[React Pages & Components]
        CTX[AuthContext / State]
        GUARD[Route Guards]
    end

    subgraph FIREBASE["Firebase Platform"]
        AUTH[Firebase Auth\nCustom Claims + JWT]
        FS[Cloud Firestore\nNoSQL Real-time DB]
        ST[Firebase Storage\nFiles & PDFs]
        FN[Cloud Functions\nServerless Node.js]
        HOST[Firebase Hosting\nCDN Global]
    end

    subgraph DOTNET[".NET 8 API (Backend)"]
        CTRL[REST Controllers]
        SVC[Services Layer]
        DB[(PostgreSQL)]
    end

    subgraph FASTAPI["FastAPI (AI Service)"]
        QUIZ[Quiz Generator]
        CHAT[RAG Chat]
        ADVIS[Academic Advisor]
    end

    USER((User)) --> HOST
    HOST --> UI
    UI --> AUTH
    UI --> FS
    UI --> ST
    UI --> FN
    UI -->|Axios + JWT| CTRL
    UI -->|fetch POST| QUIZ
    FN -->|Admin SDK| AUTH
    FN --> FS
    FN -->|HTTP| CHAT
    CTRL --> SVC --> DB
    FASTAPI -->|LLM API| LLM[OpenAI / Gemini]
```

---

## Diagram 2: Folder Hierarchy

```mermaid
graph TD
    ROOT[graduation-project-feras/]
    ROOT --> SRC[src/]
    ROOT --> FNS[functions/]
    ROOT --> PUB[public/]
    ROOT --> CFG[Config Files]

    SRC --> PAGES[pages/\nAll page components]
    SRC --> COMP[components/\nReusable UI]
    SRC --> CTX[context/\nReact Context]
    SRC --> FB[firebase/\nFirestore API layer]
    SRC --> HOOK[hooks/\nCustom hooks]
    SRC --> SERV[services/\nAxios + user service]
    SRC --> ROUTE[routes/\nAppRoutes.jsx]
    SRC --> LIB[lib/\nPure utilities]
    SRC --> UTILS[utils/\nHelper functions]
    SRC --> AUTH2[auth/\nRoute guards]

    PAGES --> STUDENT[student/]
    PAGES --> PROF[professor/]
    PAGES --> ADMIN[admin/]
    PAGES --> ASST[assistant/]
    PAGES --> SUPER[super_admin/]
    PAGES --> SHARED[shared/\nSignIn, NotFound]

    FNS --> FIDX[index.js\nAll Cloud Functions]
    FNS --> FPKG[package.json]
```

---

## Diagram 3: Authentication Flow

```mermaid
sequenceDiagram
    actor U as User
    participant SI as SignIn.jsx
    participant FA as Firebase Auth
    participant AC as AuthContext
    participant RT as React Router
    participant PG as ProtectedRoute / RequireRole

    U->>SI: Enter email + password
    SI->>FA: signInWithEmailAndPassword()
    FA-->>SI: UserCredential
    SI->>FA: getIdTokenResult(forceRefresh=true)
    FA-->>SI: { claims: { role: "student" } }
    SI->>RT: navigate("/student")

    Note over FA,AC: onAuthStateChanged fires
    FA->>AC: user object
    AC->>FA: getIdTokenResult()
    FA-->>AC: role claim
    AC-->>AC: setUser(), setRole()

    RT->>PG: render /student route
    PG->>FA: getIdTokenResult()
    FA-->>PG: role = "student"
    PG->>PG: "student" === requiredRole ✓
    PG-->>RT: render <StudentLayout />
```

---

## Diagram 4: Routing Flow

```mermaid
flowchart TD
    START([Browser URL]) --> APPROUTES[AppRoutes.jsx]

    APPROUTES --> ROOT{path?}

    ROOT -->|"/"| SIGNIN[SignIn Page]
    ROOT -->|"/admin/*"| RR1[RequireRole admin]
    ROOT -->|"/super_admin/*"| RR2[RequireRole super_admin]
    ROOT -->|"/professor/*"| RR3[RequireRole professor]
    ROOT -->|"/student"| PR1[ProtectedRoute student]
    ROOT -->|"/prof/*"| PR2[ProtectedRoute professor]
    ROOT -->|"/asst/*"| PR3[ProtectedRoute assistant]
    ROOT -->|"*"| NF[404 NotFound]

    RR1 --> RCHECK1{Has token?\nRole = admin?}
    RCHECK1 -->|No| RED1[redirect /]
    RCHECK1 -->|Yes| ADMINLAYOUT[AdminLayout + Outlet]

    PR1 --> PCHECK1{Has token?\nRole = student?}
    PCHECK1 -->|No token| RED2[redirect /]
    PCHECK1 -->|Token but wrong role| RED3[redirect /]
    PCHECK1 -->|Correct role| STUDENTLAYOUT[StudentLayout + Outlet]

    STUDENTLAYOUT --> SHOME[StudentHome /]
    STUDENTLAYOUT --> SCOURSES[StudentCoursesPage /courses]
    STUDENTLAYOUT --> SQUIZZES[StudentQuizzesPage /quizzes]
    STUDENTLAYOUT --> SROADMAP[StudentRoadmapPage /roadmap]
```

---

## Diagram 5: Quiz Feature — Sequence Diagram

```mermaid
sequenceDiagram
    actor P as Professor
    actor S as Student
    participant UI_P as ProfessorQuizzesPage
    participant UI_S as StudentQuizTakePage
    participant FS as Firestore
    participant FAPI as FastAPI

    Note over P,FAPI: Professor creates quiz

    P->>UI_P: Upload PDF
    UI_P->>FAPI: POST /api/generate-quiz (FormData)
    FAPI-->>UI_P: { questions: [...] }
    P->>UI_P: Review + click Publish
    UI_P->>FS: setDoc(quizzes/{id}, { isPublished: true })

    Note over S,FS: Student takes quiz (real-time)

    FS-->>UI_S: onSnapshot fires (new quiz visible)
    S->>UI_S: Open quiz
    UI_S->>FS: getDoc(quizzes/{id})
    FS-->>UI_S: quiz data
    UI_S->>UI_S: Start countdown timer
    S->>UI_S: Answer questions
    S->>UI_S: Click Submit
    UI_S->>UI_S: calculateResult(questions, answers)
    UI_S->>FS: addDoc(quizSubmissions, { score, ... })
    FS-->>UI_S: write confirmed
    UI_S->>UI_S: navigate to /result
```

---

## Diagram 6: Engagement Tracker — Data Flow

```mermaid
flowchart LR
    CAM[Webcam Stream] --> VID[video element]
    VID --> MP[MediaPipe FaceLandmarker\nWASM - runs in browser]
    MP --> NOSE[Nose tip X coordinate\n0.0 = left, 1.0 = right]
    NOSE --> CALC{offset > 0.15?}
    CALC -->|No - face centered| FOCUSED[focused]
    CALC -->|Yes - face off-center| DISTRACT[distracted]
    MP --> NOFACE{No face?}
    NOFACE -->|Yes| AWAY[away]

    FOCUSED --> CNT[countsRef\nIn-memory accumulator]
    DISTRACT --> CNT
    AWAY --> CNT

    CNT --> TICK{Every 10 samples\n≈ 10 seconds}
    TICK --> FN[Firebase Function\npushEngagement]
    FN --> FSDB[(Firestore\nengagementAgg)]

    style MP fill:#f0f4ff
    style FN fill:#fff0e0
    style FSDB fill:#e0ffe0
```

---

## Diagram 7: State Management Architecture

```mermaid
graph TD
    subgraph GLOBAL["Global State (React Context)"]
        AC[AuthContext\nuser, role, loading]
    end

    subgraph LOCAL["Local State (useState per page)"]
        LD[data, loading, error\nper-page pattern]
        FORM[formData, formErrors\nper-form pattern]
        MODAL[createOpen, editTarget\nmodal pattern]
    end

    subgraph HOOKS["Custom Hooks"]
        UC[useColleges\noptimistic updates]
        UA[useAuth\nFirebase Auth state]
        UAU[useAuthUser\nsynced profile]
    end

    subgraph REF["Ref State (useRef)"]
        SG[submitGuardRef\ndouble-submit prevention]
        INT[intervalRef\ntimer cleanup]
        STREAM[streamRef\nwebcam cleanup]
    end

    AC --> ALL_PAGES[All Pages via useContext]
    LOCAL --> PAGE_LEVEL[Page-Level Components]
    HOOKS --> PAGE_LEVEL
    REF --> SPECIAL[Quiz Timer\nEngagement Tracker]
```

---

## Diagram 8: Firebase Security Rules — Decision Tree

```mermaid
flowchart TD
    REQ[Firestore Request] --> AUTH{Authenticated?}
    AUTH -->|No| DENY1[❌ Deny]
    AUTH -->|Yes| CLAIMS[Get role from token claims]

    CLAIMS --> ROLE{Role?}

    ROLE -->|super_admin| ALLOW_ALL[✅ Allow everything]
    ROLE -->|admin| ADMIN_CHECK{What collection?}
    ROLE -->|professor| PROF_CHECK{What collection?}
    ROLE -->|student| STUDENT_CHECK{What collection?}
    ROLE -->|assistant| ASST_CHECK{What collection?}

    ADMIN_CHECK -->|users, colleges, buildings| ALLOW_A[✅ Allow read/write]
    ADMIN_CHECK -->|quizzes, quizSubmissions| DENY_A[❌ Deny write]

    PROF_CHECK -->|prof_courses/{theirId}| ALLOW_P[✅ Allow own courses]
    PROF_CHECK -->|prof_courses/{otherId}| DENY_P[❌ Deny]
    PROF_CHECK -->|quizzes they created| ALLOW_P2[✅ Allow]

    STUDENT_CHECK -->|quizSubmissions/{theirId}| ALLOW_S[✅ Allow own]
    STUDENT_CHECK -->|quizSubmissions/{otherId}| DENY_S[❌ Deny]
    STUDENT_CHECK -->|quizzes isPublished=true| ALLOW_S2[✅ Allow read]
```

---

## Diagram 9: API Integration Layer

```mermaid
graph LR
    subgraph PAGES[React Pages]
        P1[ProfQuizzesPage]
        P2[StudentQuizPage]
        P3[AIChat]
        P4[MaterialsPage]
        P5[RoadmapPage]
    end

    subgraph FIREBASE_API[Firebase API Layer\nsrc/firebase/]
        M1[materialsApi.js]
        M2[courseAiApi.js]
        M3[roomsApi.js]
        M4[scheduleApi.js]
        M5[attendanceFunctions.js]
    end

    subgraph EXTERNAL[External Services]
        FS[(Firestore)]
        ST[Storage]
        FN[Cloud Functions]
        DOTNET[.NET REST API]
        FAPI[FastAPI]
    end

    P1 -->|direct inline| FS
    P2 -->|direct inline| FS
    P3 --> M2 --> FN --> FAPI
    P4 --> M1 --> ST
    P4 --> M1 --> FS
    P5 -->|Axios src/services/http.js| DOTNET
    M3 --> FS
    M4 --> FS
    M5 --> FN
```

---

## Diagram 10: Component Hierarchy (Student Section)

```mermaid
graph TD
    APP[App.jsx] --> AR[AppRoutes.jsx]
    AR --> PR[ProtectedRoute]
    PR --> SL[StudentLayout.jsx]

    SL --> SIDEBAR[StudentSidebar.jsx]
    SL --> OUTLET[Outlet]

    OUTLET --> SH[StudentHome.jsx]
    OUTLET --> SC[StudentCoursesPage.jsx]
    OUTLET --> SQ[StudentQuizzesPage.jsx]
    OUTLET --> SQTAKE[StudentQuizTakePage.jsx]
    OUTLET --> SQRES[StudentQuizResultPage.jsx]
    OUTLET --> SROADMAP[StudentRoadmapPage.jsx]
    OUTLET --> SMATERIALS[StudentCourseMaterialsPage.jsx]

    SQTAKE --> TIMER[Timer Display]
    SQTAKE --> QCARD[QuestionCard]
    SQTAKE --> ENGAGE[EngagementTracker.jsx]
    ENGAGE --> MEDIAPIPE[MediaPipe WASM]
```

---

## Diagram 11: User Role Journey Map

```mermaid
journey
    title Student User Journey
    section Authentication
      Open website:        5: Student
      Enter credentials:   4: Student
      Get routed to /student: 5: Student
    section Academics
      View enrolled courses: 5: Student
      Download materials:    4: Student
      View academic roadmap: 5: Student
    section Quizzes
      See published quizzes: 5: Student
      Take quiz with timer:  3: Student
      View result:           5: Student
    section Sessions
      Join live session:     4: Student
      Engagement tracked:    2: Student
```

```mermaid
journey
    title Professor User Journey
    section Course Management
      View assigned courses: 5: Professor
      Upload materials:      4: Professor
      Create quiz manually:  4: Professor
    section AI Features
      Generate quiz from PDF: 5: Professor
      Chat with AI assistant: 5: Professor
    section Attendance
      Mark attendance:        4: Professor
      View engagement report: 4: Professor
```

---

## Diagram 12: Data Entity Relationships

```mermaid
erDiagram
    USER {
        string uid PK
        string fullName
        string email
        string role
        string collegeId
        string departmentId
    }

    QUIZ {
        string id PK
        string title
        string createdBy FK
        string collegeId
        boolean isPublished
        array questions
    }

    QUIZ_SUBMISSION {
        string id PK
        string quizId FK
        string studentUid FK
        number score
        number percentage
        array wrongQuestions
    }

    CONVERSATION {
        string id PK
        string courseDocId
        string professorId FK
    }

    MESSAGE {
        string id PK
        string conversationId FK
        string role
        string content
        string status
    }

    ATTENDANCE {
        string id PK
        string offeringId
        string professorId FK
        array students
    }

    ENGAGEMENT_AGG {
        string id PK
        string sessionId
        string studentId FK
        number focusedCount
        number distractedCount
        number awayCount
    }

    USER ||--o{ QUIZ_SUBMISSION : "submits"
    QUIZ ||--o{ QUIZ_SUBMISSION : "has"
    USER ||--o{ CONVERSATION : "owns"
    CONVERSATION ||--o{ MESSAGE : "contains"
    USER ||--o{ ENGAGEMENT_AGG : "tracked in"
```

---

## Diagram 13: Build and Deployment Pipeline

```mermaid
flowchart LR
    DEV[Developer\nLocal Machine] -->|git push| REPO[GitHub Repo]
    REPO -->|manual trigger| BUILD[npm run build\nWebpack bundles]
    BUILD --> DIST[build/ directory\nOptimized static files]
    DIST -->|firebase deploy| CDN[Firebase Hosting\nGlobal CDN]
    CDN --> USERS[Users Worldwide\nhttps://bsnu.web.app]

    REPO -->|firebase deploy --only functions| FNDEPLOY[Firebase Functions\nCloud Deploy]
    FNDEPLOY --> FNS[Cloud Functions\nActive]
```

---

## Diagram 14: Dual Route Guard System

```mermaid
flowchart TD
    URL[Route Request] --> TYPE{Which guard?}

    TYPE -->|/admin /professor /super_admin| RR[RequireRole.js\nLegacy Guard]
    TYPE -->|/student /prof /asst| PR[ProtectedRoute.jsx\nModern Guard]

    RR --> RR1[onAuthStateChanged listener\nsetup in useEffect]
    RR1 --> RR2{User exists?}
    RR2 -->|No| REDIR1[→ /]
    RR2 -->|Yes| RR3[getIdTokenResult]
    RR3 --> RR4{role === required?}
    RR4 -->|No| REDIR2[→ /]
    RR4 -->|Yes| RENDER1[Render children]

    PR --> PR1[useAuthUser hook]
    PR1 --> PR2{authLoading?}
    PR2 -->|Yes| SPIN[Show spinner]
    PR2 -->|No| PR3{User exists?}
    PR3 -->|No| REDIR3[→ /]
    PR3 -->|Yes| PR4[getIdTokenResult]
    PR4 --> PR5{role matches?}
    PR5 -->|No| REDIR4[→ /]
    PR5 -->|Yes| RENDER2[Render children]

    style RR fill:#fff3e0
    style PR fill:#e8f5e9
```
