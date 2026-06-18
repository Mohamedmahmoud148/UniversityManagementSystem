# Data Flow Diagrams

This document contains Mermaid sequence diagrams for the seven most important user journeys in the university management system. Each diagram traces the full path of a request from the user's browser through all services and back.

---

## Flow 1 — Student Login and Role-Based Routing

This flow shows how a user logs in, receives tokens from both Firebase and .NET, and is routed to their role-specific dashboard.

```mermaid
sequenceDiagram
    participant B as Browser (React)
    participant FB as Firebase Auth
    participant CF as Cloud Function
    participant API as .NET Backend
    participant DB as PostgreSQL

    B->>FB: signInWithEmailAndPassword(email, password)
    FB-->>B: FirebaseUser + ID token (with custom claims: role, entityId)

    B->>API: POST /api/auth/login { email, password }
    API->>DB: SELECT SystemUser WHERE Email = ?
    DB-->>API: user record + BCrypt hash
    API->>API: BCrypt.Verify(password, hash)
    API->>API: Generate JWT (accessToken 15min + refreshToken 7d)
    API->>DB: Store hashed refreshToken
    API-->>B: { accessToken, refreshToken, role, entityId }

    B->>B: Store both tokens in AuthContext
    B->>B: Read role from Firebase custom claims
    B->>B: React Router: navigate to /{role}/dashboard

    Note over B: Student → /student/dashboard
    Note over B: Professor → /professor/dashboard
    Note over B: Admin → /admin/dashboard
```

---

## Flow 2 — Student Takes a Live Quiz

This flow shows the complete quiz lifecycle: professor starts it, students receive it in real-time, answer, submit, and receive scores.

```mermaid
sequenceDiagram
    participant P as Professor Browser
    participant FS as Firestore
    participant S as Student Browser
    participant CF as Cloud Function

    P->>FS: doc("quizSessions/XYZ").set({ status: "waiting", offeringId, duration: 600 })
    P->>FS: collection("quizSessions/XYZ/questions").add([...questions])

    FS-->>S: onSnapshot fires: new quizSession detected
    S->>S: Show "Quiz ready" notification

    P->>FS: doc("quizSessions/XYZ").update({ status: "active", startTime: serverTimestamp() })
    FS-->>S: onSnapshot fires: status changed to "active"
    S->>S: Start 10-minute countdown timer
    S->>S: Display questions

    loop Student answers questions
        S->>S: Select answer, store in local state
    end

    alt Timer reaches zero (auto-submit)
        S->>FS: doc("quizSessions/XYZ/responses/studentUID").set({ answers, submittedAt: serverTimestamp(), submitted: true })
    else Student clicks submit manually
        S->>FS: doc("quizSessions/XYZ/responses/studentUID").set({ answers, submittedAt: serverTimestamp(), submitted: true })
    end

    FS-->>CF: onWrite trigger fires for new response document
    CF->>CF: Calculate score (compare answers to correctAnswers)
    CF->>CF: Validate submittedAt <= startTime + duration (reject late)
    CF->>FS: doc("quizSessions/XYZ/responses/studentUID").update({ score, percentage, gradedAt })

    FS-->>S: onSnapshot: score received → navigate to results page
    FS-->>P: onSnapshot: live leaderboard updates
```

---

## Flow 3 — AI Chat: Professor Asks a Question About a Lecture

This flow shows how a professor's natural-language question reaches the AI service, retrieves lecture content, and returns a grounded answer.

```mermaid
sequenceDiagram
    participant P as Professor Browser
    participant CF as Cloud Function (AI relay)
    participant FA as FastAPI Orchestrator
    participant CHROMA as ChromaDB
    participant REDIS as Redis
    participant LLM as Claude (OpenRouter)
    participant FS as Firestore

    P->>CF: httpsCallable("sendAIMessage")({ message, sessionId, subjectCode })
    CF->>CF: Verify Firebase ID token
    CF->>CF: Extract role="Professor", entityId from custom claims
    CF->>FA: POST /api/chat { message, role, userId, sessionId, subjectCode }

    FA->>FA: Classify intent → "course_info"
    FA->>FA: Check INTENT_ROLES: Professor allowed? Yes

    FA->>REDIS: LRANGE chat:{userId}:{sessionId} 0 9
    REDIS-->>FA: Last 10 conversation turns

    FA->>CHROMA: query(collection="subject_{subjectCode}", text=message, n_results=5)
    CHROMA-->>FA: Top 5 lecture chunks with similarity scores

    FA->>FA: Filter chunks with score > 0.75
    FA->>FA: Build prompt: system + lecture chunks + history + message

    FA->>LLM: POST /chat/completions (streaming)
    LLM-->>FA: Streamed response tokens

    FA->>REDIS: LPUSH chat:{userId}:{sessionId} { role:"assistant", content:response }
    FA-->>CF: { response, intent, sources }

    CF->>FS: collection("chatHistory/{userId}/sessions/{sessionId}").add({ role:"assistant", content, timestamp })
    CF-->>P: response text

    P->>P: Display response in chat UI
    FS-->>P: onSnapshot: chat history updates (persistent record)
```

---

## Flow 4 — Enrollment and Grade Recording

This flow covers the complete academic lifecycle of one enrollment: student enrolls, professor enters grades, grades are published, and GPA is updated.

```mermaid
sequenceDiagram
    participant S as Student Browser
    participant API as .NET Backend
    participant DB as PostgreSQL
    participant HF as Hangfire Jobs
    participant SIG as SignalR Hub

    Note over S,DB: === ENROLLMENT PHASE ===

    S->>API: POST /api/enrollments { subjectOfferingId }
    API->>DB: Check EnrollmentOpen on Semester
    API->>DB: Check student regulation vs. subject level/semester
    API->>DB: Check SubjectPrerequisites — all passed?
    API->>DB: Check SubjectOffering capacity (row lock)
    DB-->>API: All checks pass
    API->>DB: INSERT Enrollment (status=Enrolled)
    API->>DB: INSERT Grade (pending, all scores = NULL)
    API->>DB: UPDATE SubjectOffering SET CurrentEnrollment += 1
    API-->>S: 201 Created { enrollmentId }

    Note over S,DB: === GRADING PHASE ===

    participant P as Professor Browser
    P->>API: PUT /api/grades/{enrollmentId} { midterm:75, final:82, lab:15 }
    API->>DB: UPDATE Grade SET MidtermScore=75, FinalScore=82, LabScore=15
    API->>DB: Compute TotalScore = weighted sum
    API->>DB: Derive LetterGrade and GradePoints
    DB-->>API: Grade record updated
    API-->>P: 200 OK

    P->>API: POST /api/grades/{enrollmentId}/publish
    API->>DB: UPDATE Grade SET IsPublished=true, GradedAt=NOW()
    API->>HF: Enqueue GpaRecalculationJob(studentId)
    API->>SIG: SendToUser(studentId, "GradePublished", { subjectName, letterGrade })
    API-->>P: 200 OK

    HF->>DB: SELECT all published Grades JOIN Enrollments for student
    HF->>HF: Compute weighted GPA = SUM(GradePoints * CreditHours) / SUM(CreditHours)
    HF->>DB: UPDATE Student SET CumulativeGPA = newGpa

    SIG-->>S: GradePublished event pushed via WebSocket
    S->>S: Show toast: "Your CS301 grade is published: B+"
```

---

## Flow 5 — AI Academic Advisor Query

This is the most complex flow in the system: the `academic_advisor` module combines regulation data, student roadmap, grade history, and LLM analysis to produce a deep advisory response.

```mermaid
sequenceDiagram
    participant S as Student Browser
    participant CF as Cloud Function
    participant FA as FastAPI
    participant API as .NET Backend
    participant CHROMA as ChromaDB
    participant REDIS as Redis
    participant LLM as Claude

    S->>CF: httpsCallable("sendAIMessage")({ message: "Am I on track to graduate?", sessionId })
    CF->>FA: POST /api/chat { message, role:"Student", userId, sessionId }

    FA->>FA: Classify intent → "academic_advice"
    FA->>FA: RBAC check: Student allowed? Yes

    Note over FA,API: Parallel data fetching

    FA->>API: GET /api/students/{entityId}
    FA->>API: GET /api/students/{entityId}/grades
    FA->>API: GET /api/students/{entityId}/roadmap
    FA->>API: GET /api/regulations?departmentId=X&active=true

    API-->>FA: Student profile (GPA, level, batch)
    API-->>FA: Full grade history (all semesters)
    API-->>FA: Roadmap (completed, current, remaining subjects)
    API-->>FA: Active regulation (required subjects, credit hours)

    FA->>CHROMA: query(collection="regulations", text=message, n_results=3)
    CHROMA-->>FA: Regulation text chunks (graduation requirements)

    FA->>REDIS: LRANGE chat:{userId}:{sessionId} 0 9
    REDIS-->>FA: Conversation history

    FA->>FA: Build long-form prompt:
    Note over FA: [System: You are an academic advisor...]
    Note over FA: [Context: Student GPA=3.2, Level 3, passed 78 credit hours]
    Note over FA: [Regulation: Must complete 140 credit hours, GPA >= 2.0]
    Note over FA: [Roadmap: 22 subjects completed, 14 remaining]
    Note over FA: [Grade history: ...]
    Note over FA: [User question: Am I on track to graduate?]

    FA->>LLM: POST /chat/completions
    LLM-->>FA: Structured advisory response (JSON)

    FA->>REDIS: LPUSH conversation history
    FA-->>CF: { response, sections: { summary, gpa_analysis, warnings, recommendations } }
    CF->>CF: Write to Firestore chatHistory
    CF-->>S: Advisory response rendered in chat UI
```

---

## Flow 6 — Bulk User Import

This flow shows how an admin imports a batch of students from an Excel file, creating both Firebase and .NET records.

```mermaid
sequenceDiagram
    participant A as Admin Browser
    participant FSTORAGE as Firebase Storage
    participant CF as Cloud Function (bulkImport)
    participant FBAUTH as Firebase Auth (Admin SDK)
    participant FSTORE as Firestore
    participant API as .NET Backend
    participant DB as PostgreSQL

    A->>FSTORAGE: uploadBytes("imports/students_batch.xlsx")
    FSTORAGE-->>A: Upload complete: storage path

    A->>CF: httpsCallable("bulkImport")({ storagePath, type:"Student", departmentId })
    CF->>FSTORAGE: getDownloadURL(storagePath)
    CF->>CF: Download and parse Excel file (xlsx library)
    CF->>CF: Validate rows: required fields, format checks

    loop For each student row
        CF->>CF: Generate email: {studentCode}@university.edu
        CF->>CF: Generate temporary password
        CF->>FBAUTH: createUser({ email, password })
        FBAUTH-->>CF: { uid }
        CF->>FBAUTH: setCustomUserClaims(uid, { role:"Student", departmentId })
        CF->>FSTORE: doc("users/{uid}").set({ email, role, departmentId, createdAt })
        CF->>API: POST /api/students { fullName, studentCode, nationalId, departmentId, batchId, firebaseUid }
        API->>DB: INSERT Student + INSERT SystemUser
        DB-->>API: 201 Created
        CF->>CF: Record success
    end

    CF->>FSTORE: doc("importResults/{importId}").set({ created:45, failed:2, errors:[...] })
    CF-->>A: { created: 45, failed: 2, errors: [...] }
    A->>A: Display import summary report
```

---

## Flow 7 — Engagement Tracking (Webcam to Firestore)

This flow shows how student attention is measured in real-time using MediaPipe in the browser and aggregated to Firestore.

```mermaid
sequenceDiagram
    participant S as Student Browser (MediaPipe)
    participant BUFFER as Score Buffer (in-memory)
    participant CF as Cloud Function (recordEngagement)
    participant FSTORE as Firestore

    Note over S: Professor starts class session (Firestore "engagementSession" doc created)
    S->>S: onSnapshot detects active session
    S->>S: Show consent dialog → student accepts
    S->>S: navigator.mediaDevices.getUserMedia({ video: true })
    S->>S: Initialize MediaPipe FaceMesh (WASM load ~2s)

    loop Every 100ms (requestAnimationFrame)
        S->>S: Draw frame to hidden canvas
        S->>S: FaceMesh.send(canvas)
        S->>S: Receive landmarks (468 points per face)
        S->>S: Compute yaw angle from nose/eye landmarks
        S->>BUFFER: Push score (100/50/0) to 5-second window buffer
    end

    loop Every 5 seconds
        BUFFER->>BUFFER: Average scores in buffer (e.g., 72.4)
        BUFFER->>CF: httpsCallable("recordEngagement")({ sessionId, offeringId, score: 72, timestamp })
        CF->>CF: Verify Firebase ID token
        CF->>FSTORE: doc("engagementScores/{offeringId}/records/{date}").update({ [studentUID]: arrayUnion({ t: timestamp, s: 72 }) })
        CF-->>BUFFER: Acknowledged
    end

    Note over S,FSTORE: Professor side

    participant P as Professor Browser
    P->>FSTORE: onSnapshot("engagementScores/{offeringId}/records/{date}")
    FSTORE-->>P: Real-time per-student score array
    P->>P: Compute rolling average per student
    P->>P: Display engagement heatmap on dashboard
```

---

## Summary

| Flow | Primary Services | Key Pattern |
|------|-----------------|-------------|
| 1. Login + routing | Firebase Auth + .NET | Dual-token auth |
| 2. Live quiz | Firestore + Cloud Function | Real-time onSnapshot + serverless scoring |
| 3. AI chat (lecture Q&A) | FastAPI + ChromaDB + Claude | RAG retrieval |
| 4. Enrollment + grading | .NET + Hangfire + SignalR | Background job + real-time push |
| 5. AI academic advisor | FastAPI + .NET + Claude | Orchestrator + parallel data fetch |
| 6. Bulk import | Firebase + Cloud Function + .NET | Serverless batch processing |
| 7. Engagement tracking | MediaPipe + Firebase | In-browser ML + real-time aggregation |
