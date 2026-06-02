# 06 — Student Experience

## Overview
The student experience is the platform's most important user journey. It combines academic management (grades, exams, attendance) with an AI-powered companion that acts as a personal academic coach and study partner.

---

## Page: Student Dashboard (`/student/dashboard`)

### Layout
```
┌─────────────────────────────────────────────────────────────┐
│  NAVBAR  [Hello Ahmed 👋]  [Streak: 5🔥]  [🔔 3]  [Avatar] │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │  GPA Card     │  │ Attendance   │  │  AI Companion    │ │
│  │  3.4 / 4.0   │  │  82%         │  │  Dashboard →     │ │
│  │  ↑ +0.2      │  │  ⚠ Warning   │  │  5 Sessions      │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
│                                                             │
│  ┌─────────────────────────────┐  ┌────────────────────┐  │
│  │  Upcoming Exams (Next 7d)   │  │  Due Assignments   │  │
│  │  ─────────────────────────  │  │  ──────────────── │  │
│  │  📝 Database Midterm       │  │  📌 OS Assignment   │  │
│  │     Tomorrow 10:00 AM      │  │     Due in 2 days   │  │
│  │  📝 Networks Quiz          │  │  📌 ML Homework     │  │
│  │     Thu 2:00 PM            │  │     Due in 5 days   │  │
│  └─────────────────────────────┘  └────────────────────┘  │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  My Courses (This Semester)                          │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐ │   │
│  │  │ DB       │ │ OS       │ │ Networks │ │  ML    │ │   │
│  │  │ 78%      │ │ 65% ⚠   │ │  85%     │ │  72%   │ │   │
│  │  │ Dr. Ali  │ │ Dr. Sara │ │ Dr. Mos  │ │ Dr. Jo │ │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └────────┘ │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌────────────────────────┐  ┌───────────────────────┐     │
│  │  AI Insights Feed      │  │  Today's Study Plan   │     │
│  │  ─────────────────────│  │  ──────────────────── │     │
│  │  💡 Risk alert: OS     │  │  🎯 Review Chapter 3  │     │
│  │  📈 Improved in DB     │  │  📚 10 Due Flashcards │     │
│  │  🔥 3-day streak!      │  │  ✍️  DB Assignment    │     │
│  └────────────────────────┘  └───────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Widgets & Data Sources

| Widget | API | Refresh |
|--------|-----|---------|
| GPA Card | `GET /api/gpa/my-gpa` | 10min stale |
| Attendance Summary | `GET /api/attendance/student/{id}/report` | 5min |
| AI Companion Stats | `GET /api/companion/dashboard` | 5min |
| Upcoming Exams | `GET /api/exams/my-enrolled-exams` | 5min |
| Due Assignments | `GET /api/assignments/offering/{id}` (per course) | 5min |
| My Courses | `GET /api/enrollments/my-enrollments` | 10min |
| AI Insights | `GET /api/companion/insights?unreadOnly=true` | 3min |
| Study Plan | `GET /api/companion/dashboard` (todayRecommendations) | 5min |

### Interactions
- Click GPA Card → navigate to `/student/grades`
- Click Attendance widget → navigate to `/student/attendance`
- Click exam row → navigate to `/student/exams/{id}`
- Click assignment row → navigate to `/student/assignments/{id}`
- Click AI Insights row → acknowledge insight + navigate to action URL
- "Start Studying" button → open AI Companion modal

---

## Page: AI Companion (`/student/companion`)

### Layout — Desktop (3-column)
```
┌──────────┬─────────────────────────────┬──────────────┐
│ SIDEBAR   │ MAIN CONTENT                │ SIDE PANEL   │
│           │                             │              │
│ Profile   │ ┌─────────────────────────┐ │ 🔥 Streak    │
│ 5🔥 Days  │ │ Today's Recommendations │ │ 5 Days       │
│           │ │ ─────────────────────── │ │              │
│ Quick     │ │ 🎯 Review Chapter 3     │ │ 📊 This Week │
│ Actions:  │ │ 📚 10 Due Flashcards    │ │ 3 sessions   │
│ [Quiz Me] │ │ 📝 DB Assignment Due    │ │ 45min study  │
│ [Flash]   │ └─────────────────────────┘ │ 76% accuracy │
│ [Report]  │                             │              │
│           │ ┌─────────────────────────┐ │ Due Cards    │
│ Weekly    │ │ Recent Sessions         │ │ 10 cards due │
│ Progress  │ │ ─────────────────────── │ │ [Review Now] │
│ Chart     │ │ ✅ DB Quiz – 85%        │ │              │
│           │ │ ✅ Network Review – 72% │ │ Weak Topics  │
│ Insights  │ │ ⚡ Exam Prep – In Prog  │ │ • OS         │
│ Feed      │ └─────────────────────────┘ │ • Algorithm  │
└──────────┴─────────────────────────────┴──────────────┘
```

### Mobile Layout — Tab Navigation
```
[Dashboard] [Sessions] [Flashcards] [Insights] [Coach]
```

### Sub-pages within Companion

1. **Dashboard** — overview widgets
2. **Academic Coach** — personalized coaching chat
3. **Study Partner** — interactive quiz sessions
4. **Flashcards** — deck management + review
5. **Progress Report** — weekly/monthly reports
6. **Insights** — AI-generated insights feed
7. **Study Plan** — generated study schedule

---

## Page: Academic Coach Chat (`/student/companion/coach`)

### Layout
```
┌─────────────────────────────────────────────────────┐
│  AI Academic Coach                                   │
│  ─────────────────────────────────────────────────  │
│                                                     │
│  [AI]: مرحباً أحمد! دلوقتي متوسطك 3.4 وعندك       │
│  تحذير في حضور مادة OS. عايز نتكلم عن ده؟          │
│                                                     │
│  [User]: ايه المشكله في OS؟                        │
│                                                     │
│  [AI]: حضورك في OS 62% — تحت الـ 75% المطلوب.     │
│  كمان درجتك في الـ quiz الأخير 45%. النصيحة:       │
│  ● حضور المحاضرتين الجاييتين أساسي                 │
│  ● مذاكرة Scheduling Algorithms قبل الـ midterm    │
│                                                     │
│  ┌──────────────────────────────────┐               │
│  │ What should I focus on this week?│  [Send] 📤   │
│  └──────────────────────────────────┘               │
│                                                     │
│  Suggestions: [Quiz me on OS] [Study Plan] [Report]│
└─────────────────────────────────────────────────────┘
```

### API Flow
1. `GET /api/companion/dashboard` — load student academic data
2. Student sends message → `POST /api/Chat/messages` → intent: `academic_coach`
3. AI returns personalized coaching response
4. `POST /api/companion/insights/{id}/acknowledge` if applicable

---

## Page: Study Partner — Quiz Session (`/student/companion/quiz`)

### Flow
1. Student selects topic + difficulty
2. Click "Start Quiz" → `POST /api/companion/sessions/start`
3. AI generates question → display in quiz UI
4. Student answers → evaluate via AI chat
5. AI gives feedback + next question
6. Complete 5-10 questions → `POST /api/companion/sessions/{id}/complete`
7. Show results screen with accuracy + AI feedback

### Quiz UI
```
┌───────────────────────────────────────────────────┐
│  Question 3/10        [Topic: Databases]   [Stop] │
│  ────────────────────────────────────────────────  │
│                                                   │
│  What is normalization in database design?        │
│                                                   │
│  A) Organizing data to reduce redundancy          │
│  B) Adding more tables to a database              │
│  C) Creating backups of the database              │
│  D) Encrypting the database                       │
│                                                   │
│  [A] [B] [C] [D]                                 │
│                                                   │
│  Progress: ████████░░░░  3/10                    │
│  Accuracy: 85% ✅                                │
└───────────────────────────────────────────────────┘
```

---

## Page: Flashcards (`/student/companion/flashcards`)

### Deck List View
```
┌─────────────────────────────────────────────────────┐
│  My Flashcard Decks                [+ New Deck]     │
│  ──────────────────────────────────────────────    │
│  ┌──────────────────┐  ┌──────────────────────┐   │
│  │ 📚 Databases      │  │ 📚 OS Concepts       │   │
│  │ 15 cards          │  │ 12 cards             │   │
│  │ 🔴 8 due today   │  │ ✅ All reviewed      │   │
│  │ [Review] [View]   │  │ [Review] [View]      │   │
│  └──────────────────┘  └──────────────────────┘   │
│  ┌──────────────────┐                              │
│  │ + Generate New   │                              │
│  │   Deck with AI   │                              │
│  └──────────────────┘                              │
└─────────────────────────────────────────────────────┘
```

### Review Mode (Spaced Repetition)
```
┌───────────────────────────────────────────────────┐
│  Flashcard Review    10 due • 3 of 10             │
│  ────────────────────────────────────────────     │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │                                             │ │
│  │   What is a Primary Key in SQL?             │ │
│  │                                             │ │
│  │              [Flip Card ↕]                  │ │
│  └─────────────────────────────────────────────┘ │
│                                                   │
│  Rate your recall:                               │
│  [😰 Hard] [🤔 Medium] [😊 Easy] [⭐ Perfect]  │
└───────────────────────────────────────────────────┘
```

### API Flow
- Load due cards: `GET /api/companion/flashcards/due`
- After each flip + rating: `POST /api/companion/flashcards/cards/{id}/review` with `{ quality: 0-5 }`
- Generate new deck: `POST /api/companion/flashcards/generate`

---

## Page: Exams (`/student/exams`)

### Exam List
```
┌───────────────────────────────────────────────────┐
│  My Exams                                         │
│  ────────────────────────────────────────────     │
│  UPCOMING                                         │
│  ┌────────────────────────────────────────────┐  │
│  │ 📝 Database Midterm                        │  │
│  │ Tomorrow, 10:00 AM – 12:00 PM              │  │
│  │ 50 marks • 2 hours • Dr. Ali Hassan        │  │
│  │                          [View] [Study AI] │  │
│  └────────────────────────────────────────────┘  │
│                                                   │
│  COMPLETED                                        │
│  ┌────────────────────────────────────────────┐  │
│  │ ✅ Networks Quiz — Scored: 45/50 (90%)     │  │
│  │ Last week • Passed                          │  │
│  │                               [View Result] │  │
│  └────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

### Exam Taking Page (`/student/exams/{id}/take`)
- Timer in top right (countdown)
- Question navigation sidebar
- Auto-save every 30s: `POST /api/exams/{id}/save-progress`
- MCQ: click option to select
- Essay: textarea with character counter
- Submit button → confirmation modal → `POST /api/exams/{id}/submit`

---

## Page: Assignments (`/student/assignments`)

### Assignment List
```
┌───────────────────────────────────────────────────┐
│  Assignments                   [Filter: All ▾]   │
│                                                   │
│  ┌────────────────────────────────────────────┐  │
│  │ 📌 Database System Assignment 3             │  │
│  │ Due: Tomorrow 11:59 PM  • OS Subject        │  │
│  │ Not submitted yet  ⚠️                       │  │
│  │ Max: 20 points                 [Submit Now] │  │
│  └────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────┐  │
│  │ ✅ Networks Homework 1                     │  │
│  │ Submitted on time • Grade: 18/20           │  │
│  │ Feedback: "Excellent work!"    [View]      │  │
│  └────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

### Submit Assignment Page
```
┌───────────────────────────────────────────────────┐
│  Submit: DB Assignment 3                          │
│  Due: Tomorrow 11:59 PM                           │
│  ────────────────────────────────────────────     │
│  Text Answer (optional):                         │
│  ┌──────────────────────────────────────────┐   │
│  │                                          │   │
│  └──────────────────────────────────────────┘   │
│                                                   │
│  Attach File (PDF):                              │
│  ┌──────────────────────────────────────────┐   │
│  │  📎 Drop PDF here or click to browse    │   │
│  └──────────────────────────────────────────┘   │
│                                                   │
│           [Cancel]  [Submit Assignment]           │
└───────────────────────────────────────────────────┘
```

---

## Page: Academic Roadmap (`/student/roadmap`)

### Overview View
```
┌───────────────────────────────────────────────────┐
│  Academic Roadmap — CS Department                 │
│  ────────────────────────────────────────────     │
│  Progress: 68/144 credit hours (47%)              │
│  ████████████░░░░░░░░░░░░  GPA: 3.4              │
│                                                   │
│  SEMESTER 1  ✅ Completed                        │
│  [CS101 ✅] [MATH101 ✅] [ENG101 ✅] [PHY101 ✅] │
│                                                   │
│  SEMESTER 2  ✅ Completed                        │
│  [CS201 ✅] [MATH201 ✅] [CS202 ❌ Retake]      │
│                                                   │
│  SEMESTER 3  🔄 In Progress                      │
│  [CS301 📚] [CS302 📚] [MATH301 📚]             │
│                                                   │
│  SEMESTER 4  🔒 Locked                           │
│  [CS401] [CS402] [CS403]                          │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │  Recommended Next: [CS303] [MATH302]         │ │
│  │  Must Retake: [CS202 — Failed]               │ │
│  └─────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────┘
```

---

## Page: Grades (`/student/grades`)
```
┌───────────────────────────────────────────────────┐
│  My Grades                                        │
│  ────────────────────────────────────────────     │
│  Current GPA: 3.4   CGPA: 3.2   Hours: 68/144   │
│                                                   │
│  This Semester:                                   │
│  ┌────────────────────────────────────────────┐  │
│  │ Subject        Score  Letter  Points        │  │
│  │ ─────────────────────────────────────────  │  │
│  │ Databases      82     B+      3.5           │  │
│  │ OS             65     C+      2.5 ⚠         │  │
│  │ Networks       90     A       4.0 ✅         │  │
│  │ ML             75     B       3.0           │  │
│  └────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

---

## Page: Notifications (`/notifications`)
```
┌───────────────────────────────────────────────────┐
│  Notifications    [Mark all read]  [Filter ▾]    │
│  ────────────────────────────────────────────     │
│  🔴 URGENT                                       │
│  ┌────────────────────────────────────────────┐  │
│  │ ⚠️ Exam Tomorrow: Database Midterm         │  │
│  │ You haven't started studying yet           │  │
│  │ → Start AI Exam Prep                       │  │
│  └────────────────────────────────────────────┘  │
│                                                   │
│  📰 GENERAL                                      │
│  ┌────────────────────────────────────────────┐  │
│  │ 📚 New material uploaded: Networks Ch.5    │  │
│  │ Dr. Mohamed just uploaded new materials    │  │
│  └────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

---

## Mobile-Specific UX

### Bottom Navigation Bar (Mobile)
```
[🏠 Home] [📚 AI] [💬 Chat] [📝 Exams] [👤 Profile]
```

### Touch Interactions
- Swipe left on flashcard → "Hard" rating
- Swipe right → "Easy" rating
- Pull-to-refresh on all lists
- Long-press notification → quick actions

---

## Responsive Breakpoints for Student Pages

| Page | Mobile | Tablet | Desktop |
|------|--------|--------|---------|
| Dashboard | Single column, stacked widgets | 2-column grid | 3-column grid |
| AI Companion | Full screen chat | Side panel | 3-column layout |
| Flashcards | Full screen card | Side list + card | Deck list + review panel |
| Exams | Scrollable questions | 2-panel (nav + question) | 3-panel |
| Roadmap | Accordion by semester | Grid 4-up | Timeline view |
