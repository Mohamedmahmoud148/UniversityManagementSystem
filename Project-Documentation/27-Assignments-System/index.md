# Assignments System

> **Last refreshed:** 2026-05-31

---

## 1. Overview

The Assignments System supports the full lifecycle of academic assignments: creation by instructors, student submission (text + file), manual grading, AI-powered essay grading, and automated deadline reminders.

---

## 2. Assignment Entity

```csharp
Assignment {
    Title
    Description
    SubjectOfferingId    // which course
    DoctorId             // who created it
    Deadline             // DateTime (UTC)
    MaxGrade             // default 100
    AllowLateSubmission  // bool
    AiGradingEnabled     // bool — triggers AI grading on submission
    GradingRubric?       // JSON string — criteria for AI grader
}
```

---

## 3. AssignmentSubmission Entity

```csharp
AssignmentSubmission {
    AssignmentId / StudentId
    TextAnswer?      // essay text answer
    FileUrl?         // R2 public/signed URL
    StorageKey?      // R2 object key for deletion
    SubmittedAt
    IsLate           // true if submitted after Deadline
    Status           // Submitted | UnderReview | Graded | Rejected
    Grade?           // 0–MaxGrade
    Feedback?        // doctor's written feedback
    AiFeedback?      // AI-generated feedback text
    Strengths?       // AI-identified strengths
    Weaknesses?      // AI-identified areas for improvement
    IsAiGraded       // bool
    IsHumanReviewed  // bool — doctor reviewed AI grade
    ReviewedByDoctorId?
}
```

---

## 4. API Endpoints

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/assignments` | Doctor, Admin | Create assignment |
| GET | `/api/assignments/offering/{offeringId}` | Auth | List for offering |
| GET | `/api/assignments/{id}` | Auth | Detail |
| POST | `/api/assignments/{id}/submit` | Student | Submit (multipart, 100 MB max) |
| GET | `/api/assignments/{id}/submissions` | Doctor | All submissions |
| POST | `/api/assignments/submissions/{id}/grade` | Doctor | Manual grade |
| POST | `/api/assignments/submissions/{id}/ai-grade` | Doctor | AI grade |
| GET | `/api/assignments/{id}/my-submission` | Student | Own submission |
| DELETE | `/api/assignments/{id}` | Doctor, Admin | Delete |

---

## 5. Submission Flow

```
POST /api/assignments/{id}/submit (multipart/form-data)
Fields: textAnswer (optional), file (optional, max 100 MB)

AssignmentService.SubmitAsync():
  1. Fetch assignment → check if already submitted
  2. Compare SubmittedAt with Deadline → set IsLate flag
  3. If AllowLateSubmission=false AND IsLate → reject
  4. If file provided:
     → StorageService.UploadFileStreamAsync() → R2 "assignments/" folder
     → Store StorageKey + FileUrl
  5. CREATE AssignmentSubmission { status=Submitted, isLate, textAnswer, fileUrl }
  6. NotificationService.SendAsync(doctorId, "New submission: {title}")
```

---

## 6. AI Grading Pipeline

```
Doctor clicks "AI Grade" on a submission:
POST /api/assignments/submissions/{id}/ai-grade

AssignmentService.TriggerAiGradingAsync(submissionId):
  1. Fetch submission + assignment (title, description, rubric, maxGrade)
  2. AiService.GradeEssayAsync(submissionText, title, description, rubric, maxGrade)

FastAPI POST /api/ai/grade-submission:
  → GPT-4o-mini with structured JSON prompt
  → Returns:
    {
      "score": 82,
      "feedback": "Clear explanation of the algorithm. Missing edge case discussion.",
      "strengths": ["Good structure", "Correct time complexity analysis"],
      "weaknesses": ["Missing edge case for empty input", "No test cases"],
      "confidence": 0.87
    }

UPDATE AssignmentSubmission:
  { grade=82, aiFeedback=..., strengths=..., weaknesses=..., isAiGraded=true, status=Graded }
```

---

## 7. Automated Deadline Reminders

`AssignmentReminderJob` runs every 30 minutes via Hangfire:

```
Find assignments: Deadline > now AND Deadline <= now+24h AND deletedAt IS NULL
    │
For each assignment:
  1. Find students who have NOT submitted (excludes already-submitted students)
  2. If Deadline <= now+2h:
     title = "تذكير عاجل — موعد التسليم خلال ساعتين"
  3. Else:
     title = "تذكير — موعد تسليم الواجب غداً"
  4. Send notification to each student's SystemUserId
  5. Log: "Sent reminder for '{title}' to {count} students"
```

**Smart design:** Only non-submitters are notified — students who already submitted don't receive noise.

---

## 8. AI Chat Integration

Students can ask the AI about assignments:

```
"ايه واجباتي في كورس Data Structures؟"
→ Intent: assignment_query
→ AssignmentQueryModule:
   1. GET /api/assignments/offering/{offeringId}
   2. GET /api/assignments/{id}/my-submission for each
   3. Format: title + deadline countdown + submission status + grade
```

---

## 9. File Storage

Assignment files are stored in Cloudflare R2 under the `assignments/` prefix:
- Storage key: `assignments/{randomId}/{filename}`
- Download URLs are pre-signed with 60-minute expiry
- Deleted via `StorageService.DeleteAsync(storageKey)` when assignment is deleted

---

## 10. Status Lifecycle

```
Student submits → Submitted
    │
Doctor reviews  → UnderReview
    │
    ├─ Doctor grades manually → Graded
    ├─ AI grades → Graded (IsAiGraded=true)
    └─ Doctor rejects → Rejected
```
