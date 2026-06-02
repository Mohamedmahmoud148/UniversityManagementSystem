# 18 — User Flows

## Flow 1: Student Login to Dashboard
```
1. Open /auth/login
2. Enter username + password
3. POST /api/auth/login → receive token + user profile
4. Store tokens in localStorage
5. Decode JWT → extract role
6. Redirect to /student/dashboard
7. Dashboard fires 5 parallel queries (GPA, attendance, exams, etc.)
8. Skeleton loading → data fills in
```

## Flow 2: Student Takes Exam
```
1. See exam in dashboard "Upcoming Exams" widget
2. Click → /student/exams/{id} (pre-exam screen)
3. Read instructions + confirm
4. Click "Start Exam" (if within start time)
5. Fullscreen exam UI loads
6. Answer questions (auto-save every 30s)
7. Finish all questions → click Submit
8. Confirmation modal: "Are you sure? You have X unanswered."
9. POST /api/exams/{id}/submit
10. Redirect to /student/exams/{id}/result
11. Show score, breakdown, feedback
```

## Flow 3: Doctor Creates AI Exam
```
1. /doctor/exams → click "Generate with AI"
2. Modal: select offering, num questions, difficulty, topics
3. POST /api/exams/generate-ai → loading "AI creating your exam..."
4. Review generated questions (edit if needed)
5. Set exam date/time
6. Click "Publish"
7. Students notified via notification system
```

## Flow 4: Student AI Companion Session
```
1. Open /student/companion
2. Click "Start Quiz" on a subject
3. Select topic + difficulty
4. POST /api/companion/sessions/start
5. Chat opens with first question
6. Student answers → AI evaluates → next question
7. After 10 questions: session complete screen
8. POST /api/companion/sessions/{id}/complete
9. View score + AI feedback
10. Profile streak updated
```

## Flow 5: Doctor Views At-Risk Student
```
1. Teaching Intelligence Dashboard shows "8 at-risk students"
2. Click "View All" → /doctor/students/{offeringId}?atRiskOnly=true
3. Table shows students sorted by risk score
4. Click "Mohamed Ahmed" (risk: 82/100 Critical)
5. /doctor/students/{offeringId}/student-id
6. View: grades 65%, attendance 62%, missing 2 assignments
7. Risk factors: "Low grade, Missing assignments, Low attendance"
8. Click "Send Support Message" → notification sent to student
```

## Flow 6: Admin Imports Students (Excel)
```
1. /admin/students → click "Import Students"
2. Upload Excel file (download template first if needed)
3. POST /api/students/bulk-upload-ai
4. Progress indicator: "Processing 150 students..."
5. Result: "145 imported, 5 errors"
6. View errors inline (missing fields, duplicate IDs)
7. Fix and re-upload or skip errors
```

## Flow 7: Student Submits Assignment
```
1. /student/assignments → find pending assignment
2. Click assignment → detail page
3. Upload PDF file or type text answer
4. Click "Submit Assignment"
5. POST /api/assignments/{id}/submit (multipart)
6. Success: submission confirmed, show deadline status (on-time/late)
7. View submission in "My Submission" tab
```

## Flow 8: Student Generates Flashcards
```
1. /student/companion/flashcards → click "Generate New Deck"
2. Enter topic: "SQL Joins"
3. Select: 15 cards, mixed difficulty
4. POST /api/companion/flashcards/generate
5. Loading: "AI generating 15 flashcards..."
6. Deck appears with preview of first 3 cards
7. Click "Review Now" → flashcard review mode
8. Rate each card (Hard/Medium/Easy/Perfect)
9. POST /api/companion/flashcards/cards/{id}/review for each
10. Session complete: X cards reviewed, next review scheduled
```

## Flow 9: Doctor Exports Student Report (Excel)
```
1. /doctor/analytics/{offeringId} → click "Export Excel"
2. GET /api/teaching-intelligence/offerings/{id}/export
3. Response: ExcelExportMetaDto with 45 student rows
4. SheetJS builds Excel file in browser
5. File downloads as "Database Systems - Student Report 2024-12-01.xlsx"
6. Contains: 29 columns of student analytics data
```

## Flow 10: Student Views Academic Roadmap
```
1. /student/roadmap
2. GET /api/Regulations/my-roadmap
3. View semester-by-semester progress
4. Green = passed, Red = failed, Blue = enrolled, Gray = upcoming
5. Click subject → see grade + status
6. "Recommended Next" section: AI-suggested subjects for next semester
7. "Must Retake" section: failed subjects flagged for re-enrollment
```
