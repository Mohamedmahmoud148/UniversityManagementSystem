---
layout: default
title: "Assignments System + AI Auto-Grading"
---

# Assignments System + AI Auto-Grading

> **Doctors create assignments with optional AI grading; students submit text or files; AI grades against a rubric; doctors approve or override.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [Database Schema](#2-database-schema)
3. [API Endpoints](#3-api-endpoints)
4. [Assignment Lifecycle](#4-assignment-lifecycle)
5. [AI Auto-Grading Flow](#5-ai-auto-grading-flow)
6. [RBAC Rules](#6-rbac-rules)
7. [DTOs Reference](#7-dtos-reference)

---

## 1. Overview

The Assignments System (Phase 3) adds a complete assignment workflow on top of the existing exam system:

- **Doctors** create assignments with a deadline, max grade, and optional AI grading rubric
- **Students** submit a text answer and/or an uploaded file before the deadline
- If `AiGradingEnabled = true`, the .NET backend calls FastAPI `POST /api/ai/grade-submission` to get an AI score with feedback, strengths, and weaknesses
- **Doctors** review AI grades and can approve or override them
- All grading history is preserved (AI grade + human review are separate fields)

---

## 2. Database Schema

### `Assignments` Table (migration: `AddAssignmentsSystem`)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `Title` | text | Assignment title |
| `Description` | text | Full instructions |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings (RESTRICT) |
| `DoctorId` | ULID | FK → Doctors (RESTRICT) |
| `Deadline` | datetime | Submission cutoff |
| `MaxGrade` | float | Maximum possible grade |
| `AllowLateSubmission` | bool | Accept submissions after deadline? |
| `AiGradingEnabled` | bool | Trigger AI grading on submission? |
| `GradingRubric` | text? | Rubric text for AI grader |
| `CreatedAt` | datetime | |

### `AssignmentSubmissions` Table (migration: `AddAssignmentsSystem`)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `AssignmentId` | ULID | FK → Assignments (CASCADE) |
| `StudentId` | ULID | FK → Students (RESTRICT) |
| `TextAnswer` | text? | Written answer |
| `FileUrl` | text? | Uploaded file public URL |
| `StorageKey` | text? | Cloudflare R2 key |
| `SubmittedAt` | datetime | Submission timestamp |
| `IsLate` | bool | True if submitted after deadline |
| `Status` | int | Submitted=0, UnderReview=1, Graded=2, Rejected=3 |
| `Grade` | float? | Final confirmed grade |
| `Feedback` | text? | Doctor feedback |
| `AiFeedback` | text? | AI-generated feedback text |
| `Strengths` | text? | AI strengths (JSON array) |
| `Weaknesses` | text? | AI weaknesses (JSON array) |
| `IsAiGraded` | bool | AI grading was applied |
| `IsHumanReviewed` | bool | Doctor has reviewed AI grade |
| `ReviewedByDoctorId` | ULID? | FK → Doctor who reviewed |

**Constraint:** UNIQUE (`AssignmentId`, `StudentId`) — one submission per student per assignment.

---

## 3. API Endpoints

All endpoints are on `AssignmentsController` at `/api/assignments`.

| Method | Endpoint | Role | Description |
|--------|----------|------|-------------|
| `POST` | `/api/assignments` | Doctor | Create a new assignment |
| `GET` | `/api/assignments/{id}` | Doctor, Student, Admin | Get assignment details |
| `PUT` | `/api/assignments/{id}` | Doctor (own) | Update assignment before deadline |
| `DELETE` | `/api/assignments/{id}` | Doctor (own), Admin | Delete assignment |
| `GET` | `/api/assignments/by-offering/{offeringId}` | Doctor, Student | List all assignments for an offering |
| `POST` | `/api/assignments/{id}/submit` | Student | Submit an answer (text + optional file) |
| `GET` | `/api/assignments/{id}/my-submission` | Student | Get own submission status and grade |
| `GET` | `/api/assignments/{id}/submissions` | Doctor, Admin | List all submissions for an assignment |
| `POST` | `/api/assignments/{id}/submissions/{submissionId}/grade` | Doctor | Manually grade or override AI grade |
| `POST` | `/api/assignments/{id}/submissions/{submissionId}/ai-grade` | Doctor | Trigger AI grading for a specific submission |

---

## 4. Assignment Lifecycle

```
Doctor creates assignment
  └── POST /api/assignments
        AiGradingEnabled: true
        GradingRubric: "40 pts correctness, 30 clarity, 30 depth"
        Deadline: 2026-06-01T23:59:00Z

Student submits before deadline
  └── POST /api/assignments/{id}/submit
        TextAnswer: "Merge sort works by dividing the array..."
        (optional FileUrl from prior upload)
        → IsLate = false (before deadline)
        → Status = Submitted

If AiGradingEnabled:
  AssignmentService calls FastAPI POST /api/ai/grade-submission
        → Returns {score, feedback, strengths, weaknesses, confidence}
        → Stored in AiFeedback, Strengths, Weaknesses, IsAiGraded=true
        → Status → UnderReview

Doctor reviews AI grade
  └── POST /api/assignments/{id}/submissions/{submissionId}/grade
        Grade: 85.0  (can accept AI score or override)
        Feedback: "Good explanation, add more examples next time."
        → IsHumanReviewed = true
        → ReviewedByDoctorId = doctorId
        → Status = Graded

Student sees result
  └── GET /api/assignments/{id}/my-submission
        → Grade: 85.0, Feedback: "...", AiFeedback: "...", Strengths: [...], Weaknesses: [...]
```

---

## 5. AI Auto-Grading Flow

The AI grading is handled by the FastAPI microservice.

**Endpoint called by .NET:**
```
POST /api/ai/grade-submission
```

**Request payload:**
```json
{
  "submission_text": "Merge sort divides the array recursively...",
  "assignment_title": "Assignment 1 — Sorting Algorithms",
  "description": "Explain merge sort and compare it with quicksort",
  "rubric": "40 pts correctness, 30 clarity, 30 depth of comparison",
  "max_grade": 100
}
```

**Response:**
```json
{
  "score": 82.5,
  "feedback": "Good explanation of merge sort. The quicksort comparison section was too brief.",
  "strengths": [
    "Accurate time complexity analysis (O(n log n))",
    "Clear divide-and-conquer explanation"
  ],
  "weaknesses": [
    "Quicksort space complexity not discussed",
    "No concrete examples with arrays"
  ],
  "confidence": 0.87
}
```

**Grading prompt principles:**
- LLM scores each rubric dimension independently and sums them
- Cannot award more than `max_grade`
- Instructed: "Base evaluation ONLY on what the student wrote. Do not assume knowledge not demonstrated."
- `response_format = {"type": "json_object"}` enforced — no free-form text
- Low confidence (< 0.6) flagged for mandatory doctor review

---

## 6. RBAC Rules

| Action | Allowed Roles |
|--------|--------------|
| Create assignment | Doctor (own offering only), Admin |
| Update/delete assignment | Doctor (own), Admin |
| View assignment | Doctor (own offering), Student (enrolled), Admin |
| Submit | Student (enrolled, before deadline unless AllowLate) |
| View all submissions | Doctor (own offering), Admin |
| Grade submission | Doctor (own offering), Admin |
| Trigger AI grade | Doctor (own offering), Admin |
| View own submission | Student (own) |

---

## 7. DTOs Reference

### CreateAssignmentDto
```json
{
  "title": "Assignment 1 — Sorting",
  "description": "Explain merge sort...",
  "subjectOfferingId": "01H...",
  "deadline": "2026-06-01T23:59:00Z",
  "maxGrade": 100,
  "allowLateSubmission": false,
  "aiGradingEnabled": true,
  "gradingRubric": "40 correctness, 30 clarity, 30 depth"
}
```

### SubmitAssignmentDto
```json
{
  "textAnswer": "Merge sort works by...",
  "fileUrl": "https://cdn.r2.example.com/submissions/file.pdf",
  "storageKey": "submissions/01H.../file.pdf"
}
```

### GradeAssignmentSubmissionDto
```json
{
  "grade": 85.0,
  "feedback": "Good work, add more examples next time."
}
```

### AiGradingResultDto (internal, from FastAPI)
```json
{
  "score": 82.5,
  "feedback": "...",
  "strengths": ["..."],
  "weaknesses": ["..."],
  "confidence": 0.87
}
```
