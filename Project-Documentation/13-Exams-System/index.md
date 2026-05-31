# Exams System

> **Last refreshed:** 2026-05-31

---

## 1. Exam Entity

```csharp
Exam {
    Title
    ExamType          // Quiz | Midterm | Final
    ExamStatus        // Draft | Published | Closed
    StartTime / EndTime
    TotalMarks
    SubjectOfferingId
    CreatedByDoctorId
    IsRandomized      // different question order per student
    QuestionsPerStudent  // how many questions drawn per student
}
```

---

## 2. Exam Lifecycle

```
Doctor creates exam (Draft)
    │
Add questions (MCQ, True/False, Short Answer, Essay)
    │
Configure: randomization, time window, total marks
    │
Publish exam (Published) → enrolled students can take it
    │
Students submit answers within time window
    │
Auto-grade objective questions (MCQ, T/F)
AI-grade essays (optional, via OpenRouter)
    │
Doctor reviews + finalizes grades
    │
Close exam (Closed) → results visible to students
```

---

## 3. Question Types

| Type | Auto-graded? | AI-graded? |
|------|-------------|-----------|
| MCQ (Multiple Choice) | ✅ | — |
| True/False | ✅ | — |
| Short Answer | Partial | ✅ |
| Essay | ❌ | ✅ |

---

## 4. Randomization

When `IsRandomized = true`:
- Each student gets a different random ordering of questions
- MCQ answer options are also shuffled per student
- `QuestionsPerStudent` controls how many questions from the pool each student receives
- Ensures no two students see the same exam

---

## 5. API Endpoints

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/exams` | Doctor, Admin | Create exam |
| GET | `/api/exams/offering/{offeringId}` | Auth | List for offering |
| GET | `/api/exams/{id}` | Auth | Detail + questions |
| PUT | `/api/exams/{id}/publish` | Doctor | Publish |
| PUT | `/api/exams/{id}/close` | Doctor | Close |
| POST | `/api/exams/{id}/submit` | Student | Submit answers |
| GET | `/api/exams/{id}/results` | Doctor | All submissions |
| GET | `/api/exams/{id}/my-submission` | Student | Own result |
| GET | `/api/exams/my-exams` | Doctor | Doctor's exams |

---

## 6. ExamSubmission Entity

```csharp
ExamSubmission {
    ExamId / StudentId
    AnswersJson        // final answers
    DraftAnswersJson   // auto-saved draft (last saved = backup)
    LastSavedAt        // when draft was last saved
    Score
    IsGraded
    GradingJson        // per-question grade breakdown
    SubmittedAt
    IsCompleted
}
```

---

## 7. Proctoring

`ExamProctoringEvent` records browser events during exam:
- Tab switch / window blur
- Focus loss events
- Timestamps of each event

Doctor can review proctoring events alongside the student's submission.

```
POST /api/proctoring/event
Body: { examSubmissionId, eventType: "TabSwitch", timestamp }
```

---

## 8. AI Exam Generation

```
Doctor: "اعمل امتحان ميدتيرم لمادة Data Structures — 15 سؤال"
    │
Intent: generate_exam
    │
ExamGenerationModule:
  1. Resolve subjectOfferingId
  2. Search RAG for course material context
  3. OpenRouter generates structured question bank:
     { mcq: [...], trueFalse: [...], shortAnswer: [...] }
  4. Return to .NET → Doctor reviews → Stores questions → Publishes
```

---

## 9. Exam Reminders (Hangfire)

`ExamReminderJob` runs every 30 minutes:
- Finds Published exams starting within next 24 hours
- Sends bilingual notification (Arabic/English) to all enrolled students
- 24h window: "امتحان غداً"
- 2h window: "امتحان خلال ساعتين"

---

## 10. Grade Finalization

After exam scoring:
1. `StudentGrade` record created/updated with `FinalScore` + `GradeLetter`
2. `IsFinalized = true` — grade becomes part of GPA calculation
3. Student receives grade notification
