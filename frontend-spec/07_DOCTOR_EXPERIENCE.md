# 07 — Doctor Experience

## Overview
The doctor experience centers on the **Teaching Intelligence Platform** — an AI-powered analytics dashboard that monitors student performance, detects risks, analyzes weak topics, and generates actionable recommendations.

---

## Page: Teaching Intelligence Dashboard (`/doctor/dashboard`)

### Layout — Desktop
```
┌─────────────────────────────────────────────────────────────┐
│  NAVBAR  Teaching Intelligence  [Refresh 🔄]  [🔔 5]       │
├────────────────────────────────┬────────────────────────────┤
│  OVERVIEW STATS                │  AI RECOMMENDATIONS        │
│  ┌────────┐ ┌────────┐         │  ─────────────────────     │
│  │ 142    │ │ 8 ⚠️   │         │  ⚠️ 8 students at risk    │
│  │Students│ │At Risk │         │  📉 Attendance < 65%       │
│  └────────┘ └────────┘         │  📚 Students struggle with │
│  ┌────────┐ ┌────────┐         │     Dynamic Programming    │
│  │ 78.3%  │ │ 82%    │         │  🤖 [Full AI Insights →]   │
│  │Avg Grad│ │Attend. │         │                            │
│  └────────┘ └────────┘         │                            │
├────────────────────────────────┴────────────────────────────┤
│  MY OFFERINGS (Click to drill down)                         │
│  ┌─────────────────────┐ ┌─────────────────────────────┐   │
│  │ Database Systems     │ │ Machine Learning             │   │
│  │ Batch 2023 • G1      │ │ AI Dept • Batch 2022         │   │
│  │ 45 students          │ │ 38 students                  │   │
│  │ Avg: 76% ✅ Good     │ │ Avg: 62% ⚠️ Concerning      │   │
│  │ [Analytics →]        │ │ [Analytics →]                │   │
│  └─────────────────────┘ └─────────────────────────────┘   │
│  ┌─────────────────────┐                                    │
│  │ Programming 101      │                                   │
│  │ CS Dept • 2024       │                                   │
│  │ 59 students          │                                   │
│  │ Avg: 68% ⚠️ Warning │                                   │
│  │ [Analytics →]        │                                   │
│  └─────────────────────┘                                    │
├─────────────────────────────────────────────────────────────┤
│  AT-RISK STUDENTS (Critical + High)                        │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Name        Course       Risk  Factors       Action    │ │
│  │ ────────── ──────────── ────  ──────────── ─────────  │ │
│  │ Mohamed A.  DB Systems  82🔴  Low grade,   [Contact]  │ │
│  │                              Miss.assigns             │ │
│  │ Sara H.     Programming  74🟠  Low attend. [Contact]  │ │
│  │ Ahmed K.    ML           71🟠  Low quiz sc [Contact]  │ │
│  └────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│  WEAK TOPICS                    CLASS PERFORMANCE TREND     │
│  • Dynamic Programming 78% err  [Line Chart: Exam 1→4]     │
│  • Memory Management 65% err    Avg: 65 → 71 → 68 → 75    │
│  • SQL Joins 52% err                                        │
└─────────────────────────────────────────────────────────────┘
```

### Data Sources
| Widget | API | Refresh |
|--------|-----|---------|
| Overview Stats | `GET /api/teaching-intelligence/dashboard` | 10min |
| My Offerings | `GET /api/teaching-intelligence/offerings` | 10min |
| At-Risk Students | `GET /api/teaching-intelligence/students/at-risk` | 10min |
| Weak Topics | From dashboard response | 10min |
| AI Recommendations | From dashboard response | 10min |
| Alerts | `GET /api/teaching-intelligence/alerts?unreadOnly=true` | 5min |

---

## Page: Class Analytics (`/doctor/analytics/:offeringId`)

### Tabs: [Overview] [Students] [Topics] [Trends] [Export]

### Overview Tab
```
┌─────────────────────────────────────────────────────────────┐
│  Database Systems — Batch 2023 G1                          │
│  ← Back to Dashboard     [Export Excel 📊] [Refresh 🔄]   │
├──────────────┬──────────────┬──────────────┬───────────────┤
│ 45 Students  │ 76.2% Avg   │ 82% Attend.  │ 91% Assign.  │
│              │              │              │ Completion    │
├──────────────┴──────────────┴──────────────┴───────────────┤
│  GRADE DISTRIBUTION                                        │
│  A(85+): ████ 12 (27%)                                    │
│  B(70-84): ██████ 18 (40%)                                │
│  C(60-69): ████ 8 (18%)                                   │
│  D(50-59): ██ 5 (11%)                                     │
│  F(<50): █ 2 (4%)                                         │
├─────────────────────────────────────────────────────────────┤
│  PERFORMANCE TREND (Exams)                                  │
│  90 ─┐                                           ·         │
│  80 ─┤           ·─────·                   ·────·         │
│  70 ─┤     ·────·                                          │
│  60 ─┤                                                      │
│      └──────────────────────────────────────────────────   │
│         Quiz 1  Quiz 2  Midterm  Quiz 3  Final              │
└─────────────────────────────────────────────────────────────┘
```

### Students Tab — Full Table
```
Filter: [All ▾] [Risk: All ▾] [Trend: All ▾] [Search...]

Name             ID       Grade  Attend  Assigns  Risk    Trend
─────────────────────────────────────────────────────────────
Mohamed Ahmed   20230001  82%    95%     100%     🟢 Low  📈
Sara Hassan     20230002  65%    70%     85%      🟠 High 📉
Ahmed Khalil    20230003  91%    98%     100%     🟢 Low  📈
...

[Show: 25 per page] [← 1 2 3 →]
```

**Interactions**:
- Click student row → navigate to `/doctor/student-profile/:offeringId/:studentId`
- Click "Risk" column header → sort by risk score
- Filter "At Risk Only" checkbox
- Click "Contact" → opens pre-filled email with student info
- Export button → download Excel via SheetJS

### Topics Tab
```
┌─────────────────────────────────────────────────────────────┐
│  Topic Performance Analysis                                 │
│                                                             │
│  WEAK TOPICS (Intervention Needed)                         │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Topic              Avg Score  Error Rate  Students ⚠   │ │
│  │ ──────────────── ──────────  ──────────  ─────────── │ │
│  │ 🔴 Dynamic Prog.  32%        78%         35          │ │
│  │ 🟠 Memory Mgmt    45%        65%         28          │ │
│  │ 🟡 SQL Joins      56%        52%         22          │ │
│  └────────────────────────────────────────────────────────┘ │
│  AI Recommendation: "Conduct revision session on           │
│  Dynamic Programming before next exam."                    │
│                                                             │
│  STRONG TOPICS                                             │
│  ✅ Basic SQL (95%), ✅ ER Diagrams (88%)                  │
└─────────────────────────────────────────────────────────────┘
```

---

## Page: Student Profile Intelligence (`/doctor/students/:offeringId/:studentId`)

```
┌─────────────────────────────────────────────────────────────┐
│  ← Back   Mohamed Ahmed — 20230001                         │
│           Batch 2023 G1 • CS Department                    │
├─────────────────────┬───────────────────────────────────────┤
│  RISK ASSESSMENT    │  ACADEMIC PERFORMANCE                 │
│  ─────────────────  │  ─────────────────────────────────── │
│  Score: 74/100      │  Final Score: 65%    Grade: C+        │
│  Level: 🟠 HIGH    │  Midterm: 58%                         │
│  Factors:           │  Coursework: 72%                     │
│  • Low attendance   │  Exam Avg: 61%                        │
│  • Missing assigns  │                                       │
│  • Low quiz scores  │  ATTENDANCE                          │
│                     │  62% — 12/19 sessions                │
│  Recommended:       │  ⚠️ Below 75% threshold             │
│  "Schedule office   │                                       │
│   hour meeting"     │  ASSIGNMENTS                         │
│                     │  4/6 submitted (67%)                 │
│                     │  2 missing, 1 late                   │
├─────────────────────┴───────────────────────────────────────┤
│  AI COMPANION ACTIVITY          TREND                       │
│  Sessions: 3  Streak: 0 days   📉 Declining overall        │
│  Study Min: 45                 Attendance: -15%             │
│  Engagement: 22/100 (Low)      Grade: -8%                   │
├─────────────────────────────────────────────────────────────┤
│  ACTIONS                                                    │
│  [📧 Send Support Message] [📋 Add Note] [Flag for Review] │
└─────────────────────────────────────────────────────────────┘
```

---

## Page: Exam Management (`/doctor/exams`)

### Exam List
```
┌─────────────────────────────────────────────────────────────┐
│  Exam Management                    [+ Create] [+ AI Gen]  │
│                                                             │
│  [Filter: All Offerings ▾]  [Status: All ▾]               │
│                                                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Database Midterm          Published  50 marks          │ │
│  │ Tomorrow 10:00 AM • DB Systems                         │ │
│  │ 45 enrolled • 0 submitted                              │ │
│  │ [View] [Edit] [Results] [Analytics] [Grade]            │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Networks Quiz 3           Draft                        │ │
│  │ Scheduled: Next Week                                   │ │
│  │ [Edit] [Publish] [Delete]                              │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Create Exam Form
- Title, Type (Quiz/Midterm/Final)
- Date/time pickers
- Subject Offering selector
- Questions: drag-to-reorder, types: MCQ/TrueFalse/Essay
- "Generate with AI" button → modal with topic inputs

### Exam Results Page
```
┌─────────────────────────────────────────────────────────────┐
│  Database Midterm — Results                                 │
│  45 submitted  Avg: 38.5/50 (77%)  Pass rate: 82%          │
│                                                             │
│  Grade Distribution Chart (Histogram)                      │
│                                                             │
│  Student Results Table:                                     │
│  Name          Score  Grade  Passed  Time    Action        │
│  ─────────── ────── ───── ─────── ────── ────────────     │
│  Mohamed A.   45/50   A+    ✅     42min  [View] [Grade]  │
│  Sara H.      25/50   F     ❌     38min  [View] [Grade]  │
│                                                             │
│  [Auto-Grade All] [Export Results]                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Page: Assignment Management (`/doctor/assignments`)

Similar structure to exam management. Key unique feature:
- View submissions with file previews
- "AI Grade All" button to bulk AI-grade essay submissions
- Filter submissions by: All / Graded / Ungraded / Late

---

## Page: Attendance Management (`/doctor/attendance`)

### Create Session
```
┌─────────────────────────────────────────────────────────────┐
│  Take Attendance — Database Systems                        │
│  ─────────────────────────────────────────────────────     │
│  Session Date: [Today ▾]                                   │
│  Time: [10:00 AM ─────────► 12:00 PM]                     │
│  [Generate QR Code]                                        │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  QR CODE DISPLAYED HERE (large, printable)          │   │
│  │  Session active for: 23:45                          │   │
│  │  Students checked in: 32/45                         │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  [End Session] [Manual Override]                           │
└─────────────────────────────────────────────────────────────┘
```

---

## Page: Alerts & Notifications (`/doctor/alerts`)

```
Filter: [All] [Unread] [Critical] [Warning]

🔴 CRITICAL
┌──────────────────────────────────────────────────────────┐
│ Mohamed Ahmed reached CRITICAL risk in DB Systems        │
│ Score: 82/100 • Reasons: low grade, missing assignments  │
│ 2 hours ago  [View Student] [Mark Read]                  │
└──────────────────────────────────────────────────────────┘

🟠 WARNING
┌──────────────────────────────────────────────────────────┐
│ Attendance dropped below 65% in Programming 101          │
│ 22 students below threshold                              │
│ Yesterday  [View Class] [Send Reminder]                  │
└──────────────────────────────────────────────────────────┘
```

---

## Page: AI Teaching Assistant Chat (`/chat` — Doctor Mode)

Same UI as student chat but with different AI persona:
- Doctor-focused system prompt
- Academic analysis tone
- Can ask: "Which students need attention?", "Analyze my class performance"
- AI accesses teaching intelligence data for context-aware responses

---

## Responsive Design

| Component | Mobile | Tablet | Desktop |
|-----------|--------|--------|---------|
| Teaching Dashboard | Single column | 2-col stats + list | Full 3-panel |
| Student Table | Card view | Compact table | Full table with all columns |
| Exam Results | Card per student | Table | Table + chart |
| Analytics Charts | Full-width stacked | 2-column | Side by side |

---

## Excel Export Flow

1. Doctor clicks "Export Excel" button
2. Frontend calls `GET /api/teaching-intelligence/offerings/{id}/export`
3. Receives `ExcelExportMetaDto` with `rows: StudentExcelRowDto[]`
4. Frontend uses **SheetJS (xlsx)** to build Excel file in browser:

```typescript
import * as XLSX from 'xlsx';

function exportToExcel(data: ExcelExportMetaDto) {
  const headers = [
    'University ID', 'Student Name', 'Batch', 'Group', 'Department', 
    'College', 'Subject', 'Final Score', 'Midterm', 'Coursework', 
    'Final Exam', 'Grade', 'Total Sessions', 'Attended', 'Attendance %',
    'Total Assignments', 'Submitted', 'Missing', 'Completion %',
    'Total Exams', 'Avg Exam Score', 'Avg Quiz Score',
    'Risk Score', 'Risk Level', 'Risk Factors',
    'AI Sessions', 'Study Minutes', 'Streak Days'
  ];
  
  const rows = data.rows.map(r => [
    r.universityId, r.studentName, r.batchName, r.groupName,
    r.departmentName, r.collegeName, r.subjectName,
    r.finalScore, r.midtermScore, r.courseworkScore, r.finalExamScore,
    r.gradeCategory, r.totalSessions, r.attendedSessions, r.attendancePercent,
    r.totalAssignments, r.submittedAssignments, r.missingAssignments, r.assignmentCompletionRate,
    r.totalExams, r.avgExamScore, r.avgQuizScore,
    r.riskScore, r.riskLevel, r.riskFactors,
    r.aiSessions, r.studyMinutes, r.streakDays
  ]);
  
  const ws = XLSX.utils.aoa_to_sheet([headers, ...rows]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Students');
  XLSX.writeFile(wb, `${data.exportTitle}.xlsx`);
}
```
