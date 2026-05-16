# Randomized Per-Student Exam — Frontend Integration Guide

Every student who opens a randomized exam gets a **unique, personally assigned subset** of questions
drawn from the full question pool. The assignment is created once (lazily, on first open) and stays
fixed — a student who closes and reopens the exam sees the same questions.

---

## Doctor Flow — Create a Randomized AI Exam

### Endpoint

```
POST /api/exams/generate-ai
Authorization: Bearer <doctor-token>
Content-Type: application/json
```

### Request body

```jsonc
{
  "subjectOfferingId": "01JFKX2...",   // ULID of the offering
  "difficulty": "Medium",               // "Easy" | "Medium" | "Hard"
  "questionCount": 30,                  // pool size — total questions generated
  "examType": "Final",                  // "Quiz" | "Midterm" | "Final"
  "topics": ["Recursion", "Sorting"],   // optional topic hints for the AI
  "isRandomized": true,                 // ← enable per-student randomization
  "questionsPerStudent": 10             // ← each student sees 10 of the 30
}
```

**Rules**
- `questionCount` is the **pool** size (how many the AI generates).
- `questionsPerStudent` must be **less than** `questionCount`.
- If `questionsPerStudent` is `0` or omitted, every student gets all questions (equivalent to `isRandomized: false`).
- The exam is created in `Draft` status. Publish it separately or via Admin.

### Success response — `201 Created`

```jsonc
{
  "id": "01JFKX3...",
  "title": "Algorithms - AI Generated Draft",
  "type": "Final",
  "totalMarks": 300,
  "startTime": "2026-06-01T09:00:00Z",
  "endTime": "2026-06-01T11:00:00Z",
  "mode": "AI",
  "status": "Draft",
  "isRandomized": true,
  "questionsPerStudent": 10,
  "questions": [ /* all 30 questions with correctAnswer included */ ]
}
```

---

## Doctor Flow — Create a Manual Randomized Exam

Use the standard exam-creation endpoint; just add `isRandomized` and `questionsPerStudent` to the
exam object after creation via `PUT /api/exams/{id}` (Admin endpoint), or plan for a future
`PATCH` endpoint. For now, the AI generation endpoint is the primary randomized-exam creation path.

---

## Student Flow — Open a Randomized Exam

Students call a dedicated endpoint to retrieve **their personal variant**.
Do **not** call `GET /api/exams/{id}` for randomized exams — it will return all questions (or
deny access depending on role logic). Always use the variant endpoint.

### Endpoint

```
GET /api/exams/{examId}/my-variant
Authorization: Bearer <student-token>
```

### Success response — `200 OK`

```jsonc
{
  "id": "01JFKX3...",
  "title": "Algorithms - AI Generated Draft",
  "type": "Final",
  "totalMarks": 100,          // sum of marks for THIS student's 10 questions
  "startTime": "2026-06-01T09:00:00Z",
  "endTime": "2026-06-01T11:00:00Z",
  "isRandomized": true,
  "questionsPerStudent": 10,
  "questions": [
    {
      "id": "01JFKX4...",
      "questionText": "What is the time complexity of merge sort?",
      "questionType": "MCQ",
      "options": ["O(n)", "O(n log n)", "O(n²)", "O(log n)"],
      "mark": 10,
      "correctAnswer": null    // ← always null for students
    },
    {
      "id": "01JFKX5...",
      "questionText": "Binary search requires the array to be sorted.",
      "questionType": "TrueFalse",
      "options": null,
      "mark": 10,
      "correctAnswer": null
    }
    // ... 8 more questions
  ]
}
```

**Important**
- `correctAnswer` is always `null` for students — never display it.
- The variant is **locked after first call** — the student always gets the same questions.
- `totalMarks` reflects only the student's subset, not the full pool.
- `options` is a `string[]` for MCQ, `null` for TrueFalse / Essay.

---

## Student Flow — Submit Answers

The submission endpoint is the same regardless of randomization.
Only send answers for the questions in the student's variant.

### Endpoint

```
POST /api/exams/{examId}/submit
Authorization: Bearer <student-token>
Content-Type: application/json
```

### Request body

```jsonc
{
  "examId": "01JFKX3...",
  "answers": [
    { "questionId": "01JFKX4...", "answerText": "O(n log n)" },
    { "questionId": "01JFKX5...", "answerText": "True" }
    // ... one entry per question in the variant
  ]
}
```

### Success response — `201 Created`

```jsonc
{
  "submissionId": "01JFKX9...",
  "message": "Exam submitted successfully."
}
```

---

## Doctor Flow — Auto-Grade

After the exam window closes, the doctor triggers auto-grading. The system grades each student
against **their own variant's correct answers** only.

```
POST /api/exams/{examId}/auto-grade
Authorization: Bearer <doctor-token>
```

Response:

```jsonc
{ "gradedCount": 47 }
```

---

## Doctor Flow — View Submissions

```
GET /api/exams/{examId}/submissions
Authorization: Bearer <doctor-token>
```

Returns all student submissions with `score`, `isGraded`, and raw `answersJson`.

---

## Question Types Reference

| `questionType` | `options` field         | Expected `answerText`        |
|----------------|-------------------------|------------------------------|
| `"MCQ"`        | `["A", "B", "C", "D"]` | One of the option strings    |
| `"TrueFalse"`  | `null`                  | `"True"` or `"False"`        |
| `"Essay"`      | `null`                  | Free text (manually graded)  |

---

## ExamDto Field Reference

| Field                | Type       | Doctor | Student | Notes                                      |
|----------------------|------------|--------|---------|--------------------------------------------|
| `id`                 | string     | ✓      | ✓       | ULID                                       |
| `title`              | string     | ✓      | ✓       |                                            |
| `type`               | string     | ✓      | ✓       | "Quiz" / "Midterm" / "Final"               |
| `totalMarks`         | int        | ✓      | ✓       | Student variant: sum of their questions    |
| `startTime`          | datetime   | ✓      | ✓       | UTC ISO-8601                               |
| `endTime`            | datetime   | ✓      | ✓       | UTC ISO-8601                               |
| `mode`               | string     | ✓      | ✓       | "Structured" / "AI" / "File"              |
| `status`             | string     | ✓      | ✓       | "Draft" / "Published" / "Closed"           |
| `isRandomized`       | bool       | ✓      | ✓       |                                            |
| `questionsPerStudent`| int        | ✓      | ✓       | 0 = all questions                          |
| `questions[].correctAnswer` | string? | ✓ (non-null) | ✓ (always null) | Security: never expose to students |

---

## Error Codes

| HTTP | Scenario                                              |
|------|-------------------------------------------------------|
| 400  | Invalid ULID in path                                  |
| 401  | Missing or expired token                              |
| 403  | Student calling doctor-only endpoint (or vice versa)  |
| 404  | Exam not found                                        |
| 409  | Student already submitted (duplicate submission)       |
