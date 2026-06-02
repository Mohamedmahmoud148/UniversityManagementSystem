# 13 — Examination Platform

## Exam Types
- `Quiz` — short, quick (15-30 min)
- `Midterm` — medium exam (60-90 min)
- `Final` — full exam (120-180 min)

## Question Types
- `MCQ` — multiple choice with 4 options
- `TrueFalse` — binary choice
- `Essay` — long text answer (AI-graded)
- `ShortAnswer` — brief text

## Key Flows

### Student: Take Exam
1. Open `/student/exams/{id}` — pre-exam screen
2. Read instructions, confirm identity
3. Click "Start Exam" — starts timer
4. Navigate questions via sidebar
5. Auto-save every 30s: `POST /api/exams/{id}/save-progress`
6. Submit: confirmation modal → `POST /api/exams/{id}/submit`
7. View results on `/student/exams/{id}/result`

### Doctor: Create Exam
1. `/doctor/exams/create` — manual form
2. OR "Generate with AI": `POST /api/exams/generate-ai`
3. Preview AI questions: `POST /api/exams/offerings/{id}/preview-ai-questions`
4. Edit questions if needed
5. Set date/time and publish

### Doctor: Grade Submissions
1. View submissions table: `GET /api/exams/{id}/results`
2. Click submission → view answers
3. Grade essays manually or "AI Grade All"
4. Auto-grade MCQ: `POST /api/exams/{id}/auto-grade`

## Exam Taking UI Requirements
- **Fullscreen mode** (lock navigation)
- Timer with color: green > 30min, amber 10-30min, red < 10min
- Question navigator panel (mark visited/answered)
- Keyboard shortcuts: ↑↓ navigate questions, 1-4 select MCQ
- Paste detection for proctoring
- Tab switch detection → record proctoring event

## Proctoring Events (Frontend)
```typescript
// Track suspicious events
document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    recordEvent('tab_switch');
  }
});

window.addEventListener('blur', () => recordEvent('window_blur'));

document.addEventListener('paste', () => recordEvent('paste_detected'));

function recordEvent(eventType: string) {
  // POST /api/proctoring/event
  proctoringApi.recordEvent({
    submissionId: activeSubmissionId,
    eventType,
    timestamp: new Date().toISOString(),
  });
}
```

## Randomized Exams
When `exam.isRandomized = true`, each student sees a different subset.
Student receives their variant via: `GET /api/exams/{id}/my-variant`

## AI Exam Generation
```typescript
// Doctor flow
const response = await examsApi.createAiExam({
  subjectOfferingId: 'offering-id',
  numQuestions: 20,
  difficulty: 'Medium',
  examType: 'Midterm',
  topics: ['SQL Joins', 'Normalization', 'Transactions'],
});
// Returns full ExamDto with AI-generated questions
```
