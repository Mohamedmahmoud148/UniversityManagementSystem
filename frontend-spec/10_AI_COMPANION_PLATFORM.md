# 10 — AI Companion Platform

## Overview
The AI Companion is a personalized academic intelligence layer for students and doctors. It combines: AI chat coaching, spaced-repetition flashcards, interactive quiz sessions, progress tracking, and proactive insights.

---

## Companion Dashboard (`/student/companion`)

### Widget Grid
| Widget | Data Source | Size |
|--------|------------|------|
| Profile Summary Card | `GET /api/companion/dashboard` (.profile) | 1/3 width |
| Streak + Engagement | `.profile.currentStreakDays` | 1/3 |
| Due Flashcards | `GET /api/companion/flashcards/due` | 1/3 |
| Weekly Progress Chart | `.weeklyProgress.dailyActivity` | 2/3 |
| Today's Recommendations | `.todayRecommendations` | 1/3 |
| Recent Sessions | `GET /api/companion/sessions` | Full width |
| Insights Feed | `GET /api/companion/insights?unreadOnly=true` | 1/2 |
| Weak Subjects | `.profile.weakSubjects` | 1/2 |

### Profile Summary Card
```
┌──────────────────────────────────────┐
│  👤 Ahmed Hassan                      │
│  Learning Style: Practical            │
│  Goal: Graduation                     │
│  ─────────────────────────────────── │
│  Total Sessions: 23  |  Streak: 5🔥  │
│  Engagement: ████████░░ 80/100        │
└──────────────────────────────────────┘
```

---

## Academic Coach (`/student/companion/coach`)

Conversational AI using intent `academic_coach`. Fetches real grade and attendance data from backend.

### System Behavior
- On open: AI greets by name, shows summary of current standing
- Student can ask: "ايه وضعي في OS؟", "ازاي أحسن معدلي؟"
- AI fetches real backend data and gives data-grounded advice
- Suggestions chip bar after each response

---

## Study Partner (`/student/companion/quiz`)

### Session Types
| Type | Description | Intent |
|------|-------------|--------|
| Quiz | MCQ quiz on a topic | `quiz_me` |
| Active Recall | Open-ended Q&A | `quiz_me` |
| Concept Check | Quick check after explanation | `quiz_me` |
| Exam Prep | Practice before exam | `quiz_me` |

### Flow
1. Select topic (from enrolled courses or free text)
2. Select type + difficulty
3. `POST /api/companion/sessions/start` → sessionId
4. AI sends first question via chat
5. Student answers in chat → AI evaluates
6. After 10 questions: `POST /api/companion/sessions/{id}/complete`
7. Results screen: accuracy, AI feedback, next steps

### Session Results Screen
```
┌────────────────────────────────────┐
│  Session Complete! 🎉              │
│  ─────────────────────────────── │
│  Score: 8/10 (80%) ✅             │
│  Time: 12 minutes                  │
│  ─────────────────────────────── │
│  AI Feedback:                      │
│  "أداء ممتاز! بس ركز على Chapter  │
│   4 في الـ Deadlocks."            │
│  ─────────────────────────────── │
│  [Study Again] [View Flashcards]  │
│  [Back to Companion]               │
└────────────────────────────────────┘
```

---

## Flashcards (`/student/companion/flashcards`)

### Generate Deck
```typescript
// POST /api/companion/flashcards/generate
{
  topicName: "SQL Joins",
  cardCount: 15,
  difficulty: "mixed",
  subjectOfferingId: "optional"
}
// Response: FlashcardDeckDto with 15 cards
```

### Review Interface (SM-2 Algorithm)
- Show front (question/term)
- Student thinks...
- Click "Flip" to reveal back (answer)
- Rate: Hard(0) / Medium(3) / Easy(4) / Perfect(5)
- `POST /api/companion/flashcards/cards/{id}/review` → updates nextReviewAt
- Progress: X cards remaining today

### SM-2 Visual Feedback
- Hard → card turns red briefly, back into deck
- Easy → card slides to "done" pile
- Perfect → confetti micro-animation

---

## Progress Report (`/student/companion/progress`)

```typescript
// POST /api/Chat/messages with message requesting weekly report
// Intent: progress_report
// AI generates narrative report + data from backend
```

### Report Display
- Weekly: sessions, study time, accuracy, streak
- Monthly: grade trends, most improved subjects
- Charts: bar chart per day (study minutes), line (accuracy trend)
- AI narrative: "This week you studied 3 hours, improved in DB..."

---

## Insights Feed (`/student/companion/insights`)

```typescript
// GET /api/companion/insights
// GET /api/companion/insights?unreadOnly=true
```

### Insight Card Types
| InsightType | Icon | Color |
|------------|------|-------|
| InactivityAlert | 😴 | Amber |
| ExamApproaching | ⚡ | Red |
| AssignmentDeadline | 📌 | Orange |
| StreakMilestone | 🔥 | Gold |
| ImprovementDetected | 📈 | Green |
| WeaknessDetected | ⚠️ | Amber |
| WeeklyReport | 📊 | Blue |
| RiskAlert | 🚨 | Red |

### Acknowledge
- Click insight card → mark acknowledged
- `POST /api/companion/insights/{id}/acknowledge`
- Card slides out with fade animation

---

## Doctor Class Analytics (Companion)

Accessed by doctors: `GET /api/companion/class-analytics/{subjectOfferingId}`

Shows per-offering:
- Average grade, pass rate
- At-risk count
- Weak topics list
- AI summary

Also: `GET /api/companion/class-analytics/{id}/weak-topics`
